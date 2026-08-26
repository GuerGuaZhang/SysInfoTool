using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Gui
{
    /// <summary>
    /// 主界面 —— 按《澄明情报报告》设计语言重绘：
    /// 深色渐变页头 + 细条状态横幅 + 分组小标 + 胶囊开关 + 靛蓝主按钮。
    /// 提示类信息一律弱化（细横幅 / 小字注脚），主界面留给选项与操作。
    /// </summary>
    public class MainForm : Form
    {
        private readonly AccentButton _btnStart;
        private readonly AccentProgressBar _progress;
        private readonly Label _lblStatus;
        private readonly ToggleSwitch _tglMask;
        private readonly ToggleSwitch _tglSkipScan;
        private readonly StatusBanner _banner;
        private readonly ToolTip _tip;
        private readonly bool _isAdmin;

        public MainForm()
        {
            _isAdmin = NativeMethods.IsAdministrator();
            Text = AppVersion.DisplayName + " v" + AppVersion.Value;
            ClientSize = new Size(560, 438);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Font = new Font("Microsoft YaHei UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;

            // 页头
            var header = new HeaderPanel(AppVersion.DisplayName + " v" + AppVersion.Value,
                "系统情报 · 硬件 / 系统 / 软件 / 使用痕迹 / 网络");
            Controls.Add(header);

            // 权限状态横幅（细条提示，不占主界面）
            _banner = new StatusBanner
            {
                Location = new Point(20, 100),
                Size = new Size(ClientSize.Width - 40, 30)
            };
            UpdateBanner();
            Controls.Add(_banner);

            // 分组小标
            Controls.Add(new SectionLabel("生成选项") { Location = new Point(22, 142), Size = new Size(220, 20) });

            // 选项行：开关 / 名称 / 说明 统一水平居中对齐
            _tip = new ToolTip();
            int rowY = 172;

            _tglMask = new ToggleSwitch { Location = new Point(24, rowY), Checked = true };
            var lblMask = MakeOptionLabel("隐藏敏感信息", 82, rowY, _tglMask);
            var hintMask = MakeHint("序列号 / MAC / IP / 用户名 打码", rowY, _tglMask);

            _tglSkipScan = new ToggleSwitch { Location = new Point(24, rowY + 42) };
            var lblSkip = MakeOptionLabel("跳过文件夹体积统计", 82, rowY + 42, _tglSkipScan);
            var hintSkip = MakeHint("大容量硬盘可加速", rowY + 42, _tglSkipScan);

            // 主按钮：布置在主页面靠下的位置
            _btnStart = new AccentButton
            {
                Text = "开始生成报告",
                Location = new Point(20, 352),
                Size = new Size(ClientSize.Width - 40, 44)
            };
            _btnStart.Click += async (sender, e) => await RunAsync();

            // 进度条（默认隐藏）
            _progress = new AccentProgressBar
            {
                Location = new Point(20, 300),
                Size = new Size(ClientSize.Width - 40, 8),
                Visible = false
            };

            // 状态行（小字）
            _lblStatus = new Label
            {
                Location = new Point(20, 314),
                Size = new Size(ClientSize.Width - 40, 18),
                Font = new Font("Microsoft YaHei UI", 8.5f),
                ForeColor = Theme.Ink2,
                AutoEllipsis = true
            };

            // 页脚（弱提示，一行小字）
            var footer = new Label
            {
                Text = "输出：程序所在目录 / <电脑名> / · 完成后自动打开浏览器",
                Location = new Point(20, 414),
                Size = new Size(ClientSize.Width - 40, 16),
                Font = new Font("Microsoft YaHei UI", 8f),
                ForeColor = Theme.Ink3,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Controls.Add(_tglMask); Controls.Add(lblMask); Controls.Add(hintMask);
            Controls.Add(_tglSkipScan); Controls.Add(lblSkip); Controls.Add(hintSkip);
            Controls.Add(_btnStart);
            Controls.Add(_progress);
            Controls.Add(_lblStatus);
            Controls.Add(footer);

            AcceptButton = _btnStart;
        }

        private void UpdateBanner()
        {
            _banner.Set(_isAdmin ? StatusBanner.Kind.Ok : StatusBanner.Kind.Warn,
                _isAdmin
                    ? "管理员权限 · 可采集全部项目"
                    : "普通权限 · SMART、安全日志、BitLocker 等少数项目将受限");
        }

        /// <summary>选项名称：与开关垂直居中对齐</summary>
        private static Label MakeOptionLabel(string text, int x, int rowTop, Control toggle)
        {
            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                BackColor = Color.Transparent
            };
            var pref = lbl.PreferredSize;   // 文本实测尺寸（AutoSize 生效前 Width 不可靠）
            lbl.Location = new Point(x, rowTop + (toggle.Height - pref.Height) / 2);
            return lbl;
        }

        /// <summary>选项说明小字：右对齐且与开关垂直居中对齐（用实测尺寸，保证完整显示）</summary>
        private Label MakeHint(string text, int rowTop, Control toggle)
        {
            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 8.5f),
                ForeColor = Theme.Ink3,
                BackColor = Color.Transparent
            };
            var pref = lbl.PreferredSize;   // 文本实测尺寸
            lbl.Location = new Point(ClientSize.Width - 20 - pref.Width, rowTop + (toggle.Height - pref.Height) / 2);
            return lbl;
        }

        private async System.Threading.Tasks.Task RunAsync()
        {
            _btnStart.Enabled = false;
            _tglMask.Enabled = false;
            _tglSkipScan.Enabled = false;
            _btnStart.Text = "正在生成…";
            _progress.Visible = true;
            _progress.Value = 0;
            _lblStatus.ForeColor = Theme.Ink2;
            _lblStatus.Text = "正在采集……";

            var service = new ReportService(new ReportService.Options
            {
                Mask = _tglMask.Checked,
                SkipScan = _tglSkipScan.Checked
            });

            var progress = new Progress<Tuple<int, int, string>>(p =>
            {
                _progress.Value = (int)(p.Item1 * 100.0 / Math.Max(1, p.Item2));
                _lblStatus.Text = string.Format("正在采集 {0}/{1}：{2}", p.Item1, p.Item2, p.Item3);
            });

            try
            {
                var result = await service.RunAsync(progress);
                _progress.Value = 100;
                string hint = result.Model.Failures.Count > 0
                    ? "，其中 " + result.Model.Failures.Count + " 项采集失败（详见报告末尾）"
                    : "";
                _lblStatus.ForeColor = result.Model.Failures.Count > 0 ? Theme.Warn : Theme.Ink2;
                _lblStatus.Text = "完成（耗时 " + result.Model.DurationSeconds + " 秒" + hint + "）：已生成并打开报告";
                _tip.SetToolTip(_lblStatus, result.HtmlPath);

                try
                {
                    Process.Start(new ProcessStartInfo(result.HtmlPath) { UseShellExecute = true });
                }
                catch
                {
                    MessageBox.Show("报告已生成：\n" + result.HtmlPath, "完成",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = Theme.Err;
                _lblStatus.Text = "生成失败：" + ex.Message;
                _tip.SetToolTip(_lblStatus, ex.Message);
                MessageBox.Show("生成报告时出错：\n" + ex, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnStart.Enabled = true;
                _tglMask.Enabled = true;
                _tglSkipScan.Enabled = true;
                _btnStart.Text = "开始生成报告";
            }
        }
    }
}
