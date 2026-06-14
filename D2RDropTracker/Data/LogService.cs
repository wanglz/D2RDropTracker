using System.IO;
using System.Text;

namespace D2RDropTracker.Data;

public static class LogService
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "D2RDropTracker",
        "Logs");

    public static string CurrentLogPath =>
        Path.Combine(LogDirectory, $"d2r-tracker-{DateTime.Today:yyyy-MM-dd}.log");

    public static void Write(string context, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var entry = new StringBuilder()
                .AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}")
                .AppendLine(exception.ToString())
                .AppendLine(new string('-', 80))
                .ToString();
            lock (SyncRoot)
            {
                File.AppendAllText(CurrentLogPath, entry, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }
}
