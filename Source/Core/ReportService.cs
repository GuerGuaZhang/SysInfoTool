using System;
using System.IO;
using System.Threading.Tasks;
using SysInfoTool.Helpers;
using SysInfoTool.Output;

namespace SysInfoTool.Core
{
    /// <summary>采集编排：GUI 与控制台共用的总控</summary>
    public class ReportService
    {
        public class Options
        {
            public bool Mask = true;          // 默认脱敏
            public bool SkipScan = false;
            public bool JsonOnly = false;     // 只输出 JSON，不生成 HTML
            public string OutputDir;          // null = exe 所在目录，失败回退桌面；报告自动存入 子目录/电脑名/ 下
        }

        /// <summary>用于目录/文件名的原始计算机名（永不脱敏）；若为空则归入"未知"分类</summary>
        private static string SafeComputerName
        {
            get
            {
                string name = (Environment.MachineName ?? "").Trim();
                if (string.IsNullOrEmpty(name)) return "未知";
                // 替换 Windows 文件名不允许的字符
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                    name = name.Replace(c, '_');
                return name;
            }
        }

        public class Result
        {
            public string HtmlPath;
            public string JsonPath;
            public ReportModel Model;
        }

        private readonly Options _options;

        public ReportService(Options options)
        {
            _options = options;
        }

        public Task<Result> RunAsync(IProgress<Tuple<int, int, string>> progress)
        {
            return Task.Run(() => Run(progress));
        }

        public Result Run(IProgress<Tuple<int, int, string>> progress)
        {
            var ctx = new ReportContext
            {
                Mask = _options.Mask,
                SkipScan = _options.SkipScan,
                IsAdmin = NativeMethods.IsAdministrator(),
                Masker = new MaskHelper(_options.Mask)
            };
            ctx.Model.Masked = _options.Mask;
            ctx.Model.IsAdmin = ctx.IsAdmin;
            ctx.Model.ComputerName = ctx.MaskText(Environment.MachineName);

            var runner = new CollectorRunner();
            RegisterAll(runner);
            runner.RunAll(ctx, progress);

            // 输出目录：按电脑名分子文件夹，文件名也含电脑名
            string baseDir = ResolveBaseDir(_options.OutputDir);
            string computerDir = Path.Combine(baseDir, SafeComputerName);
            Directory.CreateDirectory(computerDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseName = SafeComputerName + "_" + stamp;
            string htmlPath = null;
            string jsonPath = Path.Combine(computerDir, baseName + ".json");

            // JSON 始终生成
            File.WriteAllText(jsonPath, ReportJson.Serialize(ctx.Model), new System.Text.UTF8Encoding(true));

            // HTML 可选
            if (!_options.JsonOnly)
            {
                htmlPath = Path.Combine(computerDir, baseName + ".html");
                string html = HtmlReportBuilder.Build(ctx.Model);
                File.WriteAllText(htmlPath, html, new System.Text.UTF8Encoding(true));
            }

            return new Result { HtmlPath = htmlPath, JsonPath = jsonPath, Model = ctx.Model };
        }

        private void RegisterAll(CollectorRunner runner)
        {
            runner.Register(new Collectors.CpuCollector());
            runner.Register(new Collectors.MotherboardCollector());
            runner.Register(new Collectors.MemoryCollector());
            runner.Register(new Collectors.DiskCollector());
            runner.Register(new Collectors.GpuCollector());
            runner.Register(new Collectors.MonitorCollector());
            runner.Register(new Collectors.NetworkAdapterCollector());
            runner.Register(new Collectors.AudioCollector());
            runner.Register(new Collectors.BatteryCollector());
            runner.Register(new Collectors.UsbCollector());
            runner.Register(new Collectors.OsCollector());
            runner.Register(new Collectors.ActivationCollector());
            runner.Register(new Collectors.WindowsUpdateCollector());
            runner.Register(new Collectors.SecurityCollector());
            runner.Register(new Collectors.ProgramsCollector());
            runner.Register(new Collectors.StartupCollector());
            runner.Register(new Collectors.ServicesCollector());
            runner.Register(new Collectors.TasksCollector());
            runner.Register(new Collectors.DriversCollector());
            runner.Register(new Collectors.BrowsersCollector());
            runner.Register(new Collectors.RuntimesCollector());
            runner.Register(new Collectors.UsersCollector());
            runner.Register(new Collectors.UsageTraceCollector());
            runner.Register(new Collectors.PerformanceCollector());
            runner.Register(new Collectors.EventLogCollector());
            runner.Register(new Collectors.NetworkConfigCollector());
        }

        /// <summary>解析基础输出目录（电脑名子文件夹由上层创建）</summary>
        private static string ResolveBaseDir(string requested)
        {
            if (!string.IsNullOrEmpty(requested))
            {
                try
                {
                    Directory.CreateDirectory(requested);
                    ProbeWritable(requested);
                    return requested;
                }
                catch { }
            }
            // exe 所在目录
            try
            {
                string exeDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                ProbeWritable(exeDir);
                return exeDir;
            }
            catch { }
            // 回退桌面
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        private static void ProbeWritable(string dir)
        {
            string probe = Path.Combine(dir, ".write_test_" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
    }
}
