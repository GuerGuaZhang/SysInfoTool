using System;
using System.Collections.Generic;
using System.Linq;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class MemoryCollector : ICollector
    {
        public string Name { get { return "内存信息"; } }
        public int Order { get { return 30; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("memory", "内存", Order, "🧱");
            try
            {
                var sticks = WmiHelper.Query("SELECT * FROM Win32_PhysicalMemory");
                ulong total = 0;
                uint speed = 0;
                string speedLine = null;
                var configuredSpeeds = new List<uint>();
                var ratedSpeeds = new List<uint>();

                if (sticks.Count == 0)
                {
                    s.Notes.Add("未能读取物理内存信息。");
                }
                else
                {
                    // 每根内存条单独一个表格，视觉上完全分开
                    int idx = 0;
                    foreach (var m in sticks)
                    {
                        idx++;
                        ulong cap = WmiHelper.U64(m, "Capacity");
                        total += cap;
                        uint configured = WmiHelper.U32(m, "ConfiguredClockSpeed");
                        uint rated = WmiHelper.U32(m, "Speed");
                        if (configured > 0) { speed = configured; configuredSpeeds.Add(configured); }
                        if (rated > 0) ratedSpeeds.Add(rated);

                        string slot = WmiHelper.Str(m, "DeviceLocator", "未知");
                        string form = "";
                        switch (WmiHelper.U32(m, "FormFactor"))
                        {
                            case 8: form = "DIMM（台式机）"; break;
                            case 12: form = "SO-DIMM（笔记本）"; break;
                            case 13: form = "板载"; break;
                            default: form = "—"; break;
                        }
                        // 频率描述：区分降频/超频/正常
                        string freqDesc;
                        if (configured == 0 && rated == 0)
                            freqDesc = "—";
                        else if (configured == 0)
                            freqDesc = rated + " MHz（标称）";
                        else if (rated == 0)
                            freqDesc = configured + " MHz";
                        else if (configured < rated)
                            freqDesc = configured + " MHz / " + rated + " MHz（降频运行）";
                        else if (configured > rated)
                            freqDesc = configured + " MHz / " + rated + " MHz（超频运行）";
                        else
                            freqDesc = configured + " MHz";

                        string title = sticks.Count > 1
                            ? "内存条 #" + idx + " — " + slot
                            : "内存条明细";

                        var t = s.NewTable(title, false, "属性", "值");
                        t.Rows.Add(new List<string> { "插槽", slot });
                        t.Rows.Add(new List<string> { "容量", FormatHelper.Bytes(cap) });
                        t.Rows.Add(new List<string> { "频率（运行/标称）", freqDesc });
                        t.Rows.Add(new List<string> { "厂商", WmiHelper.Str(m, "Manufacturer", "未知") });
                        t.Rows.Add(new List<string> { "型号（PartNumber）", WmiHelper.Str(m, "PartNumber", "未知") });
                        t.Rows.Add(new List<string> { "序列号", ctx.MaskSerial(WmiHelper.Str(m, "SerialNumber")) });
                        t.Rows.Add(new List<string> { "形态", form });
                    }

                    // ---- 卡片频率摘要：准确反映混插情况 ----
                    if (speed > 0)
                    {
                        bool ratedMixed = ratedSpeeds.Distinct().Count() > 1;
                        bool configMixed = configuredSpeeds.Distinct().Count() > 1;
                        bool allOverclocked = configuredSpeeds.Count > 0 && ratedSpeeds.Count > 0
                            && configuredSpeeds.Count == ratedSpeeds.Count
                            && configuredSpeeds.Zip(ratedSpeeds, (c, r) => c > r).All(x => x);

                        if (ratedMixed)
                        {
                            string ratedList = string.Join("/", ratedSpeeds.Select(r => r + " MHz"));
                            speedLine = speed + " MHz 运行中 · 标称混插（" + ratedList + "）";
                        }
                        else if (configMixed)
                        {
                            string configList = string.Join("/", configuredSpeeds.Select(c => c + " MHz"));
                            speedLine = "运行频率不一致（" + configList + "）";
                        }
                        else if (allOverclocked)
                            speedLine = speed + " MHz 超频运行中";
                        else
                            speedLine = speed + " MHz 运行中";
                    }
                }

                // ---- 插槽统计 ----
                uint slotTotal = 0;
                try
                {
                    var arrays = WmiHelper.Query("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");
                    foreach (var a in arrays) slotTotal += WmiHelper.U32(a, "MemoryDevices");
                }
                catch { }

                // ---- 概览 Facts ----
                s.Fact("总容量", FormatHelper.Bytes(total));
                s.Fact("已插内存条", sticks.Count + " 根");
                if (slotTotal > 0)
                {
                    uint free = slotTotal > sticks.Count ? slotTotal - (uint)sticks.Count : 0;
                    s.Fact("内存插槽", "共 " + slotTotal + " 个，空闲 " + free + " 个");
                }

                // ---- 通道数推断 + 混插提示 ----
                if (sticks.Count >= 2)
                {
                    var caps = sticks.Select(m => WmiHelper.U64(m, "Capacity")).Distinct().Count();
                    var ratedDistinct = ratedSpeeds.Where(x => x > 0).Distinct().Count();
                    s.Fact("通道数（推断）",
                        sticks.Count % 2 == 0 ? "可能为双通道（仅供参考，需进 BIOS/CPU-Z 确认）" : "可能为非对称通道（仅供参考）");
                    if (caps > 1 || ratedDistinct > 1)
                        s.Notes.Add("检测到不同容量或频率的内存混插：整组频率将按最慢的一根运行（已由上方频率反映）。");
                }

                // ---- 顶部卡片 ----
                if (sticks.Count > 0)
                {
                    s.Status = "ok"; s.StatusText = "正常";
                    var card = ctx.Model.AddCard("内存", "🧱", 30, "ok");
                    card.Lines.Add(FormatHelper.Bytes(total) + " · " + sticks.Count + " 根");
                    if (speedLine != null) card.Lines.Add(speedLine);
                }
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "WMI 查询失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }
    }
}
