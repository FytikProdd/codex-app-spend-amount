namespace CodexSpendMonitor.Services;

public static class LocalLog
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexSpendMonitor");
    private static readonly string LogPath = Path.Combine(LogDirectory, "startup.log");
    private static readonly object Sync = new();

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(
                    LogPath,
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never block app startup.
        }
    }

    public static void Write(Exception ex, string context)
    {
        Write($"{context}: {ex}");
    }

    public static void Reset()
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                File.WriteAllText(LogPath, string.Empty);
            }
        }
        catch
        {
            // Logging must never block app startup.
        }
    }
}
