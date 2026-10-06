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
        public LogLevelKind level { get; set; } = LogLevelKind.Info;
        public string Message { get; set; } = "";
        public string Tag { get; set; } = "";
        public string TimeText => DateTime.Now.ToString("MM:dd:HH:mm");
        public string LevelText => level switch
        {
            LogLevelKind.Warn => "警告",
            LogLevelKind.Error => "错误",
            _ => "信息",
        };
    }
}
