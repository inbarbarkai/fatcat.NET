using System.Collections.Generic;

namespace fatcat.Core
{
    public class ListResult
    {
        public ListResult(IList<FatEntry> entries)
        {
            this.Entries = entries;
        }

        public IList<FatEntry> Entries { get; }
    }
}