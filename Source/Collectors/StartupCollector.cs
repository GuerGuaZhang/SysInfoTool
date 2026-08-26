using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class StartupCollector : ICollector
    {
        public string Name { get { return "启动项"; } }
        public int Order { get { return 150; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("startup", "开机启动项", Order, "🚀");
            try
            {
                var t = s.NewTable("启动项列表", false, "名称", "命令/路径", "位置", "状态");

                // 注册表 Run 键 × 4 处
                ReadRunKey(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM Run（所有用户）", t, ctx,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
                ReadRunKey(Registry.LocalMachine,
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM Run（32位）", t, ctx,
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
                ReadRunKey(Registry.CurrentUser,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU Run（当前用户）", t, ctx,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
                ReadRunKey(Registry.CurrentUser,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU RunOnce", t, ctx, null);

                // 启动文件夹
                string[] startupDirs = {
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
                };
                foreach (var dir in startupDirs)
                {
                    try
                    {
                        if (!Directory.Exists(dir)) continue;
                        foreach (var f in Directory.GetFiles(dir))
                        {
                            t.Rows.Add(new List<string> {
                                Path.GetFileNameWithoutExtension(f),
                                ctx.MaskText(ctx.Masker.Path(f)),
                                dir.Contains("ProgramData") ? "启动文件夹（所有用户）" : "启动文件夹（当前用户）",
                                "已启用"
                            });
                        }
                    }
                    catch { }
                }

                s.Fact("启动项总数", t.Rows.Count + " 个");
                s.Notes.Add("计划任务形式的自启动见「计划任务」章节。");
                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "启动项读取失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private void ReadRunKey(RegistryKey root, string path, string location, TableData t,
            ReportContext ctx, string approvedPath)
        {
            foreach (var kv in RegistryHelper.Values(root, path))
            {
                string status = "已启用";
                if (approvedPath != null)
                {
                    try
                    {
                        using (var key = root.OpenSubKey(approvedPath))
                        {
                            if (key != null)
                            {
                                var raw = key.GetValue(kv.Key) as byte[];
                                if (raw != null && raw.Length > 0 && (raw[0] == 3 || raw[0] == 5))
                                    status = "已禁用";
                            }
                        }
                    }
                    catch { }
                }
                t.Rows.Add(new List<string> {
                    kv.Key, ctx.MaskText(ctx.Masker.Path(kv.Value)), location, status
                });
            }
        }
    }
}
