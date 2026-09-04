namespace ScreenBugs.Core.Settings;

/// <summary>
/// Decides what a launch starts with: the user's own settings when they have any, otherwise the
/// installer's seed. Pure, so the whole rule is testable with no file system and no registry.
/// </summary>
public static class FirstRunSeed
{
    /// <param name="savedSettingsJson">The user's settings file content, or null when there is no file.</param>
    /// <param name="installDefaultsJson">The installer's seed content, or null when there is no file.</param>
    public static SeedOutcome Decide(string? savedSettingsJson, string? installDefaultsJson)
    {
        // A file that exists but is corrupt still counts as "not a first run". Otherwise the seed
        // would resurrect itself and re-apply a startup choice the user has since changed.
        if (savedSettingsJson is not null)
        {
            return new SeedOutcome(SettingsSerializer.Deserialize(savedSettingsJson), StartAtLogin: null);
        }

        if (installDefaultsJson is not null)
        {
            var defaults = InstallDefaults.Parse(installDefaultsJson);
            return new SeedOutcome(defaults.Options, defaults.StartAtLogin);
        }

        // No installer: running from a build output folder behaves as it always has.
        return new SeedOutcome(BugOptions.Default, StartAtLogin: null);
    }
}
