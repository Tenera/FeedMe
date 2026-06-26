using System.IO;

namespace FeedMe.App;

/// <summary>Best-effort error logging to a file in the app data folder. Never throws.</summary>
internal static class CrashLog
{
    public static void Write(string message, Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FeedMe");
            Directory.CreateDirectory(directory);
            var line = $"[{DateTimeOffset.Now:O}] {message}: {exception}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(directory, "feedme.log"), line);
        }
        catch
        {
            // Logging must never take down the app.
        }
    }
}
