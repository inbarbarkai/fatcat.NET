using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace fatcat.Core
{
    public class FatSystem
    {
        // Last cluster
        public const ulong Last = ulong.MaxValue;

        // Maximum number of clusters
        public const int MAX_FAT12 = 0xFF4;

        // Header offsets
        public const int FAT_BYTES_PER_SECTOR = 0x0b;

        public const int FAT_SECTORS_PER_CLUSTER = 0x0d;
        public const int FAT_RESERVED_SECTORS = 0x0e;
        public const int FAT_FATS = 0x10;
        public const int FAT_TOTAL_SECTORS = 0x20;
        public const int FAT_SECTORS_PER_FAT = 0x24;
        public const int FAT_ROOT_DIRECTORY = 0x2c;
        public const int FAT_DISK_LABEL = 0x47;
        public const int FAT_DISK_LABEL_SIZE = 11;
        public const int FAT_DISK_OEM = 0x3;
        public const int FAT_DISK_OEM_SIZE = 8;
        public const int FAT_DISK_FS = 0x52;
        public const int FAT_DISK_FS_SIZE = 8;
        public const int FAT_CREATION_DATE = 0x10;
        public const int FAT_CHANGE_DATE = 0x16;

        public const int FAT16_SECTORS_PER_FAT = 0x16;
        public const int FAT16_DISK_FS = 0x36;
        public const int FAT16_DISK_FS_SIZE = 8;
        public const int FAT16_DISK_LABEL = 0x2b;
        public const int FAT16_DISK_LABEL_SIZE = 11;
        public const int FAT16_TOTAL_SECTORS = 0x13;
        public const int FAT16_ROOT_ENTRIES = 0x11;

        public enum FatType
        {
            Fat16,
            Fat32
        }

        private readonly Dictionary<ulong, ulong> _cache = new();
        private readonly ILogger<FatSystem> _logger;

        public FatSystem(ILogger<FatSystem> logger)
        {
            _logger = logger;
            this.TotalSize = ulong.MinValue;
            this.Type = FatType.Fat32;
        }

        #region Public Properties

        // File descriptor
        public long GlobalOffset { get; private set; }
        public Stream ImageStream { get; private set; }
        public bool CanWrite => ImageStream.CanWrite;

        // Header values
        public FatType Type { get; private set; }
        public string DiskLabel { get; private set; }
        public string OemName { get; private set; }
        public string FsType { get; private set; }

        public ulong TotalSectors { get; private set; }
        public ushort BytesPerSector { get; private set; }
        public ulong SectorsPerCluster { get; private set; }
        public ushort ReservedSectors { get; private set; }
        public byte Fats { get; private set; }
        public ulong SectorsPerFat { get; private set; }
        public ulong RootDirectory { get; private set; }
        public ulong Reserved { get; private set; }
        public ulong Strange { get; private set; }
        public uint Bits { get; private set; }

        // Specific to FAT16
        public ushort RootEntries { get; private set; }
        public ulong RootClusters { get; private set; }

        // Computed values
        public ulong FatStart { get; private set; }
        public ulong DataStart { get; private set; }
        public ulong BytesPerCluster { get; private set; }
        public ulong TotalSize { get; private set; }
        public ulong DataSize { get; private set; }
        public ulong FatSize { get; private set; }
        public ulong TotalClusters { get; private set; }

        // FAT Cache
        private bool _isCacheEnabled = false;
        public bool IsCacheEnabled => _isCacheEnabled;

        // Stats values
        public bool StatsComputed { get; private set; }
        public ulong FreeClusters { get; private set; }

        #endregion Public Properties

        public async Task EnableCache(CancellationToken cancellationToken = default)
        {
            if (_isCacheEnabled)
            {
                return;
            }
            _logger.LogInformation("Computing FAT cache...");
            for (ulong cluster = 0; cluster < this.TotalClusters; cluster++)
            {
                _cache[cluster] = await GetNextCluster(cluster, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            _isCacheEnabled = true;
        }

        private async Task<int> ReadData(long address, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            this.ImageStream.Seek(address, SeekOrigin.Begin);
            var result = await this.ImageStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            return result;
        }

        private async Task<int> WriteData(long address, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!this.CanWrite)
            {
                throw new InvalidOperationException("Cannot write to stream since it is not writable.");
            }
            this.ImageStream.Seek(address, SeekOrigin.Begin);
            await this.ImageStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            return buffer.Length;
        }

        private async Task ParseHeader(CancellationToken cancellationToken = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(128);
            try
            {
                await ReadData(0x0, buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                this.BytesPerSector = (ushort)(Utilities.ReadShort(buffer, FAT_BYTES_PER_SECTOR) & 0xffff);
                this.SectorsPerCluster = (ulong)(buffer[FAT_SECTORS_PER_CLUSTER] & 0xff);
                this.ReservedSectors = (ushort)(Utilities.ReadShort(buffer, FAT_RESERVED_SECTORS) & 0xffff);
                this.OemName = Encoding.ASCII.GetString(buffer, FAT_DISK_OEM, FAT_DISK_OEM_SIZE);
                this.Fats = buffer[FAT_FATS];

                this.SectorsPerFat = (ushort)(Utilities.ReadShort(buffer, FAT16_SECTORS_PER_FAT) & 0xffff);

                if (this.SectorsPerFat != 0)
                {
                    this.Type = FatType.Fat16;
                    this.DiskLabel = Encoding.ASCII.GetString(buffer, FAT16_DISK_LABEL, FAT16_DISK_LABEL_SIZE);
                    this.FsType = Encoding.ASCII.GetString(buffer, FAT16_DISK_FS, FAT16_DISK_FS_SIZE);
                    this.RootEntries = (ushort)(Utilities.ReadShort(buffer, FAT16_ROOT_ENTRIES) & 0xffff);
                    this.RootDirectory = 0;

                    this.TotalSectors = (ulong)(Utilities.ReadShort(buffer, FAT16_TOTAL_SECTORS) & 0xffff);
                    if (this.TotalSectors == 0)
                    {
                        this.TotalSectors = Utilities.ReadLong(buffer, FAT_TOTAL_SECTORS) & 0xffffffff;
                    }

                    ulong rootDirSectors = (this.RootEntries * FatEntry.EntrySize + this.BytesPerSector - 1UL) / this.BytesPerSector;
                    ulong dataSectors = this.TotalSectors - (this.ReservedSectors + (ulong)this.Fats * this.SectorsPerFat + rootDirSectors);
                    ulong totalClusters = dataSectors / this.SectorsPerCluster;
                    this.Bits = (totalClusters > MAX_FAT12) ? 16 : 12;
                }
                else
                {
                    this.Type = FatType.Fat32;
                    this.Bits = 32;
                    this.SectorsPerFat = Utilities.ReadLong(buffer, FAT_SECTORS_PER_FAT) & 0xffffffff;
                    this.TotalSectors = Utilities.ReadLong(buffer, FAT_TOTAL_SECTORS) & 0xffffffff;
                    this.DiskLabel = Encoding.ASCII.GetString(buffer, FAT_DISK_LABEL, FAT_DISK_LABEL_SIZE);
                    this.RootDirectory = Utilities.ReadLong(buffer, FAT_ROOT_DIRECTORY) & 0xffffffff;
                    this.FsType = Encoding.ASCII.GetString(buffer, FAT_DISK_FS, FAT_DISK_FS_SIZE);
                }

                if (this.BytesPerSector != 512)
                {
                    _logger.LogWarning("Bytes per sector is not 512: {BytesPerSector}.", this.BytesPerSector);
                }

                if (this.SectorsPerCluster > 128)
                {
                    _logger.LogWarning("Sectors per cluster high: {SectorsPerCluster}.", this.SectorsPerCluster);
                }

                if (this.Fats == 0)
                {
                    _logger.LogWarning("Fats numbner is 0.");
                }

                if (this.RootDirectory != 2 && this.Type == FatType.Fat32)
                {
                    _logger.LogWarning("Root directory is not 2: {RootDirectory}.", this.RootDirectory);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async Task<ulong> GetNextCluster(ulong cluster, long fat = 0, CancellationToken cancellationToken = default)
        {
            if (!IsValidCluster(cluster))
            {
                return 0;
            }

            if (this.IsCacheEnabled)
            {
                return _cache[cluster];
            }

            int bytes = this.Bits == 32 ? 4 : 2;
            var buffer = ArrayPool<byte>.Shared.Rent(bytes);
            try
            {
                long readAddress = (long)this.FatStart;
                readAddress += (long)this.FatSize * fat;
                readAddress += this.Bits * (long)cluster / 8;
                await this.ReadData(readAddress, buffer.AsMemory(0, bytes), cancellationToken).ConfigureAwait(false);

                ulong nextCluster;

                if (this.Type == FatType.Fat32)
                {
                    nextCluster = Utilities.ReadLong(buffer, 0) & 0x0fffffff;

                    if (nextCluster >= 0x0ffffff0)
                    {
                        return Last;
                    }
                    else
                    {
                        return nextCluster;
                    }
                }
                else
                {
                    nextCluster = (ulong)(Utilities.ReadShort(buffer, 0) & 0xffff);

                    if (this.Bits == 12)
                    {
                        ulong bit = cluster * this.Bits;
                        if (bit % 8 != 0)
                        {
                            nextCluster >>= 4;
                        }
                        nextCluster &= 0xfff;
                        if (nextCluster >= 0xff0)
                        {
                            return Last;
                        }
                        else
                        {
                            return nextCluster;
                        }
                    }
                    else
                    {
                        if (nextCluster >= 0xfff0)
                        {
                            return Last;
                        }
                        else
                        {
                            return nextCluster;
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async Task<bool> WriteNextCluster(ulong cluster, ulong nextCluster, long fat = 0, CancellationToken cancellationToken = default)
        {
            int bytes = this.Bits == 32 ? 4 : 2;
            var buffer = ArrayPool<byte>.Shared.Rent(bytes);
            try
            {
                if (!IsValidCluster(cluster))
                {
                    throw new InvalidOperationException("Trying to access a cluster outside bounds");
                }

                long offset = (long)this.FatStart;
                offset += (long)this.FatSize * fat;
                offset += this.Bits * (long)cluster / 8;

                if (this.Bits == 12)
                {
                    await this.ReadData(offset, buffer.AsMemory(0, bytes), cancellationToken).ConfigureAwait(false);
                    ulong bit = cluster * this.Bits;

                    if (bit % 8 != 0)
                    {
                        buffer[0] = (byte)(((byte)(nextCluster & 0x0f) << 4) | (buffer[0] & 0x0f));
                        buffer[1] = (byte)((nextCluster >> 4) & 0xff);
                    }
                    else
                    {
                        buffer[0] = (byte)(nextCluster & 0xff);
                        buffer[1] = (byte)((buffer[1] & 0xf0) | ((byte)(nextCluster >> 8) & 0x0f));
                    }
                }
                else
                {
                    for (int i = 0; i < (this.Bits / 8); i++)
                    {
                        buffer[i] = (byte)((nextCluster >> (8 * i)) & 0xff);
                    }
                }

                var writtenData = await WriteData(offset, buffer.AsMemory(0, bytes), cancellationToken);
                return writtenData == bytes;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public bool IsValidCluster(ulong cluster)
        {
            return cluster < this.TotalClusters;
        }

        private long GetClusterAddress(ulong cluster, bool isRoot = false)
        {
            if (this.Type == FatType.Fat32 || !isRoot)
            {
                cluster -= 2;
            }

            long addr = (long)(this.DataStart + this.BytesPerSector * this.SectorsPerCluster * cluster);

            if (this.Type == FatType.Fat16 && !isRoot)
            {
                addr += this.RootEntries * (long)FatEntry.EntrySize;
            }

            return addr;
        }

        public async Task<GetEntriesResult> GetEntries(ulong cluster, CancellationToken cancellationToken = default)
        {
            bool isRoot = false;
            bool contiguous = false;
            int foundEntries = 0;
            int badEntries = 0;
            bool isValid = false;
            HashSet<ulong> visited = new HashSet<ulong>();
            var entries = new List<FatEntry>();
            var hasFreeClusters = false;
            var filename = new StringBuilder();
            var clusters = 0;

            if (cluster == 0 && this.Type == FatType.Fat32)
            {
                cluster = this.RootDirectory;
            }

            isRoot = this.Type == FatType.Fat16 && cluster == this.RootDirectory;

            if (cluster == this.RootDirectory)
            {
                isValid = true;
            }

            if (IsValidCluster(cluster))
            {
                return null;
            }

            do
            {
                bool localZero = false;
                int localFound = 0;
                int localBadEntries = 0;
                long address = GetClusterAddress(cluster, isRoot);
                if (visited.Contains(cluster))
                {
                    _logger.LogError("Looping directory!");
                    break;
                }
                visited.Add(cluster);
                var buffer = ArrayPool<byte>.Shared.Rent((int)FatEntry.EntrySize);
                try
                {
                    for (ulong i = 0; i < this.BytesPerCluster; i += FatEntry.EntrySize)
                    {
                        // Reading data
                        await this.ReadData(address, buffer.AsMemory(0, (int)FatEntry.EntrySize), cancellationToken).ConfigureAwait(false);

                        // Creating entry
                        FatEntry entry;

                        var attributes = buffer[FatEntry.FatOffsets.Attributes];
                        if (attributes == FatEntry.FatAttributes.LongFile)
                        {
                            // Long file part
                            filename.Append(buffer);
                        }
                        else
                        {
                            var shortName = Encoding.ASCII.GetString(buffer, 0, 11);
                            var longName = filename.ToString();
                            var size = Utilities.ReadLong(buffer, FatEntry.FatOffsets.FileSize) & 0xffffffff;
                            var entryCluster = (Utilities.ReadShort(buffer, FatEntry.FatOffsets.ClusterLow) & 0xffff) | (Utilities.ReadShort(buffer, FatEntry.FatOffsets.ClusterHigh) << 16);
                            var creationDate = Utilities.ReadDateTime(buffer, FAT_CREATION_DATE);
                            var changeDate = Utilities.ReadDateTime(buffer, FAT_CHANGE_DATE);
                            entry = new FatEntry(longName, shortName, cluster, size, address, creationDate, changeDate, attributes);
                            if (!buffer.Take((int)FatEntry.EntrySize).All(i => i == 0))
                            {
                                if (entry.IsCorrect() && IsValidCluster((ulong)entryCluster))
                                {

                                    localFound++;
                                    foundEntries++;

                                    if (!isValid && entry.GetFileName() == "." && entry.Cluster == cluster)
                                    {
                                        isValid = true;
                                    }
                                    entries.Add(entry);
                                }
                                else
                                {
                                    localBadEntries++;
                                    badEntries++;
                                }

                                localZero = false;
                            }
                            else
                            {
                                localZero = true;
                            }
                        }

                        address += unchecked((long)FatEntry.EntrySize);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
                var previousCluster = cluster;

                if (isRoot)
                {
                    if (cluster + 1 < this.RootClusters)
                    {
                        cluster++;
                    }
                    else
                    {
                        cluster = Last;
                    }
                }
                else
                {
                    cluster = await GetNextCluster(cluster, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                clusters++;

                if (cluster == 0 || contiguous)
                {
                    contiguous = true;

                    hasFreeClusters = true;

                    if (!localZero && localFound > 0 && localBadEntries < localFound)
                    {
                        cluster = previousCluster + 1;
                    }
                    else
                    {
                        if (localFound == 0)
                        {
                            clusters--;
                        }
                        break;
                    }
                }

                if (!isValid)
                {
                    if (badEntries > foundEntries)
                    {
                        _logger.LogError("Entries don't look good, this is maybe not a directory.");
                        return null;
                    }
                }
            } while (cluster != Last);

            return new GetEntriesResult(entries, clusters, hasFreeClusters);
        }

        public async Task ReadFile(ulong cluster, ulong size, Stream output, bool isDeleted, CancellationToken cancellationToken = default)
        {
            bool contiguous = isDeleted;

            while ((size != 0) && cluster != Last)
            {
                ulong currentCluster = cluster;
                ulong toRead = size;
                if (toRead > BytesPerCluster || size < 0)
                {
                    toRead = BytesPerCluster;
                }
                var buffer = ArrayPool<byte>.Shared.Rent((int)this.BytesPerCluster);
                try
                {
                    await this.ReadData(GetClusterAddress(cluster), buffer.AsMemory(0, (int)this.BytesPerCluster), cancellationToken).ConfigureAwait(false);

                    size -= toRead;

                    await output.WriteAsync(buffer.AsMemory(0, (int)this.BytesPerCluster), cancellationToken).ConfigureAwait(false);

                    if (contiguous)
                    {
                        if (isDeleted)
                        {
                            do
                            {
                                cluster++;
                            } while (!await IsFreeCluster(cluster, cancellationToken));
                        }
                        else
                        {
                            if (!await IsFreeCluster(cluster, cancellationToken))
                            {
                                _logger.LogWarning("Contiguous file contains cluster that seems allocated.\nTrying to disable contiguous mode.");
                                contiguous = false;
                                cluster = await GetNextCluster(cluster, cancellationToken: cancellationToken);
                            }
                            else
                            {
                                cluster++;
                            }
                        }
                    }
                    else
                    {
                        cluster = await GetNextCluster(currentCluster, cancellationToken: cancellationToken);

                        if (cluster == 0)
                        {
                            _logger.LogWarning("One of your file's cluster is 0 (maybe FAT is broken, have a look to -2 and -m).\nTrying to enable contigous mode.");
                            contiguous = true;
                            cluster = currentCluster + 1;
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        public async Task Initialize(Stream imageStream, long globalOffset = 0, CancellationToken cancellationToken = default)
        {
            this.ImageStream = imageStream;
            this.GlobalOffset = globalOffset;
            // Parsing header
            await this.ParseHeader(cancellationToken).ConfigureAwait(false);

            // Computing values
            this.FatStart = (ulong)this.BytesPerSector * this.ReservedSectors;
            this.DataStart = this.FatStart + this.Fats * this.SectorsPerFat * this.BytesPerSector;
            this.BytesPerCluster = this.BytesPerSector * this.SectorsPerCluster;
            this.TotalSize = this.TotalSectors * this.BytesPerSector;
            this.FatSize = this.SectorsPerFat * this.BytesPerSector;
            this.TotalClusters = (this.FatSize * 8) / this.Bits;
            this.DataSize = this.TotalClusters * this.BytesPerCluster;

            if (this.Type == FatType.Fat16)
            {
                ulong rootBytes = (ulong)this.RootEntries * 32U;
                this.RootClusters = rootBytes / this.BytesPerCluster + ((rootBytes % this.BytesPerCluster == 0) ? 1U : 0U);
            }
        }

        public async Task<FatEntry> FindDirectory(FatPath path, CancellationToken cancellationToken = default)
        {
            var cluster = this.RootDirectory;
            FatEntry outputEntry = null;

            for (int i = 0; i < path.Parts.Length; i++)
            {
                if (path.Parts[i] != "")
                {
                    var part = path.Parts[i].ToLowerInvariant();
                    var entries = await GetEntries(cluster, cancellationToken: cancellationToken).ConfigureAwait(false);
                    bool found = false;
                    foreach (var entry in entries.Entries)
                    {
                        string name = entry.GetFileName();
                        if (entry.IsDirectory && name.ToLowerInvariant() == part)
                        {
                            outputEntry = entry;
                            cluster = entry.Cluster;
                            found = true;
                        }
                    }

                    if (!found)
                    {
                        return null;
                    }
                }
            }

            return outputEntry;
        }

        public async Task<FatEntry> FindFile(FatPath path, CancellationToken cancellationToken = default)
        {
            var dirname = path.DirectoryName;
            var basename = path.BaseName;
            basename = basename.ToLowerInvariant();

            var parentEntry = await FindDirectory(new FatPath(dirname), cancellationToken).ConfigureAwait(false);
            if (parentEntry != null)
            {
                var entries = await GetEntries(parentEntry.Cluster, cancellationToken);

                foreach (var entry in entries.Entries)
                {
                    if (entry.GetFileName() == basename)
                    {
                        return entry;
                    }
                }
            }

            return null;
        }

        public async Task ReadFile(FatPath path, Stream output, CancellationToken cancellationToken = default)
        {
            var entry = await FindFile(path, cancellationToken).ConfigureAwait(false);
            if (entry != null)
            {
                bool contiguous = false;
                if (entry.IsErased && await IsFreeCluster(entry.Cluster, cancellationToken))
                {
                    _logger.LogWarning("Trying to read a deleted file, enabling deleted mode.");
                    contiguous = true;
                }
                await ReadFile(entry.Cluster, entry.Size, output, contiguous, cancellationToken);
            }
        }

        public FatEntry GetRootEntry() => new("/", "/", RootDirectory, 0, 0, DateTime.MinValue, DateTime.MinValue, FatEntry.FatAttributes.Dir);

        public async Task<bool> IsFreeCluster(ulong cluster, CancellationToken cancellationToken = default)
        {
            var nextCluster = await GetNextCluster(cluster, cancellationToken: cancellationToken).ConfigureAwait(false);
            return nextCluster == 0;
        }

        private async Task ComputeStats(CancellationToken cancellationToken = default)
        {
            if (StatsComputed)
            {
                return;
            }

            StatsComputed = true;

            this.FreeClusters = 0;
            for (ulong cluster = 0; cluster < TotalClusters; cluster++)
            {
                if (await IsFreeCluster(cluster, cancellationToken).ConfigureAwait(false))
                {
                    this.FreeClusters++;
                }
            }
        }

        private async Task RewriteUnallocated(bool random, CancellationToken cancellationToken = default)
        {
            int total = 0;
            var randomGenerator = new Random();
            for (ulong cluster = 0; cluster < this.TotalClusters; cluster++)
            {
                if (await IsFreeCluster(cluster, cancellationToken))
                {
                    var buffer = ArrayPool<byte>.Shared.Rent((int)this.BytesPerCluster);
                    try
                    {
                        if (random)
                        {
                            randomGenerator.NextBytes(buffer.AsSpan(0, (int)this.BytesPerCluster));
                        }
                        await WriteData(GetClusterAddress(cluster), buffer.AsMemory(0, (int)this.BytesPerCluster), cancellationToken);
                        total++;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
        }
    }
}