using System;
using System.Threading.Tasks;
using System.Windows;
using WiFitool.Services;

namespace WiFitool
{
    public partial class App : Application
    {
        public App()
        {
            LogService.Instance.Initialize();
            DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                LogService.Instance.Error("App", "未处理的 UI 异常", e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                LogService.Instance.Error("App", "未处理的应用程序异常", e.ExceptionObject as Exception);
            };
            TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs e)
            {
                LogService.Instance.Error("App", "未观察到的异步任务异常", e.Exception);
                e.SetObserved();
            };
        }
    }
}
