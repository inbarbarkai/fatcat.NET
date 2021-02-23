using System.Collections.Generic;

namespace fatcat.Core
{
    public class GetEntriesResult
    {
        public GetEntriesResult(IList<FatEntry> entries, int clusters, bool hasFreeClusters)
        {
            this.Entries = entries;
            this.Clusters = clusters;
            this.HasFreeClusters = hasFreeClusters;
        }

        public bool HasFreeClusters { get; }

        public int Clusters { get; }

        public IList<FatEntry> Entries { get; }
    }
}