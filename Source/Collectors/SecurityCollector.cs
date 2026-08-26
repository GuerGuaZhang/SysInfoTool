using System;
using System.Collections.Generic;
using Microsoft.Win32;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class SecurityCollector : ICollector
    {
        public string Name { get { return "安全状态"; } }
        public int Order { get { return 130; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("security", "安全状态", Order, "🛡️");
            string worst = "ok";

            // 启动模式与安全启动
            try
            {
                int fw = RegistryHelper.GetInt(Registry.LocalMachine,
                    @"SYSTEM\CurrentControlSet\Control", "PEFirmwareType", 0);
                // PEFirmwareType: 1=BIOS(Legacy), 2=UEFI
                // 部分机器（含本工具实测的 Win11 台式机）缺省该值，做多级回退：
                //   SecureBoot\State 键存在 → UEFI；WMI FirmwareType → 2 = UEFI
                if (fw == 0)
                {
                    try
                    {
                        var cs = WmiHelper.First("SELECT FirmwareType FROM Win32_ComputerSystem");
                        if (cs != null)
                        {
                            uint ft = WmiHelper.U32(cs, "FirmwareType");
                            if (ft == 1) fw = 1;
                            else if (ft == 2) fw = 2;
                        }
                    }
                    catch { }
                }
                if (fw == 0 && RegistryHelper.KeyExists(Registry.LocalMachine,
                    @"SYSTEM\CurrentControlSet\Control\SecureBoot\State"))
                    fw = 2;

                if (fw == 2)
                {
                    s.Fact("启动模式", "UEFI", "ok");
                    int sb = RegistryHelper.GetInt(Registry.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled", -1);
                    if (sb == 1) { s.Fact("安全启动（Secure Boot）", "已启用", "ok"); }
                    else if (sb == 0) { s.Fact("安全启动（Secure Boot）", "未启用", "warn"); worst = "warn"; }
                    else s.Fact("安全启动（Secure Boot）", "无法确定");
                }
                else if (fw == 1)
                {
                    s.Fact("启动模式", "Legacy BIOS（传统）", "warn");
                    s.Fact("安全启动（Secure Boot）", "不可用（Legacy 模式不支持）", "warn");
                    worst = "warn";
                }
                else
                {
                    s.Fact("启动模式", "无法确定");
                }
            }
            catch { s.Fact("启动模式", "读取失败"); }

            // 杀毒软件
            try
            {
                var avs = WmiHelper.Query(@"root\SecurityCenter2", "SELECT * FROM AntiVirusProduct");
                if (avs.Count == 0)
                {
                    s.Fact("杀毒软件", "未检测到", "warn");
                    worst = "warn";
                }
                else
                {
                    var t = s.NewTable("安全软件", false, "名称", "实时防护", "病毒库");
                    foreach (var av in avs)
                    {
                        uint state = WmiHelper.U32(av, "ProductState");
                        // productState 高位字节解析（社区通用规则）
                        string provider = ((state >> 16) & 0xFF) == 0x10 ? "Windows Defender" : "第三方";
                        bool rtp = ((state >> 4) & 0xFF) == 0x10 || ((state >> 4) & 0xFF) == 0x11;
                        bool upToDate = (state & 0xFF) == 0x00;
                        t.Rows.Add(new List<string> {
                            WmiHelper.Str(av, "displayName"),
                            rtp ? "已开启" : "已关闭",
                            upToDate ? "最新" : "可能过期"
                        });
                        if (!rtp) worst = "warn";
                    }
                    t.Note = "状态由 Windows 安全中心接口报告，详细规则请以安全软件自身界面为准。";
                }
            }
            catch
            {
                s.Fact("杀毒软件", "无法读取 SecurityCenter（Server 系统或无权限）");
            }

            // 防火墙
            try
            {
                string fw = ProcessRunner.Run("netsh", "advfirewall show allprofiles state");
                if (!string.IsNullOrEmpty(fw))
                {
                    // 中英文系统均可：State ON / 状态 启用 等
                    int onCount = System.Text.RegularExpressions.Regex.Matches(fw,
                        @"(?:State|状态|配置文件状态)\s*[:：]?\s*(?:ON|On|打开|启用)").Count;
                    int offCount = System.Text.RegularExpressions.Regex.Matches(fw,
                        @"(?:State|状态|配置文件状态)\s*[:：]?\s*(?:OFF|Off|关闭|停用)").Count;
                    s.Fact("Windows 防火墙", "启用配置档 " + onCount + " 个 / 关闭 " + offCount + " 个",
                        offCount > 0 ? "warn" : "ok");
                    if (offCount > 0) worst = "warn";
                }
            }
            catch { }

            // 卷影副本
            try
            {
                var shadows = WmiHelper.Query("SELECT InstallDate, DeviceObject, VolumeName FROM Win32_ShadowCopy");
                s.Fact("卷影副本（VSS）", shadows.Count + " 个");
                if (shadows.Count > 0)
                {
                    var st = s.NewTable("卷影副本列表", true, "创建时间", "卷", "设备对象");
                    foreach (var sh in shadows)
                        st.Rows.Add(new List<string> {
                            WmiHelper.Date(sh, "InstallDate"),
                            WmiHelper.Str(sh, "VolumeName"),
                            ctx.MaskText(WmiHelper.Str(sh, "DeviceObject"))
                        });
                }
            }
            catch { s.Fact("卷影副本（VSS）", "无法读取（可能需要管理员权限）"); }

            // 系统还原点
            try
            {
                var rps = WmiHelper.Query(@"root\default", "SELECT Description, CreationTime FROM SystemRestore");
                s.Fact("系统还原点", rps.Count + " 个");
                if (rps.Count > 0)
                {
                    var rt = s.NewTable("还原点列表", true, "描述", "创建时间");
                    foreach (var r in rps)
                        rt.Rows.Add(new List<string> {
                            WmiHelper.Str(r, "Description"), WmiHelper.Date(r, "CreationTime")
                        });
                }
            }
            catch { s.Fact("系统还原点", "无法读取（可能需要管理员权限）"); }

            s.Status = worst;
            s.StatusText = worst == "ok" ? "正常" : "需关注";
        }
    }
}
