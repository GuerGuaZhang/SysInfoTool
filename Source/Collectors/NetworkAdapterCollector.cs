using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class NetworkAdapterCollector : ICollector
    {
        public string Name { get { return "网卡信息"; } }
        public int Order { get { return 70; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("netadapter", "网络适配器（硬件）", Order, "🌐");
            try
            {
                // 物理网卡
                var adapters = WmiHelper.Query(
                    "SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True");
                var t = s.NewTable("物理网卡", false,
                    "名称", "类型", "MAC 地址", "连接速率", "驱动版本", "状态");

                foreach (var a in adapters)
                {
                    string pnpId = WmiHelper.Str(a, "PNPDeviceID");
                    string type = pnpId.StartsWith("USB", StringComparison.OrdinalIgnoreCase) ? "USB 外接"
                        : WmiHelper.Str(a, "AdapterType").Contains("Wireless") || WmiHelper.Str(a, "Name").IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) >= 0
                        || WmiHelper.Str(a, "Name").IndexOf("Wireless", StringComparison.OrdinalIgnoreCase) >= 0
                        || WmiHelper.Str(a, "Name").IndexOf("802.11", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "无线" : "有线";

                    string driverVer = "";
                    try
                    {
                        var drv = WmiHelper.First("SELECT DriverVersion FROM Win32_PnPSignedDriver WHERE DeviceID='" +
                            pnpId.Replace("\\", "\\\\").Replace("'", "\\'") + "'");
                        if (drv != null) driverVer = WmiHelper.Str(drv, "DriverVersion");
                    }
                    catch { }

                    ulong speed = WmiHelper.U64(a, "Speed");
                    bool connected = WmiHelper.Str(a, "NetConnectionStatus") == "2";

                    t.Rows.Add(new List<string> {
                        WmiHelper.Str(a, "Name"),
                        type,
                        ctx.MaskText(WmiHelper.Str(a, "MACAddress")),
                        connected ? FormatHelper.BitsPerSecond(speed) : "未连接",
                        driverVer,
                        connected ? "已连接" : "未连接"
                    });
                }
                if (adapters.Count == 0) { s.Tables.Remove(t); s.Notes.Add("未找到物理网卡。"); }

                // WiFi 代数
                try
                {
                    string wlan = ProcessRunner.Run("netsh", "wlan show drivers");
                    if (!string.IsNullOrEmpty(wlan))
                    {
                        var m = Regex.Match(wlan, @"(Radio types supported|支持的无线电类型)\s*:\s*(.+)");
                        if (m.Success)
                        {
                            string radios = m.Groups[2].Value.Trim();
                            string gen = WifiGeneration(radios);
                            s.Fact("WiFi 能力", radios + "（" + gen + "）");
                        }
                    }
                }
                catch { }

                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "采集失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        public static string WifiGeneration(string radios)
        {
            if (radios.Contains("802.11be")) return "WiFi 7";
            if (radios.Contains("802.11ax")) return "WiFi 6 / 6E";
            if (radios.Contains("802.11ac")) return "WiFi 5";
            if (radios.Contains("802.11n")) return "WiFi 4";
            return "WiFi 3 或更早";
        }
    }
}
