namespace fatcat.Analysis
{
    public record FatChain
    {
        public FatChain()
        {
            
        }

        public FatChain(ulong startCluster, ulong endCluster, bool isOrphaned, bool isDirectory, int elementCount, int length, ulong size)
        {
            this.StartCluster = startCluster;
            this.EndCluster = endCluster;
            this.IsOrphaned = isOrphaned;
            this.IsDirectory = isDirectory;
            this.ElementCount = elementCount;
            this.Length = length;
            this.Size = size;
        }

        public ulong StartCluster { get; set; }
        public ulong EndCluster { get; set; }
        public bool IsOrphaned { get; set; }
        public bool IsDirectory { get; set; }
        public int ElementCount { get; set; }
        public int Length { get; set; }
        public ulong Size { get; set; }
    }
}