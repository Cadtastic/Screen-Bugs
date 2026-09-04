using System.IO;

namespace ScreenBugs.Diagnostics;

/// <summary>
/// Appends unhandled exceptions to %LocalAppData%\ScreenBugs\error.log.
/// The explicit <c>using System.IO</c> is required: the Windows Desktop SDK's implicit usings for a
/// WPF project do not include it, so <c>Path</c>, <c>Directory</c> and <c>File</c> would not resolve.
/// </summary>
public static class CrashLog
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenBugs",
        "error.log");

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, $"{DateTimeOffset.Now:O} {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Logging must not throw from inside the exception handler.
        }
    }
}
