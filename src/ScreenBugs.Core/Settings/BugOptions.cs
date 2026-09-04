using ScreenBugs.Core.Simulation;

namespace ScreenBugs.Core.Settings;

/// <summary>Everything the Options dialog controls, except the startup registration.</summary>
public sealed record BugOptions(
    IReadOnlyList<BugTypeSlot> TypeSlots,
    int BugCount,
    int FrameRate,
    TypeChangeBehavior OnTypeChange)
{
    public static BugOptions Default => new(
        [new BugTypeSlot(SpeciesId.BlackGardenAnt)],
        BugCount: 5,
        FrameRate: 60,
        TypeChangeBehavior.RespawnAll);

    /// <summary>Compares slots element by element; the synthesized version would compare the list by reference.</summary>
    public bool Equals(BugOptions? other) =>
        other is not null
        && BugCount == other.BugCount
        && FrameRate == other.FrameRate
        && OnTypeChange == other.OnTypeChange
        && TypeSlots.SequenceEqual(other.TypeSlots);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BugCount);
        hash.Add(FrameRate);
        hash.Add(OnTypeChange);
        foreach (var slot in TypeSlots)
        {
            hash.Add(slot);
        }

        return hash.ToHashCode();
    }
}
