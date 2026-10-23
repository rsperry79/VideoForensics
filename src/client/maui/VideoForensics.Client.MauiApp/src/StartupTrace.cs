using System.Globalization;

namespace VideoForensics.Client.MauiApp;

/// <summary>
/// Appends a timestamped line per startup step to %ProgramData%\VideoForensics\Logs\maui-startup.txt, so a launch
/// that never shows a window leaves a record of the last step it reached. Writes never throw: a logging failure
/// must not be what stops the app from starting.
/// </summary>
internal static class StartupTrace
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "VideoForensics",
        "Logs",
        "maui-startup.txt");

    public static void Mark(string step)
    {
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(
                LogPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + step + Environment.NewLine);
        }
        catch
        {
            // Ignored on purpose: see the class summary.
        }
    }
}
