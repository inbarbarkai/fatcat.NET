using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace fatcat.Core
{
    public abstract class FatModule
    {
        protected FatSystem System { get; }

        public FatModule(FatSystem system)
        {
            this.System = system;
        }

        public Task Initialize(Stream imageStream, long globalOffset = 0, CancellationToken cancellationToken = default)
        {
            return this.System.Initialize(imageStream, globalOffset, cancellationToken);
        }
    }
}