using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    /// <summary>使用痕迹：目录体积、Recent、回收站、临时文件、RunMRU、TypedPaths、WiFi、打印机、蓝牙、共享</summary>
    public class UsageTraceCollector : ICollector
    {
        public string Name { get { return "使用痕迹"; } }
        public int Order { get { return 220; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("traces", "使用痕迹", Order, "👣");

            // 常用目录体积
            if (ctx.SkipScan)
            {
                s.Notes.Add("已按参数跳过文件夹体积统计。");
            }
            else
            {
                try
                {
                    var folders = new List<Tuple<string, string>> {
                        Tuple.Create("桌面", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
                        Tuple.Create("文档", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                        Tuple.Create("图片", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
                        Tuple.Create("下载", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
                        Tuple.Create("视频", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)),
                    };
                    var t = s.NewTable("用户目录体积（当前用户）", false, "目录", "路径", "文件数", "体积");
                    foreach (var f in folders)
                    {
                        if (!Directory.Exists(f.Item2)) continue;
                        var r = FormatHelper.DirectorySize(f.Item2, 15);
                        t.Rows.Add(new List<string> {
                            f.Item1, ctx.Masker.Path(f.Item2), r.Item2 + " 个", FormatHelper.Bytes(r.Item1)
                        });
                    }
                    t.Note = "统计上限 15 秒/目录，超限或含大量文件时可能为部分统计；不含 OneDrive 云端占位文件实际体积。";
                }
                catch (Exception ex) { ctx.Fail(Name, "目录体积统计失败", ex); }
            }

            // 临时文件体积
            if (!ctx.SkipScan)
            {
                try
                {
                    var t = s.NewTable("临时文件体积", false, "位置", "文件数", "体积");
                    string temp = Path.GetTempPath();
                    AddSizeRow(t, ctx, "%TEMP%（当前用户）", temp);
                    AddSizeRow(t, ctx, @"Windows\Temp", Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
                    AddSizeRow(t, ctx, @"Windows\Prefetch", Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"));
                }
                catch (Exception ex) { ctx.Fail(Name, "临时文件统计失败", ex); }
            }

            // 最近打开的文件
            try
            {
                string recent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Recent");
                if (Directory.Exists(recent))
                {
                    var links = Directory.GetFiles(recent, "*.lnk")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.LastWriteTime)
                        .ToList();
                    s.Fact("最近打开文件记录", links.Count + " 条");
                    var t = s.NewTable("最近打开的文件（前 30 条）", true, "文件", "访问时间");
                    foreach (var l in links.Take(30))
                        t.Rows.Add(new List<string> {
                            ctx.MaskText(Path.GetFileNameWithoutExtension(l.Name)),
                            l.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                        });
                }
            }
            catch (Exception ex) { ctx.Fail(Name, "Recent 记录读取失败", ex); }

            // 回收站
            try
            {
                long totalSize = 0; int totalCount = 0;
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    string rb = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                    if (!Directory.Exists(rb)) continue;
                    var r = FormatHelper.DirectorySize(rb, 10);
                    totalSize += r.Item1; totalCount += (int)r.Item2;
                }
                s.Fact("回收站", totalCount > 0
                    ? totalCount + " 个项目，共 " + FormatHelper.Bytes(totalSize)
                    : "空（或无权限读取其他用户回收站）");
            }
            catch (Exception ex) { ctx.Fail(Name, "回收站统计失败", ex); }

            // RunMRU / TypedPaths
            try
            {
                var mru = RegistryHelper.Values(Registry.CurrentUser,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\RunMRU")
                    .Where(v => v.Key.Length == 1 && v.Key != "MRUList").ToList();
                if (mru.Count > 0)
                {
                    var t = s.NewTable("运行对话框历史（RunMRU）", true, "命令");
                    foreach (var v in mru)
                        t.Rows.Add(new List<string> { ctx.MaskText(v.Value.TrimEnd('\\')) });
                }
            }
            catch { }

            try
            {
                var typed = RegistryHelper.Values(Registry.CurrentUser,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths");
                if (typed.Count > 0)
                {
                    var t = s.NewTable("资源管理器地址栏历史（TypedPaths）", true, "路径");
                    foreach (var v in typed)
                        t.Rows.Add(new List<string> { ctx.MaskText(ctx.Masker.Path(v.Value)) });
                }
            }
            catch { }

            // WiFi 配置文件
            try
            {
                string output = ProcessRunner.Run("netsh", "wlan show profiles");
                if (!string.IsNullOrEmpty(output))
                {
                    var ssids = new List<string>();
                    var regex = new System.Text.RegularExpressions.Regex(@"(All User Profile|所有用户配置文件)\s*:\s*(.+)");
                    foreach (System.Text.RegularExpressions.Match m in regex.Matches(output))
                        ssids.Add(m.Groups[2].Value.Trim());
                    if (ssids.Count > 0)
                    {
                        s.Fact("保存过的 WiFi 网络", ssids.Count + " 个");
                        var t = s.NewTable("WiFi 配置文件（SSID）", ssids.Count > 15, "SSID");
                        foreach (var ssid in ssids)
                            t.Rows.Add(new List<string> { ctx.Masker.Ssid(ssid) });
                    }
                }
            }
            catch (Exception ex) { ctx.Fail(Name, "WiFi 配置文件读取失败", ex); }

            // 打印机
            try
            {
                var printers = WmiHelper.Query("SELECT Name, DriverName, PortName, Default, Network FROM Win32_Printer");
                if (printers.Count > 0)
                {
                    var t = s.NewTable("已安装打印机", false, "名称", "驱动", "端口", "默认", "网络打印机");
                    foreach (var p in printers)
                        t.Rows.Add(new List<string> {
                            ctx.MaskText(WmiHelper.Str(p, "Name")),
                            WmiHelper.Str(p, "DriverName"),
                            ctx.MaskText(WmiHelper.Str(p, "PortName")),
                            WmiHelper.Bool(p, "Default") ? "是" : "",
                            WmiHelper.Bool(p, "Network") ? "是" : ""
                        });
                }
                else s.Fact("打印机", "未安装");
            }
            catch { }

            // 蓝牙配对设备
            try
            {
                const string bthKey = @"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices";
                var macs = RegistryHelper.SubKeyNames(Registry.LocalMachine, bthKey);
                if (macs.Count > 0)
                {
                    var t = s.NewTable("蓝牙配对设备", false, "设备 MAC", "名称");
                    foreach (var mac in macs)
                    {
                        string devName = "";
                        try
                        {
                            using (var key = Registry.LocalMachine.OpenSubKey(bthKey + "\\" + mac))
                            {
                                if (key != null)
                                {
                                    var raw = key.GetValue("Name") as byte[];
                                    if (raw != null)
                                        devName = System.Text.Encoding.UTF8.GetString(raw).TrimEnd('\0');
                                }
                            }
                        }
                        catch { }
                        string pretty = string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
                        t.Rows.Add(new List<string> { ctx.MaskText(pretty), ctx.MaskText(devName.Length > 0 ? devName : "未知设备") });
                    }
                }
                else s.Fact("蓝牙配对设备", "无（或无蓝牙适配器）");
            }
            catch { }

            // 共享文件夹
            try
            {
                var shares = WmiHelper.Query("SELECT Name, Path, Description FROM Win32_Share");
                var t = s.NewTable("共享文件夹", false, "共享名", "路径", "备注");
                foreach (var sh in shares)
                    t.Rows.Add(new List<string> {
                        WmiHelper.Str(sh, "Name"),
                        ctx.MaskText(ctx.Masker.Path(WmiHelper.Str(sh, "Path"))),
                        WmiHelper.Str(sh, "Description")
                    });
            }
            catch { }

            s.Status = "ok"; s.StatusText = "正常";
        }

        private void AddSizeRow(TableData t, ReportContext ctx, string label, string path)
        {
            if (!Directory.Exists(path)) return;
            var r = FormatHelper.DirectorySize(path, 10);
            t.Rows.Add(new List<string> { label, r.Item2 + " 个", FormatHelper.Bytes(r.Item1) });
        }
    }
}
