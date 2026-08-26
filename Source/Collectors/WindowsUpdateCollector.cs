using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class WindowsUpdateCollector : ICollector
    {
        public string Name { get { return "Windows 更新"; } }
        public int Order { get { return 120; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("wu", "Windows 更新", Order, "🔄");
            try
            {
                // 更新历史（已安装的修补程序）
                var qfe = WmiHelper.Query("SELECT HotFixID, Description, InstalledOn FROM Win32_QuickFixEngineering");
                var sorted = qfe
                    .Select(q => new { Id = WmiHelper.Str(q, "HotFixID"), Desc = WmiHelper.Str(q, "Description"), Date = WmiHelper.Date(q, "InstalledOn") })
                    .OrderByDescending(x => x.Date)
                    .ToList();

                if (sorted.Count > 0)
                {
                    s.Fact("已安装更新数量", sorted.Count + " 个");
                    s.Fact("最近一次更新", sorted[0].Date + "（" + sorted[0].Id + "）");

                    var t = s.NewTable("更新历史记录", true, "KB 编号", "类型", "安装日期");
                    foreach (var u in sorted)
                        t.Rows.Add(new List<string> { u.Id, u.Desc, u.Date });
                }
                else
                {
                    s.Fact("已安装更新", "未读取到记录");
                }

                // 挂起重启检测
                var pending = new List<string>();
                if (RegistryHelper.KeyExists(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                    pending.Add("组件服务（CBS）等待重启");
                if (RegistryHelper.KeyExists(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
                    pending.Add("Windows Update 等待重启");
                // PendingFileRenameOperations 是“值”而非键，直接读值
                string pfro = RegistryHelper.HKLM(@"SYSTEM\CurrentControlSet\Control\Session Manager",
                    "PendingFileRenameOperations");
                if (pfro.Length > 0)
                    pending.Add("存在挂起的文件重命名操作");

                s.Fact("挂起更新/重启", pending.Count > 0 ? string.Join("；", pending) : "无",
                    pending.Count > 0 ? "warn" : "ok");

                s.Status = pending.Count > 0 ? "warn" : "ok";
                s.StatusText = pending.Count > 0 ? "建议重启" : "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "更新信息读取失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }
    }
}
