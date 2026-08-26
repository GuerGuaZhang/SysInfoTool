using System;
using System.Collections.Generic;
using System.Management;

namespace SysInfoTool.Helpers
{
    /// <summary>WMI 查询封装：每次查询返回字典列表，屏蔽 ManagementObject 样板代码</summary>
    public static class WmiHelper
    {
        /// <summary>查询并返回所有实例（属性名 → 值）。查询失败时抛异常，由采集器决定如何处理。</summary>
        public static List<Dictionary<string, object>> Query(string scope, string wql)
        {
            var results = new List<Dictionary<string, object>>();
            using (var searcher = new ManagementObjectSearcher(scope, wql))
            using (var collection = searcher.Get())
            {
                foreach (ManagementObject mo in collection)
                {
                    using (mo)
                    {
                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        foreach (var p in mo.Properties)
                        {
                            object v = null;
                            try { v = p.Value; } catch { /* 个别属性读取失败忽略 */ }
                            row[p.Name] = v;
                        }
                        results.Add(row);
                    }
                }
            }
            return results;
        }

        public static List<Dictionary<string, object>> Query(string wql)
        {
            return Query(@"root\cimv2", wql);
        }

        public static Dictionary<string, object> First(string wql)
        {
            var list = Query(wql);
            return list.Count > 0 ? list[0] : null;
        }

        public static Dictionary<string, object> First(string scope, string wql)
        {
            var list = Query(scope, wql);
            return list.Count > 0 ? list[0] : null;
        }

        // ---------- 常用属性安全读取 ----------

        public static string Str(Dictionary<string, object> row, string key, string def = "")
        {
            if (row == null) return def;
            object v;
            if (!row.TryGetValue(key, out v) || v == null) return def;
            var s = v.ToString().Trim();
            return s.Length == 0 ? def : s;
        }

        public static ulong U64(Dictionary<string, object> row, string key, ulong def = 0)
        {
            if (row == null) return def;
            object v;
            if (!row.TryGetValue(key, out v) || v == null) return def;
            try { return Convert.ToUInt64(v); } catch { return def; }
        }

        public static uint U32(Dictionary<string, object> row, string key, uint def = 0)
        {
            if (row == null) return def;
            object v;
            if (!row.TryGetValue(key, out v) || v == null) return def;
            try { return Convert.ToUInt32(v); } catch { return def; }
        }

        public static bool Bool(Dictionary<string, object> row, string key, bool def = false)
        {
            if (row == null) return def;
            object v;
            if (!row.TryGetValue(key, out v) || v == null) return def;
            try { return Convert.ToBoolean(v); } catch { return def; }
        }

        /// <summary>WMI 日期时间字符串 → "yyyy-MM-dd"。</summary>
        /// <remarks>
        /// 兼容两种格式：标准 CIM_DATETIME（"20260812000000.000000+480"）与
        /// 普通日期字符串（如 Win32_QuickFixEngineering.InstalledOn 返回 "2026/8/12 0:00:00"）。
        /// </remarks>
        public static string Date(Dictionary<string, object> row, string key, string def = "")
        {
            if (row == null) return def;
            object v;
            if (!row.TryGetValue(key, out v) || v == null) return def;
            string raw = v.ToString().Trim();
            if (raw.Length == 0) return def;

            // 1) 标准 CIM_DATETIME
            try
            {
                var dt = ManagementDateTimeConverter.ToDateTime(raw);
                return dt.ToString("yyyy-MM-dd");
            }
            catch { }

            // 2) 常见普通日期格式
            DateTime parsed;
            var styles = System.Globalization.DateTimeStyles.AllowWhiteSpaces;
            if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, styles, out parsed) ||
                DateTime.TryParse(raw, System.Globalization.CultureInfo.CurrentCulture, styles, out parsed))
            {
                return parsed.ToString("yyyy-MM-dd");
            }

            // 3) 裸 yyyyMMdd / yyyyMMddHHmmss
            try
            {
                if (raw.Length >= 8 && raw.Substring(0, 4).All(char.IsDigit))
                {
                    var dt = DateTime.ParseExact(raw.Substring(0, 8), "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture);
                    return dt.ToString("yyyy-MM-dd");
                }
            }
            catch { }

            return def;
        }
    }
}
