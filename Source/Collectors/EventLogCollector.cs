using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    /// <summary>事件日志摘要、蓝屏记录、关键故障事件采集 —— 面向错误排查场景</summary>
    public class EventLogCollector : ICollector
    {
        public string Name { get { return "事件日志与蓝屏"; } }
        public int Order { get { return 240; } }

        private const long Days30Ms = 30L * 24 * 3600 * 1000;
        private const long Days7Ms = 7L * 24 * 3600 * 1000;

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("eventlog", "事件日志摘要与蓝屏记录", Order, "📜");
            try
            {
                // ===== 1. 近 30 天系统/应用日志统计 =====
                CollectLogSummary(s, "System", "系统日志", Days30Ms, 20);
                CollectLogSummary(s, "Application", "应用程序日志", Days30Ms, 20);

                // ===== 2. Setup 日志（系统安装/升级问题） =====
                CollectLogSummary(s, "Setup", "安装日志", Days30Ms, 10);

                // ===== 3. 蓝屏记录：WER 1001 事件 =====
                CollectBSOD(s, ctx);

                // ===== 4. Minidump + 完整内存转储 =====
                CollectDumpInfo(s);

                // ===== 5. 意外关机/重启（Kernel-Power Event 41） =====
                CollectUnexpectedShutdown(s);

                // ===== 6. 计划内关机/重启记录（Event 1074） =====
                CollectPlannedShutdown(s);

                // ===== 7. WHEA 硬件错误 =====
                CollectWheaErrors(s);

                // ===== 8. 磁盘错误 =====
                CollectDiskErrors(s);

                // ===== 9. 服务异常崩溃 =====
                CollectServiceCrashes(s);

                // ===== 10. 驱动加载失败 =====
                CollectDriverErrors(s);

                // ===== 11. 登录失败统计（安全日志） =====
                CollectLoginFailures(s, ctx);

                s.Notes.Add("Minidump 的深度分析（调用栈、故障模块）请用 WinDbg 打开对应 .dmp 文件。驱动崩溃通常表现为 41 号（Kernel-Power）或 WHEA 事件，可对照上方系统日志严重事件。");
                if (s.Status.Length == 0) { s.Status = "ok"; s.StatusText = "正常"; }
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "事件日志读取失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        // ========================================================================
        // 通用日志摘要（支持自定义时间范围与条数）
        // ========================================================================
        private void CollectLogSummary(SectionData s, string logName, string label, long timeMs, int maxRows)
        {
            try
            {
                int critical = 0, error = 0, warning = 0;
                var severe = new List<List<string>>();
                var query = new EventLogQuery(logName, PathType.LogName,
                    "*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= " + timeMs + "]]]");
                using (var reader = new EventLogReader(query))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null)
                    {
                        using (record)
                        {
                            byte level = record.Level ?? 0;
                            if (level == 1) critical++;
                            else if (level == 2) error++;
                            else if (level == 3) warning++;
                            if (level <= 2 && severe.Count < maxRows)
                            {
                                string desc = "";
                                try { desc = record.FormatDescription() ?? ""; } catch { }
                                severe.Add(new List<string> {
                                    record.TimeCreated.HasValue ? record.TimeCreated.Value.ToString("yyyy-MM-dd HH:mm") : "—",
                                    level == 1 ? "关键" : "错误",
                                    record.ProviderName,
                                    record.Id.ToString(),
                                    Truncate(desc.Replace("\r", " ").Replace("\n", " "), 200)
                                });
                            }
                        }
                    }
                }
                string period = timeMs <= Days7Ms ? "近 7 天" : "近 30 天";
                s.Fact(label + "（" + period + "）",
                    "关键 " + critical + " / 错误 " + error + " / 警告 " + warning,
                    critical > 0 ? "error" : (error > 20 ? "warn" : ""));
                if (severe.Count > 0)
                {
                    var t = s.NewTable(label + "：最近的严重事件（最多 " + maxRows + " 条）", true,
                        "时间", "级别", "来源", "事件 ID", "描述");
                    t.Rows.AddRange(severe);
                }
            }
            catch (Exception ex)
            {
                s.Notes.Add(label + "读取失败：" + ex.Message);
            }
        }

        // ========================================================================
        // 蓝屏记录
        // ========================================================================
        private void CollectBSOD(SectionData s, ReportContext ctx)
        {
            try
            {
                var query = new EventLogQuery("System", PathType.LogName,
                    "*[System[Provider[@Name='Microsoft-Windows-WER-SystemErrorReporting'] and (EventID=1001)]]");
                var bsods = new List<List<string>>();
                using (var reader = new EventLogReader(query))
                {
                    EventRecord record;
                    int count = 0;
                    while ((record = reader.ReadEvent()) != null && count < 30)
                    {
                        using (record)
                        {
                            count++;
                            string msg = "";
                            try { msg = record.FormatDescription() ?? ""; } catch { }
                            var m = System.Text.RegularExpressions.Regex.Match(msg, @"0x[0-9A-Fa-f]{8}");
                            bsods.Add(new List<string> {
                                record.TimeCreated.HasValue ? record.TimeCreated.Value.ToString("yyyy-MM-dd HH:mm") : "—",
                                m.Success ? m.Value : "见描述",
                                Truncate(msg.Replace("\r", " ").Replace("\n", " "), 200)
                            });
                        }
                    }
                }
                if (bsods.Count > 0)
                {
                    var t = s.NewTable("蓝屏记录（BugCheck，最多 30 条）", false, "时间", "错误代码", "描述");
                    t.Rows.AddRange(bsods);
                    s.Fact("蓝屏记录", bsods.Count + " 条", "warn");
                    if (s.Status != "error") { s.Status = "warn"; s.StatusText = "有蓝屏记录"; }
                }
                else s.Fact("蓝屏记录", "未发现", "ok");
            }
            catch (Exception ex) { ctx.Fail(Name, "蓝屏事件读取失败", ex); }
        }

        // ========================================================================
        // Minidump + 完整内存转储
        // ========================================================================
        private void CollectDumpInfo(SectionData s)
        {
            try
            {
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string dumpDir = Path.Combine(winDir, "Minidump");
                if (Directory.Exists(dumpDir))
                {
                    var dumps = Directory.GetFiles(dumpDir, "*.dmp")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.LastWriteTime)
                        .ToList();
                    s.Fact("Minidump 文件", dumps.Count + " 个" +
                        (dumps.Count > 0 ? "（最近：" + dumps[0].LastWriteTime.ToString("yyyy-MM-dd") + "）" : ""));
                    if (dumps.Count > 0)
                    {
                        var dt = s.NewTable("Minidump 文件列表", true, "文件名", "大小", "时间");
                        foreach (var d in dumps.Take(20))
                            dt.Rows.Add(new List<string> {
                                d.Name, FormatHelper.Bytes(d.Length),
                                d.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                            });
                    }
                }
                string memDump = Path.Combine(winDir, "MEMORY.DMP");
                if (File.Exists(memDump))
                    s.Fact("完整内存转储", FormatHelper.Bytes(new FileInfo(memDump).Length) + "（MEMORY.DMP）");
            }
            catch { }
        }

        // ========================================================================
        // 意外关机/重启（Kernel-Power Event 41）
        // ========================================================================
        private void CollectUnexpectedShutdown(SectionData s)
        {
            try
            {
                var query = new EventLogQuery("System", PathType.LogName,
                    "*[System[Provider[@Name='Microsoft-Windows-Kernel-Power'] and (EventID=41)]]");
                var events = new List<List<string>>();
                using (var reader = new EventLogReader(query))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null && events.Count < 20)
                    {
                        using (record)
                        {
                            string msg = "";
                            try { msg = record.FormatDescription() ?? ""; } catch { }
                            // 提取 BugcheckCode
                            var bc = System.Text.RegularExpressions.Regex.Match(msg, @"BugcheckCode\s+(\d+)");
                            string bugcheck = bc.Success ? "0x" + int.Parse(bc.Groups[1].Value).ToString("X8") : "—";
                            events.Add(new List<string> {
                                record.TimeCreated.HasValue ? record.TimeCreated.Value.ToString("yyyy-MM-dd HH:mm") : "—",
                                bugcheck,
                                Truncate(msg.Replace("\r", " ").Replace("\n", " "), 200)
                            });
                        }
                    }
                }
                if (events.Count > 0)
                {
                    var t = s.NewTable("意外关机/重启记录（Kernel-Power 41，最多 20 条）", false,
                        "时间", "BugcheckCode", "描述");
                    t.Rows.AddRange(events);
                    s.Fact("意外关机/重启", events.Count + " 次（近 30 天）", "warn");
                    if (s.Status != "error") { s.Status = "warn"; s.StatusText = "有意外关机记录"; }
                }
                else s.Fact("意外关机/重启", "未发现", "ok");
            }
            catch { s.Fact("意外关机/重启", "读取失败（需管理员权限）"); }
        }

        // ========================================================================
        // 计划内关机/重启记录（Event 1074）
        // ========================================================================
        private void CollectPlannedShutdown(SectionData s)
        {
            try
            {
                var query = new EventLogQuery("System", PathType.LogName,
                    "*[System[Provider[@Name='User32'] and (EventID=1074)]]");
                var events = new List<List<string>>();
                using (var reader = new EventLogReader(query))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null && events.Count < 15)
                    {
                        using (record)
                        {
                            string msg = "";
                            try { msg = record.FormatDescription() ?? ""; } catch { }
                            // 提取操作员和原因
                            var user = System.Text.RegularExpressions.Regex.Match(msg, @"进程 (.+?) 的");
                            var reason = System.Text.RegularExpressions.Regex.Match(msg, @"原因[：:]\s*(.+)");
                            events.Add(new List<string> {
                                record.TimeCreated.HasValue ? record.TimeCreated.Value.ToString("yyyy-MM-dd HH:mm") : "—",
                                user.Success ? user.Groups[1].Value.Trim() : "—",
                                reason.Success ? reason.Groups[1].Value.Trim() : Truncate(msg, 100)
                            });
                        }
                    }
                }
                if (events.Count > 0)
                {
                    var t = s.NewTable("计划内关机/重启记录（User32 1074，最多 15 条）", true,
                        "时间", "操作者/进程", "原因");
                    t.Rows.AddRange(events);
                }
                s.Fact("计划内关机/重启", events.Count + " 次");
            }
            catch { }
        }

        // ========================================================================
        // WHEA 硬件错误（Event 1/17/18/19/20）—— CPU / 内存 / 主板硬件故障
        // ========================================================================
        private void CollectWheaErrors(SectionData s)
        {
            try
            {
                var query = new EventLogQuery("System", PathType.LogName,
                    "*[System[Provider[@Name='Microsoft-Windows-WHEA-Logger'] and (EventID=1 or EventID=17 or EventID=18 or EventID=19 or EventID=20)]]");
                var events = new List<List<string>>();
                using (var reader = new EventLogReader(query))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null && events.Count < 20)
                    {
                        using (record)
                        {
                            string desc = "";
                            try { desc = record.FormatDescription() ?? ""; } catch { }
                            string eid = record.Id.ToString();
                            string severity = "";
                            switch (record.Id)
                            {
                                case 1: severity = "致命"; break;
                                case 17: severity = ".corrected（内存）"; break;
                                case 18: severity = "corrected（CPU缓存）"; break;
                                case 19: severity = "corrected（其他）"; break;
                                case 20: severity = "corrected（PCIe）"; break;
                                default: severity = "—"; break;
                            }
                            events.Add(new List<string> {
                                record.TimeCreated.HasValue ? record.TimeCreated.Value.ToString("yyyy-MM-dd HH:mm") : "—",
                                "Event " + eid,
                                severity,
                                Truncate(desc.Replace("\r", " ").Replace("\n", " "), 200)
                            });
                        }
                    }
                }
                if (events.Count > 0)
                {
                    var t = s.NewTable("WHEA 硬件错误记录（最多 20 条）", false,
                        "时间", "事件 ID", "严重级别", "描述");
                    t.Rows.AddRange(events);
                    s.Fact("WHEA 硬件错误", events.Count + " 条", "warn");
                    if (s.Status != "error") { s.Status = "warn"; s.StatusText = "有硬件错误"; }
                }
                else s.Fact("WHEA 硬件错误", "未发现", "ok");
            }
            catch { s.Fact("WHEA 硬件错误", "读取失败"); }
        }

        // ========================================================================
        // 磁盘错误（Event 7/11/15/51/55）
        // ========================================================================
        private void CollectDiskErrors(SectionData s)
        {
            try
            {
                // disk: Event 7(坏块), 11(读写失败), 15(预留扇区), 51(磁盘重校准), 55(NTFS元数据)
                var query = new EventLogQuery("System", PathType.LogName,
                    "*[System[Provider[@Name='disk' or @Name='Ntfs' or @Name='partmgr'] and " +
                    "(EventID=7 or EventID=11 or EventID=15 or EventID=51 or EventID=55)]]");
                var events = new List<List<string>>();
                using (var reader = new EventLogReader(query))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null && events.Count < 20)
                    {
                        using (record)
                        {
                            string desc = "";
                            try { desc = record.FormatDescription() ?? ""; } catch { }
                            string source = record.ProviderName ?? "";
                            string summary = "";
                            if (source.Equals("disk", StringComparison.OrdinalIgnoreCase))
                            {
                                switch (record.Id)
                                {
                                    case 7: summary = "坏块警告"; break;
                                    case 11: summary = "读写失败"; break;
                                    case 15: summary = "预留扇区重分配"; break;
                                    default: summary = "磁盘错误"; break;
                                }
                            }
                            else if (source.Equals("Ntfs", StringComparison.OrdinalIgnoreCase))
                                summary = "NTFS 文件系统错误";
                            else
                                summary = "分区管理器错误";

                            events.Add(new List<string> {
                                record.TimeCreated.HasValue ? record.TimeCreated.Value.ToString("yyyy-MM-dd HH:mm") : "—",
                                summary,
                                source + " / Event " + record.Id,
                                Truncate(desc.Replace("\r", " ").Replace("\n", " "), 200)
                            });
                        }
                    }
                }
                if (events.Count > 0)
                {
                    var t = s.NewTable("磁盘错误记录（最多 20 条）", false,
                        "时间", "错误类型", "来源", "描述");
                    t.Rows.AddRange(events);
                    s.Fact("磁盘错误", events.Count + " 条", "warn");
                    if (s.Status != "error") { s.Status = "warn"; s.StatusText = "有磁盘错误"; }
                }
                else s.Fact("磁盘错误", "未发现", "ok");
            }
            catch { s.Fact("磁盘错误", "读取失败"); }
        }

        // ========================================================================
        // 服务异常崩溃（Event 7031/7034）
        // ========================================================================
        private void CollectServiceCrashes(SectionData s)
        {
            try
            {
                var query = new EventLogQuery("System", PathType.LogName,
                    "*[System[Provider[@Name='Service Control Manager'] and (EventID=7031 or EventID=7034)]]");
                var crashes = new Dictionary<string, int>();
                var recent = new List<List<string>>();
                using (var reader = new EventLogReader(query))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null)
                    {
                        using (record)
                        {
                            string desc = "";
                            try { desc = record.FormatDescription() ?? ""; } catch { }
                            // 提取服务名
                            var svc = System.Text.RegularExpressions.Regex.Match(desc, @"服务(.+?)因");
                            string svcName = svc.Success ? svc.Groups[1].Value.Trim() : "未知";
                            if (!crashes.ContainsKey(svcName)) crashes[svcName] = 0;
                            crashes[svcName]++;
                            if (recent.Count < 10)
                            {
                                recent.Add(new List<string> {
                                    record.TimeCreated.HasValue ? record.TimeCreated.Value.ToString("yyyy-MM-dd HH:mm") : "—",
                                    svcName,
                                    record.Id == 7031 ? "意外终止" : "非受控终止",
                                    Truncate(desc.Replace("\r", " ").Replace("\n", " "), 150)
                                });
                            }
                        }
                    }
                }
                if (crashes.Count > 0)
                {
                    int total = crashes.Values.Sum();
                    s.Fact("服务异常崩溃", total + " 次（" + crashes.Count + " 个服务受影响）", "warn");
                    var st = s.NewTable("反复崩溃的服务", true, "服务名称", "崩溃次数");
                    foreach (var kv in crashes.OrderByDescending(x => x.Value))
                        st.Rows.Add(new List<string> { kv.Key, kv.Value + " 次" });
                    if (recent.Count > 0)
                    {
                        var rt = s.NewTable("最近服务崩溃记录（最多 10 条）", true,
                            "时间", "服务", "类型", "描述");
                        rt.Rows.AddRange(recent);
                    }
                    if (s.Status != "error") { s.Status = "warn"; s.StatusText = "有服务崩溃"; }
                }
                else s.Fact("服务异常崩溃", "未发现", "ok");
            }
            catch { s.Fact("服务异常崩溃", "读取失败"); }
        }

        // ========================================================================
        // 驱动加载失败（Event 219）
        // ========================================================================
        private void CollectDriverErrors(SectionData s)
        {
            try
            {
                var query = new EventLogQuery("System", PathType.LogName,
                    "*[System[Provider[@Name='Microsoft-Windows-Kernel-PnP'] and (EventID=219)]]");
                var events = new List<List<string>>();
                using (var reader = new EventLogReader(query))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null && events.Count < 15)
                    {
                        using (record)
                        {
                            string desc = "";
                            try { desc = record.FormatDescription() ?? ""; } catch { }
                            events.Add(new List<string> {
                                record.TimeCreated.HasValue ? record.TimeCreated.Value.ToString("yyyy-MM-dd HH:mm") : "—",
                                Truncate(desc.Replace("\r", " ").Replace("\n", " "), 200)
                            });
                        }
                    }
                }
                if (events.Count > 0)
                {
                    var t = s.NewTable("驱动加载失败记录（PnP 219，最多 15 条）", true,
                        "时间", "描述");
                    t.Rows.AddRange(events);
                    s.Fact("驱动加载失败", events.Count + " 次", "warn");
                }
                else s.Fact("驱动加载失败", "未发现", "ok");
            }
            catch { s.Fact("驱动加载失败", "读取失败"); }
        }

        // ========================================================================
        // 登录失败统计（安全日志，需管理员）
        // ========================================================================
        private void CollectLoginFailures(SectionData s, ReportContext ctx)
        {
            try
            {
                var query = new EventLogQuery("Security", PathType.LogName,
                    "*[System[(EventID=4625) and TimeCreated[timediff(@SystemTime) <= " + Days30Ms + "]]]");
                int failCount = 0;
                var recent = new List<List<string>>();
                using (var reader = new EventLogReader(query))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null)
                    {
                        using (record)
                        {
                            failCount++;
                            if (recent.Count < 15)
                            {
                                string msg = "";
                                try { msg = record.FormatDescription() ?? ""; } catch { }
                                var account = System.Text.RegularExpressions.Regex.Match(msg, @"Account Name:\s*(\S+)");
                                var source = System.Text.RegularExpressions.Regex.Match(msg, @"Source Network Address:\s*(\S+)");
                                var logonType = System.Text.RegularExpressions.Regex.Match(msg, @"Logon Type:\s*(\d+)");
                                string typeDesc = "";
                                if (logonType.Success)
                                {
                                    switch (logonType.Groups[1].Value)
                                    {
                                        case "2": typeDesc = "本地"; break;
                                        case "3": typeDesc = "网络"; break;
                                        case "10": typeDesc = "远程桌面"; break;
                                        default: typeDesc = "类型" + logonType.Groups[1].Value; break;
                                    }
                                }
                                recent.Add(new List<string> {
                                    record.TimeCreated.HasValue ? record.TimeCreated.Value.ToString("yyyy-MM-dd HH:mm") : "—",
                                    account.Success ? ctx.MaskText(account.Groups[1].Value) : "—",
                                    typeDesc,
                                    source.Success ? ctx.MaskText(source.Groups[1].Value) : "—"
                                });
                            }
                        }
                    }
                }
                s.Fact("登录失败事件（近 30 天）", failCount + " 次", failCount > 50 ? "warn" : "");
                if (recent.Count > 0)
                {
                    var t = s.NewTable("最近登录失败记录（最多 15 条）", true,
                        "时间", "账户", "登录类型", "来源地址");
                    t.Rows.AddRange(recent);
                }
            }
            catch
            {
                s.Notes.Add("安全日志（登录成功/失败审计）需要管理员权限，当前未读取。");
            }
        }

        private static string Truncate(string s, int len)
        {
            return s.Length <= len ? s : s.Substring(0, len) + "…";
        }
    }
}
