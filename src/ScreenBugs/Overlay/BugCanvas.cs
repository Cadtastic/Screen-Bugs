using System.Windows;
using System.Windows.Media;
using ScreenBugs.Rendering;

namespace ScreenBugs.Overlay;

/// <summary>Draws every bug once per frame: hit disc, then the species painter in bug-local space.</summary>
public sealed class BugCanvas : FrameworkElement
{
    private readonly BugPainterRegistry painters = new();

    public BugSimulation? Simulation { get; set; }

    public void Redraw() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        if (Simulation is null)
        {
            return;
        }

        foreach (var bug in Simulation.Bugs)
        {
            var center = new Point(bug.Position.X, bug.Position.Y);
            var painter = painters.Get(bug.Species.Id);

            if (!bug.IsAlive)
            {
                // A splat has no heading, so it is translated but not rotated.
                dc.PushTransform(new TranslateTransform(center.X, center.Y));
                SplatPainter.Paint(dc, bug, painter.BodyColor);
                dc.Pop();
                continue;
            }

            dc.DrawEllipse(PainterPens.HitDisc, null, center, bug.Species.HitRadius, bug.Species.HitRadius);
            dc.PushTransform(new TranslateTransform(center.X, center.Y));
            dc.PushTransform(new RotateTransform(bug.Heading * 180.0 / Math.PI + 90.0));
            painter.Paint(dc, bug);
            dc.Pop();
            dc.Pop();
        }
    }
}
