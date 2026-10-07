using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PVI_WPF
{
    internal enum LogLevelKind {Info,Warn,Error }
   internal class LogEntry
    {
        public DateTime Time { get; set; } = DateTime.Now;   // ★ 记在"创建这条日志"的那一刻
        public LogLevelKind level { get; set; } = LogLevelKind.Info;
        public string Message { get; set; } = "";
        public string Tag { get; set; } = "";
        public string TimeText => Time.ToString("HH:mm:ss");  // ★ 用创建时间，不是"现在"
        public string LevelText => level switch
        {
            LogLevelKind.Warn => "警告",
            LogLevelKind.Error => "错误",
            _ => "信息",
        };
    }
}
