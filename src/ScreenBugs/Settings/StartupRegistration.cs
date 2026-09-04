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
}
