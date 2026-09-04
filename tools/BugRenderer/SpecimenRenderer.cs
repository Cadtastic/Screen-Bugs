using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenBugs.Core.Simulation;
using ScreenBugs.Rendering;

namespace BugRenderer;

/// <summary>
/// Draws one species to a PNG using the app's own painter, so the documentation cannot drift
/// from what actually crawls across the screen.
/// </summary>
public static class SpecimenRenderer
{
    /// <summary>How many pixels the longest side of a specimen should occupy.</summary>
    private const double TargetSize = 320.0;

    private const double Padding = 12.0;

    public static void Write(string path, SpeciesId id, BugPainterRegistry registry)
    {
        var painter = registry.Get(id);

        // A bug with the default leg phase: every leg sits at its neutral swing, which gives a
        // symmetric specimen pose rather than a frame frozen mid-stride.
        var bug = new Bug(id: 0, SpeciesCatalog.Get(id), seed: 0);

        // Paint once at 1:1 purely to learn how much room the drawing needs. The painter works
        // in bug-local space around the body centre and every species covers a different extent
        // once legs, antennae and shadow are counted, so the bounds cannot be predicted from
        // body length alone.
        var measured = new DrawingVisual();
        using (var dc = measured.RenderOpen())
        {
            painter.Paint(dc, bug);
        }

        Rect bounds = measured.ContentBounds;
        double zoom = TargetSize / Math.Max(bounds.Width, bounds.Height);
        int width = (int)Math.Ceiling((bounds.Width * zoom) + (Padding * 2));
        int height = (int)Math.Ceiling((bounds.Height * zoom) + (Padding * 2));

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(Padding, Padding));
            dc.PushTransform(new ScaleTransform(zoom, zoom));
            dc.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
            painter.Paint(dc, bug);
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
