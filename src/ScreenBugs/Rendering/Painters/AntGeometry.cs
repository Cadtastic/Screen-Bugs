using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

/// <summary>Ant drawing shared by the black garden ant and the red fire ant (specimen "ant" symbol).</summary>
public sealed class AntGeometry(Color color, float bodyLength)
{
    private const double SpecimenBodyLength = 81.0;
    private const double LegAmplitudeDegrees = 9.0;

    private readonly double scale = bodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush body = PainterPens.Brush(color);
    private readonly Pen legPen = PainterPens.Pen(color, 1.6, bodyLength / SpecimenBodyLength);
    private readonly Pen antennaPen = PainterPens.Pen(color, 1.4, bodyLength / SpecimenBodyLength);
    private readonly PathGeometry leftMandible = Shapes.Quadratic(new(-6, -44), new(-8, -52), new(-2, -50));
    private readonly PathGeometry rightMandible = Shapes.Quadratic(new(6, -44), new(8, -52), new(2, -50));
    private readonly PathGeometry leftAntenna = Shapes.Polyline(new(-5, -44), new(-14, -58), new(-26, -64));
    private readonly PathGeometry rightAntenna = Shapes.Polyline(new(5, -44), new(14, -58), new(26, -64));

    /// <summary>Initialized property rather than <c>=> color</c> so the parameter is not both captured and used in initializers (CS9124).</summary>
    public Color Color { get; } = color;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 22), 14, 19);

        LegPainter.DrawLegPair(dc, legPen, new(-6, -22), new(-22, -36), new(-30, -24), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-7, -15), new(-26, -14), new(-34, -2), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-6, -8), new(-22, 4), new(-26, 20), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        dc.DrawGeometry(null, legPen, leftMandible);
        dc.DrawGeometry(null, legPen, rightMandible);
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-5, -44), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(5, -44), bug.LegPhase, Math.PI);
        dc.DrawEllipse(body, null, new Point(0, -36), 10, 10);
        dc.DrawEllipse(body, null, new Point(0, -15), 6, 11);
        dc.DrawEllipse(body, null, new Point(0, -1), 3, 3);
        dc.DrawEllipse(body, null, new Point(0, 18), 12, 17);
        dc.Pop();

        dc.Pop();
    }
}
