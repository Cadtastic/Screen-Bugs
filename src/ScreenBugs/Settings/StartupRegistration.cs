using Microsoft.Win32;
using ScreenBugs.Diagnostics;

namespace ScreenBugs.Settings;

/// <summary>Owns this app's value under the per-user Run key. Never throws.</summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenBugs";

    /// <summary>True when a value exists, whatever path it holds: that is what Windows will try to launch.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return false;
        }
    }

    /// <summary>Enabling always rewrites the current executable path, which repairs a stale entry.</summary>
    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
        }
    }

    /// <summary>
    /// Re-points an existing value at this executable, which is what keeps startup working after
    /// an install moves the app. Does nothing when startup is off, so it can never turn it on.
    /// </summary>
    public static void Refresh()
    {
        try
        {
            if (Environment.ProcessPath is not { } current)
            {
                return;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not string existing)
            {
                return;
            }

            // SetEnabled writes the path quoted, so the quotes come off before comparing:
            // against the raw value this would never match and would rewrite on every launch.
            if (!string.Equals(existing.Trim('"'), current, StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(ValueName, $"\"{current}\"");
            }
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
        }
    }
}
