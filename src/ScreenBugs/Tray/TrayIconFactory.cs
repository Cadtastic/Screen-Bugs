using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace ScreenBugs.Tray;

/// <summary>
/// Draws the tray glyph (an ant seen from above) so no icon asset is needed: black on a light
/// system theme, red on a dark one.
/// </summary>
public static class TrayIconFactory
{
    public static Icon Create()
    {
        // A black ant reads well on a light taskbar but vanishes on Windows 11's default dark one,
        // so the dark theme gets a red ant instead.
        Color glyph = TaskbarIsLight() ? Color.FromArgb(24, 24, 24) : Color.FromArgb(216, 50, 31);

        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var pen = new Pen(glyph, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        using (var brush = new SolidBrush(glyph))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            graphics.DrawLine(pen, 12, 11, 4, 6);
            graphics.DrawLine(pen, 20, 11, 28, 6);
            graphics.DrawLine(pen, 12, 15, 3, 16);
            graphics.DrawLine(pen, 20, 15, 29, 16);
            graphics.DrawLine(pen, 12, 19, 5, 26);
            graphics.DrawLine(pen, 20, 19, 27, 26);
            graphics.DrawLine(pen, 14, 5, 10, 1);
            graphics.DrawLine(pen, 18, 5, 22, 1);

            graphics.FillEllipse(brush, 11, 3, 10, 9);
            graphics.FillEllipse(brush, 12, 11, 8, 9);
            graphics.FillEllipse(brush, 10, 19, 12, 12);
        }

        // The handle from GetHicon lives for the process lifetime; it is deliberately not destroyed.
        return Icon.FromHandle(bitmap.GetHicon());
    }

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
