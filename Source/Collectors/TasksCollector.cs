using System;
using System.Collections.Generic;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    /// <summary>计划任务：通过 Task Scheduler COM（后期绑定，避免互操作程序集依赖）</summary>
    public class TasksCollector : ICollector
    {
        public string Name { get { return "计划任务"; } }
        public int Order { get { return 170; } }

        private const int MaxTasks = 400;

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("tasks", "计划任务", Order, "📅");
            try
            {
                var rows = new List<List<string>>();
                Type svcType = Type.GetTypeFromProgID("Schedule.Service");
                if (svcType == null)
                {
                    s.Notes.Add("任务计划服务不可用。");
                    return;
                }
                dynamic service = Activator.CreateInstance(svcType);
                service.Connect();
                dynamic root = service.GetFolder("\\");
                WalkFolder(root, "\\", rows, ctx);

                s.Fact("非系统任务数量", rows.Count + " 个（不含 \\Microsoft 系统任务）");
                var t = s.NewTable("计划任务（非微软）", rows.Count > 30,
                    "名称", "路径", "状态", "触发器", "操作", "上次运行", "下次运行");
                t.Rows.AddRange(rows);
                t.Note = "系统自带任务（\\Microsoft 路径下数百个）已省略；仅列出第三方与用户任务。";

                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "计划任务读取失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private void WalkFolder(dynamic folder, string path, List<List<string>> rows, ReportContext ctx)
        {
            if (rows.Count >= MaxTasks) return;
            if (path.StartsWith("\\Microsoft", StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                dynamic tasks = folder.GetTasks(1); // 含隐藏任务
                int count = tasks.Count;
                for (int i = 1; i <= count; i++)   // COM 集合 1 起始
                {
                    if (rows.Count >= MaxTasks) break;
                    dynamic task = tasks.Item(i);
                    try { rows.Add(TaskRow(task, path, ctx)); } catch { }
                }
            }
            catch { }

            try
            {
                dynamic folders = folder.GetFolders(0);
                int fcount = folders.Count;
                for (int i = 1; i <= fcount; i++)
                {
                    if (rows.Count >= MaxTasks) break;
                    dynamic sub = folders.Item(i);
                    string subName = "\\" + (string)sub.Name;
                    try { WalkFolder(sub, subName, rows, ctx); } catch { }
                }
            }
            catch { }
        }

        private List<string> TaskRow(dynamic task, string path, ReportContext ctx)
        {
            string name = "", state = "", triggers = "", actions = "", lastRun = "", nextRun = "";
            try { name = (string)task.Name; } catch { }
            try
            {
                switch ((int)task.State)
                {
                    case 1: state = "已禁用"; break;
                    case 2: state = "排队中"; break;
                    case 3: state = "就绪"; break;
                    case 4: state = "运行中"; break;
                    default: state = "未知"; break;
                }
            }
            catch { }
            try
            {
                dynamic def = task.Definition;
                var trigList = new List<string>();
                dynamic trigs = def.Triggers;
                int tc = trigs.Count;
                for (int i = 1; i <= tc && i <= 3; i++)
                {
                    dynamic tr = trigs.Item(i);
                    trigList.Add(TriggerName((int)tr.Type));
                }
                triggers = string.Join("、", trigList);

                var actList = new List<string>();
                dynamic acts = def.Actions;
                int ac = acts.Count;
                for (int i = 1; i <= ac && i <= 2; i++)
                {
                    dynamic a = acts.Item(i);
                    try
                    {
                        if ((int)a.Type == 0) // Exec
                            actList.Add(ctx.MaskText(ctx.Masker.Path((string)a.Path)));
                    }
                    catch { }
                }
                actions = string.Join("；", actList);
            }
            catch { }
            try { lastRun = FmtComDate((string)task.LastRunTime); } catch { }
            try { nextRun = FmtComDate((string)task.NextRunTime); } catch { }

            return new List<string> { name, path, state, triggers, actions, lastRun, nextRun };
        }

        private static string TriggerName(int type)
        {
            switch (type)
            {
                case 0: return "事件触发";
                case 1: return "定时";
                case 2: return "每日";
                case 3: return "每周";
                case 4: return "每月";
                case 5: return "每月(星期)";
                case 6: return "空闲时";
                case 7: return "注册时";
                case 8: return "开机时";
                case 9: return "登录时";
                case 11: return "会话状态变化";
                default: return "其他(" + type + ")";
            }
        }

        private static string FmtComDate(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "—";
            try
            {
                // COM 返回 ISO8601，如 2026-07-30T15:04:23
                DateTime dt = DateTime.Parse(raw, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                if (dt.Year < 1990) return "从未";
                return dt.ToString("yyyy-MM-dd HH:mm");
            }
            catch { return raw; }
        }
    }
}
