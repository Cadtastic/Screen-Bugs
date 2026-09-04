using System.IO;
using ScreenBugs.Diagnostics;

namespace ScreenBugs.Settings;

/// <summary>Loads and saves the options file beside the crash log. Never throws.</summary>
public static class SettingsStore
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenBugs",
        "settings.json");

    public static BugOptions Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? SettingsSerializer.Deserialize(File.ReadAllText(FilePath))
                : BugOptions.Default;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return BugOptions.Default;
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
