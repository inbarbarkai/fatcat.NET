using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using fatcat.Core;

namespace fatcat.Analysis
{
    public abstract class FatWalk : FatModule
    {
        protected abstract bool WalkErased { get; }

        public FatWalk(FatSystem system) : base(system)
        {
        }

        protected async Task Walk(ulong cluster = 0, CancellationToken cancellationToken = default)
        {
            FatEntry root;

            if (cluster == this.System.RootDirectory)
            {
                root = this.System.GetRootEntry();
            }
            else
            {
                root = new FatEntry("/", "/", cluster, 0, 0, DateTime.MinValue, DateTime.MinValue, FatEntry.FatAttributes.Dir, false);
            }
            var visited = new HashSet<ulong>();
            await this.OnEntry(root, root, "/", cancellationToken).ConfigureAwait(false);
            await ExecuteWalk(visited, root, "/", cancellationToken).ConfigureAwait(false);
        }
        protected virtual Task OnEntry(FatEntry parent, FatEntry entry, string name, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnDirectory(FatEntry parent, FatEntry entry, string name, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        private async Task ExecuteWalk(HashSet<ulong> visited, FatEntry currentEntry, string name, CancellationToken cancellationToken = default)
        {
            var cluster = currentEntry.Cluster;

            if (visited.Contains(cluster))
            {
                return;
            }

            visited.Add(cluster);

            var entries = await this.System.GetEntries(cluster, cancellationToken: cancellationToken);
            foreach (var entry in entries.Entries)
            {
                if ((!WalkErased) && entry.IsErased)
                {
                    continue;
                }

                if (entry.GetFileName() != "." && entry.GetFileName() != "..")
                {
                    string subname = name;

                    if (subname != "" && subname != "/")
                    {
                        subname += "/";
                    }

                    subname += entry.GetFileName();

                    if (entry.IsDirectory)
                    {
                        await this.OnDirectory(currentEntry, entry, subname, cancellationToken);
                        await this.ExecuteWalk(visited, entry, subname, cancellationToken);
                    }

                    await this.OnEntry(currentEntry, entry, subname, cancellationToken);
                }
            }
        }
    }
}