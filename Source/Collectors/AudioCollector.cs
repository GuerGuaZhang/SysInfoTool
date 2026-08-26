using System;
using System.Collections.Generic;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class AudioCollector : ICollector
    {
        public string Name { get { return "声卡信息"; } }
        public int Order { get { return 80; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("audio", "音频设备（声卡）", Order, "🔊");
            try
            {
                var devs = WmiHelper.Query("SELECT * FROM Win32_SoundDevice");
                var t = s.NewTable("音频设备", false, "名称", "制造商", "驱动版本", "状态");
                foreach (var d in devs)
                {
                    string driverVer = "";
                    try
                    {
                        string pnpId = WmiHelper.Str(d, "PNPDeviceID");
                        if (pnpId.Length > 0)
                        {
                            var drv = WmiHelper.First("SELECT DriverVersion FROM Win32_PnPSignedDriver WHERE DeviceID='" +
                                pnpId.Replace("\\", "\\\\").Replace("'", "\\'") + "'");
                            if (drv != null) driverVer = WmiHelper.Str(drv, "DriverVersion");
                        }
                    }
                    catch { }
                    t.Rows.Add(new List<string> {
                        WmiHelper.Str(d, "Name"),
                        WmiHelper.Str(d, "Manufacturer"),
                        driverVer,
                        WmiHelper.Str(d, "Status")
                    });
                }
                if (devs.Count == 0) { s.Tables.Remove(t); s.Notes.Add("未找到音频设备。"); }
                s.Status = "ok"; s.StatusText = "正常";
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "采集失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }
    }
}
