using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    /// <summary>性能快照：开机时间、内存占用、Top 进程、WinSAT 评分</summary>
    public class PerformanceCollector : ICollector
    {
        public string Name { get { return "性能快照"; } }
        public int Order { get { return 230; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("performance", "性能快照与体验指数", Order, "📈");
            try
            {
                // 运行时间
                var uptime = TimeSpan.FromMilliseconds(NativeMethods.GetTickCount64());
                s.Fact("系统已连续运行（Uptime）", FormatHelper.Uptime(uptime),
                    uptime.TotalDays > 30 ? "warn" : "");
                if (uptime.TotalDays > 30)
                    s.Notes.Add("连续运行超过 30 天：建议重启以完成更新并释放累积的资源占用。");

                // 内存占用快照
                var mem = new NativeMethods.MEMORYSTATUSEX();
                if (NativeMethods.GlobalMemoryStatusEx(mem))
                {
                    ulong total = mem.ullTotalPhys, avail = mem.ullAvailPhys;
                    ulong used = total > avail ? total - avail : 0;
                    double pct = total > 0 ? used * 100.0 / total : 0;
                    s.Fact("物理内存占用（此刻）",
                        FormatHelper.Bytes(used) + " / " + FormatHelper.Bytes(total) + "（" + pct.ToString("F1") + "%）",
                        pct > 90 ? "warn" : "");
                    s.Fact("已提交内存",
                        FormatHelper.Bytes(total > mem.ullAvailPageFile ? mem.ullTotalPageFile - mem.ullAvailPageFile : 0)
                        + " / 提交上限 " + FormatHelper.Bytes(mem.ullTotalPageFile));
                }

                // Top 进程
                try
                {
                    var procs = Process.GetProcesses()
                        .OrderByDescending(p => SafeWorkingSet(p))
                        .Take(20)
                        .ToList();
                    var t = s.NewTable("内存占用 Top 20 进程（快照）", false, "进程", "PID", "内存占用");
                    foreach (var p in procs)
                    {
                        t.Rows.Add(new List<string> {
                            ctx.MaskText(SafeName(p)), SafeId(p).ToString(), FormatHelper.Bytes(SafeWorkingSet(p))
                        });
                        try { p.Dispose(); } catch { }
                    }
                }
                catch { }

                // 句柄/进程/线程总数
                try
                {
                    var all = Process.GetProcesses();
                    int threads = all.Sum(p => SafeThreads(p));
                    s.Fact("进程 / 线程总数", all.Length + " 个进程 / " + threads + " 个线程");
                    foreach (var p in all) { try { p.Dispose(); } catch { } }
                }
                catch { }

                // WinSAT 体验指数
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        @"Performance\WinSAT\DataStore");
                    if (Directory.Exists(dir))
                    {
                        var file = Directory.GetFiles(dir, "*Formal.Assessment*.xml")
                            .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                            .FirstOrDefault();
                        if (file != null)
                        {
                            var doc = XDocument.Load(file);
                            var spr = doc.Descendants("WinSPR").FirstOrDefault();
                            if (spr != null)
                            {
                                var t = s.NewTable("Windows 体验指数（WinSAT）", false, "子项", "得分");
                                AddScore(t, spr, "SystemScore", "综合评分");
                                AddScore(t, spr, "CPUScore", "CPU");
                                AddScore(t, spr, "MemoryScore", "内存");
                                AddScore(t, spr, "GraphicsScore", "图形");
                                AddScore(t, spr, "GamingScore", "游戏图形");
                                AddScore(t, spr, "DiskScore", "主硬盘");
                                s.Fact("WinSAT 评估时间", new FileInfo(file).LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                            }
                        }
                        else s.Fact("WinSAT 体验指数", "未找到评估记录（可运行 winsat formal 生成）");
                    }
                }
                catch { }

                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "性能快照采集失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private static void AddScore(TableData t, XElement spr, string element, string label)
        {
            var e = spr.Element(element);
            if (e != null) t.Rows.Add(new List<string> { label, e.Value });
        }

        private static long SafeWorkingSet(Process p) { try { return p.WorkingSet64; } catch { return 0; } }
        private static int SafeId(Process p) { try { return p.Id; } catch { return 0; } }
        private static int SafeThreads(Process p) { try { return p.Threads.Count; } catch { return 0; } }
        private static string SafeName(Process p) { try { return p.ProcessName; } catch { return "?"; } }
    }
}
