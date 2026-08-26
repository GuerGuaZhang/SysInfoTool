using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    /// <summary>运行库与开发环境：.NET / VC++ / Java / Python / Node / DirectX / 字体</summary>
    public class RuntimesCollector : ICollector
    {
        public string Name { get { return "运行库与字体"; } }
        public int Order { get { return 200; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("runtimes", "运行库、开发环境与字体", Order, "🧰");
            try
            {
                // .NET Framework
                int release = RegistryHelper.GetInt(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", "Release", 0);
                if (release > 0)
                    s.Fact(".NET Framework", DotNetVersion(release) + "（Release " + release + "）");

                // 旧版 .NET
                var oldNet = RegistryHelper.SubKeyNames(Registry.LocalMachine, @"SOFTWARE\Microsoft\NET Framework Setup\NDP")
                    .Where(k => k.StartsWith("v") && k != "v4").ToList();
                if (oldNet.Count > 0)
                    s.Fact("旧版 .NET", string.Join("、", oldNet));

                // VC++ 运行库（从卸载列表匹配）
                var vcList = new List<string>();
                string[] uninstallPaths = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };
                foreach (var path in uninstallPaths)
                {
                    foreach (var sub in RegistryHelper.SubKeyNames(Registry.LocalMachine, path))
                    {
                        string name = RegistryHelper.HKLM(path + "\\" + sub, "DisplayName");
                        if (name.IndexOf("Visual C++", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            (name.Contains("Redistributable") || name.Contains("Runtime")))
                        {
                            string ver = RegistryHelper.HKLM(path + "\\" + sub, "DisplayVersion");
                            vcList.Add(name.Replace("Microsoft Visual C++ ", "") + (ver.Length > 0 ? "（" + ver + "）" : ""));
                        }
                    }
                }
                if (vcList.Count > 0)
                {
                    var vt = s.NewTable("Visual C++ 运行库（" + vcList.Distinct().Count() + " 个）", true, "名称");
                    foreach (var v in vcList.Distinct().OrderBy(v => v))
                        vt.Rows.Add(new List<string> { v });
                }

                // DirectX
                string dxVer = RegistryHelper.HKLM(@"SOFTWARE\Microsoft\DirectX", "Version");
                bool hasD3D12 = File.Exists(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "d3d12.dll"));
                s.Fact("DirectX", hasD3D12 ? "DirectX 12（运行时 " + dxVer + "）" : "运行时 " + dxVer);

                // Java / Python / Node
                ProbeRuntime(s, "Java", "java", "-version", true);
                ProbeRuntime(s, "Python", "python", "--version", false);
                ProbeRuntime(s, "Python (py launcher)", "py", "--version", false);
                ProbeRuntime(s, "Node.js", "node", "--version", false);
                ProbeRuntime(s, "Git", "git", "--version", false);

                // 字体
                try
                {
                    var fonts = RegistryHelper.Values(Registry.LocalMachine,
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
                    s.Fact("已安装字体", fonts.Count + " 个");
                    var ft = s.NewTable("字体列表", true, "字体名称", "文件");
                    foreach (var f in fonts.OrderBy(f => f.Key))
                        ft.Rows.Add(new List<string> { f.Key.Replace(" (TrueType)", "").Replace(" (OpenType)", ""), f.Value });
                }
                catch { }

                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "运行库信息读取失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private void ProbeRuntime(SectionData s, string label, string exe, string args, bool stderrOutput)
        {
            try
            {
                // where.exe 定位完整路径（避免 PATH 中同名歧义）
                string where = ProcessRunner.Run("where.exe", exe, 5000);
                if (string.IsNullOrEmpty(where)) return;
                string resolved = (where.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? "").Trim();
                if (resolved.Length == 0 || resolved.IndexOf(exe, StringComparison.OrdinalIgnoreCase) < 0)
                    return;

                string output;
                if (resolved.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                    resolved.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                {
                    // 批处理需经 cmd.exe 运行
                    string cmdArgs = "/c \"\"" + resolved + "\" " + args + "\"";
                    output = stderrOutput
                        ? ProcessRunner.RunMerged("cmd.exe", cmdArgs, 8000)
                        : ProcessRunner.Run("cmd.exe", cmdArgs, 8000);
                }
                else
                {
                    // 合并 stdout/stderr：java -version 等把版本写到 stderr
                    output = stderrOutput
                        ? ProcessRunner.RunMerged(resolved, args, 8000)
                        : ProcessRunner.Run(resolved, args, 8000);
                }
                if (string.IsNullOrWhiteSpace(output))
                {
                    s.Fact(label, "已安装（版本信息不可读）");
                    return;
                }
                string firstLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? "";
                s.Fact(label, firstLine.Trim());
            }
            catch { }
        }

        private static string DotNetVersion(int release)
        {
            if (release >= 533320) return "4.8.1";
            if (release >= 528040) return "4.8";
            if (release >= 461808) return "4.7.2";
            if (release >= 461308) return "4.7.1";
            if (release >= 460798) return "4.7";
            if (release >= 394802) return "4.6.2";
            if (release >= 394254) return "4.6.1";
            return "4.5-4.6";
        }
    }
}
