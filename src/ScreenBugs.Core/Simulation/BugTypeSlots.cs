namespace ScreenBugs.Core.Simulation;

/// <summary>Rules for a list of <see cref="BugTypeSlot"/>: what a slot may hold, and how the list resizes.</summary>
public static class BugTypeSlots
{
    /// <summary>Ten distinct choices exist, so a duplicate-free list can never be longer.</summary>
    public const int MaxSlots = 10;

    public static IReadOnlyList<BugTypeSlot> AllChoices { get; } =
        [BugTypeSlot.Random, .. SpeciesCatalog.All.Select(species => new BugTypeSlot(species.Id))];

    /// <summary>Every choice not held by a different slot, plus this slot's own current value.</summary>
    public static IReadOnlyList<BugTypeSlot> AvailableFor(IReadOnlyList<BugTypeSlot> slots, int index) =>
        AllChoices.Where(choice => choice == slots[index] || !HeldByOther(slots, index, choice)).ToList();

    /// <summary>Clamps to [1, MaxSlots]. Shrinking keeps the prefix; growing appends unused choices.</summary>
    public static IReadOnlyList<BugTypeSlot> Resize(IReadOnlyList<BugTypeSlot> slots, int count)
    {
        count = Math.Clamp(count, 1, MaxSlots);
        var resized = slots.Take(count).ToList();
        while (resized.Count < count)
        {
            // Slots appended earlier in this loop count as taken.
            resized.Add(AllChoices.First(choice => !resized.Contains(choice)));
        }

        return resized;
    }

    /// <summary>Drops duplicates keeping the first occurrence; an empty result becomes a single Random slot.</summary>
    public static IReadOnlyList<BugTypeSlot> Sanitize(IReadOnlyList<BugTypeSlot> slots)
    {
        var unique = new List<BugTypeSlot>();
        foreach (var slot in slots)
        {
            if (!unique.Contains(slot))
            {
                unique.Add(slot);
            }
        }

        return unique.Count == 0 ? [BugTypeSlot.Random] : unique;
    }

    private static bool HeldByOther(IReadOnlyList<BugTypeSlot> slots, int index, BugTypeSlot choice)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (i != index && slots[i] == choice)
            {
                return true;
            }
        }

        return false;
    }
}
