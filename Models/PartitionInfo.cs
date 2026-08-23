namespace WiFitool.Models
{
    internal sealed class PartitionInfo
    {
        public string Name { get; set; }
        public string Media { get; set; }
        public long Offset { get; set; }
        public long Size { get; set; }
        public string FileSystem { get; set; }
        public string Compression { get; set; }
        public int BlockSize { get; set; }
        public int DictionarySize { get; set; }
        public string Filter { get; set; }
        public bool Xattrs { get; set; }
        public bool Tailends { get; set; }
        public bool Exportable { get; set; }
        public bool Duplicates { get; set; }
        public bool LittleEndian { get; set; }
        public int Jffs2PageSize { get; set; }
        public int Jffs2EraseBlockSize { get; set; }
        public long UsedBytes { get; set; }
        public bool Extracted { get; set; }
        public bool Modified { get; set; }
        public bool Repacked { get; set; }
        public uint SquashFsCreationTime { get; set; }
        public string SquashFsRootMode { get; set; }
        public uint SquashFsRootUid { get; set; }
        public uint SquashFsRootGid { get; set; }

        public bool CanExtract { get { return FileSystem == "SquashFS" || FileSystem == "JFFS2"; } }
        public string Status
        {
            get { return Modified ? "待导出" : Extracted ? "已解包" : CanExtract ? "可解包" : "只读"; }
        }
    }
}
