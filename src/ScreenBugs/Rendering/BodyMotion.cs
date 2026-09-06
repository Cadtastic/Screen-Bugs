using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Body bob, per-segment undulation and antenna waggle, shared by the painters that use them.</summary>
public static class BodyMotion
{
    private const double BobDips = 1.0;
    private const double AntennaAmplitudeDegrees = 3.0;

    /// <summary>Sideways body offset in specimen units: one DIP times sin(4π phase).</summary>
    public static double Bob(float legPhase, double scale) =>
        BobDips / scale * Math.Sin(4.0 * Math.PI * legPhase);

    /// <summary>
    /// Sideways offset in specimen units for a point at <paramref name="alongBody"/> (0 at the
    /// head, 1 at the tail): amplitude times sin(2π(phase − wavelengths·alongBody)). Subtracting
    /// the position term makes the crest travel head to tail as the phase advances. One cycle
    /// per stride, matching the leg frequency rather than <see cref="Bob"/>'s two.
    /// Driven from LegPhase alone, so a paused bug stops rippling and a faster bug ripples faster.
    /// </summary>
    public static double Undulate(float legPhase, double scale, double alongBody, double amplitudeDips, double wavelengths) =>
        amplitudeDips / scale * Math.Sin(2.0 * Math.PI * (legPhase - wavelengths * alongBody));

    /// <summary>Antenna rotation in degrees: 3° times sin(2π phase + side); side is 0 for the left antenna and π for the right.</summary>
    public static double AntennaAngle(float legPhase, double side) =>
        AntennaAmplitudeDegrees * Math.Sin(2.0 * Math.PI * legPhase + side);

    /// <summary>Strokes an antenna rotated about its base by <see cref="AntennaAngle"/>.</summary>
    public static void DrawAntenna(DrawingContext dc, Pen pen, PathGeometry antenna, Point basePoint, float legPhase, double side)
    {
        dc.PushTransform(new RotateTransform(AntennaAngle(legPhase, side), basePoint.X, basePoint.Y));
        dc.DrawGeometry(null, pen, antenna);
        dc.Pop();
    }
}
