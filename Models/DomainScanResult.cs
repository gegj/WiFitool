using System;
using System.Collections.Generic;

namespace WiFitool.Models
{
    internal sealed class DomainScanResult
    {
        public string Address { get; set; }
        public string FilePath { get; set; }
        public string SourcePath { get; set; }
        public long Offset { get; set; }
        public string DnsStatus { get; set; }
        public bool IsIp { get; set; }
        public bool IsChecked { get; set; }
        public List<DomainScanOccurrence> Occurrences { get; private set; }
        public DomainScanResult() { Occurrences = new List<DomainScanOccurrence>(); }
        public string DisplayType { get { return IsIp ? "公网 IPv4" : "域名"; } }
    }

    internal sealed class DomainScanOccurrence
    {
        public string SourcePath { get; set; }
        public long Offset { get; set; }
        public string FilePath { get; set; }
    }
}
