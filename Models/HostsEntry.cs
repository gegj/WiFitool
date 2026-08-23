namespace WiFitool.Models
{
    internal sealed class HostsEntry
    {
        public bool Enabled { get; set; }
        public string IpAddress { get; set; }
        public string HostNames { get; set; }
        public string Comment { get; set; }
        public int OriginalLineNumber { get; set; }
        public string OriginalText { get; set; }
        public bool IsRaw { get; set; }
    }
}
