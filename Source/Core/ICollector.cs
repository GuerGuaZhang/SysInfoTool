using System.Collections.Generic;

namespace SysInfoTool.Core
{
    /// <summary>采集器接口：每个模块一个实现，只产出 SectionData/CardData，不关心输出格式</summary>
    public interface ICollector
    {
        string Name { get; }      // 用于进度显示与失败汇总
        int Order { get; }        // 报告章节排序
        void Collect(ReportContext ctx);
    }

    /// <summary>采集过程共享上下文</summary>
    public class ReportContext
    {
        public bool Mask = true;          // 默认脱敏
        public bool SkipScan = false;     // 跳过文件夹体积统计
        public bool IsAdmin;
        public Helpers.MaskHelper Masker; // 脱敏工具（由入口初始化）
        public ReportModel Model = new ReportModel();

        public string MaskText(string s) { return Masker == null ? s : Masker.Text(s); }
        public string MaskSerial(string s) { return Masker == null ? s : Masker.Serial(s); }

        public void Fail(string collectorName, string what, System.Exception ex)
        {
            string msg = collectorName + "：" + what + "（" + ex.GetType().Name + ": " + ex.Message + "）";
            lock (Model.Failures) Model.Failures.Add(msg);
        }
    }
}
