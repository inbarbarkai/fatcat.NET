using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using fatcat.Core;
using Microsoft.Extensions.Logging;

namespace fatcat.Analysis
{
    public class FatChains : FatModule
    {
        private readonly Dictionary<ulong, IList<FatEntry>> _orphanEntries = new();
        private readonly Dictionary<ulong, FatEntry> _clusterToEntry = new();
        private readonly ILogger<FatChains> _logger;
        private bool _saveEntries = false;
        private bool _exploreDamage = false;

        public FatChains(FatSystem system, ILogger<FatChains> logger) : base(system)
        {
            _logger = logger;
        }

        public async Task<IList<FatChain>> ChainsAnalysis(CancellationToken cancellationToken = default)
        {
            await this.System.EnableCache(cancellationToken);
            _logger.LogInformation("Building the chains.");
            var chains = await this.FindChains(cancellationToken);

            _logger.LogInformation("Found {ChainCount} chains.", chains.Count);
            _logger.LogInformation("Running the recursive differential analysis.");

            var visited = new HashSet<ulong>();
            _exploreDamage = false;
            _saveEntries = false;
            await this.RecursiveExploration(chains, visited, this.System.RootDirectory, cancellationToken: cancellationToken);
            visited.Add(0);

            _logger.LogInformation("Having a look at the chains.");
            _saveEntries = true;
            await this.ExploreChains(chains, visited, cancellationToken);
            var orphandChains = GetOrphands(chains);
            return orphandChains;
        }

        private async Task ExploreChains(Dictionary<ulong, FatChain> chains, HashSet<ulong> visited, CancellationToken cancellationToken = default)
        {
            bool foundNew;
            _exploreDamage = true;
            do
            {
                foundNew = false;
                foreach (var chain in chains.Values)
                {
                    if (chain.IsOrphaned)
                    {
                        var entries = await this.System.GetEntries(chain.StartCluster, cancellationToken).ConfigureAwait(false);
                        if (entries.Entries.Count > 0)
                        {
                            chain.IsDirectory = true;
                            if (await this.RecursiveExploration(chains, visited, chain.StartCluster, entries.Entries, cancellationToken))
                            {
                                foundNew = true;
                            }
                        }
                    }
                }
            } while (foundNew);
        }

        private async Task<bool> RecursiveExploration(Dictionary<ulong, FatChain> chains, HashSet<ulong> visited, ulong cluster, IEnumerable<FatEntry> inputEntries = null, CancellationToken cancellationToken = default)
        {
            if (visited.Contains(cluster))
            {
                return false;
            }

            if (!_exploreDamage && await this.System.GetNextCluster(cluster, cancellationToken: cancellationToken) == 0)
            {
                return false;
            }

            visited.Add(cluster);

            bool foundNew = false;

            _logger.LogDebug("Exploring {ClusterId}.", cluster);

            var entries = inputEntries ?? (await this.System.GetEntries(cluster, cancellationToken)).Entries;

            foreach (var entry in entries)
            {
                var entryCluster = entry.Cluster;
                bool wasOrphaned = false;

                if (entry.IsErased)
                {
                    continue;
                }

                // Search the cluster in the previously visited chains, if it
                // exists, mark it as non-orphaned
                if (!chains.ContainsKey(entryCluster))
                {
                    if (entry.GetFileName() != ".." && entry.GetFileName() != ".")
                    {
                        if (chains[cluster].IsOrphaned)
                        {
                            wasOrphaned = true;

                            if (_saveEntries)
                            {
                                _orphanEntries[cluster].Add(entry);
                                _clusterToEntry[entryCluster] = entry;
                            }
                        }

                        chains[entryCluster].IsOrphaned = false;

                        if (!entry.IsDirectory)
                        {
                            chains[entryCluster].Size = entry.Size;
                        }
                    }
                }
                else
                {
                    // Creating the entry
                    if (_exploreDamage && entry.GetFileName() != ".")
                    {
                        chains[entryCluster].StartCluster = cluster;
                        chains[entryCluster].EndCluster = cluster;
                        chains[entryCluster].IsDirectory = entry.IsDirectory;
                        chains[entryCluster].ElementCount = 1;
                        chains[entryCluster].IsOrphaned = (entry.GetFileName() == "..");

                        if (!chains[entryCluster].IsOrphaned)
                        {
                            wasOrphaned = true;
                            if (_saveEntries)
                            {
                                _orphanEntries[cluster].Add(entry);
                                _clusterToEntry[entryCluster] = entry;
                            }
                        }

                        foundNew = true;
                    }
                }

                if (entry.IsDirectory && entry.GetFileName() != "..")
                {
                    await this.RecursiveExploration(chains, visited, entryCluster, cancellationToken: cancellationToken);
                }

                if (wasOrphaned)
                {
                    chains[cluster].ElementCount += chains[entryCluster].ElementCount;
                    chains[cluster].Size += chains[entryCluster].Size;
                }
            }

            return foundNew;
        }

        private async Task<Dictionary<ulong, FatChain>> FindChains(CancellationToken cancellationToken = default)
        {
            var seen = new HashSet<ulong>();
            var chains = new Dictionary<ulong, FatChain>();

            for (var cluster = this.System.RootDirectory; cluster < this.System.TotalClusters; cluster++)
            {
                // This cluster is new
                if (!seen.Contains(cluster))
                {
                    // If this is an allocated cluster
                    if (!await this.System.IsFreeCluster(cluster, cancellationToken).ConfigureAwait(false))
                    {
                        var localSeen = new HashSet<ulong>();
                        var next = cluster;
                        int length = 1;
                        // Walking through the chain
                        while (true)
                        {
                            var tmp = await this.System.GetNextCluster(next, cancellationToken: cancellationToken);
                            if (tmp == FatSystem.Last || !this.System.IsValidCluster(tmp))
                            {
                                break;
                            }
                            if (localSeen.Contains(tmp))
                            {
                                _logger.LogError("Loop!");
                                break;
                            }
                            next = tmp;
                            length++;
                            seen.Add(next);
                            localSeen.Add(next);
                        }

                        var chain = new FatChain
                        {
                            StartCluster = cluster,
                            EndCluster = next,
                            Length = length
                        };

                        if (chain.StartCluster == this.System.RootDirectory)
                        {
                            chain.IsOrphaned = false;
                        }

                        chains[next] = chain;
                    }

                    seen.Add(cluster);
                }
            }

            var chainsByStart = new Dictionary<ulong, FatChain>();
            foreach (var chain in chains.Values)
            {
                chainsByStart[chain.StartCluster] = chain;
            }

            return chainsByStart;
        }
        private IList<FatChain> GetOrphands(Dictionary<ulong, FatChain> chains)
        {
            var orphanedChains = new List<FatChain>();

            foreach (var chain in chains.Values)
            {
                if (chain.StartCluster < 2)
                {
                    chain.IsOrphaned = false;
                }

                if (chain.IsOrphaned)
                {
                    if (!chain.IsDirectory)
                    {
                        chain.Size = (ulong)chain.Length * System.BytesPerCluster;
                    }
                    orphanedChains.Add(chain);
                }
            }

            return orphanedChains;
        }

        private async Task<(int size, bool isContiguous)> GetChainSize(ulong cluster, CancellationToken cancellationToken = default)
        {
            var visited = new HashSet<ulong>();
            int length = 0;
            bool stop;

            var isContiguous = true;

            do
            {
                stop = true;
                var currentCluster = cluster;
                visited.Add(cluster);
                length++;
                cluster = await this.System.GetNextCluster(cluster, cancellationToken: cancellationToken);
                if (this.System.IsValidCluster(cluster) && cluster != FatSystem.Last)
                {
                    if (currentCluster + 1 != cluster)
                    {
                        isContiguous = false;
                    }
                    if (visited.Contains(cluster))
                    {
                        _logger.LogError("Loop detected, {SourceCluster} points to {TargetCluster} that I already met.", currentCluster, cluster);
                    }
                    else
                    {
                        stop = false;
                    }
                }
            } while (!stop);

            return (length, isContiguous);
        }
    }
}