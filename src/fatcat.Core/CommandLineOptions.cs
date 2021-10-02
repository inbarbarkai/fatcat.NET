using CommandLine;

namespace fatcat
{
    public abstract class CommandLineOptions
    {
        [Option(Required = true)]
        public string ImagePath { get; set; }

        [Option('O', "offset", Default = 0)]
        public long GlobalOffset { get; set; }
    }

    [Verb("list")]
    public class ListPath : CommandLineOptions
    {
        [Option('p', "path", Required = true)]
        public string Path { get; set; }

        [Option('d', "deleted")]
        public bool ListDeleted { get; set; }
    }
}