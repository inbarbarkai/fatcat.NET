using System.Threading;
using System.Threading.Tasks;

namespace fatcat.Core
{
    public static class FatSystemExtensions
    {
        public static async Task<GetEntriesResult> GetEntries(this FatSystem system, FatPath path, CancellationToken cancellationToken = default)
        {
            var directory = await system.FindDirectory(path, cancellationToken).ConfigureAwait(false);
            if (directory != null)
            {
                return await system.GetEntries(directory.Cluster, cancellationToken);
            }
            return null;
        }
    }
}
