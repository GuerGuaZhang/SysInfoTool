using System;
using System.Globalization;
using System.IO;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class OsCollector : ICollector
    {
        public string Name { get { return "操作系统信息"; } }
        public int Order { get { return 110; } }

        private const string CvKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("os", "操作系统", Order, "🪟");
            try
            {
                string productName = RegistryHelper.HKLM(CvKey, "ProductName");
                string editionId = RegistryHelper.HKLM(CvKey, "EditionID");
                int build = RegistryHelper.GetInt(Microsoft.Win32.Registry.LocalMachine, CvKey, "CurrentBuildNumber", 0);
                if (build <= 0) int.TryParse(RegistryHelper.HKLM(CvKey, "CurrentBuild"), out build);
                string ubr = RegistryHelper.HKLM(CvKey, "UBR", "0");
                string displayVer = RegistryHelper.HKLM(CvKey, "DisplayVersion");
                if (displayVer.Length == 0) displayVer = RegistryHelper.HKLM(CvKey, "ReleaseId");

                // Win11 的 ProductName 仍写 Windows 10，按 Build 修正
                if (build >= 22000 && productName.StartsWith("Windows 10"))
                    productName = "Windows 11" + productName.Substring("Windows 10".Length);

                var os = WmiHelper.First("SELECT * FROM Win32_OperatingSystem");

                s.Fact("版本", productName + (editionId.Length > 0 ? "（" + EditionName(editionId) + "）" : ""));
                s.Fact("版本号 / Build", (displayVer.Length > 0 ? displayVer + " · " : "") + "Build " + build + "." + ubr);
                s.Fact("系统架构", Environment.Is64BitOperatingSystem ? "x64（64 位）" : "32 位");
                if (os != null)
                {
                    s.Fact("安装日期", WmiHelper.Date(os, "InstallDate"));
                    s.Fact("系统目录", WmiHelper.Str(os, "SystemDirectory"));
                }

                // 计算机与域
                try
                {
                    var cs = WmiHelper.First("SELECT Name, Domain, PartOfDomain, Workgroup FROM Win32_ComputerSystem");
                    if (cs != null)
                    {
                        bool inDomain = WmiHelper.Bool(cs, "PartOfDomain");
                        s.Fact("计算机名", ctx.MaskText(WmiHelper.Str(cs, "Name")));
                        s.Fact(inDomain ? "域" : "工作组", ctx.MaskText(WmiHelper.Str(cs, inDomain ? "Domain" : "Workgroup")));
                    }
                }
                catch { }

                // 语言与区域
                s.Fact("系统语言", CultureInfo.InstalledUICulture.NativeName + "（" + CultureInfo.InstalledUICulture.Name + "）");
                s.Fact("区域设置", RegionInfo.CurrentRegion.DisplayName + "（" + RegionInfo.CurrentRegion.Name + "）");
                s.Fact("时区", TimeZoneInfo.Local.DisplayName);

                // 页面文件
                try
                {
                    var pfs = WmiHelper.Query("SELECT * FROM Win32_PageFileUsage");
                    if (pfs.Count > 0)
                    {
                        foreach (var pf in pfs)
                        {
                            uint allocated = WmiHelper.U32(pf, "AllocatedBaseSize");
                            uint current = WmiHelper.U32(pf, "CurrentUsage");
                            s.Fact("页面文件（" + WmiHelper.Str(pf, "Name") + "）",
                                allocated + " MB，当前使用 " + current + " MB");
                        }
                    }
                    else
                    {
                        var setting = WmiHelper.First("SELECT * FROM Win32_PageFileSetting");
                        s.Fact("页面文件", setting == null ? "由系统管理 / 未启用固定页面文件" : WmiHelper.Str(setting, "Name"));
                    }
                }
                catch { }

                // 快速启动 / 休眠
                int hiberboot = RegistryHelper.GetInt(Microsoft.Win32.Registry.LocalMachine,
                    @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", -1);
                if (hiberboot >= 0)
                    s.Fact("快速启动", hiberboot == 1 ? "已启用" : "已关闭", hiberboot == 1 ? "ok" : "");

                try
                {
                    string hiberPath = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "hiberfil.sys");
                    if (File.Exists(hiberPath))
                        s.Fact("休眠文件", FormatHelper.Bytes(new FileInfo(hiberPath).Length) + "（" + hiberPath + "）");
                    else
                        s.Fact("休眠文件", "未启用休眠");
                }
                catch { }

                // 环境变量（折叠表）
                try
                {
                    var envT = s.NewTable("系统环境变量", true, "变量", "值");
                    var envs = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
                    foreach (System.Collections.DictionaryEntry kv in envs)
                        envT.Rows.Add(new System.Collections.Generic.List<string> {
                            kv.Key.ToString(), ctx.MaskText(kv.Value == null ? "" : kv.Value.ToString())
                        });
                    var envU = s.NewTable("用户环境变量", true, "变量", "值");
                    var envsU = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);
                    foreach (System.Collections.DictionaryEntry kv in envsU)
                        envU.Rows.Add(new System.Collections.Generic.List<string> {
                            kv.Key.ToString(), ctx.MaskText(kv.Value == null ? "" : kv.Value.ToString())
                        });
                }
                catch { }

                s.Status = "ok"; s.StatusText = "正常";

                var card = ctx.Model.AddCard("系统", "🪟", 10, "ok");
                card.Lines.Add(productName);
                card.Lines.Add("Build " + build + "." + ubr);
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "采集失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private static string EditionName(string editionId)
        {
            switch (editionId)
            {
                case "Core": return "家庭版";
                case "CoreSingleLanguage": return "家庭单语言版";
                case "Professional": return "专业版";
                case "ProfessionalWorkstation": return "专业工作站版";
                case "Enterprise": return "企业版";
                case "EnterpriseS": return "企业版 LTSC";
                case "IoTEnterprise": case "IoTEnterpriseS": return "IoT 企业版 LTSC";
                case "Education": return "教育版";
                case "ServerStandard": return "Server 标准版";
                case "ServerDatacenter": return "Server 数据中心版";
                default: return editionId;
            }
        }
    }
}
