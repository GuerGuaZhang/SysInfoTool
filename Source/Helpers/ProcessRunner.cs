using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SysInfoTool.Helpers
{
    /// <summary>
    /// 外部命令执行封装：netsh / powercfg / driverquery 等，带超时保护。
    /// 注意：Windows 10/11 的原生工具在输出被重定向到管道时，编码并不统一——
    /// 有的按控制台代码页输出（中文系统为 GBK），有的输出 UTF-8，个别输出 UTF-16。
    /// 因此这里读取原始字节后自动探测编码，避免中文内容乱码导致解析失败。
    /// </summary>
    public static class ProcessRunner
    {
        /// <summary>运行命令并返回标准输出；超时或失败返回 null</summary>
        public static string Run(string fileName, string arguments, int timeoutMs = 30000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    // 异步读 stderr，防管道缓冲写满造成死锁
                    var errTask = ReadToEndBytesAsync(p.StandardError.BaseStream);
                    var outBytes = ReadToEndBytes(p.StandardOutput.BaseStream);
                    errTask.Wait(2000);
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        return null;
                    }
                    return Decode(outBytes);
                }
            }
            catch { return null; }
        }

        /// <summary>运行命令并返回退出码（stderr 同时异步消费，避免死锁）</summary>
        public static int RunExitCode(string fileName, string arguments, int timeoutMs = 30000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    var outTask = ReadToEndBytesAsync(p.StandardOutput.BaseStream);
                    var errTask = ReadToEndBytesAsync(p.StandardError.BaseStream);
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        return -1;
                    }
                    Task.WaitAll(outTask, errTask);
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }

        /// <summary>
        /// 运行命令并合并 stdout/stderr 返回（适合 java -version 等把版本输出写到 stderr 的命令）。
        /// 优先返回 stdout，为空时返回 stderr；超时或失败返回 null。
        /// </summary>
        public static string RunMerged(string fileName, string arguments, int timeoutMs = 15000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    var outTask = ReadToEndBytesAsync(p.StandardOutput.BaseStream);
                    var errTask = ReadToEndBytesAsync(p.StandardError.BaseStream);
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        return null;
                    }
                    Task.WaitAll(outTask, errTask);
                    string stdout = Decode(outTask.Result);
                    if (stdout.Length > 0) return stdout;
                    return Decode(errTask.Result);
                }
            }
            catch { return null; }
        }

        // ---------- 内部：原始字节读取与编码探测 ----------

        private static byte[] ReadToEndBytes(Stream s)
        {
            using (var ms = new MemoryStream())
            {
                var buf = new byte[8192];
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                    ms.Write(buf, 0, n);
                return ms.ToArray();
            }
        }

        private static async Task<byte[]> ReadToEndBytesAsync(Stream s)
        {
            using (var ms = new MemoryStream())
            {
                var buf = new byte[8192];
                int n;
                while ((n = await s.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false)) > 0)
                    ms.Write(buf, 0, n);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 自动解码：优先 UTF-16 BOM / UTF-8 BOM；无 BOM 时先按严格 UTF-8 尝试，
        /// 成功则用 UTF-8（Win11 常见），失败则回退系统 ANSI 代码页（如中文系统 GBK）。
        /// </summary>
        public static string Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            // UTF-16 LE / BE BOM
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            // UTF-8 BOM
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            // 严格 UTF-8 探测
            if (IsValidUtf8(bytes))
                return Encoding.UTF8.GetString(bytes);
            // 回退 ANSI 代码页
            return Encoding.Default.GetString(bytes);
        }

        /// <summary>严格校验字节序列是否为合法 UTF-8（多字节序列 + 排除 BOM/控制异常）</summary>
        private static bool IsValidUtf8(byte[] bytes)
        {
            int i = 0;
            int n = bytes.Length;
            while (i < n)
            {
                byte b = bytes[i];
                if (b < 0x80) { i++; continue; }
                int extra;
                if ((b & 0xE0) == 0xC0) extra = 1;          // 110xxxxx
                else if ((b & 0xF0) == 0xE0) extra = 2;     // 1110xxxx
                else if ((b & 0xF8) == 0xF0) extra = 3;     // 11110xxx
                else return false;                           // 孤立续字节或非法首字节
                if (i + extra >= n) return false;
                for (int k = 1; k <= extra; k++)
                {
                    if ((bytes[i + k] & 0xC0) != 0x80) return false;
                }
                i += extra + 1;
            }
            return true;
        }
    }
}
