using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class CentipedePainter : IBugPainter
{
    private const double SpecimenBodyLength = 145.5;
    private const double LegAmplitudeDegrees = 10.0;
    private const int AnimatedPairs = 9;
    private const double SegmentSpacing = 13.0;
    private const double FirstSegmentY = -58.0;

    private static readonly Color Body = PainterPens.Hex("#b5702c");
    private static readonly Color Dark = PainterPens.Hex("#7a4519");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.Centipede).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush body = PainterPens.Brush(Body);
    private readonly SolidColorBrush dark = PainterPens.Brush(Dark);
    private readonly SolidColorBrush black = PainterPens.Brush(Colors.Black);
    private readonly Pen outline = PainterPens.Pen(Dark, 1.0, 1.0);
    private readonly Pen legPen;
    private readonly Pen hindLegPen;
    private readonly Pen antennaPen;
    private readonly PathGeometry leftHindLeg = Shapes.Polyline(new(-8, 59), new(-16, 72), new(-20, 80));
    private readonly PathGeometry rightHindLeg = Shapes.Polyline(new(8, 59), new(16, 72), new(20, 80));
    private readonly PathGeometry leftAntenna = Shapes.Quadratic(new(-5, -78), new(-20, -86), new(-30, -92));
    private readonly PathGeometry rightAntenna = Shapes.Quadratic(new(5, -78), new(20, -86), new(30, -92));

    public CentipedePainter()
    {
        legPen = PainterPens.Pen(PainterPens.Hex("#d9a441"), 2.0, scale);
        hindLegPen = PainterPens.Pen(Body, 2.2, scale);
        antennaPen = PainterPens.Pen(Dark, 1.5, scale);
    }

    public Color BodyColor => Body;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 4), 14, 70);

        // Each pair lags the one ahead by an eighth of a cycle, giving a wave along the body.
        for (int i = 0; i < AnimatedPairs; i++)
        {
            double y = FirstSegmentY + SegmentSpacing * i;
            LegPainter.DrawLegPair(
                dc, legPen, new(-8, y), new(-18, y + 5), new(-24, y + 14),
                LegPainter.Swing(bug.LegPhase, 0.125 * i, LegAmplitudeDegrees));
        }

        dc.DrawGeometry(null, hindLegPen, leftHindLeg);
        dc.DrawGeometry(null, hindLegPen, rightHindLeg);

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-5, -78), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(5, -78), bug.LegPhase, Math.PI);
        for (int i = 0; i < AnimatedPairs; i++)
        {
            dc.DrawEllipse(body, outline, new Point(0, FirstSegmentY + SegmentSpacing * i), 9, 7);
        }

        dc.DrawEllipse(body, outline, new Point(0, 59), 8, 6.5);
        dc.DrawEllipse(dark, null, new Point(0, -72), 9, 8);
        dc.DrawEllipse(black, null, new Point(-4, -75), 1.5, 1.5);
        dc.DrawEllipse(black, null, new Point(4, -75), 1.5, 1.5);
        dc.Pop();

        dc.Pop();
    }
}
