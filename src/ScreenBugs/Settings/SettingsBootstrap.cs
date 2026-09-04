using System.IO;
using ScreenBugs.Diagnostics;

namespace ScreenBugs.Settings;

/// <summary>
/// Loads the options a launch starts with, seeding them from the installer's defaults the first
/// time a user runs the app. Owns the file reads; <see cref="FirstRunSeed"/> owns the rule.
/// The explicit <c>using System.IO</c> is required, as in <see cref="CrashLog"/>.
/// </summary>
public static class SettingsBootstrap
{
    public static BugOptions Load()
    {
        string? saved = SettingsStore.TryRead();

        // A file that exists but could not be read is not a first run. Seeding over it would
        // overwrite the user's real settings and re-apply the seed's startup choice on what is
        // usually a transient failure, so run on defaults this once and leave the disk alone.
        if (saved is null && SettingsStore.Exists)
        {
            StartupRegistration.Refresh();
            return BugOptions.Default;
        }

        string? seed = ReadInstallDefaults();
        var outcome = FirstRunSeed.Decide(saved, seed);

        // Only save (and apply the seed's startup choice) when a seed was actually read: with no
        // seed there is nothing to remember, and writing defaults here would consume the user's
        // one first run, so a later install's seed would never be adopted.
        if (saved is null && seed is not null)
        {
            SettingsStore.Save(outcome.Options);
            if (outcome.StartAtLogin is { } startAtLogin)
            {
                StartupRegistration.SetEnabled(startAtLogin);
            }
        }

        // Every launch, not just the first: an install that moved the app leaves the Run value
        // pointing at the old path, and only the app is in a position to repair it.
        StartupRegistration.Refresh();

        return outcome.Options;
    }

    /// <summary>The installer's seed, from beside the executable, or null when running unpackaged.</summary>
    private static string? ReadInstallDefaults()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, InstallDefaults.FileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return null;
        }
    }
}
