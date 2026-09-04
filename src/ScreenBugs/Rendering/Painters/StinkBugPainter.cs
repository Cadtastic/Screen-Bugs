using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class StinkBugPainter : IBugPainter
{
    private const double SpecimenBodyLength = 102.0;
    private const double LegAmplitudeDegrees = 7.0;

    private static readonly Color Olive = PainterPens.Hex("#6b8a3a");
    private static readonly Color Dark = PainterPens.Hex("#3f5a20");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.StinkBug).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush olive = PainterPens.Brush(Olive);
    private readonly SolidColorBrush dark = PainterPens.Brush(Dark);
    private readonly SolidColorBrush scutellum = PainterPens.Brush(PainterPens.Hex("#7d9a48"));
    private readonly SolidColorBrush eye = PainterPens.Brush(PainterPens.Hex("#1a1a1a"));
    private readonly Pen outline = PainterPens.Pen(Dark, 1.0, 1.0);
    private readonly Pen scutellumOutline = PainterPens.Pen(Dark, 0.8, 1.0);
    private readonly Pen legPen;
    private readonly Pen antennaPen;
    private readonly Pen bandPen;
    private readonly PathGeometry shield = Shapes.Figure(
        new(0, -48), closed: true,
        Shapes.Line(new(12, -34)), Shapes.Line(new(34, -20)), Shapes.Line(new(32, 8)),
        Shapes.Quad(new(22, 44), new(0, 54)), Shapes.Quad(new(-22, 44), new(-32, 8)),
        Shapes.Line(new(-34, -20)), Shapes.Line(new(-12, -34)));
    private readonly PathGeometry scutellumShape = Shapes.Polygon(new(-18, -18), new(18, -18), new(0, 22));
    private readonly PathGeometry leftAntenna = Shapes.Polyline(new(-4, -46), new(-16, -64), new(-22, -82));
    private readonly PathGeometry rightAntenna = Shapes.Polyline(new(4, -46), new(16, -64), new(22, -82));
    private readonly Point[] mottle =
    [
        new(-22, -8), new(24, -4), new(-20, 14), new(22, 18), new(-8, 36), new(10, 38), new(0, -30),
    ];

    public StinkBugPainter()
    {
        legPen = PainterPens.Pen(PainterPens.Hex("#4f6a2c"), 2.5, scale);
        antennaPen = PainterPens.Pen(Dark, 2.0, scale);
        bandPen = PainterPens.Pen(PainterPens.Hex("#c9b16a"), 2.4, scale);
    }

    public Color BodyColor => Olive;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 8), 36, 50);

        LegPainter.DrawLegPair(dc, legPen, new(-16, -22), new(-38, -40), new(-50, -26), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-28, -4), new(-56, -6), new(-62, 16), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-24, 18), new(-50, 34), new(-54, 58), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        DrawBandedAntenna(dc, leftAntenna, new Point(-4, -46), 1, bug.LegPhase, 0.0);
        DrawBandedAntenna(dc, rightAntenna, new Point(4, -46), -1, bug.LegPhase, Math.PI);
        dc.DrawGeometry(olive, outline, shield);
        dc.DrawLine(outline, new Point(-34, -20), new Point(34, -20));
        dc.DrawGeometry(scutellum, scutellumOutline, scutellumShape);
        foreach (var dot in mottle)
        {
            dc.DrawEllipse(dark, null, dot, 1.5, 1.5);
        }

        dc.DrawEllipse(eye, null, new Point(-7, -38), 2, 2);
        dc.DrawEllipse(eye, null, new Point(7, -38), 2, 2);
        dc.Pop();

        dc.Pop();
    }

    /// <summary>
    /// Antenna plus its two light bands, rotated together about the base so the bands travel with it.
    /// <paramref name="mirror"/> is 1 for the left antenna and -1 for the right.
    /// </summary>
    private void DrawBandedAntenna(DrawingContext dc, PathGeometry antenna, Point basePoint, int mirror, float legPhase, double side)
    {
        dc.PushTransform(new RotateTransform(BodyMotion.AntennaAngle(legPhase, side), basePoint.X, basePoint.Y));
        dc.DrawGeometry(null, antennaPen, antenna);
        dc.DrawLine(bandPen, new Point(-10 * mirror, -55), new Point(-14 * mirror, -61));
        dc.DrawLine(bandPen, new Point(-19 * mirror, -72), new Point(-21 * mirror, -78));
        dc.Pop();
    }
}
