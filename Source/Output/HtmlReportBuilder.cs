using System;
using System.Collections.Generic;
using System.Text;
using SysInfoTool.Core;

namespace SysInfoTool.Output
{
    /// <summary>
    /// 自包含 HTML 报告渲染 —— 设计语言《澄明情报报告 v1.3（全弹性）》：
    ///   · 流体骨架：body 网格 = 弹性侧栏 + minmax(0,1fr) 内容区，任意宽高比下填满且不溢出
    ///   · 流体字号/间距：全部字号与留白用 clamp()（--fs-0..6 / --space-1..4 设计令牌）
    ///   · 三态侧栏：全宽（≥1240px）→ 图标 rail（平板竖屏/小笔记本）→ 抽屉（手机）
    ///   · 自适应组件：看板卡片 auto-fill 自适应列数、事实值列弹性填满、表格 100% 填满（过窄时容器内滚动）
    ///   · 容器查询增强：内容区按自身宽度再调内部排版；滚动高亮、平滑锚点、移动抽屉、回到顶部
    /// </summary>
    public static class HtmlReportBuilder
    {
        public static string Build(ReportModel m)
        {
            var sb = new StringBuilder(160 * 1024);
            sb.Append("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            sb.Append("<title>电脑信息报告 - ").Append(H(m.ComputerName)).Append("</title>");
            sb.Append("<style>").Append(Css).Append("</style></head><body>");

            // ---------- 侧边导航（分组；三态：全宽 / 图标 rail / 抽屉） ----------
            sb.Append("<nav class=\"sidebar\" id=\"sidebar\">");
            sb.Append("<div class=\"side-head\"><div class=\"side-brand\">🖥️ 系统情报</div>");
            sb.Append("<div class=\"side-sub\">").Append(H(AppVersion.DisplayName)).Append(" v").Append(H(m.ToolVersion)).Append("</div></div>");
            sb.Append("<div class=\"side-body\">");
            sb.Append("<div class=\"side-group\"><div class=\"side-group-title\">总览</div><ul>");
            sb.Append("<li><a href=\"#top\" data-spy title=\"总览看板\"><span class=\"nav-ico\">📊</span><span class=\"nav-text\">总览看板</span></a></li>");
            if (m.Failures.Count > 0)
                sb.Append("<li><a href=\"#failures\" class=\"fail-link\" data-spy title=\"采集失败项\"><span class=\"nav-ico\">⚠️</span><span class=\"nav-text\">采集失败项</span></a></li>");
            sb.Append("</ul></div>");

            foreach (var cat in ReportModel.CategoryOrder)
            {
                var list = m.Sections.FindAll(s => ReportModel.CategoryFor(s.Order) == cat);
                if (list.Count == 0) continue;
                sb.Append("<div class=\"side-group\"><div class=\"side-group-title\">").Append(H(cat)).Append("</div><ul>");
                foreach (var s in list)
                    sb.Append("<li><a href=\"#").Append(s.Id).Append("\" data-spy title=\"")
                      .Append(H(s.Icon + " " + s.Title)).Append("\"><span class=\"nav-ico\">")
                      .Append(H(s.Icon)).Append("</span><span class=\"nav-text\">").Append(H(s.Title)).Append("</span></a></li>");
                sb.Append("</ul></div>");
            }
            sb.Append("</div>");
            sb.Append("<div class=\"side-foot\">自包含报告 · 可离线查看</div>");
            sb.Append("</nav>");

            sb.Append("<button class=\"nav-toggle\" id=\"navToggle\" aria-label=\"目录\">☰</button>");
            sb.Append("<div class=\"backdrop\" id=\"backdrop\"></div>");

            // ---------- 主内容 ----------
            sb.Append("<main class=\"content\" id=\"top\">");

            // Hero
            sb.Append("<header class=\"hero\">");
            sb.Append("<div class=\"hero-kicker\">SYSTEM INTELLIGENCE REPORT</div>");
            sb.Append("<h1>🖥️ 电脑信息报告</h1>");
            sb.Append("<p class=\"hero-desc\">本机硬件 · 系统 · 软件 · 使用痕迹 · 网络 全景快照</p>");
            sb.Append("<div class=\"meta-chips\">");
            sb.Append("<span class=\"chip\">💻 计算机 <b>").Append(H(m.ComputerName)).Append("</b></span>");
            sb.Append("<span class=\"chip\">🕒 生成于 <b>").Append(m.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss")).Append("</b></span>");
            sb.Append("<span class=\"chip\">⏱ 采集耗时 <b>").Append(m.DurationSeconds).Append(" 秒</b></span>");
            sb.Append("<span class=\"chip").Append(m.IsAdmin ? " chip-ok" : " chip-warn").Append("\">权限 <b>")
              .Append(m.IsAdmin ? "管理员" : "普通用户（部分受限）").Append("</b></span>");
            sb.Append("<span class=\"chip").Append(m.Masked ? " chip-ok" : " chip-warn").Append("\">脱敏 <b>")
              .Append(m.Masked ? "已开启 🔒" : "已关闭 ⚠️").Append("</b></span>");
            sb.Append("</div></header>");

            // 看板卡片（按 Order 排序）
            if (m.Cards.Count > 0)
            {
                var sorted = new System.Collections.Generic.List<CardData>(m.Cards);
                sorted.Sort((a, b) => a.Order.CompareTo(b.Order));
                sb.Append("<section class=\"cards\">");
                foreach (var c in sorted)
                {
                    sb.Append("<div class=\"card").Append(StatusClass(c.Status)).Append("\">");
                    sb.Append("<div class=\"card-title\">").Append(H(c.Icon + " " + c.Title));
                    if (c.Status.Length > 0)
                        sb.Append("<span class=\"badge ").Append(BadgeClass(c.Status)).Append("\">")
                          .Append(BadgeText(c.Status)).Append("</span>");
                    sb.Append("</div>");
                    foreach (var line in c.Lines)
                        sb.Append("<div class=\"card-line\">").Append(H(line)).Append("</div>");
                    sb.Append("</div>");
                }
                sb.Append("</section>");
            }

            // 分组章节
            bool firstGroup = true;
            foreach (var cat in ReportModel.CategoryOrder)
            {
                var list = m.Sections.FindAll(s => ReportModel.CategoryFor(s.Order) == cat);
                if (list.Count == 0) continue;
                if (!firstGroup) sb.Append("<div class=\"group-gap\"></div>");
                firstGroup = false;
                sb.Append("<div class=\"group-head\"><span class=\"group-title\">").Append(H(cat)).Append("</span>")
                  .Append("<span class=\"group-count\">").Append(list.Count).Append(" 个章节</span></div>");
                foreach (var s in list)
                    RenderSection(sb, s);
            }

            // 设计边界（未采集项）——集中折叠展示，替代各章节重复注释
            sb.Append("<details class=\"boundaries\"><summary><span class=\"sum-title\">📐 设计边界 · 未采集项说明</span>")
              .Append("<span class=\"sum-count\">").Append(ReportJson.Boundaries.Length).Append(" 项 · 点击展开</span></summary>");
            sb.Append("<div class=\"boundary-grid\">");
            foreach (var b in ReportJson.Boundaries)
                sb.Append("<span class=\"boundary-item\">").Append(H(b)).Append("</span>");
            sb.Append("</div></details>");

            // 采集失败项
            if (m.Failures.Count > 0)
            {
                sb.Append("<section class=\"fail-box\" id=\"failures\">");
                sb.Append("<h2>⚠️ 采集失败项<span class=\"badge b-error\">").Append(m.Failures.Count).Append(" 项</span></h2>");
                sb.Append("<ul class=\"fail-list\">");
                foreach (var f in m.Failures)
                    sb.Append("<li>").Append(H(f)).Append("</li>");
                sb.Append("</ul>");
                sb.Append("<p class=\"note\">以上项目未获取成功，不影响其余章节的完整性。以管理员身份重新运行通常可解决大部分权限类失败。</p>");
                sb.Append("</section>");
            }

            sb.Append("<footer class=\"report-footer\">由「").Append(H(AppVersion.DisplayName)).Append(" v")
              .Append(H(m.ToolVersion)).Append("」生成 · 自包含 HTML，可离线查看与转发")
              .Append(m.Masked ? " · 已脱敏" : "")
              .Append("</footer>");
            sb.Append("</main>");

            sb.Append("<a class=\"to-top\" id=\"toTop\" href=\"#top\" aria-label=\"回到顶部\">↑</a>");
            sb.Append("<script>").Append(Script).Append("</script>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static void RenderSection(StringBuilder sb, SectionData s)
        {
            sb.Append("<section class=\"section\" id=\"").Append(s.Id).Append("\">");
            sb.Append("<h2><span class=\"sec-icon\">").Append(H(s.Icon)).Append("</span>")
              .Append(H(s.Title));
            if (s.StatusText.Length > 0)
                sb.Append("<span class=\"badge ").Append(BadgeClass(s.Status)).Append("\">")
                  .Append(H(s.StatusText)).Append("</span>");
            sb.Append("</h2>");

            if (s.Facts.Count > 0)
            {
                sb.Append("<table class=\"facts\"><tbody>");
                foreach (var f in s.Facts)
                {
                    sb.Append("<tr><th>").Append(H(f.Item1)).Append("</th><td>");
                    if (f.Item3.Length > 0)
                        sb.Append("<span class=\"badge ").Append(BadgeClass(f.Item3)).Append("\">");
                    sb.Append(H(f.Item2));
                    if (f.Item3.Length > 0) sb.Append("</span>");
                    sb.Append("</td></tr>");
                }
                sb.Append("</tbody></table>");
            }

            foreach (var t in s.Tables)
            {
                if (t.Rows.Count == 0) continue;   // 空表不渲染
                if (t.Collapsed)
                {
                    sb.Append("<details class=\"tbl-details\"><summary><span class=\"sum-title\">")
                      .Append(H(t.Title ?? "明细"))
                      .Append("</span><span class=\"sum-count\">").Append(t.Rows.Count).Append(" 行 · 点击展开</span></summary>");
                    RenderTable(sb, t);
                    sb.Append("</details>");
                }
                else
                {
                    sb.Append("<h3 class=\"tbl-title\"><span>").Append(H(t.Title ?? "明细"))
                      .Append("</span><span class=\"tbl-count\">").Append(t.Rows.Count).Append(" 行</span></h3>");
                    RenderTable(sb, t);
                }
                if (t.Note != null)
                    sb.Append("<p class=\"note\">").Append(H(t.Note)).Append("</p>");
            }

            foreach (var n in s.Notes)
                sb.Append("<p class=\"note note-info\">ℹ️ ").Append(H(n)).Append("</p>");

            sb.Append("</section>");
        }

        private static void RenderTable(StringBuilder sb, TableData t)
        {
            sb.Append("<div class=\"tbl-wrap\"><table class=\"data\"><thead><tr>");
            foreach (var h in t.Headers)
                sb.Append("<th>").Append(H(h)).Append("</th>");
            sb.Append("</tr></thead><tbody>");
            foreach (var row in t.Rows)
            {
                sb.Append("<tr>");
                foreach (var cell in row)
                    sb.Append("<td>").Append(H(cell)).Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
        }

        private static string StatusClass(string status)
        {
            switch (status)
            {
                case "ok": return " card-ok";
                case "warn": return " card-warn";
                case "error": return " card-error";
                default: return "";
            }
        }

        private static string BadgeClass(string status)
        {
            switch (status)
            {
                case "ok": return "b-ok";
                case "warn": return "b-warn";
                case "error": return "b-error";
                default: return "b-none";
            }
        }

        private static string BadgeText(string status)
        {
            switch (status)
            {
                case "ok": return "正常";
                case "warn": return "需关注";
                case "error": return "异常";
                default: return "";
            }
        }

        private static string H(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        // ============================================================
        // 设计语言 CSS —— 全弹性版（流体网格 + clamp 令牌 + 三态侧栏 + 容器查询）
        // ============================================================
        private const string Css = @"
:root{
  --bg:#eef1f7; --surface:#ffffff;
  --ink:#1a2134; --ink-2:#5b6577; --ink-3:#98a1b3;
  --line:#e3e8f1; --line-2:#eef1f7;
  --accent:#3d5af1; --accent-ink:#2f49d8; --accent-soft:#eef1fe;
  --ok:#15803d; --ok-soft:#e7f6ec; --ok-line:#bfe6cd;
  --warn:#b45309; --warn-soft:#fdf3e2; --warn-line:#f2ddb3;
  --err:#b91c1c; --err-soft:#fdeaea; --err-line:#f2c3c3;
  --side-bg:#131a2e; --side-bg2:#0f1526; --side-ink:#b9c2d8; --side-dim:#7b86a3;
  --shadow-sm:0 1px 2px rgba(19,26,46,.05);
  --shadow-md:0 2px 6px rgba(19,26,46,.05),0 14px 32px rgba(19,26,46,.08);
  /* 流体字号层级 */
  --fs-0:clamp(10px,.7vw + 6px,12px);
  --fs-1:clamp(11.5px,.8vw + 7px,13px);
  --fs-2:clamp(12.5px,.95vw + 8px,15px);
  --fs-3:clamp(13.5px,1.05vw + 8px,16px);
  --fs-4:clamp(15px,1.2vw + 9px,19px);
  --fs-5:clamp(17px,1.5vw + 9px,22px);
  --fs-6:clamp(23px,2.6vw + 8px,36px);
  /* 流体间距 */
  --space-1:clamp(6px,.6vw,10px);
  --space-2:clamp(10px,1.1vw,16px);
  --space-3:clamp(14px,1.7vw,24px);
  --space-4:clamp(20px,2.6vw,38px);
  --r-sm:8px; --r-md:clamp(10px,1vw,14px); --r-lg:clamp(14px,1.6vw,22px);
}
*{margin:0;padding:0;box-sizing:border-box}
html{scroll-behavior:smooth}

/* 流体骨架：弹性侧栏 + 内容区 */
body{display:grid;grid-template-columns:clamp(200px,17vw,300px) minmax(0,1fr);
  min-height:100vh;font-family:'Segoe UI','Microsoft YaHei','PingFang SC','Noto Sans SC',system-ui,sans-serif;
  background:var(--bg);color:var(--ink);font-size:var(--fs-2);line-height:1.65;-webkit-font-smoothing:antialiased;
  /* 限制 body 重排范围 */
  contain:layout style}
b,strong{font-weight:650}
td,th{font-variant-numeric:tabular-nums}

/* ---------- 侧边导航 ---------- */
.sidebar{position:sticky;top:0;height:100vh;overflow-y:auto;grid-column:1;z-index:50;
  background:linear-gradient(180deg,var(--side-bg) 0%,var(--side-bg2) 100%);
  color:var(--side-ink);display:flex;flex-direction:column;
  /* GPU 加速 + 限制重排范围 */
  will-change:transform;transform:translateZ(0);contain:layout style paint;
  /* 优化滚动性能 */
  overscroll-behavior:contain}
/* 侧栏内部元素：滚动时不需要重排 */
.side-head{padding:var(--space-3) var(--space-3) var(--space-2);border-bottom:1px solid rgba(255,255,255,.07);contain:layout}
.side-brand{font-size:var(--fs-4);font-weight:700;color:#fff;letter-spacing:.5px}
.side-sub{margin-top:4px;font-size:var(--fs-0);color:var(--side-dim);letter-spacing:.5px}
.side-body{flex:1;padding:var(--space-1) 0 var(--space-2)}
.side-group{margin-top:6px}
.side-group-title{padding:var(--space-1) var(--space-3) 4px;font-size:var(--fs-0);
  font-weight:700;color:var(--side-dim);letter-spacing:2.5px}
.side-group ul{list-style:none}
.side-group li a{display:flex;align-items:center;gap:10px;padding:clamp(5px,.6vw,8px) var(--space-3);
  color:var(--side-ink);text-decoration:none;font-size:var(--fs-1);border-left:3px solid transparent;
  transition:background .1s ease .02s,color .1s ease .02s;
  /* 合成层优化 */
  will-change:auto}
.side-group li a .nav-ico{font-size:var(--fs-3);width:1.4em;text-align:center;flex-shrink:0}
.side-group li a:hover{background:rgba(255,255,255,.05);color:#fff}
.side-group li a.active{background:linear-gradient(90deg,rgba(61,90,241,.22),rgba(61,90,241,.05));
  color:#fff;border-left-color:var(--accent)}
.side-group .fail-link{color:#f3a7a7}
.side-foot{padding:12px var(--space-3);font-size:var(--fs-0);color:var(--side-dim);
  border-top:1px solid rgba(255,255,255,.07)}

/* ---------- 主内容（容器查询上下文） ---------- */
.content{grid-column:2;min-width:0;padding:var(--space-4);
  /* 容器查询保持，但添加 contain 限制重排范围 */
  container-type:inline-size;contain:layout style}
.hero{position:relative;border-radius:var(--r-lg);padding:clamp(22px,3vw,46px) clamp(18px,3vw,44px);
  margin-bottom:var(--space-3);color:#fff;overflow:hidden;
  background:linear-gradient(135deg,#1b2340 0%,#252e58 52%,#3d5af1 135%);box-shadow:var(--shadow-md);
  contain:layout paint}
.hero::before{content:'';position:absolute;inset:0;
  background-image:radial-gradient(rgba(255,255,255,.07) 1px,transparent 1px);background-size:22px 22px}
.hero>*{position:relative}
.hero-kicker{font-size:var(--fs-0);font-weight:700;letter-spacing:4px;color:#9db0ff;
  text-transform:uppercase;margin-bottom:var(--space-1)}
.hero h1{font-size:var(--fs-6);font-weight:720;letter-spacing:1px;margin-bottom:6px}
.hero-desc{font-size:var(--fs-2);color:#c3cdf0;margin-bottom:var(--space-2)}
.meta-chips{display:flex;flex-wrap:wrap;gap:var(--space-1)}
.chip{display:inline-flex;align-items:center;gap:6px;background:rgba(255,255,255,.09);
  border:1px solid rgba(255,255,255,.16);border-radius:999px;padding:clamp(3px,.4vw,6px) clamp(10px,1.1vw,16px);
  font-size:var(--fs-1);color:#dfe5f7}
.chip b{color:#fff;font-weight:650}
.chip-ok{border-color:rgba(74,222,128,.45);background:rgba(22,163,74,.18)}
.chip-warn{border-color:rgba(251,191,36,.5);background:rgba(217,119,6,.2)}

/* ---------- 看板卡片 ---------- */
.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(clamp(200px,17vw,330px),1fr));
  gap:var(--space-2);margin-bottom:var(--space-1)}
.card{background:var(--surface);border:1px solid var(--line);border-radius:var(--r-md);
  box-shadow:var(--shadow-sm);padding:var(--space-2) clamp(14px,1.6vw,22px);
  border-top:3px solid #9aa3b5;min-width:0;
  /* 关闭 hover 动画以提升拖拽性能，改用伪类渐变 */
  transition:box-shadow .12s ease,color .12s ease;
  contain:layout style paint}
.card:hover{box-shadow:var(--shadow-md)}
.card-ok{border-top-color:var(--ok)} .card-warn{border-top-color:var(--warn)} .card-error{border-top-color:var(--err)}
.card-title{display:flex;align-items:center;gap:8px;font-weight:700;font-size:var(--fs-3);margin-bottom:8px}
.card-title .badge{margin-left:auto}
.card-line{font-size:var(--fs-1);color:var(--ink-2);line-height:1.7;word-break:break-all}

/* ---------- 分组标题 ---------- */
.group-gap{height:var(--space-1)}
.group-head{display:flex;align-items:baseline;gap:12px;margin:var(--space-1) 6px var(--space-2)}
.group-title{font-size:var(--fs-1);font-weight:750;color:var(--ink-3);letter-spacing:3px}
.group-title::before{content:'';display:inline-block;width:14px;height:3px;border-radius:3px;
  background:var(--accent);margin-right:10px;vertical-align:1px}
.group-count{font-size:var(--fs-0);color:var(--ink-3)}

/* ---------- 章节卡片 ---------- */
.section{background:var(--surface);border:1px solid var(--line);border-radius:var(--r-lg);
  box-shadow:var(--shadow-sm);padding:var(--space-3);margin-bottom:var(--space-2);
  scroll-margin-top:18px;min-width:0;
  /* 关键优化：限制每个章节的重排/重绘范围 */
  contain:layout style;
  /* 延迟渲染不可见章节 */
  content-visibility:auto;
  /* 内容区高度估算（触发懒加载的阈值） */
  contain-intrinsic-size:auto 400px}
.section h2{display:flex;align-items:center;gap:10px;font-size:var(--fs-5);font-weight:720;
  padding-bottom:var(--space-1);border-bottom:1px solid var(--line-2);margin-bottom:var(--space-2)}
.section h2 .sec-icon{font-size:var(--fs-4);line-height:1}
.section h2 .badge{margin-left:auto}
.section h3{font-size:var(--fs-2);font-weight:650;color:var(--ink-2);margin:var(--space-2) 0 8px}

/* ---------- 概览事实 ---------- */
.facts{width:100%;border-collapse:separate;border-spacing:0;font-size:var(--fs-1);margin-bottom:4px}
.facts th{width:clamp(130px,16vw,250px);text-align:left;padding:var(--space-1) clamp(10px,1.1vw,16px);
  background:#f4f6fb;color:var(--ink-2);font-weight:600;border-bottom:1px solid var(--line);
  vertical-align:top;white-space:nowrap}
.facts td{padding:var(--space-1) clamp(10px,1.1vw,16px);border-bottom:1px solid var(--line-2);
  word-break:break-all;vertical-align:top}
.facts tbody tr:last-child th,.facts tbody tr:last-child td{border-bottom:none}
.facts td .badge{margin-left:0}

/* ---------- 数据表 ---------- */
.tbl-wrap{overflow-x:auto;border:1px solid var(--line);border-radius:var(--r-sm);
  margin:var(--space-1) 0 4px;max-width:100%}
.data{width:100%;border-collapse:collapse;font-size:var(--fs-1);min-width:0}
.data thead th{position:sticky;top:0;z-index:1;background:#f2f5fb;padding:var(--space-1) clamp(9px,1vw,14px);
  border-bottom:1px solid var(--line);text-align:left;font-weight:650;color:var(--ink-2);white-space:nowrap}
.data tbody td{padding:clamp(5px,.6vw,9px) clamp(9px,1vw,14px);border-bottom:1px solid var(--line-2);
  word-break:break-all;vertical-align:top}
.data tbody tr:nth-child(even){background:#fafbfe}
.data tbody tr:hover{background:var(--accent-soft)}
.data tbody tr:last-child td{border-bottom:none}
.tbl-title{display:flex;align-items:baseline;gap:10px;margin:var(--space-2) 2px 8px!important}
.tbl-title .tbl-count{font-size:var(--fs-0);color:var(--ink-3);font-weight:500}

/* ---------- 折叠表 ---------- */
.tbl-details{margin:var(--space-2) 0 4px;border:1px solid var(--line);border-radius:var(--r-sm);overflow:hidden}
.tbl-details summary{cursor:pointer;list-style:none;display:flex;align-items:center;justify-content:space-between;
  gap:10px;padding:var(--space-1) clamp(10px,1.2vw,16px);background:#f6f8fd;font-size:var(--fs-1);
  font-weight:650;color:var(--accent-ink);user-select:none}
.tbl-details summary::-webkit-details-marker{display:none}
.tbl-details summary .sum-count{font-size:var(--fs-0);color:var(--ink-3);font-weight:500}
.tbl-details summary:hover{background:var(--accent-soft)}
.tbl-details[open] summary{border-bottom:1px solid var(--line)}
.tbl-details .tbl-wrap{border:none;border-radius:0;margin:0}

/* ---------- 设计边界 ---------- */
.boundaries{margin:var(--space-2) 0 4px;border:1px dashed var(--line);border-radius:var(--r-sm);overflow:hidden;background:#fafbfe}
.boundaries summary{cursor:pointer;list-style:none;display:flex;align-items:center;justify-content:space-between;
  gap:10px;padding:var(--space-1) clamp(10px,1.2vw,16px);font-size:var(--fs-1);font-weight:650;
  color:var(--ink-2);user-select:none}
.boundaries summary::-webkit-details-marker{display:none}
.boundaries summary .sum-count{font-size:var(--fs-0);color:var(--ink-3);font-weight:500}
.boundaries summary:hover{background:var(--line-2)}
.boundaries[open] summary{border-bottom:1px dashed var(--line)}
.boundary-grid{display:flex;flex-wrap:wrap;gap:clamp(6px,.7vw,10px);padding:clamp(10px,1.1vw,16px)}
.boundary-item{display:inline-flex;align-items:center;padding:3px clamp(9px,1vw,14px);border-radius:999px;
  background:var(--line-2);border:1px solid var(--line);color:var(--ink-2);font-size:var(--fs-0);line-height:1.6}

/* ---------- 徽章 ---------- */
.badge{display:inline-flex;align-items:center;padding:2px clamp(8px,1vw,12px);border-radius:999px;
  font-size:var(--fs-0);font-weight:650;line-height:1.7;white-space:nowrap}
.b-ok{background:var(--ok-soft);color:var(--ok);border:1px solid var(--ok-line)}
.b-warn{background:var(--warn-soft);color:var(--warn);border:1px solid var(--warn-line)}
.b-error{background:var(--err-soft);color:var(--err);border:1px solid var(--err-line)}
.b-none{background:#eef1f6;color:var(--ink-2);border:1px solid var(--line)}

/* ---------- 注释与失败 ---------- */
.note{font-size:var(--fs-1);color:var(--ink-2);margin-top:10px;padding:var(--space-1) clamp(10px,1.2vw,16px);
  line-height:1.7;background:#f8fafc;border-left:3px solid var(--ink-3);
  border-radius:0 var(--r-sm) var(--r-sm) 0;max-width:72ch}
.note-info{border-left-color:var(--accent);background:var(--accent-soft)}
.fail-box{background:var(--err-soft);border:1px solid var(--err-line);border-radius:var(--r-lg);
  padding:var(--space-3);margin-bottom:var(--space-2);scroll-margin-top:18px;contain:layout style}
.fail-box h2{display:flex;align-items:center;gap:10px;font-size:var(--fs-5);font-weight:720;margin-bottom:8px}
.fail-box h2 .badge{margin-left:auto}
.fail-list{margin:10px 0 4px 20px;font-size:var(--fs-1);color:#7f1d1d;line-height:1.9}
.fail-box .note{border-left-color:var(--err);background:rgba(255,255,255,.55)}

/* ---------- 页脚 / 工具按钮 ---------- */
.report-footer{text-align:center;color:var(--ink-3);font-size:var(--fs-0);padding:var(--space-3) 0 8px;letter-spacing:.5px}
.to-top{position:fixed;right:var(--space-2);bottom:var(--space-2);width:42px;height:42px;
  border-radius:50%;background:var(--side-bg);color:#fff;font-size:18px;text-decoration:none;display:flex;
  align-items:center;justify-content:center;box-shadow:var(--shadow-md);opacity:0;
  pointer-events:none;transition:opacity .15s ease,transform .15s ease;z-index:30;
  will-change:opacity;transform:translateZ(0)}
.to-top.show{opacity:1;pointer-events:auto}
.to-top:hover{transform:translateY(-3px);background:var(--accent)}

.nav-toggle,.backdrop{display:none}

/* ---------- 视口自适应：三态侧栏 ---------- */
@media(max-width:1240px){
  body{grid-template-columns:62px minmax(0,1fr)}
  .side-head{padding:14px 0 10px;text-align:center;border-bottom:none}
  .side-brand{font-size:0}
  .side-brand::before{content:'🖥️';font-size:20px}
  .side-sub,.side-foot{display:none}
  .side-group-title{display:none}
  .side-group li a{flex-direction:column;gap:0;padding:9px 0;border-left:none;border-bottom:1px solid transparent}
  .side-group li a .nav-ico{font-size:19px;width:auto}
  .side-group li a .nav-text{display:none}
  .side-group li a.active{background:linear-gradient(180deg,rgba(61,90,241,.28),rgba(61,90,241,.08));
    border-left:none;border-bottom-color:var(--accent)}
}
@media(max-width:760px){
  body{grid-template-columns:minmax(0,1fr)}
  .content{grid-column:1}
  .sidebar{position:fixed;left:0;top:0;bottom:0;width:min(78vw,300px);transform:translateX(-100%);
    transition:transform .2s cubic-bezier(.4,0,.2,1);box-shadow:0 0 60px rgba(0,0,0,.35)}
  .sidebar.open{transform:translateX(0)}
  .side-head{padding:20px 22px 14px;text-align:left;border-bottom:1px solid rgba(255,255,255,.07)}
  .side-brand{font-size:16px}
  .side-brand::before{content:''}
  .side-sub{display:block}
  .side-foot{display:block}
  .side-group-title{display:block;padding:10px 22px 4px}
  .side-group li a{flex-direction:row;gap:8px;padding:7px 22px;border-left:3px solid transparent}
  .side-group li a .nav-text{display:inline}
  .nav-toggle{display:flex;position:fixed;top:12px;left:12px;z-index:60;width:40px;height:40px;
    align-items:center;justify-content:center;border:none;border-radius:10px;background:var(--side-bg);
    color:#fff;font-size:17px;cursor:pointer;box-shadow:var(--shadow-md)}
  .backdrop{display:block;position:fixed;inset:0;background:rgba(10,14,28,.45);z-index:45;
    opacity:0;pointer-events:none;transition:opacity .2s ease}
  .backdrop.show{opacity:1;pointer-events:auto}
}

/* ---------- 容器查询增强 ---------- */
@container (max-width:520px){
  .cards{grid-template-columns:1fr}
  .hero h1{font-size:22px}
  .facts th{width:40%;white-space:normal}
  .chip{font-size:11.5px}
}

@media(min-width:1560px){
  .cards{grid-template-columns:repeat(5,minmax(0,1fr))}
}

/* ---------- 打印 ---------- */
@media print{
  body{display:block;background:#fff}
  .sidebar,.nav-toggle,.backdrop,.to-top{display:none!important}
  .content{padding:0}
  .hero,.card,.section,.fail-box{box-shadow:none}
  .hero{background:#1b2340!important;-webkit-print-color-adjust:exact;print-color-adjust:exact}
  .tbl-details summary{display:none}
  .tbl-details .tbl-wrap{display:block;border:none}
  .section{break-inside:avoid-page;content-visibility:visible}
  *{-webkit-print-color-adjust:exact;print-color-adjust:exact}
}
";

        // ============================================================
        // 交互脚本：滚动高亮 / 平滑锚点 / 移动端抽屉 / 回到顶部
        // ============================================================
        private const string Script = @"
(function(){
  /* ---- 滚动高亮：rAF 节流 + 缓存 offsetTop ---- */
  var links=[].slice.call(document.querySelectorAll('.side-body a[data-spy]'));
  var map=[];
  links.forEach(function(a){
    var el=document.querySelector(a.getAttribute('href'));
    if(el)map.push({a:a,el:el,top:0});
  });
  /* 缓存所有目标的 offsetTop，避免每次滚动都触发 layout */
  function cacheTops(){for(var i=0;i<map.length;i++)map[i].top=map[i].el.offsetTop-120;}
  cacheTops();
  /* resize 时重新缓存 */
  var resizeTimer;
  window.addEventListener('resize',function(){
    clearTimeout(resizeTimer);
    resizeTimer=setTimeout(cacheTops,200);
  },{passive:true});

  var toTop=document.getElementById('toTop');
  var ticking=false;
  function onScroll(){
    if(!ticking){
      requestAnimationFrame(function(){
        var pos=window.scrollY+100,cur=null;
        for(var i=0;i<map.length;i++){if(map[i].top<=pos)cur=map[i];}
        for(var i=0;i<map.length;i++){
          var active=map[i]===cur;
          if(map[i].a.classList.contains('active')!==active){
            map[i].a.classList.toggle('active',active);
          }
        }
        if(toTop){
          var show=window.scrollY>600;
          if(toTop.classList.contains('show')!==show)toTop.classList.toggle('show',show);
        }
        ticking=false;
      });
      ticking=true;
    }
  }
  window.addEventListener('scroll',onScroll,{passive:true});
  onScroll();

  /* ---- 移动端抽屉导航 ---- */
  var side=document.getElementById('sidebar'),bd=document.getElementById('backdrop'),
      tg=document.getElementById('navToggle');
  function close(){if(side)side.classList.remove('open');if(bd)bd.classList.remove('show');}
  if(tg)tg.addEventListener('click',function(){
    side.classList.toggle('open');bd.classList.toggle('show');
  });
  if(bd)bd.addEventListener('click',close);
  links.forEach(function(a){a.addEventListener('click',function(){
    close();var el=document.querySelector(a.getAttribute('href'));
    if(el){el.scrollIntoView({behavior:'smooth',block:'start'});}
  });});
})();
";
    }
}
