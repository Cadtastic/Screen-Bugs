using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Body bob and antenna waggle shared by every painter.</summary>
public static class BodyMotion
{
    private const double BobDips = 1.0;
    private const double AntennaAmplitudeDegrees = 3.0;

    /// <summary>Sideways body offset in specimen units: one DIP times sin(4π phase).</summary>
    public static double Bob(float legPhase, double scale) =>
        BobDips / scale * Math.Sin(4.0 * Math.PI * legPhase);

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
