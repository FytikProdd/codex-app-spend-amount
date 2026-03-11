using CodexSpendMonitor.Services;
using Microsoft.UI.Xaml;

namespace CodexSpendMonitor;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        LocalLog.Write("App ctor completed");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        LocalLog.Write("OnLaunched entered");
        _window = new MainWindow();
        LocalLog.Write("MainWindow created");
        _window.Activate();
        LocalLog.Write("Window activated");
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LocalLog.Write(e.Exception, "App.UnhandledException");
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LocalLog.Write(ex, "AppDomain.CurrentDomain.UnhandledException");
        }
        else
        {
            LocalLog.Write($"AppDomain.CurrentDomain.UnhandledException: {e.ExceptionObject}");
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LocalLog.Write(e.Exception, "TaskScheduler.UnobservedTaskException");
    }
}
