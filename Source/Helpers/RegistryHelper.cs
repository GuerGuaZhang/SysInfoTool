using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace SysInfoTool.Helpers
{
    /// <summary>注册表安全读取封装：所有读取不抛异常</summary>
    public static class RegistryHelper
    {
        public static string GetString(RegistryKey root, string path, string name, string def = "")
        {
            try
            {
                using (var key = root.OpenSubKey(path))
                {
                    if (key == null) return def;
                    var v = key.GetValue(name);
                    if (v == null) return def;
                    // MULTI_SZ（如 PendingFileRenameOperations）→ string[]，拼成一行
                    var arr = v as string[];
                    if (arr != null) return string.Join("；", arr);
                    return v.ToString();
                }
            }
            catch { return def; }
        }

        public static int GetInt(RegistryKey root, string path, string name, int def = -1)
        {
            try
            {
                using (var key = root.OpenSubKey(path))
                {
                    if (key == null) return def;
                    var v = key.GetValue(name);
                    if (v == null) return def;
                    return Convert.ToInt32(v);
                }
            }
            catch { return def; }
        }

        public static long GetLong(RegistryKey root, string path, string name, long def = -1)
        {
            try
            {
                using (var key = root.OpenSubKey(path))
                {
                    if (key == null) return def;
                    var v = key.GetValue(name);
                    if (v == null) return def;
                    if (v is byte[]) return BitConverter.ToInt64((byte[])v, 0);
                    return Convert.ToInt64(v);
                }
            }
            catch { return def; }
        }

        public static bool KeyExists(RegistryKey root, string path)
        {
            try
            {
                using (var key = root.OpenSubKey(path))
                    return key != null;
            }
            catch { return false; }
        }

        /// <summary>枚举子键名称</summary>
        public static List<string> SubKeyNames(RegistryKey root, string path)
        {
            var names = new List<string>();
            try
            {
                using (var key = root.OpenSubKey(path))
                {
                    if (key == null) return names;
                    names.AddRange(key.GetSubKeyNames());
                }
            }
            catch { }
            return names;
        }

        /// <summary>枚举一个键下的所有值（名称 → 字符串值）</summary>
        public static List<KeyValuePair<string, string>> Values(RegistryKey root, string path)
        {
            var list = new List<KeyValuePair<string, string>>();
            try
            {
                using (var key = root.OpenSubKey(path))
                {
                    if (key == null) return list;
                    foreach (var name in key.GetValueNames())
                    {
                        var v = key.GetValue(name);
                        list.Add(new KeyValuePair<string, string>(name, v == null ? "" : v.ToString()));
                    }
                }
            }
            catch { }
            return list;
        }

        // 常用快捷方式
        public static string HKLM(string path, string name, string def = "")
        {
            return GetString(Registry.LocalMachine, path, name, def);
        }

        public static string HKCU(string path, string name, string def = "")
        {
            return GetString(Registry.CurrentUser, path, name, def);
        }
    }
}
