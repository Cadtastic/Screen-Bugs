using System.Drawing;
using System.Drawing.Drawing2D;

namespace ScreenBugs.Tray;

/// <summary>
/// The ant the app identifies itself with, drawn at any size. One copy of the geometry, shared by
/// the tray icon, the window title bars and the installer's icon generator.
/// </summary>
public static class AntGlyph
{
    /// <summary>The coordinate space the geometry below is written in.</summary>
    public const int DesignSize = 32;

    public static Bitmap Draw(int size, Color color)
    {
        var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var pen = new Pen(color, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        using (var brush = new SolidBrush(color))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // The world transform scales the pen width along with the geometry, so the legs stay
            // proportional at every size.
            float scale = size / (float)DesignSize;
            graphics.ScaleTransform(scale, scale);

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

        return bitmap;
    }
}
