using System.Drawing;
using Microsoft.Win32;

namespace ScreenBugs.Tray;

/// <summary>
/// Draws the ant glyph the app identifies itself with, so no icon asset is needed: black on a
/// light system theme, red on a dark one. Serves both the tray and window title bars, so the two
/// always match. The theme is read once, at the first call.
/// </summary>
public static class TrayIconFactory
{
    private static Icon? icon;
    private static System.Windows.Media.ImageSource? imageSource;

    /// <summary>The glyph as a WinForms icon, for the tray.</summary>
    public static Icon Create()
    {
        if (icon is not null)
        {
            return icon;
        }

        using var bitmap = Draw();

        // The handle from GetHicon lives for the process lifetime; it is deliberately not destroyed.
        icon = Icon.FromHandle(bitmap.GetHicon());
        return icon;
    }

    /// <summary>The same glyph as a WPF image, for window title bars and the taskbar.</summary>
    public static System.Windows.Media.ImageSource CreateImageSource()
    {
        if (imageSource is not null)
        {
            return imageSource;
        }

        var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            Create().Handle,
            System.Windows.Int32Rect.Empty,
            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        imageSource = source;
        return imageSource;
    }

    /// <summary>
    /// A black ant reads well on a light taskbar but vanishes on Windows 11's default dark one,
    /// so the dark theme gets a red ant instead.
    /// </summary>
    private static Bitmap Draw() =>
        AntGlyph.Draw(
            AntGlyph.DesignSize,
            TaskbarIsLight() ? Color.FromArgb(24, 24, 24) : Color.FromArgb(216, 50, 31));

    /// <summary>True when the taskbar uses the light theme. Windows 11 defaults to dark, so that is the fallback.</summary>
    private static bool TaskbarIsLight()
    {
        try
        {
            object? value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "SystemUsesLightTheme",
                null);
            return value is int light && light != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
