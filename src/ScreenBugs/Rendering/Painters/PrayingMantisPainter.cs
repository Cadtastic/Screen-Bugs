using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class PrayingMantisPainter : IBugPainter
{
    private const double SpecimenBodyLength = 168.0;
    private const double LegAmplitudeDegrees = 6.0;

    private static readonly Color Green = PainterPens.Hex("#5fae46");
    private static readonly Color Limb = PainterPens.Hex("#4f9a3c");
    private static readonly Color Dark = PainterPens.Hex("#2f6b23");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.PrayingMantis).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush green = PainterPens.Brush(Green);
    private readonly SolidColorBrush dark = PainterPens.Brush(Dark);
    private readonly Pen outline = PainterPens.Pen(Dark, 1.0, 1.0);
    private readonly Pen vein = PainterPens.Pen(PainterPens.Hex("#3f8a30"), 0.8, 1.0);
    private readonly Pen legPen;
    private readonly Pen forelegPen;
    private readonly Pen antennaPen;
    private readonly PathGeometry abdomen = Shapes.Figure(
        new(0, -22), closed: true,
        Shapes.Bezier(new(14, -10), new(14, 40), new(6, 72)),
        Shapes.Quad(new(0, 80), new(-6, 72)),
        Shapes.Bezier(new(-14, 40), new(-14, -10), new(0, -22)));
    private readonly PathGeometry leftVein = Shapes.Quadratic(new(-6, -10), new(-8, 30), new(-3, 62));
    private readonly PathGeometry rightVein = Shapes.Quadratic(new(6, -10), new(8, 30), new(3, 62));
    private readonly PathGeometry leftForeleg = Shapes.Polyline(new(-3, -60), new(-22, -44), new(-12, -26));
    private readonly PathGeometry rightForeleg = Shapes.Polyline(new(3, -60), new(22, -44), new(12, -26));
    private readonly PathGeometry head = Shapes.Polygon(new(-12, -84), new(12, -84), new(0, -66));
    private readonly PathGeometry leftAntenna = Shapes.Polyline(new(-4, -86), new(-10, -100));
    private readonly PathGeometry rightAntenna = Shapes.Polyline(new(4, -86), new(10, -100));

    public PrayingMantisPainter()
    {
        legPen = PainterPens.Pen(Limb, 2.5, scale);
        forelegPen = PainterPens.Pen(Limb, 4.0, scale);
        antennaPen = PainterPens.Pen(Limb, 1.0, scale);
    }

    public Color BodyColor => Green;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 26), 16, 54);

        // Only the four walking legs animate; the raptorial forelegs are drawn folded and static.
        LegPainter.DrawLegPair(dc, legPen, new(-6, -12), new(-40, -30), new(-56, -4), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-6, 6), new(-36, 30), new(-42, 62), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        dc.DrawGeometry(green, outline, abdomen);
        dc.DrawLine(outline, new Point(0, -18), new Point(0, 66));
        dc.DrawGeometry(null, vein, leftVein);
        dc.DrawGeometry(null, vein, rightVein);
        dc.DrawRoundedRectangle(green, outline, new Rect(-4, -66, 8, 46), 3, 3);
        dc.DrawGeometry(null, forelegPen, leftForeleg);
        dc.DrawGeometry(null, forelegPen, rightForeleg);
        dc.DrawLine(outline, new Point(-22, -44), new Point(-12, -26));
        dc.DrawLine(outline, new Point(22, -44), new Point(12, -26));
        dc.DrawGeometry(green, outline, head);
        dc.DrawEllipse(dark, null, new Point(-11, -84), 4, 4);
        dc.DrawEllipse(dark, null, new Point(11, -84), 4, 4);
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-4, -86), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(4, -86), bug.LegPhase, Math.PI);
        dc.Pop();

        dc.Pop();
    }
}
