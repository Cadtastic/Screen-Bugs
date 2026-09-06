using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenBugs.Core.Simulation;
using ScreenBugs.Rendering;

namespace BugRenderer;

/// <summary>
/// Draws one species across a full stride, eight frames side by side, so the gait and the body
/// wave can be judged without watching the live overlay. A review aid only: nothing here ships.
/// </summary>
public static class FilmstripRenderer
{
    private const int Frames = 8;
    private const double Gap = 8.0;
    private const double Padding = 4.0;

    public static void Write(string path, SpeciesId id, BugPainterRegistry registry)
    {
        var painter = registry.Get(id);
        var species = SpeciesCatalog.Get(id);

        var bugs = new Bug[Frames];
        for (int k = 0; k < Frames; k++)
        {
            bugs[k] = new Bug(id: 0, species, seed: 0) { LegPhase = (float)k / Frames };
        }

        // One set of bounds for every frame, so the wave shows as displacement rather than as
        // each frame re-centring on its own extent.
        Rect bounds = Rect.Empty;
        foreach (var bug in bugs)
        {
            bounds.Union(SpecimenRenderer.Measure(painter, bug));
        }

        double zoom = SpecimenRenderer.TargetSize / Math.Max(bounds.Width, bounds.Height);
        double frameWidth = Math.Ceiling(bounds.Width * zoom);
        double frameHeight = Math.Ceiling(bounds.Height * zoom);
        int width = (int)Math.Ceiling((Padding * 2) + (Frames * frameWidth) + ((Frames - 1) * Gap));
        int height = (int)Math.Ceiling((Padding * 2) + frameHeight);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Opaque white so the strip reads the same in any image viewer, light or dark.
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

            for (int k = 0; k < Frames; k++)
            {
                dc.PushTransform(new TranslateTransform(Padding + (k * (frameWidth + Gap)), Padding));
                dc.PushTransform(new ScaleTransform(zoom, zoom));
                dc.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
                painter.Paint(dc, bugs[k]);
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        SpecimenRenderer.SavePng(target, path);
    }
}
