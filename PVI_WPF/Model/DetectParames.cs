using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PVI_WPF
{
    internal class DetectParams
    {
        // ↓↓↓ 这 4 个值是 2026-10-06 用 37 张图（D:\PictureForMvt\PillsPic）实测调出来的 ↓↓↓
        //     依据：碎块 3011~8932（71 个） / 正常药片 17.4万~22.2万 / 正常长宽比 ≥1.39 / 正常饱和度 ≥71.4
        public double NoiseMinArea { get; set; } = 20000;  // 去噪：小于它的碎块直接扔
        public bool FillHoles { get; set; } = true;   // 是否填孔
        public string Channel { get; set; } = "灰度"; //通道 灰度/饱和度
        public string ThresholdMethod { get; set; } = "Otsu 自动"; //otsu/固定阈值
        public string LightOrDark { get; set; } = "dark"; //目标颜色
        public int FixedThreshold { get; set; } = 100; //固定阈值

        public double MinArea { get; set; } = 120000;   // 判定：小于它 → NG
        public double MaxArea { get; set; } = 300000;   // 判定：大于它 → NG（粘连 2 颗约 36 万）

        public double MinAspect { get; set; } = 1.25; //长宽比：正常药片最小 1.39，留余量

        public double MinStaturation { get; set; } = 50; //饱和度：正常 ≥71，白药片 7~23

        public int BorderMargin { get; set; } = 2; //触边距离
    }
}
