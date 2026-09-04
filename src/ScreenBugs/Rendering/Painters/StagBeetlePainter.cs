using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class StagBeetlePainter : IBugPainter
{
    private const double SpecimenBodyLength = 110.0;
    private const double LegAmplitudeDegrees = 7.0;

    private static readonly Color Brown = PainterPens.Hex("#2b1b12");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.StagBeetle).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush brown = PainterPens.Brush(Brown);
    private readonly SolidColorBrush black = PainterPens.Brush(Colors.Black);
    private readonly Pen outline = PainterPens.Pen(PainterPens.Hex("#150c07"), 1.0, 1.0);
    private readonly Pen seam = PainterPens.Pen(PainterPens.Hex("#5a3a26"), 1.0, 1.0);
    private readonly Pen legPen;
    private readonly Pen antennaPen;
    private readonly Pen antlerPen;
    private readonly Pen tinePen;
    private readonly PathGeometry leftAntenna = Shapes.Polyline(new(-16, -50), new(-26, -58));
    private readonly PathGeometry rightAntenna = Shapes.Polyline(new(16, -50), new(26, -58));
    private readonly PathGeometry leftAntler = Shapes.Cubic(new(-8, -52), new(-18, -62), new(-24, -78), new(-16, -94));
    private readonly PathGeometry rightAntler = Shapes.Cubic(new(8, -52), new(18, -62), new(24, -78), new(16, -94));
    private readonly PathGeometry leftTip = Shapes.Quadratic(new(-16, -94), new(-12, -100), new(-4, -98));
    private readonly PathGeometry rightTip = Shapes.Quadratic(new(16, -94), new(12, -100), new(4, -98));

    public StagBeetlePainter()
    {
        legPen = PainterPens.Pen(Brown, 3.0, scale);
        antennaPen = PainterPens.Pen(Brown, 1.5, scale);
        antlerPen = PainterPens.Pen(Brown, 5.0, scale);
        tinePen = PainterPens.Pen(Brown, 3.0, scale);
    }

    public Color BodyColor => Brown;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 20), 28, 46);

        LegPainter.DrawLegPair(dc, legPen, new(-14, -24), new(-40, -44), new(-48, -28), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-18, -4), new(-52, -10), new(-58, 14), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-16, 20), new(-44, 40), new(-46, 66), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-16, -50), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(16, -50), bug.LegPhase, Math.PI);
        dc.DrawGeometry(null, antlerPen, leftAntler);
        dc.DrawGeometry(null, antlerPen, rightAntler);
        dc.DrawLine(tinePen, new Point(-19, -76), new Point(-9, -80));
        dc.DrawLine(tinePen, new Point(19, -76), new Point(9, -80));
        dc.DrawGeometry(null, tinePen, leftTip);
        dc.DrawGeometry(null, tinePen, rightTip);
        dc.DrawEllipse(brown, outline, new Point(0, 18), 24, 40);
        dc.DrawLine(seam, new Point(0, -22), new Point(0, 58));
        dc.DrawRoundedRectangle(brown, outline, new Rect(-19, -40, 38, 22), 7, 7);
        dc.DrawRoundedRectangle(brown, null, new Rect(-14, -52, 28, 14), 3, 3);
        dc.DrawEllipse(black, null, new Point(-13, -46), 2.5, 2.5);
        dc.DrawEllipse(black, null, new Point(13, -46), 2.5, 2.5);
        dc.Pop();

        dc.Pop();
    }
}
