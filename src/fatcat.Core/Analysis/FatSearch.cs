using System.Threading;
using System.Threading.Tasks;
using fatcat.Core;
using Microsoft.Extensions.Logging;

namespace fatcat.Analysis
{
    public class FatSearch : FatWalk
    {
        private readonly ILogger<FatSearch> _logger;
        private ulong _searchCluster;
        private int _found;

        public FatSearch(FatSystem system, ILogger<FatSearch> logger) : base(system)
        {
            _logger = logger;
        }

        protected override bool WalkErased => true;

        public async Task Search(ulong cluster, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Searching for an entry referencing {ClusterId}.", cluster);
            _searchCluster = cluster;
            await this.Walk(cluster, cancellationToken).ConfigureAwait(false);
            if (_found == 0)
            {
                _logger.LogInformation("No entry was found.");
            }
        }

        protected override Task OnEntry(FatEntry parent, FatEntry entry, string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (entry.Cluster == _searchCluster)
            {
                _logger.LogDebug("Found '{EntryName}' in directory '{DirectoryName}'(ClusterId)", name, parent.GetFileName(), parent.Cluster);
                _found++;
            }
            return Task.CompletedTask;
        }
    }
}