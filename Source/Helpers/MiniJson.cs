using System;
using System.Collections;
using System.Globalization;
using System.Text;

namespace SysInfoTool.Helpers
{
    /// <summary>
    /// 极简 JSON 序列化器：支持字符串/数字/布尔/日期/枚举/集合/对象公共字段。
    /// 专为报告数据设计，零外部依赖，保证单文件 exe；支持可选的美化缩进输出。
    /// </summary>
    public static class MiniJson
    {
        public static string Serialize(object obj)
        {
            return Serialize(obj, false);
        }

        /// <param name="pretty">true 时输出带 2 空格缩进的多行 JSON，便于阅读与 AI 解析</param>
        public static string Serialize(object obj, bool pretty)
        {
            var sb = new StringBuilder(64 * 1024);
            Write(sb, obj, pretty, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object obj, bool pretty, int depth)
        {
            if (obj == null) { sb.Append("null"); return; }

            var type = obj.GetType();

            if (obj is string) { WriteString(sb, (string)obj); return; }
            if (obj is bool) { sb.Append((bool)obj ? "true" : "false"); return; }
            if (obj is DateTime) { WriteString(sb, ((DateTime)obj).ToString("yyyy-MM-dd HH:mm:ss")); return; }
            if (type.IsEnum) { WriteString(sb, obj.ToString()); return; }
            if (IsNumber(type))
            {
                sb.Append(Convert.ToString(obj, CultureInfo.InvariantCulture));
                return;
            }

            var dict = obj as IDictionary;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    NewLine(sb, pretty, depth + 1);
                    WriteString(sb, Convert.ToString(kv.Key, CultureInfo.InvariantCulture));
                    sb.Append(':');
                    if (pretty) sb.Append(' ');
                    Write(sb, kv.Value, pretty, depth + 1);
                }
                if (!first) NewLine(sb, pretty, depth);
                sb.Append('}');
                return;
            }

            var list = obj as IEnumerable;
            if (list != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    NewLine(sb, pretty, depth + 1);
                    Write(sb, item, pretty, depth + 1);
                }
                if (!first) NewLine(sb, pretty, depth);
                sb.Append(']');
                return;
            }

            // 普通对象：序列化公共字段与可读属性
            sb.Append('{');
            {
                bool first = true;
                foreach (var f in type.GetFields())
                {
                    if (!first) sb.Append(',');
                    first = false;
                    NewLine(sb, pretty, depth + 1);
                    WriteString(sb, f.Name);
                    sb.Append(':');
                    if (pretty) sb.Append(' ');
                    Write(sb, f.GetValue(obj), pretty, depth + 1);
                }
                foreach (var p in type.GetProperties())
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    NewLine(sb, pretty, depth + 1);
                    WriteString(sb, p.Name);
                    sb.Append(':');
                    if (pretty) sb.Append(' ');
                    object val;
                    try { val = p.GetValue(obj, null); } catch { val = null; }
                    Write(sb, val, pretty, depth + 1);
                }
                if (!first) NewLine(sb, pretty, depth);
            }
            sb.Append('}');
        }

        private static void NewLine(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        private static bool IsNumber(Type t)
        {
            return t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort)
                || t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong)
                || t == typeof(float) || t == typeof(double) || t == typeof(decimal);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
