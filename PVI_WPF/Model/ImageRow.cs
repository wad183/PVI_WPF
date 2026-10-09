using System;

namespace PVI_WPF
{
    /// <summary>历史查询结果里的一行（对应数据库 ImageResult 的一行）</summary>
    internal class ImageRow
    {
        public long Id { get; set; }
        public string FileName { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public int Total { get; set; }
        public int OkCount { get; set; }
        public int NgCount { get; set; }
        public int ReviewCount { get; set; }
        public long ElapsedMs { get; set; }
        public string ProfileName { get; set; } = "";
        public string DetectedAt { get; set; } = "";
        public string TimeText => DetectedAt.Length >= 16 ? DetectedAt.Substring(5) : DetectedAt;
    }
}
