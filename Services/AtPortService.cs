using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WiFitool.Services
{
    internal sealed class AtPortInfo
    {
        public string PortName { get; set; }
        public string FriendlyName { get; set; }
        public bool IsNamedAtPort { get; set; }
        public override string ToString() { return string.IsNullOrWhiteSpace(FriendlyName) ? PortName : FriendlyName; }
    }

    internal sealed class AtPortConnection : IDisposable
    {
        private readonly SerialPort port;
        private readonly string portName;

        internal AtPortConnection(SerialPort port, string portName) { this.port = port; this.portName = portName; }

        public string Send(string command, int waitMilliseconds = 800)
        {
            LogService.Instance.Debug("AT", portName + " -> " + command + "，等待 " + waitMilliseconds + " ms");
            port.DiscardInBuffer();
            port.Write(command + "\r\n");
            Thread.Sleep(waitMilliseconds);
            var response = port.BytesToRead > 0 ? port.ReadExisting() : "";
            LogService.Instance.Debug("AT", portName + " <- " + response);
            return response;
        }

        public void Dispose()
        {
            try { if (port.IsOpen) port.Close(); }
            finally { port.Dispose(); }
        }
    }

    internal static class AtPortService
    {
        public static async Task<string> DetectDeviceNetworkAsync(string portName, CancellationToken token)
        {
            LogService.Instance.Info("AT", "开始读取设备网络：" + portName);
            using (var receiver = new TftpRouteReceiver())
            {
                receiver.Start();
                await Task.Run(() =>
                {
                    using (var connection = Open(portName))
                    {
                        connection.Send("AT+SHELL=ip route > /tmp/route.txt", 500);
                        foreach (var address in GetLocalIpv4Addresses())
                        {
                            token.ThrowIfCancellationRequested();
                            connection.Send("AT+SHELL=tftp -p -l /tmp/route.txt -r route.txt " + address, 700);
                        }
                    }
                }, token);
                var completed = await Task.WhenAny(receiver.Received, Task.Delay(5000, token));
                token.ThrowIfCancellationRequested();
                var network = completed == receiver.Received ? ParseNetwork(await receiver.Received) : "";
                LogService.Instance.Info("AT", "设备网络读取结果：" + (string.IsNullOrWhiteSpace(network) ? "未识别" : network));
                return network;
            }
        }

        public static string FindLocalIpForNetwork(string network)
        {
            IPAddress networkAddress;
            IPAddress mask;
            if (!TryParseNetwork(network, out networkAddress, out mask)) return "";
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up))
                foreach (var address in adapter.GetIPProperties().UnicastAddresses.Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork))
                    if (IsInNetwork(address.Address, networkAddress, mask)) return address.Address.ToString();
            return "";
        }

        private static List<string> GetLocalIpv4Addresses()
        {
            return NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up)
                .SelectMany(x => x.GetIPProperties().UnicastAddresses)
                .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x.Address) && !x.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                .Select(x => x.Address.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static Task<List<string>> DetectAsync(CancellationToken token)
        {
            return Task.Run(() =>
            {
                var available = GetAvailablePorts();
                LogService.Instance.Debug("AT", "可用串口：" + string.Join(", ", available.Select(x => x.PortName)));
                var namedAtPorts = available.Where(port => port.IsNamedAtPort).ToList();
                var candidates = namedAtPorts.Count > 0 ? namedAtPorts : available;
                var detected = new List<string>();
                foreach (var candidate in candidates)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        using (var connection = Open(candidate.PortName))
                        {
                            var response = connection.Send("AT", 700);
                            if (response.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0) detected.Add(candidate.PortName);
                        }
                    }
                    catch { }
                }
                LogService.Instance.Info("AT", "识别到 AT 端口：" + (detected.Count == 0 ? "无" : string.Join(", ", detected)));
                return detected;
            }, token);
        }

        public static List<AtPortInfo> GetAvailablePorts()
        {
            var names = GetPortFriendlyNames();
            var ports = SerialPort.GetPortNames()
                .Where(port => names.ContainsKey(port))
                .OrderBy(GetPortNumber).Select(port =>
            {
                string friendlyName;
                names.TryGetValue(port, out friendlyName);
                return new AtPortInfo { PortName = port, FriendlyName = friendlyName, IsNamedAtPort = IsAtPortName(friendlyName) };
            }).ToList();
            return ports;
        }

        public static AtPortConnection Open(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName) || !SerialPort.GetPortNames().Any(x => string.Equals(x, portName, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("AT 端口不可用：" + portName);
            LogService.Instance.Info("AT", "打开 AT 端口：" + portName);
            var port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            try { port.Open(); return new AtPortConnection(port, portName); }
            catch { port.Dispose(); throw; }
        }

        private static int GetPortNumber(string name)
        {
            int value;
            return int.TryParse(name.Substring(3), out value) ? value : int.MaxValue;
        }

        private static Dictionary<string, string> GetPortFriendlyNames()
        {
            var ports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var searcher = new ManagementObjectSearcher("SELECT Name, Present FROM Win32_PnPEntity WHERE Name LIKE '%(COM%' AND Present = TRUE") )
            using (var results = searcher.Get())
            {
                foreach (ManagementObject item in results)
                {
                    var name = item["Name"] as string;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var match = Regex.Match(name, @"\((COM\d+)\)", RegexOptions.IgnoreCase);
                    if (match.Success) ports[match.Groups[1].Value] = name;
                }
            }
            return ports;
        }

        private static bool IsAtPortName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && Regex.IsMatch(name, @"(^|\W)AT(\W|$)", RegexOptions.IgnoreCase);
        }

        private static string ParseNetwork(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return "";
            var match = Regex.Match(response, @"(?m)^\s*(\d{1,3}(?:\.\d{1,3}){3})/(\d{1,2})\s+dev\s+\S+", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value + "/" + match.Groups[2].Value;
            match = Regex.Match(response, @"(?m)^\s*(\d{1,3}(?:\.\d{1,3}){3})\s+.*?(\d{1,3}(?:\.\d{1,3}){3})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value + "/" + PrefixLength(IPAddress.Parse(match.Groups[2].Value)) : "";
        }

        private static bool TryParseNetwork(string value, out IPAddress network, out IPAddress mask)
        {
            network = null; mask = null;
            var parts = (value ?? "").Split('/');
            IPAddress address; int prefix;
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out address) || !int.TryParse(parts[1], out prefix) || prefix < 0 || prefix > 32) return false;
            var bytes = new byte[4]; for (var i = 0; i < 4; i++) bytes[i] = (byte)(prefix >= (i + 1) * 8 ? 255 : prefix <= i * 8 ? 0 : 256 - (1 << (8 - (prefix - i * 8))));
            mask = new IPAddress(bytes); network = new IPAddress(And(address.GetAddressBytes(), bytes)); return true;
        }

        private static bool IsInNetwork(IPAddress address, IPAddress network, IPAddress mask) { return And(address.GetAddressBytes(), mask.GetAddressBytes()).SequenceEqual(network.GetAddressBytes()); }
        private static byte[] And(byte[] left, byte[] right) { return left.Select((value, index) => (byte)(value & right[index])).ToArray(); }
        private static int PrefixLength(IPAddress mask) { return mask.GetAddressBytes().Sum(value => Convert.ToString(value, 2).Count(x => x == '1')); }
    }
}
