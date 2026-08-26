using System;
using System.Text.RegularExpressions;

namespace SysInfoTool.Helpers
{
    /// <summary>敏感信息脱敏：序列号 / MAC / IP / 用户名 / SSID / 计算机名</summary>
    public class MaskHelper
    {
        private readonly bool _enabled;
        private readonly string _userName;
        private readonly string _computerName;

        private static readonly Regex MacRegex = new Regex(
            @"\b([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}\b", RegexOptions.Compiled);

        private static readonly Regex IpRegex = new Regex(
            @"\b(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.\d{1,3}\b", RegexOptions.Compiled);

        public MaskHelper(bool enabled)
        {
            _enabled = enabled;
            _userName = Environment.UserName ?? "";
            _computerName = Environment.MachineName ?? "";
        }

        public bool Enabled { get { return _enabled; } }

        /// <summary>通用文本脱敏：用户名、计算机名、MAC、IPv4</summary>
        public string Text(string s)
        {
            if (!_enabled || string.IsNullOrEmpty(s)) return s;

            if (_userName.Length >= 2)
                s = Regex.Replace(s, Regex.Escape(_userName), "***", RegexOptions.IgnoreCase);
            if (_computerName.Length >= 2)
                s = Regex.Replace(s, Regex.Escape(_computerName), "***", RegexOptions.IgnoreCase);

            // MAC：保留 OUI 前三字节
            s = MacRegex.Replace(s, m =>
            {
                string sep = m.Value.Contains(":") ? ":" : "-";
                var parts = m.Value.Split(sep[0]);
                return parts[0] + sep + parts[1] + sep + parts[2] + sep + "**" + sep + "**" + sep + "**";
            });

            // IPv4：保留前两段
            s = IpRegex.Replace(s, m => m.Groups[1].Value + "." + m.Groups[2].Value + ".*.*");

            return s;
        }

        /// <summary>序列号类完全打码</summary>
        public string Serial(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            if (!_enabled) return s.Trim();
            var t = s.Trim();
            return t.Length <= 2 ? "****" : t.Substring(0, 2) + new string('*', Math.Min(10, t.Length));
        }

        /// <summary>SSID 部分打码</summary>
        public string Ssid(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (!_enabled) return s;
            return s.Length <= 2 ? s + "***" : s.Substring(0, 2) + "***";
        }

        /// <summary>用户路径脱敏：C:\Users\张三 → C:\Users\***</summary>
        public string Path(string s)
        {
            if (!_enabled || string.IsNullOrEmpty(s)) return s;
            return Regex.Replace(s, @"([A-Za-z]:\\[Uu]sers\\)([^\\]+)", m => m.Groups[1].Value + "***");
        }
    }
}
