using System;
using System.Collections.Generic;
using SysInfoTool.Core;
using SysInfoTool.Helpers;

namespace SysInfoTool.Collectors
{
    public class ActivationCollector : ICollector
    {
        public string Name { get { return "激活状态"; } }
        public int Order { get { return 115; } }

        public void Collect(ReportContext ctx)
        {
            var s = ctx.Model.AddSection("activation", "Windows 激活状态", Order, "🔑");
            try
            {
                var products = WmiHelper.Query(
                    "SELECT Description, LicenseStatus, PartialProductKey, ProductKeyChannel, GracePeriodRemaining " +
                    "FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL");

                Dictionary<string, object> win = null;
                foreach (var p in products)
                {
                    string pdesc = WmiHelper.Str(p, "Description");
                    if (pdesc.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        win = p;
                        break;
                    }
                }
                if (win == null && products.Count > 0) win = products[0];

                if (win == null)
                {
                    s.Notes.Add("未能读取激活信息。");
                    return;
                }

                uint status = WmiHelper.U32(win, "LicenseStatus", 999);
                string statusText;
                string statusLevel;
                switch (status)
                {
                    case 0: statusText = "未激活"; statusLevel = "error"; break;
                    case 1: statusText = "已激活（永久/在有效期内）"; statusLevel = "ok"; break;
                    case 2: statusText = "宽限期（OOB Grace）"; statusLevel = "warn"; break;
                    case 3: statusText = "宽限期（OOT Grace）"; statusLevel = "warn"; break;
                    case 4: statusText = "非正版宽限期"; statusLevel = "error"; break;
                    case 5: statusText = "通知状态（未激活）"; statusLevel = "error"; break;
                    case 6: statusText = "延长宽限期"; statusLevel = "warn"; break;
                    default: statusText = "未知（" + status + "）"; statusLevel = "warn"; break;
                }

                string channel = WmiHelper.Str(win, "ProductKeyChannel");
                string desc = WmiHelper.Str(win, "Description");
                string channelCn = ChannelName(channel, desc);

                s.Fact("激活状态", statusText, statusLevel);
                s.Fact("激活渠道", channelCn);
                s.Fact("部分产品密钥", ctx.MaskSerial(WmiHelper.Str(win, "PartialProductKey")));
                if (status == 1)
                {
                    uint grace = WmiHelper.U32(win, "GracePeriodRemaining");
                    if (grace > 0 && channelCn.Contains("KMS"))
                        s.Fact("KMS 剩余有效期", (grace / 60 / 24) + " 天（到期后自动续期，需能连接 KMS 服务器）", "warn");
                }

                s.Status = statusLevel;
                s.StatusText = status == 1 ? "已激活" : "未激活";
                if (status != 1)
                    s.Notes.Add("如显示未激活但实际系统已激活，可能是读取了非 Windows 的许可条目，请以「设置 → 系统 → 激活」为准。");
            }
            catch (Exception ex)
            {
                ctx.Fail(Name, "许可信息读取失败（可能需要管理员权限）", ex);
                s.Notes.Add("采集失败：" + ex.Message);
                s.Status = "error"; s.StatusText = "失败";
            }
        }

        private static string ChannelName(string channel, string description)
        {
            string d = (description ?? "").ToUpperInvariant();
            if (d.Contains("VOLUME_KMSCLIENT")) return "KMS 批量激活（客户端）";
            if (d.Contains("VOLUME_KMS")) return "KMS 批量激活";
            if (d.Contains("VOLUME_MAK")) return "MAK 批量激活";
            if (d.Contains("OEM_DM") || d.Contains("OEM:DM")) return "OEM 数字权利（主板预装）";
            if (d.Contains("RETAIL")) return "零售版数字权利";
            if (!string.IsNullOrEmpty(channel))
            {
                if (channel.IndexOf("KMS", StringComparison.OrdinalIgnoreCase) >= 0) return "KMS 批量激活";
                if (channel.IndexOf("MAK", StringComparison.OrdinalIgnoreCase) >= 0) return "MAK 批量激活";
                if (channel.IndexOf("OEM", StringComparison.OrdinalIgnoreCase) >= 0) return "OEM 激活";
                if (channel.IndexOf("Retail", StringComparison.OrdinalIgnoreCase) >= 0) return "零售激活";
                return channel;
            }
            return "未知渠道";
        }
    }
}
