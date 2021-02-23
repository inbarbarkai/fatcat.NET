using System;

namespace fatcat.Core
{
    public static class FatEntryExtensions
    {
        public static bool IsCorrect(this FatEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }
            if (entry.Attributes != 0 && !entry.IsDirectory && !entry.IsFile)
            {
                return false;
            }

            if (entry.IsDirectory && entry.Cluster == 0 && entry.GetFileName() != "..")
            {
                return false;
            }

            return false;
        }
    }
}