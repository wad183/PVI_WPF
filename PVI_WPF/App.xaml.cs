using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;

namespace PVI_WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // HALCON 安装根目录；系统里设了 HALCONROOT 就优先用它
        internal const string DefaultHalconRoot = @"D:\Halcon23A\HALCONprogram\HALCON-24.11-Progress-Steady";

        internal static string HalconRoot
        {
            get
            {
                string? fromEnvironment = Environment.GetEnvironmentVariable("HALCONROOT");
                return string.IsNullOrWhiteSpace(fromEnvironment) ? DefaultHalconRoot : fromEnvironment;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 崩溃日志：任何未处理异常都写进 exe 旁边的 crash.log
            DispatcherUnhandledException += (s, args) => LogCrash(args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, args) => LogCrash(args.ExceptionObject as Exception);

            // NuGet 包里只有托管程序集，halcon.dll 这些原生库用本机装好的 HALCON。
            // 把 <HALCONROOT>\bin\x64-win64 加进 PATH，.NET 才找得到（许可证也在 HALCONROOT 下面找）。
            // 必须在第一次调用 HALCON 之前执行。
            string nativeDirectory = Path.Combine(HalconRoot, "bin", "x64-win64");
            Environment.SetEnvironmentVariable("HALCONROOT", HalconRoot);
            Environment.SetEnvironmentVariable("HALCONARCH", "x64-win64");
            Environment.SetEnvironmentVariable(
                "PATH",
                nativeDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
        }

        internal static void LogCrash(Exception? ex)
        {
            try
            {
                // 写在 exe 旁边（一定可写）；%AppData% 在某些环境会被安全策略挡住
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.AppendAllText(path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{new string('-', 60)}{Environment.NewLine}");
            }
            catch
            {
                // 记日志本身失败就算了，别再抛
            }
        }
    }

}
