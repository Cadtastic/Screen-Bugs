using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Draws a squashed bug as a fading blob of darkened body color with a few droplets.</summary>
public static class SplatPainter
{
    private const double DarkenFraction = 0.30;
    private const double BlobRadiusMin = 0.15;
    private const double BlobRadiusMax = 0.30;
    private const double BlobSpread = 0.35;
    private const double DropletRadiusMin = 0.04;
    private const double DropletRadiusMax = 0.08;
    private const double DropletDistanceMin = 0.5;
    private const double DropletDistanceMax = 0.9;

    public static void Paint(DrawingContext dc, Bug bug, Color bodyColor)
    {
        // Seeded from the bug so the splat keeps the same shape every frame while it fades.
        var random = new Random(bug.Seed);
        double size = bug.Species.BodyLength;
        var brush = PainterPens.Brush(PainterPens.Darken(bodyColor, DarkenFraction));

        dc.PushOpacity(Math.Clamp(1.0 - bug.SquashProgress, 0.0, 1.0));

        int blobs = random.Next(6, 10);
        for (int i = 0; i < blobs; i++)
        {
            double radius = size * Range(random, BlobRadiusMin, BlobRadiusMax);
            double distance = size * BlobSpread * random.NextDouble();
            dc.DrawEllipse(brush, null, Polar(random, distance), radius, radius);
        }

        int droplets = random.Next(3, 6);
        for (int i = 0; i < droplets; i++)
        {
            double radius = size * Range(random, DropletRadiusMin, DropletRadiusMax);
            double distance = size * Range(random, DropletDistanceMin, DropletDistanceMax);
            dc.DrawEllipse(brush, null, Polar(random, distance), radius, radius);
        }

        dc.Pop();
    }

    private static double Range(Random random, double min, double max) => min + (max - min) * random.NextDouble();

    private static Point Polar(Random random, double distance)
    {
        double angle = random.NextDouble() * Math.Tau;
        return new Point(Math.Cos(angle) * distance, Math.Sin(angle) * distance);
    }
}
