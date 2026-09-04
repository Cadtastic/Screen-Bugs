namespace ScreenBugs.Tests;

public sealed class BugTypeSlotsTests
{
    private static readonly BugTypeSlot Ant = new(SpeciesId.BlackGardenAnt);
    private static readonly BugTypeSlot Roach = new(SpeciesId.HissingCockroach);
    private static readonly BugTypeSlot Mantis = new(SpeciesId.PrayingMantis);

    private static SlotSetting[] Rows(params BugTypeSlot[] types) =>
        types.Select(type => new SlotSetting(type)).ToArray();

    private static BugTypeSlot[] TypesOf(IReadOnlyList<SlotSetting> slots) =>
        slots.Select(slot => slot.Type).ToArray();

    [Fact]
    public void AllChoices_is_random_then_the_nine_species_in_catalog_order()
    {
        var choices = BugTypeSlots.AllChoices;

        Assert.Equal(10, choices.Count);
        Assert.True(choices[0].IsRandom);
        Assert.Equal(SpeciesCatalog.All.Select(s => s.Id), choices.Skip(1).Select(c => c.Species!.Value));
    }

    [Fact]
    public void AvailableFor_excludes_types_held_by_other_rows_but_keeps_its_own()
    {
        var forFirst = BugTypeSlots.AvailableFor(Rows(Ant, Mantis), 0);

        Assert.Contains(Ant, forFirst);
        Assert.DoesNotContain(Mantis, forFirst);
        Assert.Equal(9, forFirst.Count);
    }

    [Fact]
    public void AvailableFor_excludes_random_when_another_row_holds_it()
    {
        var forFirst = BugTypeSlots.AvailableFor(Rows(Ant, BugTypeSlot.Random), 0);

        Assert.DoesNotContain(BugTypeSlot.Random, forFirst);
        Assert.Contains(Ant, forFirst);
    }

    [Fact]
    public void AvailableFor_ignores_speed_when_deciding_what_is_taken()
    {
        var slots = new[] { SlotSetting.For(SpeciesId.BlackGardenAnt, 2.5f), new SlotSetting(Mantis) };

        var forSecond = BugTypeSlots.AvailableFor(slots, 1);

        Assert.DoesNotContain(Ant, forSecond);
        Assert.Contains(Mantis, forSecond);
    }

    [Fact]
    public void Resize_growing_appends_the_first_unused_types_at_default_speed()
    {
        var grown = BugTypeSlots.Resize(Rows(Ant), 3);

        Assert.Equal([Ant, BugTypeSlot.Random, Roach], TypesOf(grown));
        Assert.All(grown.Skip(1), slot => Assert.Equal(SlotSetting.DefaultSpeed, slot.SpeedMultiplier));
    }

    [Fact]
    public void Resize_shrinking_keeps_the_leading_rows_with_their_speeds()
    {
        var slots = new[] { SlotSetting.For(SpeciesId.BlackGardenAnt, 2f), new SlotSetting(Mantis), new SlotSetting(Roach) };

        var shrunk = BugTypeSlots.Resize(slots, 2);

        Assert.Equal([Ant, Mantis], TypesOf(shrunk));
        Assert.Equal(2f, shrunk[0].SpeedMultiplier);
    }

    [Fact]
    public void Resize_clamps_the_count_between_one_and_max()
    {
        Assert.Single(BugTypeSlots.Resize(Rows(Ant, Mantis), 0));
        Assert.Equal(BugTypeSlots.MaxSlots, BugTypeSlots.Resize(Rows(Ant), 99).Count);
    }

    [Fact]
    public void Resize_to_max_produces_every_distinct_type()
    {
        var all = BugTypeSlots.Resize(Rows(Ant), BugTypeSlots.MaxSlots);

        Assert.Equal(BugTypeSlots.MaxSlots, TypesOf(all).Distinct().Count());
    }

    [Fact]
    public void Sanitize_drops_repeated_types_and_never_returns_empty()
    {
        Assert.Equal([Ant, Mantis], TypesOf(BugTypeSlots.Sanitize(Rows(Ant, Mantis, Ant))));
        Assert.Equal([BugTypeSlot.Random], TypesOf(BugTypeSlots.Sanitize([])));
    }

    [Fact]
    public void Sanitize_keeps_the_first_rows_speed_and_clamps_out_of_range_speeds()
    {
        var slots = new[]
        {
            SlotSetting.For(SpeciesId.BlackGardenAnt, 2f),
            SlotSetting.For(SpeciesId.BlackGardenAnt, 0.5f),
            SlotSetting.For(SpeciesId.PrayingMantis, 99f),
            SlotSetting.For(SpeciesId.HissingCockroach, 0f),
        };

        var clean = BugTypeSlots.Sanitize(slots);

        Assert.Equal(3, clean.Count);
        Assert.Equal(2f, clean[0].SpeedMultiplier);
        Assert.Equal(SlotSetting.MaxSpeed, clean[1].SpeedMultiplier);
        Assert.Equal(SlotSetting.MinSpeed, clean[2].SpeedMultiplier);
    }
}
