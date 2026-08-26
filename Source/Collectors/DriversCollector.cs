using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class DriversCollector : ICollector
    {
        public string Name { get { return "驱动程序"; } }
        public int Order { get { return 180; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("drivers", "驱动程序", Order, "🧩");
            try
            {
                // 问题设备（最重要，优先列出）
                var problems = WmiHelper.Query(
                    "SELECT Name, DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode<>0");
                if (problems.Count > 0)
                {
                    var pt = s.NewTable("⚠️ 问题设备（驱动异常）", false, "设备", "错误代码", "设备 ID");
                    foreach (var p in problems)
                    {
                        uint code = WmiHelper.U32(p, "ConfigManagerErrorCode");
                        pt.Rows.Add(new List<string> {
                            WmiHelper.Str(p, "Name", "未知设备"),
                            code + "（" + CmErrorName(code) + "）",
                            ctx.MaskText(WmiHelper.Str(p, "DeviceID"))
                        });
                    }
                    s.Fact("问题设备", problems.Count + " 个", "warn");
                    s.Status = "warn"; s.StatusText = "有异常设备";
                }
                else
                {
                    s.Fact("问题设备", "无", "ok");
                    s.Status = "ok"; s.StatusText = "正常";
                }

                // 完整驱动列表（driverquery 比 WMI Win32_PnPSignedDriver 快得多）
                // 先试详细模式（15 列，含启动类型），超时/失败再退回普通模式（4 列）
                string csv = ProcessRunner.Run("driverquery", "/v /fo csv", 45000);
                bool verbose = !string.IsNullOrEmpty(csv);
                if (!verbose)
                    csv = ProcessRunner.Run("driverquery", "/fo csv", 30000);
                if (!string.IsNullOrEmpty(csv))
                {
                    var rows = ParseDriverCsv(csv, verbose);
                    s.Fact("已安装驱动", rows.Count + " 个");
                    var t = s.NewTable("驱动列表", true,
                        "模块名", "显示名称", "类型", "启动类型", "签名", "日期", "提供商");
                    t.Rows.AddRange(rows);
                }
                else
                {
                    s.Notes.Add("driverquery 执行失败，未获取完整驱动列表。");
                }
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "驱动信息读取失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        /// <summary>解析 driverquery CSV 输出。</summary>
        /// <param name="verbose">true = /v 详细模式（15 列）；false = 普通模式（4 列）</param>
        private List<List<string>> ParseDriverCsv(string csv, bool verbose)
        {
            var rows = new List<List<string>>();
            var lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            // /v 表头: Module Name,Display Name,Description,Driver Type,Start Mode,State,Status,
            //          Accept Stop,Accept Pause,Paged Pool(bytes),Code(bytes),BSS(bytes),Link Date,Path,Init(bytes)
            // 普通表头: Module Name,Display Name,Driver Type,Link Date
            foreach (var line in lines.Skip(1))
            {
                var cols = SplitCsvLine(line);
                if (verbose && cols.Count >= 13)
                {
                    rows.Add(new List<string> {
                        cols[0],                          // 模块名
                        cols[1],                          // 显示名
                        cols[3],                          // 类型
                        cols[4],                          // 启动类型
                        "—",                              // 签名状态（driverquery 不含）
                        cols[12],                         // 日期（Link Date）
                        ""                                // 提供商（不含）
                    });
                }
                else if (!verbose && cols.Count >= 4)
                {
                    rows.Add(new List<string> {
                        cols[0],                          // 模块名
                        cols[1],                          // 显示名
                        cols[2],                          // 类型
                        "—",                              // 启动类型（普通模式不含）
                        "—",                              // 签名状态（不含）
                        cols[3],                          // 日期（Link Date）
                        ""                                // 提供商（不含）
                    });
                }
            }
            return rows;
        }

        /// <summary>处理带引号的 CSV 行</summary>
        private static List<string> SplitCsvLine(string line)
        {
            var cols = new List<string>();
            bool inQuotes = false;
            var cur = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else cur.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { cols.Add(cur.ToString()); cur.Length = 0; }
                    else cur.Append(c);
                }
            }
            cols.Add(cur.ToString());
            return cols;
        }

        private static string CmErrorName(uint code)
        {
            switch (code)
            {
                case 1: return "配置不正确";
                case 10: return "设备无法启动";
                case 12: return "资源冲突";
                case 22: return "设备已禁用";
                case 28: return "未安装驱动程序";
                case 31: return "驱动程序加载失败";
                case 43: return "设备报告故障已停用";
                case 45: return "设备未连接";
                case 52: return "驱动签名无法验证";
                default: return "错误 " + code;
            }
        }
    }
}
