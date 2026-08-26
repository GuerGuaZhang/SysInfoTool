using System;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class CpuCollector : ICollector
    {
        public string Name { get { return "CPU 信息"; } }
        public int Order { get { return 10; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("cpu", "CPU（处理器）", Order, "🧠");
            try
            {
                var cpu = WmiHelper.First("SELECT * FROM Win32_Processor");
                if (cpu == null) { s.Notes.Add("未能获取 CPU 信息。"); return; }

                string name = WmiHelper.Str(cpu, "Name");
                uint cores = WmiHelper.U32(cpu, "NumberOfCores");
                uint threads = WmiHelper.U32(cpu, "NumberOfLogicalProcessors");
                uint maxClock = WmiHelper.U32(cpu, "MaxClockSpeed");
                uint curClock = WmiHelper.U32(cpu, "CurrentClockSpeed");
                uint l2 = WmiHelper.U32(cpu, "L2CacheSize");   // KB
                uint l3 = WmiHelper.U32(cpu, "L3CacheSize");   // KB

                s.Fact("型号", name);
                s.Fact("制造商", WmiHelper.Str(cpu, "Manufacturer"));
                s.Fact("步进", WmiHelper.Str(cpu, "Stepping"));
                s.Fact("核心数 / 线程数", cores + " 核 / " + threads + " 线程");
                s.Fact("最大频率（基础/标称）", FormatHelper.Mhz(maxClock));
                s.Fact("当前频率（采样时刻）", FormatHelper.Mhz(curClock));
                if (l2 > 0) s.Fact("L2 缓存", FormatHelper.Bytes(l2 * 1024.0));
                if (l3 > 0) s.Fact("L3 缓存", FormatHelper.Bytes(l3 * 1024.0));
                s.Fact("插槽", WmiHelper.Str(cpu, "SocketDesignation"));
                s.Fact("位宽", WmiHelper.U32(cpu, "AddressWidth") + " 位");
                s.Fact("处理器 ID", ctx.MaskSerial(WmiHelper.Str(cpu, "ProcessorId")));

                // 虚拟化
                bool hvRunning = false;
                try
                {
                    var cs = WmiHelper.First("SELECT HypervisorPresent FROM Win32_ComputerSystem");
                    if (cs != null && cs.ContainsKey("HypervisorPresent") && cs["HypervisorPresent"] != null)
                    {
                        hvRunning = Convert.ToBoolean(cs["HypervisorPresent"]);
                        s.Fact("Hyper-V 虚拟机监控程序", hvRunning ? "已运行" : "未运行");
                    }
                }
                catch { }

                try
                {
                    object virt;
                    if (cpu.TryGetValue("VirtualizationFirmwareEnabled", out virt) && virt != null)
                    {
                        bool v = Convert.ToBoolean(virt);
                        // 有 Hypervisor 运行时 VT-x/AMD-V 由虚拟机监控程序接管，WMI 常报 False，属正常
                        s.Fact("固件虚拟化（VT-x/AMD-V）",
                            v ? "已启用" : (hvRunning ? "由 Hyper-V 接管（正常）" : "未启用"),
                            v || hvRunning ? "ok" : "warn");
                    }
                }
                catch { }

                s.Status = "ok"; s.StatusText = "正常";

                var card = ctx.Model.AddCard("CPU", "🧠", 20, "ok");
                card.Lines.Add(name);
                card.Lines.Add(cores + " 核 " + threads + " 线程 · " + FormatHelper.Mhz(maxClock));
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
