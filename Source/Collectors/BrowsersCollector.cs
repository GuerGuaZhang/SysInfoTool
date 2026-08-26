using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class BrowsersCollector : ICollector
    {
        public string Name { get { return "浏览器"; } }
        public int Order { get { return 190; } }

        // 浏览器探测表：名称 / 卸载注册表键名特征 / exe 常见路径 / Chromium 用户数据目录
        private static readonly BrowserDef[] KnownBrowsers = {
            new BrowserDef("Google Chrome", "Chrome",
                new[] { @"Google\Chrome\Application\chrome.exe" },
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data")),
            new BrowserDef("Microsoft Edge", "Edge",
                new[] { @"Microsoft\Edge\Application\msedge.exe" },
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data")),
            new BrowserDef("Mozilla Firefox", "Firefox", new string[0], null),
            new BrowserDef("360 安全浏览器", "360Chrome", new string[0], null),
            new BrowserDef("360 极速浏览器", "360ChromeX", new string[0], null),
            new BrowserDef("QQ 浏览器", "QQBrowser", new string[0], null),
            new BrowserDef("搜狗浏览器", "SogouExplorer", new string[0], null),
        };

        private class BrowserDef
        {
            public string Name; public string RegHint; public string[] ExeRelPaths; public string UserDataDir;
            public BrowserDef(string n, string h, string[] p, string u) { Name = n; RegHint = h; ExeRelPaths = p; UserDataDir = u; }
        }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("browsers", "浏览器", Order, "🌍");
            try
            {
                // 从卸载列表找浏览器版本
                var uninstall = CollectUninstallHints();

                var found = new List<List<string>>();
                foreach (var def in KnownBrowsers)
                {
                    string version = FindVersion(def, uninstall);
                    if (version == null) continue;
                    string profileInfo = "";
                    if (def.UserDataDir != null && Directory.Exists(def.UserDataDir))
                        profileInfo = "用户数据目录存在";
                    found.Add(new List<string> { def.Name, version, profileInfo });
                }

                if (found.Count > 0)
                {
                    var t = s.NewTable("已安装浏览器", false, "浏览器", "版本", "备注");
                    t.Rows.AddRange(found);
                }
                else
                {
                    s.Notes.Add("未识别到常见浏览器。");
                }

                // Chromium 系：书签与扩展（不读历史记录）
                CollectChromiumData(ctx, s, "Google Chrome",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data"));
                CollectChromiumData(ctx, s, "Microsoft Edge",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data"));

                s.Notes.Add("按设定，第一版不采集浏览器历史记录。书签名可能暴露个人兴趣，已受脱敏开关保护。");
                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "浏览器信息读取失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private Dictionary<string, string> CollectUninstallHints()
        {
            var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] paths = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            foreach (var root in new[] { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser })
            {
                foreach (var path in paths)
                {
                    foreach (var sub in RegistryHelper.SubKeyNames(root, path))
                    {
                        string name = RegistryHelper.GetString(root, path + "\\" + sub, "DisplayName");
                        string ver = RegistryHelper.GetString(root, path + "\\" + sub, "DisplayVersion");
                        if (name.Length > 0 && ver.Length > 0) hints[name] = ver;
                    }
                }
            }
            return hints;
        }

        private string FindVersion(BrowserDef def, Dictionary<string, string> uninstall)
        {
            foreach (var kv in uninstall)
                if (kv.Key.IndexOf(def.RegHint, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kv.Value;
            // exe 文件版本兜底
            foreach (var rel in def.ExeRelPaths)
            {
                foreach (var baseDir in new[] {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
                {
                    try
                    {
                        string full = Path.Combine(baseDir, rel);
                        if (File.Exists(full))
                            return System.Diagnostics.FileVersionInfo.GetVersionInfo(full).FileVersion;
                    }
                    catch { }
                }
            }
            return null;
        }

        private void CollectChromiumData(ReportContext ctx, SectionData s, string browserName, string userDataDir)
        {
            if (!Directory.Exists(userDataDir)) return;

            // 所有配置文件目录（Default、Profile 1、Profile 2 …）
            var profiles = new List<string>();
            try
            {
                foreach (var dir in Directory.GetDirectories(userDataDir))
                {
                    string name = Path.GetFileName(dir);
                    if (name == "Default" || (name.StartsWith("Profile") && name.Length > 7 &&
                        name.Substring(7).All(char.IsDigit)))
                        profiles.Add(dir);
                }
            }
            catch { }
            if (profiles.Count == 0) profiles.Add(Path.Combine(userDataDir, "Default"));

            // 书签（JSON 文本解析，避免依赖）
            try
            {
                int totalUrls = 0;
                var allFolders = new List<string>();
                foreach (var profile in profiles)
                {
                    string bookmarksFile = Path.Combine(profile, "Bookmarks");
                    if (!File.Exists(bookmarksFile)) continue;
                    string json = File.ReadAllText(bookmarksFile);
                    totalUrls += CountOccurrences(json, "\"type\": \"url\"") + CountOccurrences(json, "\"type\":\"url\"");
                    allFolders.AddRange(ExtractBookmarkFolders(json));
                }
                allFolders = allFolders.Distinct().ToList();
                if (totalUrls > 0)
                {
                    string line = "书签共 " + totalUrls + " 条（" + profiles.Count + " 个配置文件）";
                    if (allFolders.Count > 0)
                        line += "，文件夹：" + string.Join("、", allFolders.Take(8).Select(f => ctx.MaskText(f)))
                            + (allFolders.Count > 8 ? " 等 " + allFolders.Count + " 个" : "");
                    s.Fact(browserName + " 书签", line);
                }
            }
            catch { }

            // 扩展（读 manifest.json，跨全部配置文件去重）
            try
            {
                var extNames = new List<string>();
                foreach (var profile in profiles)
                {
                    string extRoot = Path.Combine(profile, "Extensions");
                    if (!Directory.Exists(extRoot)) continue;
                    foreach (var extDir in Directory.GetDirectories(extRoot))
                    {
                        string versionDir = Directory.GetDirectories(extDir).OrderByDescending(d => d).FirstOrDefault();
                        if (versionDir == null) continue;
                        string manifest = Path.Combine(versionDir, "manifest.json");
                        if (!File.Exists(manifest)) continue;
                        string name = ReadManifestName(File.ReadAllText(manifest));
                        extNames.Add(name ?? Path.GetFileName(extDir));
                    }
                }
                extNames = extNames.Distinct().ToList();
                if (extNames.Count > 0)
                {
                    var et = s.NewTable(browserName + " 扩展（" + extNames.Count + " 个）", extNames.Count > 10, "扩展名称");
                    foreach (var n in extNames.OrderBy(n => n))
                        et.Rows.Add(new List<string> { ctx.MaskText(n) });
                }
            }
            catch { }
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
            {
                count++; idx += pattern.Length;
            }
            return count;
        }

        /// <summary>从 Bookmarks JSON 提取 "type": "folder" 的 name（粗略文本解析）</summary>
        private static List<string> ExtractBookmarkFolders(string json)
        {
            var names = new List<string>();
            var regex = new System.Text.RegularExpressions.Regex(
                "\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"[^{}]*\"type\"\\s*:\\s*\"folder\"");
            var regex2 = new System.Text.RegularExpressions.Regex(
                "\"type\"\\s*:\\s*\"folder\"[^{}]*\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            foreach (System.Text.RegularExpressions.Match m in regex.Matches(json))
                AddName(names, m.Groups[1].Value);
            foreach (System.Text.RegularExpressions.Match m in regex2.Matches(json))
                AddName(names, m.Groups[1].Value);
            return names.Distinct().ToList();
        }

        private static void AddName(List<string> names, string raw)
        {
            string n = raw.Replace("\\\"", "\"").Replace("\\\\", "\\");
            if (n.Length > 0 && n != "Bookmarks bar" && n != "Other bookmarks" &&
                n != "书签栏" && n != "其他书签" && names.Count < 30)
                names.Add(n);
        }

        /// <summary>从 manifest.json 提取 name；__MSG_xxx__ 形式的本地化占位返回 null</summary>
        private static string ReadManifestName(string json)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success) return null;
            string name = m.Groups[1].Value.Replace("\\\"", "\"");
            if (name.StartsWith("__MSG_")) return null;
            return name;
        }
    }
}
