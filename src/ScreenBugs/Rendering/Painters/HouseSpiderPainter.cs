using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class HouseSpiderPainter : IBugPainter
{
    private const double SpecimenBodyLength = 66.0;
    private const double LegAmplitudeDegrees = 7.0;

    private static readonly Color Brown = PainterPens.Hex("#4a3b2f");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.HouseSpider).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush brown = PainterPens.Brush(Brown);
    private readonly SolidColorBrush abdomen = PainterPens.Brush(PainterPens.Hex("#5a4838"));
    private readonly SolidColorBrush black = PainterPens.Brush(Colors.Black);
    private readonly Pen outline = PainterPens.Pen(PainterPens.Hex("#33261b"), 1.0, 1.0);
    private readonly Pen legPen;
    private readonly Pen palpPen;
    private readonly Pen chevronPen;
    private readonly (Point Center, double Radius)[] eyes =
    [
        (new(-4, -24), 1.5), (new(0, -25), 1.8), (new(4, -24), 1.5), (new(-7, -21), 1.3), (new(7, -21), 1.3),
    ];

    public HouseSpiderPainter()
    {
        legPen = PainterPens.Pen(Brown, 2.5, scale);
        palpPen = PainterPens.Pen(Brown, 2.0, scale);
        chevronPen = PainterPens.Pen(PainterPens.Hex("#a08a6a"), 2.0, scale);
    }

    public Color BodyColor => Brown;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 14), 22, 28);

        // Eight legs: pairs 1 and 3 in phase, pairs 2 and 4 half a cycle behind.
        LegPainter.DrawLegPair(dc, legPen, new(-8, -26), new(-34, -66), new(-52, -84), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-11, -20), new(-50, -46), new(-78, -50), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-12, -12), new(-54, -6), new(-80, 8), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-10, -6), new(-40, 26), new(-52, 60), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        dc.DrawLine(palpPen, new Point(-4, -27), new Point(-7, -35));
        dc.DrawLine(palpPen, new Point(4, -27), new Point(7, -35));
        dc.DrawEllipse(abdomen, outline, new Point(0, 14), 18, 24);
        dc.DrawLine(chevronPen, new Point(0, -4), new Point(-8, 6));
        dc.DrawLine(chevronPen, new Point(0, -4), new Point(8, 6));
        dc.DrawLine(chevronPen, new Point(0, 8), new Point(-7, 16));
        dc.DrawLine(chevronPen, new Point(0, 8), new Point(7, 16));
        dc.DrawLine(chevronPen, new Point(0, 20), new Point(-5, 27));
        dc.DrawLine(chevronPen, new Point(0, 20), new Point(5, 27));
        dc.DrawEllipse(brown, outline, new Point(0, -16), 12, 12);
        foreach (var (center, radius) in eyes)
        {
            dc.DrawEllipse(black, null, center, radius, radius);
        }

        dc.Pop();

        dc.Pop();
    }
}
