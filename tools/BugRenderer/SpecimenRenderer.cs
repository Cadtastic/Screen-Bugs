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

    /// <summary>
    /// Half the white outline's width, in final-image pixels. The README displays these around
    /// 150px tall, so this lands near three visible pixels.
    /// </summary>
    private const int OutlineRadius = 8;

    /// <summary>
    /// Everything is drawn at this multiple and scaled back down at the end. Dilation works on
    /// whole pixels, so its outer edge is hard; the downscale is what antialiases it.
    /// </summary>
    private const int Supersample = 2;

    private static readonly double Padding = OutlineRadius + 4.0;

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

        var bare = Render(painter, bug, bounds, zoom * Supersample, width * Supersample, height * Supersample);
        var outlined = OutlineCompositor.AddOutline(bare, OutlineRadius * Supersample);
        var final = Downscale(outlined, width, height);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(final));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static RenderTargetBitmap Render(
        IBugPainter painter, Bug bug, Rect bounds, double zoom, int width, int height)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(Padding * Supersample, Padding * Supersample));
            dc.PushTransform(new ScaleTransform(zoom, zoom));
            dc.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
            painter.Paint(dc, bug);
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        return target;
    }

    private static RenderTargetBitmap Downscale(BitmapSource source, int width, int height)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(source, new Rect(0, 0, width, height));
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        return target;
    }
}
