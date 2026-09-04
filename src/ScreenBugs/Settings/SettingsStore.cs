using System.IO;
using ScreenBugs.Diagnostics;

namespace ScreenBugs.Settings;

/// <summary>Reads and writes the options file beside the crash log. Never throws.</summary>
public static class SettingsStore
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenBugs",
        "settings.json");

    /// <summary>True when the file is there, whether or not it can be read.</summary>
    public static bool Exists => File.Exists(FilePath);

    /// <summary>The file's text, or null when there is no file or it cannot be read.</summary>
    public static string? TryRead()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return null;
        }
    }

    public static void Save(BugOptions options)
    {
        try
        {
            string directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);

            // Write beside the target and move over it, so a crash mid-write cannot truncate the file.
            string temporary = Path.Combine(directory, "settings.json.tmp");
            File.WriteAllText(temporary, SettingsSerializer.Serialize(options));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
        }
    }
}
