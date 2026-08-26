using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SysInfoTool.Core
{
    /// <summary>采集器并行调度：全部模块并行跑，单个失败不影响整体，按完成数汇报进度</summary>
    public class CollectorRunner
    {
        private readonly List<ICollector> _collectors;

        public CollectorRunner()
        {
            _collectors = new List<ICollector>();
        }

        public void Register(ICollector c) { _collectors.Add(c); }

        public int Total { get { return _collectors.Count; } }

        /// <param name="progress">回调参数：(已完成数, 总数, 刚完成的模块名)</param>
        public void RunAll(ReportContext ctx, IProgress<Tuple<int, int, string>> progress)
        {
            int done = 0;
            var sw = Stopwatch.StartNew();

            // 并行度限制为 6，避免 WMI 服务被同时打满
            var options = new ParallelOptions { MaxDegreeOfParallelism = 6 };
            Parallel.ForEach(_collectors, options, collector =>
            {
                try
                {
                    collector.Collect(ctx);
                }
                catch (Exception ex)
                {
                    ctx.Fail(collector.Name, "模块整体采集失败", ex);
                }
                int n = Interlocked.Increment(ref done);
                if (progress != null)
                    progress.Report(Tuple.Create(n, _collectors.Count, collector.Name));
            });

            sw.Stop();
            ctx.Model.DurationSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1);

            // 按 Order 排序章节，保证报告结构稳定
            ctx.Model.Sections = ctx.Model.Sections.OrderBy(s => s.Order).ToList();
        }
    }
}
