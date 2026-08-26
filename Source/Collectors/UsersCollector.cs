using System;
using System.Collections.Generic;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class UsersCollector : ICollector
    {
        public string Name { get { return "用户账户"; } }
        public int Order { get { return 210; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("users", "用户账户", Order, "👤");
            try
            {
                var accounts = WmiHelper.Query(
                    "SELECT Name, SID, AccountType, Disabled, Lockout, PasswordRequired FROM Win32_UserAccount WHERE LocalAccount=True");

                // 最后登录时间（Win32_NetworkLoginProfile）
                var lastLogons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var profiles = WmiHelper.Query("SELECT Name, LastLogon FROM Win32_NetworkLoginProfile");
                    foreach (var p in profiles)
                    {
                        string n = WmiHelper.Str(p, "Name");
                        int slash = n.LastIndexOf('\\');
                        if (slash >= 0) n = n.Substring(slash + 1);
                        lastLogons[n] = WmiHelper.Date(p, "LastLogon");
                    }
                }
                catch { }

                // 管理员组成员（先查，供账户类型标注使用）
                var adminNames = new List<string>();
                try
                {
                    var admins = WmiHelper.Query(
                        "SELECT * FROM Win32_GroupUser WHERE GroupComponent=\"Win32_Group.Domain='" +
                        Environment.MachineName + "',Name='Administrators'\"");
                    foreach (var a in admins)
                    {
                        string part = WmiHelper.Str(a, "PartComponent");
                        var m = System.Text.RegularExpressions.Regex.Match(part, "Name=\"([^\"]+)\"");
                        if (m.Success) adminNames.Add(m.Groups[1].Value);
                    }
                    if (adminNames.Count > 0)
                        s.Fact("本地管理员组成员", string.Join("、", adminNames.ConvertAll(n => ctx.MaskText(n))));
                }
                catch { }

                var t = s.NewTable("本地账户", false, "用户名", "SID", "类型", "状态", "最后登录");
                foreach (var a in accounts)
                {
                    string name = WmiHelper.Str(a, "Name");
                    bool disabled = WmiHelper.Bool(a, "Disabled");
                    // 按是否属于 Administrators 组标注，比 AccountType 数值更直观
                    bool isAdmin = adminNames.Contains(name);
                    string type = isAdmin ? "管理员" : "标准用户";
                    string lastLogon = lastLogons.ContainsKey(name) ? lastLogons[name] : "—";
                    if (lastLogon.Length == 0) lastLogon = "—";

                    t.Rows.Add(new List<string> {
                        ctx.MaskText(name),
                        ctx.MaskSerial(WmiHelper.Str(a, "SID")),
                        type,
                        disabled ? "已禁用" : "启用中",
                        lastLogon
                    });
                }

                s.Fact("当前会话用户", ctx.MaskText(Environment.UserDomainName + "\\" + Environment.UserName));
                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "账户信息读取失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }
    }
}
