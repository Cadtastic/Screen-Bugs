namespace ScreenBugs.Tests;

public sealed class SlotSpeciesSourceTests
{
    private static readonly BugTypeSlot Ant = new(SpeciesId.BlackGardenAnt);
    private static readonly BugTypeSlot Mantis = new(SpeciesId.PrayingMantis);

    private static SlotSpeciesSource Seeded(params BugTypeSlot[] slots) =>
        new(new SystemRandomSource(1234)) { Slots = slots };

    [Fact]
    public void A_single_species_slot_always_yields_that_species()
    {
        var source = Seeded(Ant);

        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(SpeciesId.BlackGardenAnt, source.Next().Id);
        }
    }

    [Fact]
    public void A_single_random_slot_yields_every_species()
    {
        var source = Seeded(BugTypeSlot.Random);

        var seen = new HashSet<SpeciesId>();
        for (int i = 0; i < 2000; i++)
        {
            seen.Add(source.Next().Id);
        }

        Assert.Equal(9, seen.Count);
    }

    [Fact]
    public void Two_species_slots_yield_only_those_two()
    {
        var source = Seeded(Ant, Mantis);

        var seen = new HashSet<SpeciesId>();
        for (int i = 0; i < 500; i++)
        {
            seen.Add(source.Next().Id);
        }

        Assert.Equal([SpeciesId.BlackGardenAnt, SpeciesId.PrayingMantis], seen.Order());
    }

    [Fact]
    public void A_random_slot_may_repeat_a_species_another_slot_holds()
    {
        // Draw 1 picks slot index 1 (Random); draw 2 picks catalog index 1 (black garden ant).
        var source = new SlotSpeciesSource(new ScriptedRandomSource(1, 1)) { Slots = [Ant, BugTypeSlot.Random] };

        Assert.Equal(SpeciesId.BlackGardenAnt, source.Next().Id);
    }

    [Fact]
    public void A_single_species_slot_makes_no_random_draw_at_all()
    {
        var source = new SlotSpeciesSource(new ScriptedRandomSource()) { Slots = [Ant] };

        Assert.Equal(SpeciesId.BlackGardenAnt, source.Next().Id);
    }

    [Fact]
    public void A_single_random_slot_draws_only_the_species_index()
    {
        var source = new SlotSpeciesSource(new ScriptedRandomSource(3)) { Slots = [BugTypeSlot.Random] };

        Assert.Equal(SpeciesCatalog.All[3].Id, source.Next().Id);
    }

    [Fact]
    public void Setting_slots_sanitizes_them()
    {
        var source = Seeded(Ant);

        source.Slots = [];
        Assert.Equal([BugTypeSlot.Random], source.Slots);

        source.Slots = [Ant, Ant];
        Assert.Equal([Ant], source.Slots);
    }
}
