using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PVI_WPF
{
    internal class PillResult
    {
        public int index { get; set; }

        public double Row { get; set; } // 行
        public double Col { get; set; } // 列

        public double AngleDeg { get; set; } //(90~-90)
        public double Length { get; set; }
        public double Width { get; set; }

        public double Area { get; set; }

        public double MeanSaturation { get; set; }

        public bool IsOK { get; set; } 
        public bool IsIncomplete { get; set;}//图片内完整

        public string VerdictText=>IsIncomplete ? "复检" :(IsOK ? "ok" : "NG");
        public string SizeText => $"{Length:F0} x {Width:F0}";
    }
}
