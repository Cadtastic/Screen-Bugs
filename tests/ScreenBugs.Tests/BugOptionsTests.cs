namespace ScreenBugs.Tests;

public sealed class BugOptionsTests
{
    private static readonly BugTypeSlot Ant = new(SpeciesId.BlackGardenAnt);
    private static readonly BugTypeSlot Mantis = new(SpeciesId.PrayingMantis);

    [Fact]
    public void Default_equals_a_second_default_despite_a_fresh_slot_list()
    {
        Assert.Equal(BugOptions.Default, BugOptions.Default);
        Assert.Equal(BugOptions.Default.GetHashCode(), BugOptions.Default.GetHashCode());
    }

    [Fact]
    public void Defaults_are_one_black_ant_five_bugs_sixty_fps_respawn_all()
    {
        var options = BugOptions.Default;

        Assert.Equal([Ant], options.TypeSlots);
        Assert.Equal(5, options.BugCount);
        Assert.Equal(60, options.FrameRate);
        Assert.Equal(TypeChangeBehavior.RespawnAll, options.OnTypeChange);
    }

    [Fact]
    public void Records_with_equal_slot_contents_in_different_lists_are_equal()
    {
        var a = BugOptions.Default with { TypeSlots = new List<BugTypeSlot> { Ant, Mantis } };
        var b = BugOptions.Default with { TypeSlots = new[] { Ant, Mantis } };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Changing_any_member_breaks_equality()
    {
        var baseline = BugOptions.Default with { TypeSlots = new[] { Ant, Mantis } };

        Assert.NotEqual(baseline, baseline with { TypeSlots = new[] { Ant } });
        Assert.NotEqual(baseline, baseline with { BugCount = 6 });
        Assert.NotEqual(baseline, baseline with { FrameRate = 30 });
        Assert.NotEqual(baseline, baseline with { OnTypeChange = TypeChangeBehavior.AgeOut });
    }
}
