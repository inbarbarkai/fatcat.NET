using System.IO;
using System.Threading;
using System.Threading.Tasks;
using fatcat.Core;
using Microsoft.Extensions.Logging;

namespace fatcat.Analysis
{
    public class FatExtract : FatWalk
    {
        private string _targetDirectory;

        private readonly ILogger<FatExtract> _logger;

        public FatExtract(FatSystem system, ILogger<FatExtract> logger) : base(system)
        {
            _logger = logger;
        }

        private bool _walkErased;
        protected override bool WalkErased => _walkErased;

        public Task Extract(ulong cluster, string directory, bool erased, CancellationToken cancellationToken = default)
        {
            _walkErased = erased;
            _targetDirectory = directory;
            return this.Walk(cluster, cancellationToken);
        }

        protected override async Task OnDirectory(FatEntry parent, FatEntry entry, string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            await base.OnDirectory(parent, entry, name, cancellationToken).ConfigureAwait(false);

            string directory = _targetDirectory + "/" + name;

            Directory.CreateDirectory(directory);
        }

        protected override async Task OnEntry(FatEntry parent, FatEntry entry, string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            await base.OnEntry(parent, entry, name, cancellationToken).ConfigureAwait(false);

            if (!entry.IsDirectory)
            {
                bool contiguous = false;

                if (entry.IsErased)
                {
                    _logger.LogInformation("Trying to read deleted file, enabling contiguous mode.");
                    contiguous = true;
                }

                string target = _targetDirectory + name;
                _logger.LogDebug("Extracting {FileName} to {DirectoryName}.", name, target);
                using (var stream = File.OpenWrite(target))
                {
                    await System.ReadFile(entry.Cluster, entry.Size, stream, contiguous, cancellationToken);
                }
            }
        }
    }
}