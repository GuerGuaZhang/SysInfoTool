using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SysInfoTool.Gui
{
    /// <summary>圆角矩形路径（GDI+）</summary>
    internal static class Ui
    {
        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>深色渐变页头：kicker 小标 + 标题 + 副题 + 靛蓝角标</summary>
    internal sealed class HeaderPanel : Panel
    {
        private readonly string _title;
        private readonly string _subtitle;

        public HeaderPanel(string title, string subtitle)
        {
            _title = title;
            _subtitle = subtitle;
            Height = 84;
            Dock = DockStyle.Top;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        // 禁止基类绘制系统底色（避免深色主题下圆角外露出黑色方块）
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            using (var b = new LinearGradientBrush(ClientRectangle, Theme.HeaderTop, Theme.HeaderBottom, 40f))
                g.FillRectangle(b, ClientRectangle);

            // 显示器小图标（简洁几何）
            int x = 24, y = 22;
            using (var pen = new Pen(Color.White, 2f))
            {
                g.DrawRectangle(pen, x, y, 28, 18);
                g.DrawLine(pen, x + 8, y + 23, x + 20, y + 23);
                g.DrawLine(pen, x + 12, y + 26, x + 16, y + 26);
            }
            using (var acc = new SolidBrush(Theme.Accent))
                g.FillRectangle(acc, x + 22, y, 6, 6);

            // kicker
            using (var f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                TextRenderer.DrawText(g, "SYSTEM INTELLIGENCE TOOL", f,
                    new Rectangle(66, 14, Width - 80, 16), Theme.HeaderKicker, TextFormatFlags.Left);

            // 标题
            using (var f = new Font("Microsoft YaHei UI", 12.5f, FontStyle.Bold))
                TextRenderer.DrawText(g, _title, f,
                    new Rectangle(66, 32, Width - 80, 26), Color.White, TextFormatFlags.Left);

            // 副题
            using (var f = new Font("Microsoft YaHei UI", 8f))
                TextRenderer.DrawText(g, _subtitle, f,
                    new Rectangle(66, 60, Width - 80, 16), Theme.HeaderDesc, TextFormatFlags.Left);

            // 底部靛蓝角标
            using (var acc = new SolidBrush(Theme.Accent))
                g.FillRectangle(acc, 24, Height - 3, 44, 3);
        }
    }

    /// <summary>细条状态横幅（管理员/权限提示，弱化展示，不占主界面）</summary>
    internal sealed class StatusBanner : Control
    {
        public enum Kind { Ok, Warn, Error }

        private Kind _kind = Kind.Ok;
        private string _text = "";

        public StatusBanner()
        {
            Height = 30;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public void Set(Kind kind, string text)
        {
            _kind = kind;
            _text = text;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            Color bg = Theme.OkSoft, fg = Theme.Ok, ln = Theme.OkLine;
            if (_kind == Kind.Warn) { bg = Theme.WarnSoft; fg = Theme.Warn; ln = Theme.WarnLine; }
            else if (_kind == Kind.Error) { bg = Theme.ErrSoft; fg = Theme.Err; ln = Theme.ErrLine; }

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.Rounded(r, 8))
            {
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                using (var p = new Pen(ln)) g.DrawPath(p, path);
            }
            string icon = _kind == Kind.Ok ? "✓" : (_kind == Kind.Warn ? "⚠" : "✕");
            using (var f = new Font("Segoe UI Symbol", 9f))
                TextRenderer.DrawText(g, icon, f, new Rectangle(10, 0, 18, Height), fg, TextFormatFlags.VerticalCenter);
            using (var f = new Font("Microsoft YaHei UI", 8.5f))
                TextRenderer.DrawText(g, _text, f, new Rectangle(32, 0, Width - 40, Height), fg,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>分组小标（靛蓝刻度 + 文字），呼应 HTML 分组标题</summary>
    internal sealed class SectionLabel : Control
    {
        private readonly string _text;

        public SectionLabel(string text)
        {
            _text = text;
            Height = 20;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            using (var b = new SolidBrush(Theme.Accent))
                g.FillRectangle(b, 0, 4, 5, 12);
            using (var f = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold))
                TextRenderer.DrawText(g, _text, f, new Rectangle(12, 0, Width - 12, Height), Theme.Ink3,
                    TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>胶囊开关（toggle）</summary>
    internal sealed class ToggleSwitch : Control
    {
        private bool _checked;

        public ToggleSwitch()
        {
            Width = 46;
            Height = 26;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public event EventHandler CheckedChanged;

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !_checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            var track = new Rectangle(1, 4, Width - 2, Height - 8);
            using (var path = Ui.Rounded(track, track.Height / 2))
            {
                using (var b = new SolidBrush(_checked ? Theme.Accent : Theme.Track))
                    g.FillPath(b, path);
            }
            int knob = _checked ? track.Right - track.Height + 2 : track.Left + 2;
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, knob, 6, Height - 12, Height - 12);
        }
    }

    /// <summary>主操作按钮（靛蓝圆角，悬停/按下/禁用三态）</summary>
    internal sealed class AccentButton : Button
    {
        private bool _hover;
        private bool _down;

        public AccentButton()
        {
            Height = 44;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            Color fill = Theme.Accent;
            if (!Enabled) fill = Theme.Disabled;
            else if (_down) fill = Theme.AccentPressed;
            else if (_hover) fill = Theme.AccentHover;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.Rounded(r, 9))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            }
            // 文字：按测量尺寸手动居中，保证像素级对齐
            using (var f = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold))
            {
                var sz = TextRenderer.MeasureText(g, Text, f);
                TextRenderer.DrawText(g, Text, f,
                    new Point(Math.Max(0, (r.Width - sz.Width) / 2), Math.Max(0, (r.Height - sz.Height) / 2)),
                    Color.White);
            }
        }
    }

    /// <summary>靛蓝进度条</summary>
    internal sealed class AccentProgressBar : Control
    {
        private int _value;

        public AccentProgressBar()
        {
            Height = 8;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public int Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.Rounded(r, Height / 2))
            {
                using (var b = new SolidBrush(Theme.Line)) g.FillPath(b, path);
            }
            if (_value <= 0) return;
            int w = (int)(r.Width * _value / 100.0);
            var fill = new Rectangle(r.X, r.Y, w, r.Height);
            using (var clip = Ui.Rounded(r, Height / 2))
            {
                g.SetClip(clip);
                using (var b = new LinearGradientBrush(fill, Theme.Accent, Color.FromArgb(0x7C, 0x8E, 0xF5), 0f))
                    g.FillRectangle(b, fill);
                g.ResetClip();
            }
        }
    }
}
