using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class SevenSpotLadybugPainter : IBugPainter
{
    private const double SpecimenBodyLength = 90.0;
    private const double LegAmplitudeDegrees = 8.0;

    private static readonly Color Red = PainterPens.Hex("#d8321f");
    private static readonly Color Black = PainterPens.Hex("#1a1a1a");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.SevenSpotLadybug).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush red = PainterPens.Brush(Red);
    private readonly SolidColorBrush black = PainterPens.Brush(Black);
    private readonly SolidColorBrush white = PainterPens.Brush(PainterPens.Hex("#f2f2f2"));
    private readonly Pen outline = PainterPens.Pen(PainterPens.Hex("#8e1b0f"), 1.0, 1.0);
    private readonly Pen seam = PainterPens.Pen(PainterPens.Hex("#8e1b0f"), 1.2, 1.0);
    private readonly Pen legPen;
    private readonly Pen antennaPen;
    private readonly PathGeometry leftAntenna = Shapes.Polyline(new(-4, -46), new(-10, -56));
    private readonly PathGeometry rightAntenna = Shapes.Polyline(new(4, -46), new(10, -56));
    private readonly (Point Center, double Radius)[] spots =
    [
        (new(0, -24), 4.5), (new(-14, -12), 5), (new(14, -12), 5),
        (new(-24, 10), 5), (new(24, 10), 5), (new(-10, 28), 5), (new(10, 28), 5),
    ];

    public SevenSpotLadybugPainter()
    {
        legPen = PainterPens.Pen(Black, 2.0, scale);
        antennaPen = PainterPens.Pen(Black, 1.2, scale);
    }

    public Color BodyColor => Red;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 10), 38, 38);

        LegPainter.DrawLegPair(dc, legPen, new(-14, -18), new(-30, -30), new(-36, -20), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-24, -4), new(-44, -8), new(-50, 4), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-20, 14), new(-38, 26), new(-40, 42), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-4, -46), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(4, -46), bug.LegPhase, Math.PI);
        dc.DrawEllipse(red, outline, new Point(0, 6), 36, 36);
        dc.DrawLine(seam, new Point(0, -28), new Point(0, 42));
        foreach (var (center, radius) in spots)
        {
            dc.DrawEllipse(black, null, center, radius, radius);
        }

        dc.DrawEllipse(black, null, new Point(0, -30), 22, 10);
        dc.DrawEllipse(white, null, new Point(-12, -31), 3, 3);
        dc.DrawEllipse(white, null, new Point(12, -31), 3, 3);
        dc.DrawEllipse(black, null, new Point(0, -42), 9, 6);
        dc.Pop();

        dc.Pop();
    }
}
