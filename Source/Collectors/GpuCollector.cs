using System;
using System.Collections.Generic;
using Microsoft.Win32;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class GpuCollector : ICollector
    {
        public string Name { get { return "显卡信息"; } }
        public int Order { get { return 50; } }

        private const string DisplayClassKey =
            @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("gpu", "显卡（GPU）", Order, "🎮");
            try
            {
                var gpus = WmiHelper.Query("SELECT * FROM Win32_VideoController");
                var card = ctx.Model.AddCard("显卡", "🎮", 40);

                var t = s.NewTable("显卡明细", false,
                    "型号", "显存", "驱动版本", "驱动日期", "当前分辨率", "设备 ID");

                foreach (var g in gpus)
                {
                    string name = WmiHelper.Str(g, "Name");
                    string pnpId = WmiHelper.Str(g, "PNPDeviceID");

                    // 显存：WMI 的 AdapterRAM 是 32 位，超过 4GB 会溢出，优先从注册表读 64 位值
                    ulong vram = VramFromRegistry(pnpId);
                    if (vram == 0) vram = WmiHelper.U32(g, "AdapterRAM");

                    string driverVer = WmiHelper.Str(g, "DriverVersion");
                    string driverDate = FormatHelper.LooseDate(WmiHelper.Str(g, "DriverDate"));

                    string res = "";
                    uint w = WmiHelper.U32(g, "CurrentHorizontalResolution");
                    uint h = WmiHelper.U32(g, "CurrentVerticalResolution");
                    uint hz = WmiHelper.U32(g, "CurrentRefreshRate");
                    if (w > 0 && h > 0) res = w + " × " + h + (hz > 0 ? " @ " + hz + " Hz" : "");

                    t.Rows.Add(new List<string> {
                        name,
                        vram > 0 ? FormatHelper.Bytes(vram) : "—",
                        driverVer, driverDate, res,
                        ctx.MaskText(pnpId)
                    });

                    card.Lines.Add(name + (vram > 0 ? " · " + FormatHelper.Bytes(vram) : ""));
                }

                // 显卡驱动注册表补充信息（INF 版本等）
                try
                {
                    var detail = s.NewTable("驱动注册信息", true, "设备", "驱动提供商", "INF 版本");
                    foreach (var sub in RegistryHelper.SubKeyNames(Registry.LocalMachine, DisplayClassKey))
                    {
                        if (sub.Length != 4) continue; // 0000、0001…
                        string path = DisplayClassKey + "\\" + sub;
                        string desc = RegistryHelper.HKLM(path, "DriverDesc");
                        if (desc.Length == 0) continue;
                        detail.Rows.Add(new List<string> {
                            desc,
                            RegistryHelper.HKLM(path, "ProviderName"),
                            RegistryHelper.HKLM(path, "DriverVersion") + " / " +
                                FormatHelper.LooseDate(RegistryHelper.HKLM(path, "DriverDate"))
                        });
                    }
                    if (detail.Rows.Count == 0) s.Tables.Remove(detail);
                }
                catch { }

                s.Status = "ok"; s.StatusText = "正常";
                card.Status = "ok";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "采集失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private static ulong VramFromRegistry(string pnpDeviceId)
        {
            try
            {
                foreach (var sub in RegistryHelper.SubKeyNames(Registry.LocalMachine, DisplayClassKey))
                {
                    if (sub.Length != 4) continue;
                    string path = DisplayClassKey + "\\" + sub;
                    string match = RegistryHelper.HKLM(path, "MatchingDeviceId");
                    if (match.Length == 0 || pnpDeviceId.Length == 0) continue;
                    if (pnpDeviceId.IndexOf(match, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    long v = RegistryHelper.GetLong(Registry.LocalMachine, path, "HardwareInformation.qwMemorySize");
                    if (v > 0) return (ulong)v;
                }
            }
            catch { }
            return 0;
        }
    }
}
