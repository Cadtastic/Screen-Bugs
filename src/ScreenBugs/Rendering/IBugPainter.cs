using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Draws one species (spec 6).</summary>
public interface IBugPainter
{
    /// <summary>Main body color; the splat is drawn in a darkened version of it.</summary>
    Color BodyColor { get; }

    /// <summary>Draws the bug in bug-local space: origin at the body center, bug facing up (negative Y), DIP units.</summary>
    void Paint(DrawingContext dc, Bug bug);
}
