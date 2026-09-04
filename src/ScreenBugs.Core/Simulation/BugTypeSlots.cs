namespace ScreenBugs.Core.Simulation;

/// <summary>
/// Rules for a list of <see cref="SlotSetting"/>: what type a row may hold, and how the list
/// resizes. Uniqueness is judged on the type alone; two rows may share a speed.
/// </summary>
public static class BugTypeSlots
{
    /// <summary>Ten distinct types exist, so a duplicate-free list can never be longer.</summary>
    public const int MaxSlots = 10;

    public static IReadOnlyList<BugTypeSlot> AllChoices { get; } =
        [BugTypeSlot.Random, .. SpeciesCatalog.All.Select(species => new BugTypeSlot(species.Id))];

    /// <summary>Every type not held by a different row, plus this row's own current type.</summary>
    public static IReadOnlyList<BugTypeSlot> AvailableFor(IReadOnlyList<SlotSetting> slots, int index) =>
        AllChoices.Where(choice => choice == slots[index].Type || !HeldByOther(slots, index, choice)).ToList();

    /// <summary>Clamps to [1, MaxSlots]. Shrinking keeps the prefix; growing appends unused types at default speed.</summary>
    public static IReadOnlyList<SlotSetting> Resize(IReadOnlyList<SlotSetting> slots, int count)
    {
        count = Math.Clamp(count, 1, MaxSlots);
        var resized = slots.Take(count).ToList();
        while (resized.Count < count)
        {
            // Rows appended earlier in this loop count as taken.
            var next = AllChoices.First(choice => resized.All(slot => slot.Type != choice));
            resized.Add(new SlotSetting(next));
        }

        return resized;
    }

    /// <summary>
    /// Drops rows repeating an earlier row's type, clamps every speed, and turns an empty list
    /// into a single Random row.
    /// </summary>
    public static IReadOnlyList<SlotSetting> Sanitize(IReadOnlyList<SlotSetting> slots)
    {
        var unique = new List<SlotSetting>();
        foreach (var slot in slots)
        {
            if (unique.All(existing => existing.Type != slot.Type))
            {
                unique.Add(slot with { SpeedMultiplier = SlotSetting.ClampSpeed(slot.SpeedMultiplier) });
            }
        }

        return unique.Count == 0 ? [SlotSetting.Random] : unique;
    }

    private static bool HeldByOther(IReadOnlyList<SlotSetting> slots, int index, BugTypeSlot choice)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (i != index && slots[i].Type == choice)
            {
                return true;
            }
        }

        return false;
    }
}
