using System;
using System.Diagnostics;

namespace fatcat.Core
{
    [DebuggerDisplay("Name = {GetShortFileName()}, IsDirectory = {IsDirectory}")]
    public class FatEntry
    {
        public const ulong EntrySize = 0x20;
        // Offsets

        public class FatOffsets
        {
            public const int ShortName = 0x00;
            public const int Attributes = 0x0b;
            public const int ClusterLow = 0x1a;
            public const int ClusterHigh = 0x14;
            public const int FileSize = 0x1c;
        }
        // Attributes
        public class FatAttributes
        {
            public const byte Hide = (1 << 1);
            public const byte Dir = (1 << 4);
            public const byte LongFile = (0xf);
            public const byte File = (0x20);
        }
        // Prefix used for erased files
        public const int Erased = 0xe5;
        public FatEntry(string longName, string shortName, ulong cluster, ulong size, long address, DateTime creationDate, DateTime changeDate, byte attributes, bool isErased)
        {
            this.LongName = longName;
            this.ShortName = shortName;
            this.Cluster = cluster;
            this.Size = size;
            this.Address = address;
            this.CreationDate = creationDate;
            this.ChangeDate = changeDate;
            this.Attributes = attributes;
            this.IsErased = isErased;
        }

        public bool IsDirectory => (this.Attributes & FatAttributes.Dir) == FatAttributes.Dir;
        public bool IsFile => (this.Attributes & FatAttributes.File) == FatAttributes.File;
        public bool IsHidden => (this.Attributes & FatAttributes.Hide) == FatAttributes.Hide;
        public bool IsErased { get; }
        public string ShortName { get; }
        public string LongName { get; }
        public byte Attributes { get; }
        public ulong Cluster { get; }
        public ulong Size { get; }
        public DateTime CreationDate { get; }
        public DateTime ChangeDate { get; }
        public long Address { get; }

        public string GetFileName()
        {
            if (string.IsNullOrEmpty(this.LongName))
            {
                return this.GetShortFileName();
            }
            return this.LongName;
        }

        public string GetShortFileName()
        {
            var ext = this.ShortName.Substring(8, 3);
            var name = this.ShortName.Substring(0, 8);
            if (this.IsErased)
            {
                name = name.Substring(1);
            }
            if (string.IsNullOrEmpty(ext))
            {
                return name;
            }
            return name + "." + ext;
        }
    }
}