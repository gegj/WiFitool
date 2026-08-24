using System.Collections.Generic;

namespace WiFitool.Models
{
    internal sealed class AdbStatusInfo
    {
        public bool PortConnected { get; set; }
        public string DeviceState { get; set; }
        public string Serial { get; set; }
        public string TransportId { get; set; }
        public string DeviceType { get; set; }
        public string SoftwareVersion { get; set; }
        public AdbPartitionSpace System { get; set; }
        public AdbPartitionSpace Userdata { get; set; }

        public AdbStatusInfo()
        {
            DeviceState = "no-port";
            Serial = "";
            TransportId = "";
            DeviceType = "";
            SoftwareVersion = "";
        }
    }

    internal sealed class AdbPartitionSpace
    {
        public string Mount { get; set; }
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public long FreeBytes { get; set; }
    }

    internal sealed class ProcessInfo
    {
        public int Pid { get; set; }
        public int ParentPid { get; set; }
        public string User { get; set; }
        public string State { get; set; }
        public string Name { get; set; }
        public string Arguments { get; set; }
        public string ExecutablePath { get; set; }
        public bool IsCoreProcess { get; set; }
        public string RiskLevel { get; set; }
        public string DisplayKind { get { return IsCoreProcess ? "系统核心" : "普通"; } }
    }

    internal sealed class StartupSource
    {
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public string MatchedText { get; set; }
        public string MatchType { get; set; }
        public string Context { get; set; }
    }

    internal sealed class ProcessScanResult
    {
        public List<ProcessInfo> Processes { get; private set; }
        public Dictionary<int, List<StartupSource>> StartupSources { get; private set; }

        public ProcessScanResult()
        {
            Processes = new List<ProcessInfo>();
            StartupSources = new Dictionary<int, List<StartupSource>>();
        }
    }

}
