using System;
using System.IO;

namespace SysInfoTool.Helpers
{
    /// <summary>通用格式化工具</summary>
    public static class FormatHelper
    {
        /// <summary>字节数 → 可读字符串</summary>
        public static string Bytes(double bytes, int digits = 1)
        {
            if (bytes < 0) return "—";
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            int i = 0;
            while (bytes >= 1024 && i < units.Length - 1) { bytes /= 1024; i++; }
            return bytes.ToString("F" + digits) + " " + units[i];
        }

        public static string BytesFromUlong(ulong bytes)
        {
            return Bytes(bytes);
        }

        /// <summary>百分比</summary>
        public static string Percent(double used, double total)
        {
            if (total <= 0) return "—";
            return (used / total * 100).ToString("F1") + "%";
        }

        /// <summary>MHz → GHz 可读频率</summary>
        public static string Mhz(uint mhz)
        {
            if (mhz == 0) return "—";
            if (mhz >= 1000) return (mhz / 1000.0).ToString("F2") + " GHz";
            return mhz + " MHz";
        }

        /// <summary>bps → 可读速率</summary>
        public static string BitsPerSecond(ulong bps)
        {
            if (bps == 0) return "—";
            if (bps >= 1000000000UL) return (bps / 1000000000.0).ToString("F0") + " Gbps";
            if (bps >= 1000000UL) return (bps / 1000000.0).ToString("F0") + " Mbps";
            if (bps >= 1000UL) return (bps / 1000.0).ToString("F0") + " Kbps";
            return bps + " bps";
        }

        /// <summary>时间间隔 → 可读运行时长</summary>
        public static string Uptime(TimeSpan ts)
        {
            if (ts.TotalDays >= 1)
                return (int)ts.TotalDays + " 天 " + ts.Hours + " 小时 " + ts.Minutes + " 分钟";
            if (ts.TotalHours >= 1)
                return (int)ts.TotalHours + " 小时 " + ts.Minutes + " 分钟";
            return ts.Minutes + " 分钟";
        }

        /// <summary>YYYYMMDD / YYYYMMddHHmmss 等 InstallDate 格式归一化</summary>
        public static string LooseDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            raw = raw.Trim();
            if (raw.Length >= 8 && raw.Substring(0, 4).All(char.IsDigit))
            {
                try
                {
                    var dt = DateTime.ParseExact(raw.Substring(0, 8), "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture);
                    return dt.ToString("yyyy-MM-dd");
                }
                catch { return raw; }
            }
            return raw;
        }

        /// <summary>目录体积快速统计（带时间与文件数上限保护）</summary>
        public static Tuple<long, long> DirectorySize(string path, int maxSeconds = 15, long maxFiles = 200000)
        {
            long size = 0, count = 0;
            if (!Directory.Exists(path)) return Tuple.Create(0L, 0L);
            var start = Environment.TickCount;
            var stack = new System.Collections.Generic.Stack<string>();
            stack.Push(path);
            while (stack.Count > 0)
            {
                if (Environment.TickCount - start > maxSeconds * 1000 || count > maxFiles) break;
                string dir = stack.Pop();
                string[] files;
                try { files = Directory.GetFiles(dir); } catch { continue; }
                foreach (var f in files)
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if ((fi.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        size += fi.Length;
                        count++;
                    }
                    catch { }
                }
                string[] subs;
                try { subs = Directory.GetDirectories(dir); } catch { continue; }
                foreach (var s in subs)
                {
                    try
                    {
                        if ((new DirectoryInfo(s).Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        stack.Push(s);
                    }
                    catch { }
                }
            }
            return Tuple.Create(size, count);
        }
    }

    internal static class StringExt
    {
        public static bool All(this string s, Func<char, bool> pred)
        {
            foreach (var c in s) if (!pred(c)) return false;
            return s.Length > 0;
        }
    }
}
