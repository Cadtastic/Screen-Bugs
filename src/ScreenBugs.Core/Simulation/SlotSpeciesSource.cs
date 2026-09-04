namespace ScreenBugs.Core.Simulation;

/// <summary>Chooses a species from the configured slots: pick a slot, then resolve it.</summary>
public sealed class SlotSpeciesSource(IRandomSource rng) : ISpeciesSource
{
    private IReadOnlyList<SlotSetting> slots = [SlotSetting.Random];

    /// <summary>Assigning runs <see cref="BugTypeSlots.Sanitize"/>, so this is never empty and never repeats a type.</summary>
    public IReadOnlyList<SlotSetting> Slots
    {
        get => slots;
        set => slots = BugTypeSlots.Sanitize(value);
    }

    public SpawnChoice Next()
    {
        // With one slot no draw is made, which keeps seeded runs identical to the pre-options behavior.
        int index = slots.Count == 1 ? 0 : rng.NextInt(slots.Count);

        var species = slots[index].Type.Species is { } chosen
            ? SpeciesCatalog.Get(chosen)
            : SpeciesCatalog.All[rng.NextInt(SpeciesCatalog.All.Count)];

        return new SpawnChoice(species, index);
    }

    public float SpeedFor(int slotIndex) =>
        slotIndex >= 0 && slotIndex < slots.Count
            ? slots[slotIndex].SpeedMultiplier
            : SlotSetting.DefaultSpeed;
}
