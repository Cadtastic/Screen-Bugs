namespace ScreenBugs.Core.Simulation;

/// <summary>Chooses a species from the configured slots: pick a slot, then resolve it.</summary>
public sealed class SlotSpeciesSource(IRandomSource rng) : ISpeciesSource
{
    private IReadOnlyList<BugTypeSlot> slots = [BugTypeSlot.Random];

    /// <summary>Assigning runs <see cref="BugTypeSlots.Sanitize"/>, so this is never empty and never has duplicates.</summary>
    public IReadOnlyList<BugTypeSlot> Slots
    {
        get => slots;
        set => slots = BugTypeSlots.Sanitize(value);
    }

    public BugSpecies Next()
    {
        // With one slot no draw is made, which keeps seeded runs identical to the pre-options behavior.
        BugTypeSlot slot = slots.Count == 1 ? slots[0] : slots[rng.NextInt(slots.Count)];

        return slot.Species is { } species
            ? SpeciesCatalog.Get(species)
            : SpeciesCatalog.All[rng.NextInt(SpeciesCatalog.All.Count)];
    }
}
