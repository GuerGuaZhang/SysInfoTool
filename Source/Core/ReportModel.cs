using System;
using System.Collections.Generic;

namespace SysInfoTool.Core
{
    /// <summary>报告中的一张表格</summary>
    public class TableData
    {
        public string Title;                    // 表格小标题，可为 null
        public List<string> Headers = new List<string>();
        public List<List<string>> Rows = new List<List<string>>();
        public bool Collapsed;                  // 默认折叠（长列表）
        public string Note;                     // 表格下方备注，可为 null
    }

    /// <summary>报告中的一个章节</summary>
    public class SectionData
    {
        public string Id;                       // 锚点 id
        public string Title;                    // 章节标题
        public int Order;                       // 排序
        public string Icon = "📋";
        // 概览键值对（名称, 值, 状态: ok/warn/error/空）
        public List<Tuple<string, string, string>> Facts = new List<Tuple<string, string, string>>();
        public List<TableData> Tables = new List<TableData>();
        public List<string> Notes = new List<string>();       // 提示/失败说明
        public string Status = "";              // 章节整体状态徽章: ok/warn/error/""
        public string StatusText = "";

        public void Fact(string name, string value, string status = "")
        {
            if (string.IsNullOrEmpty(value)) value = "—";
            Facts.Add(Tuple.Create(name, value, status ?? ""));
        }

        public TableData NewTable(string title, bool collapsed, params string[] headers)
        {
            var t = new TableData { Title = title, Collapsed = collapsed };
            t.Headers.AddRange(headers);
            Tables.Add(t);
            return t;
        }
    }

    /// <summary>顶部看板卡片</summary>
    public class CardData
    {
        public string Title;
        public string Icon = "💻";
        public int Order;                        // 排序：系统10/CPU20/内存30/显卡40/硬盘50/电池60
        public List<string> Lines = new List<string>();
        public string Status = "";   // ok/warn/error/""
    }

    /// <summary>整份报告的数据模型（采集层与输出层之间的唯一通道）</summary>
    public class ReportModel
    {
        public string ToolVersion = AppVersion.Value;
        public string ComputerName = "";
        public DateTime GeneratedAt = DateTime.Now;
        public double DurationSeconds;
        public bool Masked = true;
        public bool IsAdmin;
        public List<CardData> Cards = new List<CardData>();
        public List<SectionData> Sections = new List<SectionData>();
        public List<string> Failures = new List<string>();   // 采集失败项汇总
        private readonly object _lock = new object();

        public SectionData AddSection(string id, string title, int order, string icon)
        {
            var s = new SectionData { Id = id, Title = title, Order = order, Icon = icon };
            lock (_lock) Sections.Add(s);   // 采集器并行执行，需同步
            return s;
        }

        public CardData AddCard(string title, string icon, int order = 99, string status = "")
        {
            var c = new CardData { Title = title, Icon = icon, Order = order, Status = status };
            lock (_lock) Cards.Add(c);      // 采集器并行执行，需同步
            return c;
        }

        // ---------- 章节分组（HTML 侧栏导航 / JSON 分类共用） ----------

        /// <summary>按采集器 Order 归属报告分组：硬件 / 系统 / 软件 / 账户与痕迹 / 性能与日志 / 网络</summary>
        public static string CategoryFor(int order)
        {
            if (order < 110) return "硬件";
            if (order < 140) return "系统";
            if (order < 210) return "软件";
            if (order < 230) return "账户与痕迹";
            if (order < 250) return "性能与日志";
            return "网络";
        }

        public static readonly string[] CategoryOrder = {
            "硬件", "系统", "软件", "账户与痕迹", "性能与日志", "网络"
        };

        public static int CategoryIndex(string category)
        {
            for (int i = 0; i < CategoryOrder.Length; i++)
                if (CategoryOrder[i] == category) return i;
            return CategoryOrder.Length;
        }
    }
}
