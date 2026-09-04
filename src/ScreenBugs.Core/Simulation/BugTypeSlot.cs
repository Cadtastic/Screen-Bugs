namespace ScreenBugs.Core.Simulation;

/// <summary>
/// One bug-type slot in the options: a specific species, or Random when
/// <see cref="Species"/> is null, meaning any of the nine at spawn time.
/// </summary>
public readonly record struct BugTypeSlot(SpeciesId? Species)
{
    public static readonly BugTypeSlot Random = new(null);

    public bool IsRandom => Species is null;
}
