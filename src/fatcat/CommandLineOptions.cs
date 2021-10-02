using CommandLine;

namespace fatcat
{
    public abstract class CommandLineOptions
    {
        [Option('i', "image", Required = true)]
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

    [Verb("read")]
    public class ReadFile : CommandLineOptions, IReadFileByCluster, IReadFileByPath
    {
        public string Path { get; set; }

        public ulong Cluster { get; set; }

        public ulong Size { get; set; }

        public bool IsDeleted { get; set; }

        [Option('o', "output", Required = true)]
        public string Output { get; set; }
    }

    public interface IReadFileByPath
    {
        [Option('p', "path", Required = true, SetName = "Path")]
        string Path { get; set; }
    }

    public interface IReadFileByCluster
    {
        [Option('c', "cluster", SetName = "Cluster", Required = true)]
        ulong Cluster { get; set; }

        [Option('s', "size", SetName = "Cluster", Required = true)]
        ulong Size { get; set; }

        [Option('d', "deleted")]
        bool IsDeleted { get; set; }
    }
}