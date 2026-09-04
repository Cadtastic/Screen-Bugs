using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ScreenBugs.Tray;

namespace IconGen;

/// <summary>The MUI2 wizard images: the welcome/finish side panel and the inner page header.</summary>
public static class WizardBitmaps
{
    private static readonly Color Ant = Color.FromArgb(216, 50, 31);
    private static readonly Color Panel = Color.FromArgb(250, 250, 250);

    /// <summary>The welcome and finish panel: one large ant, centred, low on the panel.</summary>
    public static Bitmap Side(int width, int height) =>
        Compose(width, height, glyphSize: 132, x: (width - 132) / 2, y: height - 168);

    /// <summary>The inner page header strip: a small ant at the right, clear of the title.</summary>
    public static Bitmap Header(int width, int height) =>
        Compose(width, height, glyphSize: 44, x: width - 52, y: (height - 44) / 2);

    private static Bitmap Compose(int width, int height, int glyphSize, int x, int y)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var glyph = AntGlyph.Draw(glyphSize, Ant))
        {
            graphics.Clear(Panel);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(glyph, x, y);
        }

        return bitmap;
    }
}
