namespace ScreenBugs.Tests;

public sealed class BugTypeSlotsTests
{
    private static readonly BugTypeSlot Ant = new(SpeciesId.BlackGardenAnt);
    private static readonly BugTypeSlot Roach = new(SpeciesId.HissingCockroach);
    private static readonly BugTypeSlot Mantis = new(SpeciesId.PrayingMantis);

    [Fact]
    public void AllChoices_is_random_then_the_nine_species_in_catalog_order()
    {
        var choices = BugTypeSlots.AllChoices;

        Assert.Equal(10, choices.Count);
        Assert.True(choices[0].IsRandom);
        Assert.Equal(SpeciesCatalog.All.Select(s => s.Id), choices.Skip(1).Select(c => c.Species!.Value));
    }

    [Fact]
    public void AvailableFor_excludes_choices_held_by_other_slots_but_keeps_its_own()
    {
        var slots = new[] { Ant, Mantis };

        var forFirst = BugTypeSlots.AvailableFor(slots, 0);

        Assert.Contains(Ant, forFirst);
        Assert.DoesNotContain(Mantis, forFirst);
        Assert.Equal(9, forFirst.Count);
    }

    [Fact]
    public void AvailableFor_excludes_random_when_another_slot_holds_it()
    {
        var slots = new[] { Ant, BugTypeSlot.Random };

        var forFirst = BugTypeSlots.AvailableFor(slots, 0);

        Assert.DoesNotContain(BugTypeSlot.Random, forFirst);
        Assert.Contains(Ant, forFirst);
    }

    [Fact]
    public void Resize_growing_appends_the_first_unused_choices()
    {
        var grown = BugTypeSlots.Resize([Ant], 3);

        Assert.Equal([Ant, BugTypeSlot.Random, Roach], grown);
    }

    [Fact]
    public void Resize_shrinking_keeps_the_leading_slots()
    {
        var shrunk = BugTypeSlots.Resize([Ant, Mantis, Roach], 2);

        Assert.Equal([Ant, Mantis], shrunk);
    }

    [Fact]
    public void Resize_clamps_the_count_between_one_and_max()
    {
        Assert.Single(BugTypeSlots.Resize([Ant, Mantis], 0));
        Assert.Equal(BugTypeSlots.MaxSlots, BugTypeSlots.Resize([Ant], 99).Count);
    }

    [Fact]
    public void Resize_to_max_produces_every_distinct_choice()
    {
        var all = BugTypeSlots.Resize([Ant], BugTypeSlots.MaxSlots);

        Assert.Equal(BugTypeSlots.MaxSlots, all.Distinct().Count());
    }

    [Fact]
    public void Sanitize_drops_duplicates_and_never_returns_empty()
    {
        Assert.Equal([Ant, Mantis], BugTypeSlots.Sanitize([Ant, Mantis, Ant]));
        Assert.Equal([BugTypeSlot.Random], BugTypeSlots.Sanitize([]));
    }
}
