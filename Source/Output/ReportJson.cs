using System;
using System.Collections.Generic;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Output
{
    /// <summary>
    /// 面向程序/AI 解析的结构化 JSON 生成器（规范 v1.1）。
    /// 与 HTML 报告同一数据源，但按“机器友好”原则重新组织：
    ///   - 顶层 meta 块承载环境元信息，机器可先读元数据再决定如何消费
    ///   - facts 由 Tuple 三元组改为具名字段对象 {name, value, level}
    ///   - 表格行由“裸数组”改为“列名 → 值”的对象，AI 可直接按列名取值
    ///   - 章节带 category 分组与稳定排序（分组序 → Order）
    /// 输出为 2 空格缩进的美化 JSON。
    /// </summary>
    public static class ReportJson
    {
        public const string SchemaVersion = "1.1";

        /// <summary>设计边界（未采集项）——集中声明一次，替代各章节重复注释；HTML 与 JSON 共用</summary>
        public static readonly string[] Boundaries = {
            "温度 / 风扇等传感器读数",
            "SMART 详细属性（通电时间、剩余寿命、重映射扇区）",
            "硬盘读写测速",
            "内存颗粒 SPD（DRAM 芯片厂商）",
            "CPU 睿频频率与 TDP",
            "显卡核心代号、实时频率与 BIOS 版本",
            "显示器面板类型（IPS / TN / VA）",
            "USB 接口协商速率（USB 3.2 Gen2 等）",
            "浏览器历史 / 下载 / 搜索记录",
            "Jump Lists 明细",
            "WiFi 密码与连接次数、时长",
            "账户创建日期",
            "启动项影响级别（高 / 中 / 低）",
            "防火墙规则全表、路由表与 ARP 表",
            "实时监控曲线（CPU/GPU 占用、温度）"
        };

        public static string Serialize(ReportModel m)
        {
            return MiniJson.Serialize(Build(m), true);
        }

        public static Dictionary<string, object> Build(ReportModel m)
        {
            var meta = new Dictionary<string, object>
            {
                { "tool", AppVersion.DisplayName },
                { "version", m.ToolVersion },
                { "generated_at", m.GeneratedAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz") },
                { "duration_seconds", m.DurationSeconds },
                { "masked", m.Masked },
                { "admin", m.IsAdmin },
                { "computer_name", m.ComputerName },
                { "failures_count", m.Failures.Count }
            };

            // 看板卡片（按 Order 排序）
            var sortedCards = new List<CardData>(m.Cards);
            sortedCards.Sort((a, b) => a.Order.CompareTo(b.Order));
            var summary = new List<object>();
            foreach (var c in sortedCards)
            {
                summary.Add(new Dictionary<string, object>
                {
                    { "title", c.Title },
                    { "icon", c.Icon },
                    { "level", NormalizeLevel(c.Status) },
                    { "lines", ToList(c.Lines) }
                });
            }

            // 章节：按（分组序, Order）稳定排序，与 HTML 分组一致
            var ordered = new List<SectionData>(m.Sections);
            ordered.Sort((a, b) =>
            {
                int c = ReportModel.CategoryIndex(ReportModel.CategoryFor(a.Order))
                      .CompareTo(ReportModel.CategoryIndex(ReportModel.CategoryFor(b.Order)));
                if (c != 0) return c;
                return a.Order.CompareTo(b.Order);
            });

            var sections = new List<object>();
            foreach (var s in ordered)
            {
                var facts = new List<object>();
                foreach (var f in s.Facts)
                {
                    facts.Add(new Dictionary<string, object>
                    {
                        { "name", f.Item1 },
                        { "value", f.Item2 },
                        { "level", NormalizeLevel(f.Item3) }
                    });
                }

                var tables = new List<object>();
                foreach (var t in s.Tables)
                {
                    if (t.Rows.Count == 0) continue;   // 与 HTML 一致：空表不输出
                    var rows = new List<object>();
                    foreach (var row in t.Rows)
                    {
                        var obj = new Dictionary<string, object>();
                        for (int i = 0; i < row.Count; i++)
                        {
                            string key = (i < t.Headers.Count && t.Headers[i].Length > 0)
                                ? t.Headers[i] : ("col" + i);
                            if (!obj.ContainsKey(key)) obj[key] = row[i];
                            else obj["col" + i] = row[i];   // 重名列兜底
                        }
                        rows.Add(obj);
                    }
                    tables.Add(new Dictionary<string, object>
                    {
                        { "title", t.Title ?? "" },
                        { "collapsed", t.Collapsed },
                        { "columns", ToList(t.Headers) },
                        { "rows", rows },
                        { "note", t.Note }
                    });
                }

                sections.Add(new Dictionary<string, object>
                {
                    { "id", s.Id },
                    { "title", s.Title },
                    { "category", ReportModel.CategoryFor(s.Order) },
                    { "order", s.Order },
                    { "icon", s.Icon },
                    { "status", new Dictionary<string, object>
                        {
                            { "level", NormalizeLevel(s.Status) },
                            { "text", s.StatusText }
                        } },
                    { "facts", facts },
                    { "tables", tables },
                    { "notes", ToList(s.Notes) }
                });
            }

            return new Dictionary<string, object>
            {
                { "schema_version", SchemaVersion },
                { "meta", meta },
                { "summary", summary },
                { "sections", sections },
                { "boundaries", ToList(Boundaries) },
                { "failures", ToList(m.Failures) }
            };
        }

        /// <summary>空状态归一化为 "normal"，避免 "" 造成歧义</summary>
        private static string NormalizeLevel(string level)
        {
            return string.IsNullOrEmpty(level) ? "normal" : level;
        }

        private static List<object> ToList(System.Collections.IEnumerable items)
        {
            var list = new List<object>();
            foreach (var item in items) list.Add(item);
            return list;
        }
    }
}
