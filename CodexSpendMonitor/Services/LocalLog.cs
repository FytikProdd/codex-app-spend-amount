namespace CodexSpendMonitor.Services;

public static class LocalLog
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "startup.log");
    private static readonly object Sync = new();

    public static void Write(string message)
    {
        lock (Sync)
        {
            File.AppendAllText(
                LogPath,
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
    }

    public static void Write(Exception ex, string context)
    {
        Write($"{context}: {ex}");
    }

    public static void Reset()
    {
        lock (Sync)
        {
            File.WriteAllText(LogPath, string.Empty);
        }
    }
}
