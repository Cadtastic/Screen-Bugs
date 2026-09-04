namespace ScreenBugs.Tests;

public sealed class SlotSpeciesSourceTests
{
    private static readonly BugTypeSlot Ant = new(SpeciesId.BlackGardenAnt);
    private static readonly BugTypeSlot Mantis = new(SpeciesId.PrayingMantis);

    private static SlotSpeciesSource Seeded(params BugTypeSlot[] types) =>
        new(new SystemRandomSource(1234)) { Slots = types.Select(t => new SlotSetting(t)).ToArray() };

    [Fact]
    public void A_single_species_row_always_yields_that_species()
    {
        var source = Seeded(Ant);

        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(SpeciesId.BlackGardenAnt, source.Next().Species.Id);
        }
    }

    [Fact]
    public void A_single_random_row_yields_every_species()
    {
        var source = Seeded(BugTypeSlot.Random);

        var seen = new HashSet<SpeciesId>();
        for (int i = 0; i < 2000; i++)
        {
            seen.Add(source.Next().Species.Id);
        }

        Assert.Equal(9, seen.Count);
    }

    [Fact]
    public void Two_species_rows_yield_only_those_two()
    {
        var source = Seeded(Ant, Mantis);

        var seen = new HashSet<SpeciesId>();
        for (int i = 0; i < 500; i++)
        {
            seen.Add(source.Next().Species.Id);
        }

        Assert.Equal([SpeciesId.BlackGardenAnt, SpeciesId.PrayingMantis], seen.Order());
    }

    [Fact]
    public void A_random_row_may_repeat_a_species_another_row_holds()
    {
        // Draw 1 picks slot index 1 (Random); draw 2 picks catalog index 1 (black garden ant).
        var source = new SlotSpeciesSource(new ScriptedRandomSource(1, 1))
        {
            Slots = [new SlotSetting(Ant), SlotSetting.Random],
        };

        var choice = source.Next();

        Assert.Equal(SpeciesId.BlackGardenAnt, choice.Species.Id);
        Assert.Equal(1, choice.SlotIndex);
    }

    [Fact]
    public void A_single_species_row_makes_no_random_draw_at_all()
    {
        var source = new SlotSpeciesSource(new ScriptedRandomSource()) { Slots = [new SlotSetting(Ant)] };

        Assert.Equal(SpeciesId.BlackGardenAnt, source.Next().Species.Id);
    }

    [Fact]
    public void A_single_random_row_draws_only_the_species_index()
    {
        var source = new SlotSpeciesSource(new ScriptedRandomSource(3)) { Slots = [SlotSetting.Random] };

        Assert.Equal(SpeciesCatalog.All[3].Id, source.Next().Species.Id);
    }

    [Fact]
    public void The_choice_reports_the_row_it_came_from()
    {
        var source = new SlotSpeciesSource(new ScriptedRandomSource(1)) { Slots = [new SlotSetting(Ant), new SlotSetting(Mantis)] };

        var choice = source.Next();

        Assert.Equal(1, choice.SlotIndex);
        Assert.Equal(SpeciesId.PrayingMantis, choice.Species.Id);
    }

    [Fact]
    public void SpeedFor_returns_the_rows_multiplier_and_defaults_outside_the_range()
    {
        var source = new SlotSpeciesSource(new SystemRandomSource(1))
        {
            Slots = [SlotSetting.For(SpeciesId.BlackGardenAnt, 2.5f), SlotSetting.For(SpeciesId.PrayingMantis, 0.5f)],
        };

        Assert.Equal(2.5f, source.SpeedFor(0));
        Assert.Equal(0.5f, source.SpeedFor(1));
        Assert.Equal(SlotSetting.DefaultSpeed, source.SpeedFor(-1));
        Assert.Equal(SlotSetting.DefaultSpeed, source.SpeedFor(7));
    }

    [Fact]
    public void SpeedFor_follows_a_later_edit_to_the_rows()
    {
        var source = new SlotSpeciesSource(new SystemRandomSource(1))
        {
            Slots = [SlotSetting.For(SpeciesId.BlackGardenAnt, 1f)],
        };
        Assert.Equal(1f, source.SpeedFor(0));

        source.Slots = [SlotSetting.For(SpeciesId.BlackGardenAnt, 3f)];

        Assert.Equal(3f, source.SpeedFor(0));
    }

    [Fact]
    public void Setting_rows_sanitizes_them()
    {
        var source = Seeded(Ant);

        source.Slots = [];
        Assert.Equal([BugTypeSlot.Random], source.Slots.Select(s => s.Type));

        source.Slots = [new SlotSetting(Ant), new SlotSetting(Ant)];
        Assert.Single(source.Slots);
    }
}
