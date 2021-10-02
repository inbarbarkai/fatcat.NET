using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using fatcat.Core;

namespace fatcat.Display
{
    public static class FatSystemDisplayExtensions
    {
        public static async Task List(this FatSystem system, FatPath path, bool listDeleted = false, CancellationToken cancellationToken = default)
        {
            var directory = await system.FindDirectory(path, cancellationToken).ConfigureAwait(false);
            if (directory != null)
            {
                await system.List(directory.Cluster, listDeleted, cancellationToken);
            }
        }

        public static async Task List(this FatSystem system, ulong cluster, bool listDeleted = false, CancellationToken cancellationToken = default)
        {
            var entries = await system.GetEntries(cluster, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("Directory cluster: {0}", cluster);
            if (entries.HasFreeClusters)
            {
                Console.WriteLine("Warning: this directory has free clusters that was read contiguously.");
            }
            system.List(entries.Entries, listDeleted);
        }

        public static void List(this FatSystem system, IEnumerable<FatEntry> entries, bool listDeleted = false, CancellationToken cancellationToken = default)
        {
            foreach (var entry in entries)
            {
                if (entry.IsErased && !listDeleted)
                {
                    continue;
                }

                if (entry.IsDirectory)
                {
                    Console.Write("d");
                }
                else
                {
                    Console.Write("f");
                }

                string name = "/" + entry.GetFileName();
                if (entry.IsDirectory)
                {
                    name += "/";
                }

                string shrtname = entry.GetShortFileName();
                if (name != shrtname)
                {
                    name += " (" + shrtname + ")";
                }

                Console.Write(" {0:yyyy-MM-dd HH:mm:ss} ", entry.ChangeDate);
                Console.Write(" {0}", name.PadLeft(50));

                Console.Write(" c={0}", entry.Cluster);

                if (!entry.IsDirectory)
                {
                    string pretty = Utilities.PrettySize(entry.Size);
                    Console.Write(" s={0} ({1})", entry.Size, pretty);
                }

                if (entry.IsHidden)
                {
                    Console.Write(" h");
                }
                if (entry.IsErased)
                {
                    Console.Write(" d");
                }

                Console.Write("\n");
            }
        }
    }
}
