using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class BatteryCollector : ICollector
    {
        public string Name { get { return "电池信息"; } }
        public int Order { get { return 90; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("battery", "电池（笔记本）", Order, "🔋");
            string tmpFile = Path.Combine(Path.GetTempPath(), "sysinfo_battery_" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                // 先确认有电池
                var batteries = WmiHelper.Query("SELECT * FROM Win32_Battery");
                if (batteries.Count == 0)
                {
                    s.Fact("电池", "未检测到（台式机或电池未识别）");
                    s.Status = "ok"; s.StatusText = "不适用";
                    return;
                }

                int code = ProcessRunner.RunExitCode("powercfg",
                    "/batteryreport /output \"" + tmpFile + "\" /xml", 30000);

                if (code != 0 || !File.Exists(tmpFile))
                {
                    // 回退到 WMI 基础信息
                    foreach (var b in batteries)
                        s.Fact("电池（" + WmiHelper.Str(b, "Name") + "）", WmiHelper.Str(b, "BatteryStatus"));
                    s.Notes.Add("batteryreport 生成失败，仅显示基础信息（可能需要管理员权限）。");
                    return;
                }

                var doc = XDocument.Load(tmpFile);
                var report = doc.Root;
                if (report == null) { s.Notes.Add("电池报告解析失败。"); return; }

                var infos = report.Elements("Batteries").Elements("Battery").ToList();
                var t = s.NewTable("电池明细", false,
                    "电池", "设计容量", "当前满充容量", "健康度", "循环次数", "制造商");

                double worstHealth = 100;
                foreach (var b in infos)
                {
                    long design = ParseLong(b.Element("DesignCapacity"));
                    long full = ParseLong(b.Element("FullChargeCapacity"));
                    long cycles = ParseLong(b.Element("CycleCount"));
                    string health = "—";
                    if (design > 0 && full > 0)
                    {
                        double h = full * 100.0 / design;
                        worstHealth = Math.Min(worstHealth, h);
                        health = h.ToString("F1") + "%";
                    }
                    t.Rows.Add(new List<string> {
                        El(b, "Id") ?? "电池",
                        design > 0 ? design + " mWh" : "—",
                        full > 0 ? full + " mWh" : "—",
                        health,
                        cycles >= 0 ? cycles.ToString() : "—",
                        El(b, "Manufacturer") ?? "—"
                    });
                }

                // 最近使用比例（插电 vs 电池），来自 usage history
                try
                {
                    var usage = report.Elements("UsageHistory").Elements("HistoryEntry")
                        .OrderByDescending(x => (string)x.Element("StartDate")).Take(200).ToList();
                    long ac = 0, dc = 0;
                    foreach (var u in usage)
                    {
                        long dur = ParseLong(u.Element("Duration"));
                        bool onAc = ((string)u.Element("Ac") ?? "0") == "1";
                        if (onAc) ac += dur; else dc += dur;
                    }
                    if (ac + dc > 0)
                        s.Fact("近期使用比例（记录期内）",
                            "插电 " + (ac * 100.0 / (ac + dc)).ToString("F0") + "% / 电池 " + (dc * 100.0 / (ac + dc)).ToString("F0") + "%");
                }
                catch { }

                s.Fact("总体健康度", worstHealth >= 100 ? "—" : worstHealth.ToString("F1") + "%",
                    worstHealth >= 80 ? "ok" : (worstHealth >= 60 ? "warn" : "error"));

                s.Status = worstHealth >= 80 ? "ok" : (worstHealth >= 60 ? "warn" : "error");
                s.StatusText = worstHealth >= 80 ? "良好" : (worstHealth >= 60 ? "有损耗" : "损耗严重");

                var card = ctx.Model.AddCard("电池", "🔋", 60, s.Status);
                card.Lines.Add("健康度 " + (worstHealth >= 100 ? "—" : worstHealth.ToString("F1") + "%"));
                if (infos.Count > 0)
                {
                    long cycles = ParseLong(infos[0].Element("CycleCount"));
                    if (cycles >= 0) card.Lines.Add("循环 " + cycles + " 次");
                }
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "电池报告解析失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
            finally
            {
                try { if (File.Exists(tmpFile)) File.Delete(tmpFile); } catch { }
            }
        }

        private static string El(XElement e, string name)
        {
            var c = e.Element(name);
            return c == null ? null : c.Value;
        }

        private static long ParseLong(XElement e)
        {
            if (e == null) return -1;
            long v;
            return long.TryParse(e.Value, out v) ? v : -1;
        }
    }
}
