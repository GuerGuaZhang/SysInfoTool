using System;
using System.Text;

namespace SysInfoTool.Helpers
{
    /// <summary>EDID 二进制解析：型号、制造商、生产日期、物理尺寸</summary>
    public class EdidInfo
    {
        public string ManufacturerId = "";   // 三字母厂商代码，如 AUO / BOE / SAM
        public string ProductCode = "";
        public string SerialNumber = "";
        public int ManufactureWeek;
        public int ManufactureYear;          // 实际年份
        public double WidthCm;
        public double HeightCm;
        public string MonitorName = "";      // 描述符里的显示器名称
        public string DiagonalInch
        {
            get
            {
                if (WidthCm <= 0 || HeightCm <= 0) return "";
                double inch = Math.Sqrt(WidthCm * WidthCm + HeightCm * HeightCm) / 2.54;
                return inch.ToString("F1") + " 英寸";
            }
        }
    }

    public static class EdidParser
    {
        public static EdidInfo Parse(byte[] edid)
        {
            if (edid == null || edid.Length < 128) return null;
            // 校验头
            byte[] header = { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
            for (int i = 0; i < 8; i++)
                if (edid[i] != header[i]) return null;

            var info = new EdidInfo();

            // 制造商 ID（8-9 字节，压缩 ASCII）
            int id = (edid[8] << 8) | edid[9];
            char c1 = (char)('A' + ((id >> 10) & 0x1F) - 1);
            char c2 = (char)('A' + ((id >> 5) & 0x1F) - 1);
            char c3 = (char)('A' + (id & 0x1F) - 1);
            if (char.IsLetter(c1) && char.IsLetter(c2) && char.IsLetter(c3))
                info.ManufacturerId = new string(new[] { c1, c2, c3 });

            // 产品代码（10-11 字节，小端）
            info.ProductCode = ((edid[11] << 8) | edid[10]).ToString("X4");

            // 序列号（12-15 字节，小端）
            uint serial = (uint)(edid[12] | (edid[13] << 8) | (edid[14] << 16) | (edid[15] << 24));
            if (serial != 0) info.SerialNumber = serial.ToString();

            // 生产日期（16=周, 17=年份-1990）
            info.ManufactureWeek = edid[16];
            info.ManufactureYear = edid[17] + 1990;

            // 物理尺寸（21=宽cm, 22=高cm）
            info.WidthCm = edid[21];
            info.HeightCm = edid[22];

            // 描述符（54-125 字节，每个 18 字节），找 0xFC（显示器名称）
            for (int i = 54; i <= 108; i += 18)
            {
                if (edid[i] == 0 && edid[i + 1] == 0 && edid[i + 2] == 0 && edid[i + 3] == 0xFC)
                {
                    var sb = new StringBuilder();
                    for (int j = i + 5; j < i + 18; j++)
                    {
                        byte b = edid[j];
                        if (b == 0x0A || b == 0x00) break;
                        sb.Append((char)b);
                    }
                    info.MonitorName = sb.ToString().Trim();
                    break;
                }
            }

            return info;
        }
    }
}
