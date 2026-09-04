using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class HissingCockroachPainter : IBugPainter
{
    private const double SpecimenBodyLength = 136.0;
    private const double LegAmplitudeDegrees = 8.0;

    private static readonly Color Shell = PainterPens.Hex("#3b2314");
    private static readonly Color Dark = PainterPens.Hex("#2b1a0f");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.HissingCockroach).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush shell = PainterPens.Brush(Shell);
    private readonly SolidColorBrush dark = PainterPens.Brush(Dark);
    private readonly SolidColorBrush horn = PainterPens.Brush(PainterPens.Hex("#4a2e18"));
    private readonly Pen shellOutline = PainterPens.Pen(PainterPens.Hex("#24140a"), 1.0, 1.0);
    private readonly Pen darkOutline = PainterPens.Pen(PainterPens.Hex("#1a0f08"), 1.0, 1.0);
    private readonly Pen legPen;
    private readonly Pen antennaPen;
    private readonly Pen bandPen;
    private readonly PathGeometry leftAntenna = Shapes.Quadratic(new(-6, -62), new(-30, -95), new(-62, -98));
    private readonly PathGeometry rightAntenna = Shapes.Quadratic(new(6, -62), new(30, -95), new(62, -98));
    private readonly PathGeometry[] bands =
    [
        Shapes.Quadratic(new(-21, -20), new(0, -17), new(21, -20)),
        Shapes.Quadratic(new(-27, -4), new(0, -1), new(27, -4)),
        Shapes.Quadratic(new(-30, 12), new(0, 15), new(30, 12)),
        Shapes.Quadratic(new(-29, 28), new(0, 31), new(29, 28)),
        Shapes.Quadratic(new(-26, 44), new(0, 47), new(26, 44)),
        Shapes.Quadratic(new(-20, 58), new(0, 61), new(20, 58)),
    ];

    // Pens that scale with the body are built here because a field initializer cannot read another instance field.
    public HissingCockroachPainter()
    {
        legPen = PainterPens.Pen(Shell, 3.5, scale);
        antennaPen = PainterPens.Pen(Shell, 2.0, scale);
        bandPen = PainterPens.Pen(PainterPens.Hex("#9a6230"), 2.5, scale);
    }

    public Color BodyColor => Shell;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(4, 22), 34, 58);

        LegPainter.DrawLegPair(dc, legPen, new(-18, -30), new(-48, -52), new(-62, -30), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-24, -2), new(-58, -8), new(-70, 14), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-20, 26), new(-50, 44), new(-58, 72), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-6, -62), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(6, -62), bug.LegPhase, Math.PI);
        dc.DrawEllipse(dark, null, new Point(0, -58), 9, 6);
        dc.DrawEllipse(shell, shellOutline, new Point(0, 18), 30, 54);
        foreach (var band in bands)
        {
            dc.DrawGeometry(null, bandPen, band);
        }

        dc.DrawEllipse(dark, darkOutline, new Point(0, -42), 27, 17);
        dc.DrawEllipse(horn, null, new Point(-10, -52), 6, 4);
        dc.DrawEllipse(horn, null, new Point(10, -52), 6, 4);
        dc.Pop();

        dc.Pop();
    }
}
