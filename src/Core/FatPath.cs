using System;
using System.Linq;

namespace fatcat.Core
{
    public class FatPath
    {
        public const char PathDelimiter = '/';

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
                var name = "";
                foreach (var part in this.Parts.Take(this.Parts.Length - 1))
                {
                    name += part + PathDelimiter;
                }
                return name;
            }
        }

        public string BaseName => this.Parts.Last();

        public string[] Parts { get; }

        public static implicit operator FatPath(string path) => new FatPath(path);
    }
}