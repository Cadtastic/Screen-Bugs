using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Frozen brushes and pens for painters.</summary>
public static class PainterPens
{
    /// <summary>Alpha 1: invisible, yet non-transparent to Windows layered-window hit testing (spec 7.2).</summary>
    public static readonly SolidColorBrush HitDisc = Brush(Color.FromArgb(1, 0, 0, 0));

    /// <summary>Black at about 8 percent opacity for the shadow under each bug (spec 6).</summary>
    public static readonly SolidColorBrush Shadow = Brush(Color.FromArgb(20, 0, 0, 0));

    /// <summary>Parses an SVG-style "#rrggbb" color.</summary>
    public static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Round-capped pen. <paramref name="specimenWidth"/> is in specimen units and is raised so the
    /// line is never thinner than one DIP after <paramref name="scale"/> is applied.
    /// </summary>
    public static Pen Pen(Color color, double specimenWidth, double scale)
    {
        var pen = new Pen(Brush(color), Math.Max(specimenWidth, 1.0 / scale))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();
        return pen;
    }

    /// <summary>Moves a color toward black by <paramref name="fraction"/> (0 to 1).</summary>
    public static Color Darken(Color color, double fraction) => Color.FromRgb(
        (byte)(color.R * (1 - fraction)),
        (byte)(color.G * (1 - fraction)),
        (byte)(color.B * (1 - fraction)));
}
