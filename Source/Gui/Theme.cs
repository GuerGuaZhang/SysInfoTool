using System.Drawing;

namespace SysInfoTool.Gui
{
    /// <summary>
    /// 界面设计令牌 —— 与《澄明情报报告》HTML 设计语言同源：
    /// 深海军蓝页头 + 靛蓝主色 + 三态状态色 + 冷灰蓝背景，全站统一。
    /// </summary>
    internal static class Theme
    {
        // 中性色
        public static readonly Color Bg = Color.FromArgb(0xEE, 0xF1, 0xF7);      // 页面底
        public static readonly Color Surface = Color.White;                       // 表面
        public static readonly Color Ink = Color.FromArgb(0x1A, 0x21, 0x34);      // 主文本
        public static readonly Color Ink2 = Color.FromArgb(0x5B, 0x65, 0x77);     // 次级文本
        public static readonly Color Ink3 = Color.FromArgb(0x98, 0xA1, 0xB3);     // 弱文本（提示）
        public static readonly Color Line = Color.FromArgb(0xE3, 0xE8, 0xF1);     // 分隔线
        public static readonly Color Line2 = Color.FromArgb(0xEE, 0xF1, 0xF7);

        // 主色
        public static readonly Color Accent = Color.FromArgb(0x3D, 0x5A, 0xF1);   // 靛蓝
        public static readonly Color AccentHover = Color.FromArgb(0x2F, 0x49, 0xD8);
        public static readonly Color AccentPressed = Color.FromArgb(0x27, 0x39, 0xB8);
        public static readonly Color AccentSoft = Color.FromArgb(0xEE, 0xF1, 0xFE);
        public static readonly Color AccentText = Color.FromArgb(0x2F, 0x49, 0xD8);
        public static readonly Color Disabled = Color.FromArgb(0xA9, 0xB2, 0xCC);

        // 状态色（ok / warn / error）
        public static readonly Color Ok = Color.FromArgb(0x15, 0x80, 0x3D);
        public static readonly Color OkSoft = Color.FromArgb(0xE7, 0xF6, 0xEC);
        public static readonly Color OkLine = Color.FromArgb(0xBF, 0xE6, 0xCD);
        public static readonly Color Warn = Color.FromArgb(0xB4, 0x53, 0x09);
        public static readonly Color WarnSoft = Color.FromArgb(0xFD, 0xF3, 0xE2);
        public static readonly Color WarnLine = Color.FromArgb(0xF2, 0xDD, 0xB3);
        public static readonly Color Err = Color.FromArgb(0xB9, 0x1C, 0x1C);
        public static readonly Color ErrSoft = Color.FromArgb(0xFD, 0xEA, 0xEA);
        public static readonly Color ErrLine = Color.FromArgb(0xF2, 0xC3, 0xC3);

        // 页头 / 开关轨道
        public static readonly Color HeaderTop = Color.FromArgb(0x1B, 0x23, 0x40);
        public static readonly Color HeaderBottom = Color.FromArgb(0x25, 0x2E, 0x58);
        public static readonly Color HeaderKicker = Color.FromArgb(0x9D, 0xB0, 0xFF);
        public static readonly Color HeaderDesc = Color.FromArgb(0xC3, 0xCD, 0xF0);
        public static readonly Color Track = Color.FromArgb(0xCB, 0xD3, 0xE1);
    }
}
