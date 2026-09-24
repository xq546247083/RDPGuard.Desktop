using RDPGuard.Manager;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace RDPGuard
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 单实例防重复运行检测
            if (ExistsCurrentProcess())
            {
                Environment.Exit(0);
                return;
            }

            // 注册全局异常处理
            RegisterGlobalExceptionHandling();

            DbInitializer.Initialize();
            NotifyIconManager.Init();
            Lactor.ReLoad();
        }

        /// <summary>
        /// 注册全局异常处理
        /// </summary>
        private void RegisterGlobalExceptionHandling()
        {
            DispatcherUnhandledException += Application_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            Lactor.ShowToolTip($"异常: {e.Exception?.Message ?? string.Empty}");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            Lactor.ShowToolTip($"非UI异常: {exception?.Message ?? string.Empty}");
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            Lactor.ShowToolTip($"Task异常: {e.Exception?.Message ?? string.Empty}");
        }

        /// <summary>
        /// 是否存在同名进程正在运行
        /// </summary>
        private static bool ExistsCurrentProcess()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var processList = Process.GetProcessesByName(currentProcess.ProcessName);
                foreach (var item in processList)
                {
                    if (item.Id != currentProcess.Id)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }
    }
}
