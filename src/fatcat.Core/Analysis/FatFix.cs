using System.Threading;
using System.Threading.Tasks;
using fatcat.Core;
using Microsoft.Extensions.Logging;

namespace fatcat.Analysis
{
    public class FatFix : FatWalk
    {
        private readonly ILogger<FatFix> _logger;

        public FatFix(FatSystem system, ILogger<FatFix> logger) : base(system)
        {
            _logger = logger;
        }

        protected override bool WalkErased => false;

        public Task Fix(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Searching for damaged files & directories.");
            return Walk(cancellationToken: cancellationToken);
        }

        public async Task FixChain(ulong cluster, ulong size, CancellationToken cancellationToken = default(CancellationToken))
        {
            bool fixIt = true;

            if (size == 0)
            {
                _logger.LogError("Size is zero, not fixing.");
                return;
            }

            _logger.LogDebug("Fixing the FAT ({Clusters} clusters)", size);

            for (ulong i = 0; i < size; i++)
            {
                if (!await this.System.IsFreeCluster(cluster + i, cancellationToken).ConfigureAwait(false))
                {
                    fixIt = false;
                }
            }

            if (fixIt)
            {
                _logger.LogDebug("Clusters are free, fixing.");
                for (ulong i = 0; i < size; i++)
                {
                    if (await this.System.IsFreeCluster(cluster + i, cancellationToken).ConfigureAwait(false))
                    {
                        if (i == size - 1)
                        {
                            await this.System.WriteNextCluster(cluster + i, FatSystem.Last, 0, cancellationToken);
                            await this.System.WriteNextCluster(cluster + i, FatSystem.Last, 1, cancellationToken);
                        }
                        else
                        {
                            await this.System.WriteNextCluster(cluster + i, cluster + i + 1, 0, cancellationToken);
                            await this.System.WriteNextCluster(cluster + i, cluster + i + 1, 1, cancellationToken);
                        }
                    }
                }
            }
            else
            {
                _logger.LogInformation("There is allocated clusters in the list, not fixing.");
            }
        }

        protected override async Task OnEntry(FatEntry parent, FatEntry entry, string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            await base.OnEntry(parent, entry, name, cancellationToken).ConfigureAwait(false);
            var cluster = entry.Cluster;

            if (await this.System.IsFreeCluster(cluster, cancellationToken))
            {
                if (entry.IsDirectory)
                {
                    var result = await this.System.GetEntries(entry.Cluster, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Directory '{DirectoryName}'({ClusterId}) seems broken, trying to repair FAT.", name, cluster);
                    await FixChain(cluster, (ulong)result.Clusters, cancellationToken);
                }
                else
                {
                    _logger.LogInformation("File {Name}/{FileName} seems broken.", name, entry.GetFileName());
                    await FixChain(entry.Cluster, entry.Size / this.System.BytesPerCluster + 1, cancellationToken);
                }
            }
        }
    }
}