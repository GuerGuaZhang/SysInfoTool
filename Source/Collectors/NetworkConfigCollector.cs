using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class NetworkConfigCollector : ICollector
    {
        public string Name { get { return "网络配置"; } }
        public int Order { get { return 250; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("netconfig", "网络配置", Order, "🕸️");
            try
            {
                // 活动接口 IP 配置
                var t = s.NewTable("活动网络连接", false,
                    "接口", "类型", "IPv4 地址/掩码", "网关", "DNS", "MAC");
                int activeCount = 0;
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var ip = nic.GetIPProperties();
                    var ipv4 = ip.UnicastAddresses.FirstOrDefault(a =>
                        a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    string ipText = ipv4 != null
                        ? ctx.MaskText(ipv4.Address.ToString()) + "/" + MaskToCidr(ipv4.IPv4Mask)
                        : "无 IPv4";
                    string gw = ip.GatewayAddresses.Count > 0
                        ? ctx.MaskText(ip.GatewayAddresses[0].Address.ToString()) : "—";
                    var dns = string.Join("、", ip.DnsAddresses
                        .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(d => ctx.MaskText(d.ToString())));
                    string typeName = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "无线" : "有线";

                    t.Rows.Add(new List<string> {
                        ctx.MaskText(nic.Name), typeName, ipText, gw,
                        dns.Length > 0 ? dns : "—",
                        ctx.MaskText(nic.GetPhysicalAddress().ToString())
                    });
                    activeCount++;
                }
                s.Fact("活动网络连接", activeCount + " 个");
                if (activeCount == 0) { s.Tables.Remove(t); }

                // 代理
                string proxyEnable = RegistryHelper.HKCU(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings", "ProxyEnable", "0");
                string proxyServer = RegistryHelper.HKCU(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings", "ProxyServer");
                string autoConfig = RegistryHelper.HKCU(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings", "AutoConfigURL");
                if (proxyEnable == "1")
                    s.Fact("代理设置", "手动代理：" + ctx.MaskText(proxyServer), "warn");
                else if (autoConfig.Length > 0)
                    s.Fact("代理设置", "PAC 脚本：" + ctx.MaskText(autoConfig), "warn");
                else
                    s.Fact("代理设置", "未启用系统代理", "ok");

                // 监听端口
                try
                {
                    string netstat = ProcessRunner.Run("netstat", "-ano -p tcp", 30000);
                    if (!string.IsNullOrEmpty(netstat))
                    {
                        var listeners = ParseNetstat(netstat);
                        var pidNames = new Dictionary<int, string>();
                        var pt = s.NewTable("TCP 监听端口（" + listeners.Count + " 个）", listeners.Count > 25,
                            "本地地址", "端口", "PID", "进程");
                        foreach (var l in listeners)
                        {
                            if (!pidNames.ContainsKey(l.Item2))
                            {
                                string pname = "?";
                                try { pname = Process.GetProcessById(l.Item2).ProcessName; } catch { }
                                pidNames[l.Item2] = pname;
                            }
                            pt.Rows.Add(new List<string> {
                                ctx.MaskText(l.Item1),
                                l.Item1.Contains(":") ? l.Item1.Substring(l.Item1.LastIndexOf(':') + 1) : "?",
                                l.Item2.ToString(),
                                pidNames[l.Item2]
                            });
                        }
                    }
                }
                catch (Exception ex) { ctx.Fail(Name, "监听端口读取失败", ex); }

                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "网络配置读取失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private static string MaskToCidr(System.Net.IPAddress mask)
        {
            if (mask == null) return "";
            var bytes = mask.GetAddressBytes();
            int bits = bytes.Sum(b => CountBits(b));
            return bits.ToString();
        }

        private static int CountBits(byte b)
        {
            int n = 0;
            while (b != 0) { n += b & 1; b >>= 1; }
            return n;
        }

        /// <summary>解析 netstat -ano -p tcp，返回 LISTENING 行（本地地址, PID）</summary>
        private static List<Tuple<string, int>> ParseNetstat(string output)
        {
            var list = new List<Tuple<string, int>>();
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;
                if (!parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase)) continue;
                if (!parts[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase)) continue;
                int pid;
                if (!int.TryParse(parts[4], out pid)) continue;
                list.Add(Tuple.Create(parts[1], pid));
            }
            return list;
        }
    }
}
