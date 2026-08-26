using System;
using System.Collections.Generic;
using Microsoft.Win32;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class UsbCollector : ICollector
    {
        public string Name { get { return "USB 信息"; } }
        public int Order { get { return 100; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("usb", "USB 控制器与设备历史", Order, "🔌");

            // USB 控制器
            try
            {
                var ctrls = WmiHelper.Query("SELECT * FROM Win32_USBController");
                var t = s.NewTable("USB 控制器", false, "名称", "制造商", "规格", "状态");
                foreach (var c in ctrls)
                {
                    string name = WmiHelper.Str(c, "Name");
                    string spec = "USB 2.0";
                    if (name.IndexOf("xHCI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("eXtensible", StringComparison.OrdinalIgnoreCase) >= 0)
                        spec = "USB 3.x（xHCI）";
                    else if (name.IndexOf("EHCI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             name.IndexOf("Enhanced", StringComparison.OrdinalIgnoreCase) >= 0)
                        spec = "USB 2.0（EHCI）";
                    else if (name.IndexOf("USB4", StringComparison.OrdinalIgnoreCase) >= 0)
                        spec = "USB4";
                    t.Rows.Add(new List<string> {
                        name, WmiHelper.Str(c, "Manufacturer"), spec, WmiHelper.Str(c, "Status")
                    });
                }
                if (ctrls.Count == 0) { s.Tables.Remove(t); s.Notes.Add("未找到 USB 控制器信息。"); }
            }
            catch (Exception ex) { ctx.Fail(Name, "USB 控制器读取失败", ex); }

            // USB 存储设备历史
            try
            {
                const string usbstor = @"SYSTEM\CurrentControlSet\Enum\USBSTOR";
                var hist = s.NewTable("USB 存储设备历史（U盘/移动硬盘）", false,
                    "设备", "序列号", "首次安装时间", "最后连接时间");
                int count = 0;

                foreach (var devClass in RegistryHelper.SubKeyNames(Registry.LocalMachine, usbstor))
                {
                    string classPath = usbstor + "\\" + devClass;
                    foreach (var inst in RegistryHelper.SubKeyNames(Registry.LocalMachine, classPath))
                    {
                        string instPath = classPath + "\\" + inst;
                        string first = ReadPropTime(instPath, "0064");
                        string last = ReadPropTime(instPath, "0066");
                        if (last.Length == 0) last = ReadPropTime(instPath, "0065");

                        // 友好名称
                        string friendly = RegistryHelper.HKLM(instPath, "FriendlyName");
                        string devName = friendly.Length > 0 ? friendly : PrettifyUsbName(devClass);
                        string serial = inst;
                        int amp = serial.IndexOf('&');
                        if (amp > 0) serial = serial.Substring(0, amp);

                        hist.Rows.Add(new List<string> {
                            ctx.MaskText(devName),
                            ctx.MaskSerial(serial),
                            first.Length > 0 ? first : "—",
                            last.Length > 0 ? last : "—"
                        });
                        count++;
                    }
                }
                if (count == 0)
                {
                    s.Tables.Remove(hist);
                    s.Notes.Add("未读取到 USB 存储设备历史（可能需要管理员权限）。");
                }
            }
            catch (Exception ex) { ctx.Fail(Name, "USB 设备历史读取失败", ex); }

            s.Notes.Add("表中时间为注册表记录，首次安装时间在 Windows 8 及以上版本可用。");
            s.Status = "ok"; s.StatusText = "正常";
        }

        private static string PrettifyUsbName(string raw)
        {
            // 形如 Disk&Ven_三星&Prod_XXX&Rev_1.0 → 清理分隔符
            return raw.Replace("&Ven_", " ").Replace("&Prod_", " ").Replace("&Rev_", " Rev.")
                      .Replace("Disk&", "").Replace("CDROM&", "").Replace("&", " ").Replace("_", " ").Trim();
        }

        /// <summary>读取 Properties\{83da6326...} 下的 FILETIME 属性值</summary>
        private static string ReadPropTime(string instancePath, string valueName)
        {
            const string props = @"\Properties\{83da6326-97a6-4088-9453-a1923f573b29}";
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(instancePath + props))
                {
                    if (key == null) return "";
                    object v = key.GetValue(valueName);
                    var bytes = v as byte[];
                    if (bytes != null && bytes.Length >= 8)
                    {
                        long ft = BitConverter.ToInt64(bytes, 0);
                        if (ft <= 0) return "";
                        return DateTime.FromFileTime(ft).ToString("yyyy-MM-dd HH:mm");
                    }
                }
            }
            catch { }
            return "";
        }
    }
}
