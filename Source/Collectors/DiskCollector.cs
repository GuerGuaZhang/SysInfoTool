using System;
using System.Collections.Generic;
using System.Linq;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class DiskCollector : ICollector
    {
        public string Name { get { return "硬盘信息"; } }
        public int Order { get { return 40; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("disk", "硬盘与分区", Order, "💽");
            try
            {
                bool anyWarn = false;
                var card = ctx.Model.AddCard("硬盘", "💽", 50);
                bool modernOk = false;

                // 首选：MSFT 存储 API（Win8+），可识别 SSD/HDD 与 NVMe/SATA
                try
                {
                    var disks = WmiHelper.Query(@"root\Microsoft\Windows\Storage", "SELECT * FROM MSFT_PhysicalDisk");
                    if (disks.Count > 0)
                    {
                        modernOk = true;
                        var t = s.NewTable("物理硬盘", false,
                            "型号", "容量", "类型", "接口协议", "健康状态", "序列号");
                        foreach (var d in disks)
                        {
                            string bus = BusTypeName(WmiHelper.U32(d, "BusType"));
                            string media = MediaTypeName(WmiHelper.U32(d, "MediaType"), WmiHelper.U32(d, "SpindleSpeed"));
                            string health = HealthName(WmiHelper.U32(d, "HealthStatus"));
                            if (health != "健康") anyWarn = true;
                            t.Rows.Add(new List<string> {
                                WmiHelper.Str(d, "FriendlyName"),
                                FormatHelper.Bytes(WmiHelper.U64(d, "Size")),
                                media, bus, health,
                                ctx.MaskSerial(WmiHelper.Str(d, "SerialNumber"))
                            });
                            card.Lines.Add(WmiHelper.Str(d, "FriendlyName") + "（" + media + "/" + bus + "，" + health + "）");
                        }

                        // 分区方案
                        try
                        {
                            var logicalDisks = WmiHelper.Query(@"root\Microsoft\Windows\Storage", "SELECT * FROM MSFT_Disk");
                            var styleT = s.NewTable("分区方案", false, "磁盘编号", "型号", "分区方案");
                            foreach (var d in logicalDisks)
                            {
                                string style = "未知";
                                switch (WmiHelper.U32(d, "PartitionStyle"))
                                {
                                    case 1: style = "MBR"; break;
                                    case 2: style = "GPT"; break;
                                    case 0: style = "RAW（未分区）"; break;
                                }
                                styleT.Rows.Add(new List<string> {
                                    "磁盘 " + WmiHelper.U32(d, "Number"),
                                    WmiHelper.Str(d, "FriendlyName"), style
                                });
                            }
                        }
                        catch { }
                    }
                }
                catch { modernOk = false; }

                // 回退：Win32_DiskDrive
                if (!modernOk)
                {
                    var disks = WmiHelper.Query("SELECT * FROM Win32_DiskDrive");
                    var t = s.NewTable("物理硬盘", false, "型号", "容量", "接口", "状态", "序列号");
                    foreach (var d in disks)
                    {
                        string status = WmiHelper.Str(d, "Status", "未知");
                        if (!status.Equals("OK", StringComparison.OrdinalIgnoreCase)) anyWarn = true;
                        t.Rows.Add(new List<string> {
                            WmiHelper.Str(d, "Model"),
                            FormatHelper.Bytes(WmiHelper.U64(d, "Size")),
                            WmiHelper.Str(d, "InterfaceType"),
                            status,
                            ctx.MaskSerial(WmiHelper.Str(d, "SerialNumber"))
                        });
                        card.Lines.Add(WmiHelper.Str(d, "Model") + "（" + status + "）");
                    }
                    s.Notes.Add("当前系统不支持现代存储接口，SSD/HDD 与协议识别不可用。");
                }

                // 分区与使用率
                try
                {
                    var vols = WmiHelper.Query("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3");
                    var vt = s.NewTable("分区与使用率", false,
                        "盘符", "卷标", "文件系统", "容量", "已用", "可用", "使用率");
                    foreach (var v in vols.OrderBy(v => WmiHelper.Str(v, "DeviceID")))
                    {
                        ulong size = WmiHelper.U64(v, "Size");
                        ulong freeSpace = WmiHelper.U64(v, "FreeSpace");
                        ulong used = size > freeSpace ? size - freeSpace : 0;
                        double pct = size > 0 ? used * 100.0 / size : 0;
                        string usage = size > 0 ? pct.ToString("F1") + "%" : "—";
                        vt.Rows.Add(new List<string> {
                            WmiHelper.Str(v, "DeviceID"),
                            WmiHelper.Str(v, "VolumeName"),
                            WmiHelper.Str(v, "FileSystem"),
                            FormatHelper.Bytes(size),
                            FormatHelper.Bytes(used),
                            FormatHelper.Bytes(freeSpace),
                            usage
                        });
                        if (size > 0 && pct >= 90) anyWarn = true;
                    }
                }
                catch (Exception ex) { ctx.Fail(Name, "分区信息读取失败", ex); }

                // BitLocker 状态（需要管理员）
                try
                {
                    var evs = WmiHelper.Query(@"root\CIMV2\Security\MicrosoftVolumeEncryption",
                        "SELECT * FROM Win32_EncryptableVolume");
                    if (evs.Count > 0)
                    {
                        var bt = s.NewTable("BitLocker 加密状态", false, "盘符", "加密状态");
                        foreach (var ev in evs)
                        {
                            string status;
                            switch (WmiHelper.U32(ev, "ProtectionStatus"))
                            {
                                case 0: status = "未加密 / 已关闭保护"; break;
                                case 1: status = "已加密并启用保护"; break;
                                default: status = "未知"; break;
                            }
                            bt.Rows.Add(new List<string> { WmiHelper.Str(ev, "DriveLetter"), status });
                        }
                    }
                }
                catch
                {
                    s.Notes.Add("BitLocker 状态需要管理员权限读取，当前未获取。");
                }

                s.Status = anyWarn ? "warn" : "ok";
                s.StatusText = anyWarn ? "需关注" : "正常";
                card.Status = s.Status;
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "采集失败", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private static string BusTypeName(uint bus)
        {
            switch (bus)
            {
                case 3: return "ATA";
                case 7: return "USB";
                case 10: return "SAS";
                case 11: return "SATA";
                case 12: return "SD";
                case 13: return "MMC";
                case 14: return "虚拟磁盘";
                case 15: return "文件后备虚拟盘";
                case 16: return "存储空间";
                case 17: return "NVMe";
                default: return "其他(" + bus + ")";
            }
        }

        private static string MediaTypeName(uint media, uint spindle)
        {
            switch (media)
            {
                case 3: return "HDD（机械）";
                case 4: return "SSD（固态）";
                case 5: return "SCM（傲腾类）";
                default:
                    if (spindle > 0 && spindle < 0xFFFFFFFE) return "HDD（机械）";
                    return "未识别";
            }
        }

        private static string HealthName(uint h)
        {
            switch (h)
            {
                case 0: return "健康";
                case 1: return "警告";
                case 2: return "不健康";
                default: return "未知";
            }
        }
    }
}
