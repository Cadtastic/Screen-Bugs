using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Two-segment legs with the gait swing from spec 6.</summary>
public static class LegPainter
{
    /// <summary>Signed swing in radians: amplitude times sin(2π(phase + groupOffset)).</summary>
    public static double Swing(float legPhase, double groupOffset, double amplitudeDegrees) =>
        amplitudeDegrees * Math.PI / 180.0 * Math.Sin(2.0 * Math.PI * (legPhase + groupOffset));

    /// <summary>Draws one leg rotated about the hip by <paramref name="swingRadians"/>.</summary>
    public static void DrawLeg(DrawingContext dc, Pen pen, Point hip, Point knee, Point foot, double swingRadians)
    {
        dc.PushTransform(new RotateTransform(swingRadians * 180.0 / Math.PI, hip.X, hip.Y));
        dc.DrawLine(pen, hip, knee);
        dc.DrawLine(pen, knee, foot);
        dc.Pop();
    }

    /// <summary>
    /// Draws a left leg (negative X) and its right-side mirror with the same signed swing.
    /// Because the right leg's geometry is mirrored, one signed rotation moves the left foot
    /// forward and the right foot backward, so a pair is always in antiphase.
    /// </summary>
    public static void DrawLegPair(DrawingContext dc, Pen pen, Point hip, Point knee, Point foot, double swingRadians)
    {
        DrawLeg(dc, pen, hip, knee, foot, swingRadians);
        DrawLeg(dc, pen, Mirror(hip), Mirror(knee), Mirror(foot), swingRadians);
    }

    public static Point Mirror(Point point) => new(-point.X, point.Y);
}
