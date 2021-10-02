using System;
using System.Diagnostics;
using System.Linq;

namespace fatcat.Core
{
    [DebuggerDisplay("{Path}")]
    public class FatPath
    {
#if NETSTANDARD2_1
        public const char PathDelimiter = '/'; 
#else
        public readonly char[] PathDelimiter = new[] { '/' };
#endif

        public FatPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }
            this.Path = path;
            this.Parts = path.Split(PathDelimiter, StringSplitOptions.RemoveEmptyEntries); 
        }

        public string Path { get; }

        public string DirectoryName
        {
            get
            {
                var name = "/";
                foreach (var part in this.Parts.Take(this.Parts.Length - 1))
                {
#if NETSTANDARD2_1
                    name += part + PathDelimiter;
#else
                    name += part + PathDelimiter[0];
#endif
                }
                return name;
            }
        }

        public string BaseName => this.Parts.Last();

        public string[] Parts { get; }

        public static implicit operator FatPath(string path) => new FatPath(path);
    }
}