using System.Runtime.InteropServices;
using CodexSpendMonitor.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace CodexSpendMonitor;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            LocalLog.Reset();
            LocalLog.Write("Program.Main entered");
            XamlCheckProcessRequirements();
            LocalLog.Write("XamlCheckProcessRequirements passed");
            ComWrappersSupport.InitializeComWrappers();
            LocalLog.Write("ComWrappers initialized");

            Application.Start(callbackParams =>
            {
                LocalLog.Write("Application.Start callback entered");
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                LocalLog.Write("SynchronizationContext set");
                var app = new App();
                LocalLog.Write("App created");
            });
        }
        catch (Exception ex)
        {
            LocalLog.Write(ex, "Program.Main failed");
            throw;
        }
    }

    [DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();
}
