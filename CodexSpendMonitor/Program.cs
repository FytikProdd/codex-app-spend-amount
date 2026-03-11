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
            ShowStartupError(ex);
            throw;
        }
    }

    [DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private static void ShowStartupError(Exception ex)
    {
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexSpendMonitor",
            "startup.log");

        string message =
            "Codex Spend Popout could not start." + Environment.NewLine + Environment.NewLine +
            ex.Message + Environment.NewLine + Environment.NewLine +
            $"Log: {logPath}";

        try
        {
            MessageBox(IntPtr.Zero, message, "Codex Spend Popout", 0x00000010);
        }
        catch
        {
            // Best-effort only.
        }
    }
}
