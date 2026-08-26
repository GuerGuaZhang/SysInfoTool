using System;
using System.Text;
using System.Windows.Forms;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool
{
    internal static class Program
    {
        /// <summary>
        /// 用法：
        ///   无参数            → 图形界面（默认）
        ///   --console         → 控制台模式（无界面，直接生成）
        ///   --json-only       → 只输出 JSON，不生成 HTML（配合 --console 使用）
        ///   --no-mask         → 关闭脱敏（默认开启）
        ///   --skip-scan       → 跳过文件夹体积统计
        ///   --out 目录        → 指定报告输出目录
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            bool console = false;
            var options = new ReportService.Options();

            foreach (var arg in args)
            {
                string a = arg.ToLowerInvariant();
                if (a == "--console") console = true;
                else if (a == "--json-only") options.JsonOnly = true;
                else if (a == "--no-mask") options.Mask = false;
                else if (a == "--mask") options.Mask = true;
                else if (a == "--skip-scan") options.SkipScan = true;
                else if (a == "--help" || a == "-h" || a == "/?")
                {
                    console = true;
                    NativeMethods.AttachParentConsole();
                    PrepareConsole();
                    PrintHelp();
                    return 0;
                }
                else if (a.StartsWith("--out="))
                    options.OutputDir = arg.Substring(6).Trim('"');
            }

            if (console)
            {
                NativeMethods.AttachParentConsole();
                PrepareConsole();
                return RunConsole(options);
            }

            // GUI 模式
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Gui.MainForm());
            return 0;
        }

        /// <summary>控制台输出统一 UTF-8，避免在 GBK/OEM 终端下中文乱码</summary>
        private static void PrepareConsole()
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try { Console.InputEncoding = Encoding.UTF8; } catch { }
        }

        private static int RunConsole(ReportService.Options options)
        {
            Console.WriteLine(AppVersion.DisplayName + " v" + AppVersion.Value + "（控制台模式）");
            Console.WriteLine("脱敏：" + (options.Mask ? "开启" : "关闭") +
                "，目录扫描：" + (options.SkipScan ? "跳过" : "开启"));
            Console.WriteLine();

            var service = new ReportService(options);
            var progress = new Progress<Tuple<int, int, string>>(p =>
            {
                Console.Write("\r[{0}/{1}] {2}                    ", p.Item1, p.Item2, p.Item3);
            });

            try
            {
                var result = service.Run(progress);
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("完成，耗时 " + result.Model.DurationSeconds + " 秒。");
                if (result.Model.Failures.Count > 0)
                    Console.WriteLine("注意：有 " + result.Model.Failures.Count + " 项采集失败（详见报告末尾），建议以管理员身份重试。");
                if (result.HtmlPath != null)
                    Console.WriteLine("HTML 报告：" + result.HtmlPath);
                Console.WriteLine("JSON 数据：" + result.JsonPath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("生成失败：" + ex);
                return 1;
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine(AppVersion.DisplayName + " v" + AppVersion.Value);
            Console.WriteLine("用法：SysInfoTool.exe [参数]");
            Console.WriteLine("  无参数        打开图形界面");
            Console.WriteLine("  --console     控制台模式，直接生成报告");
            Console.WriteLine("  --json-only   只输出 JSON（配合 --console 使用）");
            Console.WriteLine("  --no-mask     关闭脱敏（默认开启：隐藏序列号/MAC/IP/用户名）");
            Console.WriteLine("  --skip-scan   跳过文件夹体积统计");
            Console.WriteLine("  --out=目录    指定报告输出根目录");
            Console.WriteLine();
            Console.WriteLine("输出结构：");
            Console.WriteLine("  <输出目录>/<电脑名>/<电脑名>_日期时间.json");
            Console.WriteLine("  无法获取电脑名时，归入「未知」文件夹");
        }
    }
}
