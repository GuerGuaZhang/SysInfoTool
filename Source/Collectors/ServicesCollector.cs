using System;
using System.Collections.Generic;
using System.Linq;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class ServicesCollector : ICollector
    {
        public string Name { get { return "系统服务"; } }
        public int Order { get { return 160; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("services", "系统服务", Order, "⚙️");
            try
            {
                var svcs = WmiHelper.Query("SELECT Name, DisplayName, State, StartMode, PathName FROM Win32_Service");

                int running = svcs.Count(v => WmiHelper.Str(v, "State") == "Running");
                int stopped = svcs.Count(v => WmiHelper.Str(v, "State") == "Stopped");
                int disabled = svcs.Count(v => WmiHelper.Str(v, "StartMode") == "Disabled");
                int manual = svcs.Count(v => WmiHelper.Str(v, "StartMode") == "Manual");
                int auto = svcs.Count(v => WmiHelper.Str(v, "StartMode") == "Auto");

                s.Fact("服务总数", svcs.Count + " 个");
                s.Fact("运行中", running + " 个");
                s.Fact("已停止", stopped + " 个");
                s.Fact("启动类型", "自动 " + auto + " / 手动 " + manual + " / 禁用 " + disabled);

                // 第三方服务（可执行路径不在 Windows 目录下的运行中服务）
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var thirdParty = svcs.Where(v =>
                {
                    string path = WmiHelper.Str(v, "PathName").Trim('"');
                    return WmiHelper.Str(v, "State") == "Running" && path.Length > 0 &&
                        !path.StartsWith(winDir, StringComparison.OrdinalIgnoreCase) &&
                        path.IndexOf(@"\Windows\", StringComparison.OrdinalIgnoreCase) < 0;
                }).ToList();

                if (thirdParty.Count > 0)
                {
                    var t3 = s.NewTable("运行中的第三方服务（非系统目录）", false, "名称", "显示名", "可执行路径");
                    foreach (var v in thirdParty.OrderBy(v => WmiHelper.Str(v, "Name")))
                        t3.Rows.Add(new List<string> {
                            WmiHelper.Str(v, "Name"), WmiHelper.Str(v, "DisplayName"),
                            ctx.MaskText(ctx.Masker.Path(WmiHelper.Str(v, "PathName")))
                        });
                }

                var t = s.NewTable("全部服务", true, "名称", "显示名", "状态", "启动类型");
                foreach (var v in svcs.OrderBy(v => WmiHelper.Str(v, "State") != "Running").ThenBy(v => WmiHelper.Str(v, "Name")))
                    t.Rows.Add(new List<string> {
                        WmiHelper.Str(v, "Name"),
                        WmiHelper.Str(v, "DisplayName"),
                        StateName(WmiHelper.Str(v, "State")),
                        StartModeName(WmiHelper.Str(v, "StartMode"))
                    });

                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "服务列表读取失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private static string StateName(string state)
        {
            switch (state)
            {
                case "Running": return "运行中";
                case "Stopped": return "已停止";
                case "Paused": return "已暂停";
                case "Start Pending": return "正在启动";
                case "Stop Pending": return "正在停止";
                default: return state;
            }
        }

        private static string StartModeName(string mode)
        {
            switch (mode)
            {
                case "Auto": return "自动";
                case "Manual": return "手动";
                case "Disabled": return "禁用";
                default: return mode;
            }
        }
    }
}
