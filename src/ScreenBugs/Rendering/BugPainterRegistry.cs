using ScreenBugs.Rendering.Painters;

namespace ScreenBugs.Rendering;

/// <summary>Maps each <see cref="SpeciesId"/> to its painter.</summary>
public sealed class BugPainterRegistry
{
    // Temporary: every species draws as a black garden ant until the other painters land,
    // so the overlay can be exercised with one painter first.
    private static readonly IBugPainter Placeholder = new BlackGardenAntPainter();

    private readonly Dictionary<SpeciesId, IBugPainter> painters =
        Enum.GetValues<SpeciesId>().ToDictionary(id => id, _ => Placeholder);

    public IBugPainter Get(SpeciesId id) => painters[id];
}
