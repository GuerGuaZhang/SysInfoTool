using System;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class MotherboardCollector : ICollector
    {
        public string Name { get { return "主板与 BIOS"; } }
        public int Order { get { return 20; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("motherboard", "主板与 BIOS", Order, "🔌");
            try
            {
                var bb = WmiHelper.First("SELECT * FROM Win32_BaseBoard");
                if (bb != null)
                {
                    s.Fact("主板制造商", WmiHelper.Str(bb, "Manufacturer"));
                    s.Fact("主板型号", WmiHelper.Str(bb, "Product"));
                    s.Fact("主板版本", WmiHelper.Str(bb, "Version"));
                    s.Fact("主板序列号", ctx.MaskSerial(WmiHelper.Str(bb, "SerialNumber")));
                }

                var bios = WmiHelper.First("SELECT * FROM Win32_BIOS");
                if (bios != null)
                {
                    s.Fact("BIOS 厂商", WmiHelper.Str(bios, "Manufacturer"));
                    s.Fact("BIOS 版本", WmiHelper.Str(bios, "SMBIOSBIOSVersion"));
                    s.Fact("BIOS 日期", WmiHelper.Date(bios, "ReleaseDate"));
                    string major = WmiHelper.Str(bios, "SMBIOSMajorVersion");
                    string minor = WmiHelper.Str(bios, "SMBIOSMinorVersion");
                    if (major.Length > 0) s.Fact("SMBIOS 版本", major + "." + minor);
                }

                // 内存插槽总数
                try
                {
                    var arrays = WmiHelper.Query("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");
                    uint slots = 0;
                    foreach (var a in arrays) slots += WmiHelper.U32(a, "MemoryDevices");
                    if (slots > 0) s.Fact("物理内存插槽总数", slots.ToString());
                }
                catch { }

                // TPM
                try
                {
                    var tpm = WmiHelper.First(@"root\cimv2\Security\MicrosoftTpm", "SELECT * FROM Win32_Tpm");
                    if (tpm != null)
                    {
                        string spec = WmiHelper.Str(tpm, "SpecVersion");
                        bool enabled = WmiHelper.Bool(tpm, "IsEnabled_InitialValue");
                        bool activated = WmiHelper.Bool(tpm, "IsActivated_InitialValue");
                        s.Fact("TPM 规范版本", spec.Contains("2.0") ? "2.0" : (spec.Length > 0 ? spec : "—"));
                        s.Fact("TPM 状态", enabled ? (activated ? "已启用并已激活" : "已启用") : "未启用",
                            enabled ? "ok" : "warn");
                    }
                    else
                    {
                        s.Fact("TPM", "未检测到 TPM 设备", "warn");
                    }
                }
                catch
                {
                    s.Fact("TPM", "无法读取（可能需要管理员权限）");
                }

                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "WMI 查询失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }
    }
}
