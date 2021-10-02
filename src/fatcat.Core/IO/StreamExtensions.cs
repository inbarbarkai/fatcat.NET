using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace fatcat.IO
{
#if NETSTANDARD2_0
    internal static class StreamExtensions
    {
        internal static async Task<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var temp = new byte[buffer.Length];
            var result = await stream.ReadAsync(temp, 0, temp.Length, cancellationToken).ConfigureAwait(false);
            temp.CopyTo(buffer);
            return result;
        }

        internal static async Task WriteAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var temp = new byte[buffer.Length];
            buffer.CopyTo(temp);
            await stream.WriteAsync(temp, 0, temp.Length, cancellationToken).ConfigureAwait(false);
        }
    }
#endif
}
