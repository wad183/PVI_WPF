using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PVI_WPF
{
    /// <summary>把结果写成 CSV。★ 关键：UTF-8 带 BOM，否则 Excel 打开中文表头是乱码</summary>
    internal static class CsvExporter
    {
        private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        /// <summary>字段里出现逗号/引号/换行时，用引号包起来（内部引号写两遍）</summary>
        private static string Esc(string? s)
        {
            s ??= "";
            return s.Contains(',') || s.Contains('"') || s.Contains('\n')
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;
        }

        /// <summary>图片级汇总（一行一张图）</summary>
        public static void ExportImages(string filePath, IEnumerable<ImageRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("检测时间,文件名,图片路径,总数,合格,NG,复检,耗时ms,配置");
            foreach (ImageRow r in rows)
            {
                sb.AppendLine(string.Join(",",
                    Esc(r.DetectedAt), Esc(r.FileName), Esc(r.ImagePath),
                    r.Total, r.OkCount, r.NgCount, r.ReviewCount, r.ElapsedMs, Esc(r.ProfileName)));
            }
            File.WriteAllText(filePath, sb.ToString(), Utf8Bom);
        }

        /// <summary>药片级明细（一行一颗）</summary>
        public static void ExportPills(string filePath, string imageName, IEnumerable<PillResult> pills)
        {
            var sb = new StringBuilder();
            sb.AppendLine("图片,序号,行,列,角度,长,宽,面积,饱和度,判定");
            foreach (PillResult p in pills)
            {
                sb.AppendLine(string.Join(",",
                    Esc(imageName), p.index,
                    p.Row.ToString("F1"), p.Col.ToString("F1"), p.AngleDeg.ToString("F1"),
                    p.Length.ToString("F0"), p.Width.ToString("F0"),
                    p.Area.ToString("F0"), p.MeanSaturation.ToString("F1"),
                    Esc(p.IsIncomplete ? "复检" : (p.IsOK ? "OK" : "NG"))));
            }
            File.WriteAllText(filePath, sb.ToString(), Utf8Bom);
        }
    }
}
