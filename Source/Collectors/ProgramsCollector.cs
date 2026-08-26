using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class ProgramsCollector : ICollector
    {
        public string Name { get { return "已安装程序"; } }
        public int Order { get { return 140; } }

        private static readonly string[] UninstallPaths = {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("programs", "已安装程序", Order, "📦");
            try
            {
                var programs = new List<string[]>(); // name, version, publisher, date, source
                ReadUninstall(Registry.LocalMachine, UninstallPaths[0], "系统(64位)", programs, ctx);
                ReadUninstall(Registry.LocalMachine, UninstallPaths[1], "系统(32位)", programs, ctx);
                ReadUninstall(Registry.CurrentUser, UninstallPaths[0], "当前用户", programs, ctx);

                // 去重（同名同版本）
                var dedup = programs
                    .GroupBy(p => p[0] + "|" + p[1])
                    .Select(g => g.First())
                    .OrderBy(p => p[0])
                    .ToList();

                s.Fact("程序总数", dedup.Count + " 个");

                var t = s.NewTable("程序列表", dedup.Count > 40, "名称", "版本", "发布者", "安装日期", "来源");
                foreach (var p in dedup)
                    t.Rows.Add(new List<string> { p[0], p[1], ctx.MaskText(p[2]), p[3], p[4] });

                s.Notes.Add("列表来自注册表卸载信息，不含 Microsoft Store（UWP）应用与绿色软件。浏览器扩展与书签见「浏览器」章节。");
                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "程序列表读取失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private static void ReadUninstall(RegistryKey root, string path, string source,
            List<string[]> output, ReportContext ctx)
        {
            foreach (var sub in RegistryHelper.SubKeyNames(root, path))
            {
                try
                {
                    string subPath = path + "\\" + sub;
                    string name = RegistryHelper.GetString(root, subPath, "DisplayName");
                    if (name.Length == 0) continue;
                    // 跳过系统组件与纯补丁
                    int sysComp = RegistryHelper.GetInt(root, subPath, "SystemComponent", 0);
                    if (sysComp == 1) continue;
                    string releaseType = RegistryHelper.GetString(root, subPath, "ReleaseType");
                    if (releaseType.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        releaseType.IndexOf("Hotfix", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    int isUpdate = RegistryHelper.GetInt(root, subPath, "IsMinorUpdate", 0);
                    if (isUpdate == 1) continue;

                    output.Add(new[] {
                        name.Trim(),
                        RegistryHelper.GetString(root, subPath, "DisplayVersion"),
                        RegistryHelper.GetString(root, subPath, "Publisher"),
                        FormatHelper.LooseDate(RegistryHelper.GetString(root, subPath, "InstallDate")),
                        source
                    });
                }
                catch { }
            }
        }
    }
}
