using System.Collections.Generic;

namespace WiFitool.Models
{
    internal sealed class ImageInfo
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public long TableOffset { get; set; }
        public int EraseBlockSize { get; set; }
        public bool IsStandalone { get; set; }
        public List<PartitionInfo> Partitions { get; set; }
    }
}
