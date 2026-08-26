using System;
using System.Collections.Generic;
using Microsoft.Win32;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class MonitorCollector : ICollector
    {
        public string Name { get { return "显示器信息"; } }
        public int Order { get { return 60; } }

        private const string DisplayEnumKey = @"SYSTEM\CurrentControlSet\Enum\DISPLAY";

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("monitor", "显示器", Order, "🖥️");
            try
            {
                var t = s.NewTable("显示器明细（EDID）", false,
                    "名称", "面板制造商", "生产日期", "物理尺寸", "序列号");
                int found = 0;

                foreach (var mfg in RegistryHelper.SubKeyNames(Registry.LocalMachine, DisplayEnumKey))
                {
                    string mfgPath = DisplayEnumKey + "\\" + mfg;
                    foreach (var inst in RegistryHelper.SubKeyNames(Registry.LocalMachine, mfgPath))
                    {
                        byte[] edid = null;
                        try
                        {
                            using (var key = Registry.LocalMachine.OpenSubKey(mfgPath + "\\" + inst + "\\Device Parameters"))
                            {
                                if (key != null) edid = key.GetValue("EDID") as byte[];
                            }
                        }
                        catch { }
                        if (edid == null) continue;

                        var info = EdidParser.Parse(edid);
                        if (info == null) continue;
                        found++;

                        string friendly = RegistryHelper.HKLM(mfgPath + "\\" + inst, "FriendlyName");
                        string name = info.MonitorName.Length > 0 ? info.MonitorName
                            : (friendly.Length > 0 ? friendly : mfg + "\\" + inst);
                        string prodDate = info.ManufactureYear > 1990
                            ? info.ManufactureYear + " 年" + (info.ManufactureWeek > 0 ? " 第 " + info.ManufactureWeek + " 周" : "")
                            : "—";
                        string size = info.DiagonalInch.Length > 0
                            ? info.DiagonalInch + "（" + info.WidthCm.ToString("F0") + " × " + info.HeightCm.ToString("F0") + " cm）"
                            : "—";

                        t.Rows.Add(new List<string> {
                            ctx.MaskText(name),
                            info.ManufacturerId + "（代码）",
                            prodDate, size,
                            ctx.MaskSerial(info.SerialNumber)
                        });
                    }
                }

                if (found == 0)
                {
                    s.Tables.Remove(t);
                    s.Notes.Add("未在注册表中找到显示器 EDID 信息（可能需要管理员权限）。");
                }

                // 当前分辨率/刷新率取自显卡当前模式
                try
                {
                    var gpus = WmiHelper.Query("SELECT Name, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController");
                    foreach (var g in gpus)
                    {
                        uint w = WmiHelper.U32(g, "CurrentHorizontalResolution");
                        uint h = WmiHelper.U32(g, "CurrentVerticalResolution");
                        uint hz = WmiHelper.U32(g, "CurrentRefreshRate");
                        if (w > 0 && h > 0)
                            s.Fact("当前显示模式（" + WmiHelper.Str(g, "Name") + "）",
                                w + " × " + h + (hz > 0 ? " @ " + hz + " Hz" : ""));
                    }
                }
                catch { }

                s.Notes.Add("EDID 制造商为三字母代码（AUO=友达、BOE=京东方、SAM=三星、LGD=LG、CMN=群创）。");
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
