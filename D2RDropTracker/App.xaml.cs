using System.Threading;
using System.Windows;
using D2RDropTracker.Data;

namespace D2RDropTracker;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            LogService.Write("UI 未处理异常", args.Exception);
            System.Windows.MessageBox.Show(
                $"程序发生异常，日志已写入：\n{LogService.CurrentLogPath}",
                "D2R 掉落统计器",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                LogService.Write("后台未处理异常", exception);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogService.Write("异步任务异常", args.Exception);
            args.SetObserved();
        };

        _singleInstanceMutex = new Mutex(true, "D2RDropTracker.SingleInstance", out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("D2R 掉落统计器已经在运行，请检查任务栏托盘。",
                "程序已运行", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
