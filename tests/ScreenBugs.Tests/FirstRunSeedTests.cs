namespace ScreenBugs.Tests;

public sealed class FirstRunSeedTests
{
    private const string Saved = """{"TypeSlots":[{"Type":"Centipede","Speed":1}],"BugCount":9}""";
    private const string Seed = """{"TypeSlots":[{"Type":"HouseSpider","Speed":1}],"BugCount":12,"StartAtLogin":true}""";

    [Fact]
    public void Saved_settings_win_over_the_seed_and_leave_startup_alone()
    {
        var outcome = FirstRunSeed.Decide(Saved, Seed);

        Assert.Equal([SlotSetting.For(SpeciesId.Centipede)], outcome.Options.TypeSlots);
        Assert.Equal(9, outcome.Options.BugCount);
        Assert.Null(outcome.StartAtLogin);
    }

    [Fact]
    public void With_no_saved_settings_the_seed_is_adopted()
    {
        var outcome = FirstRunSeed.Decide(savedSettingsJson: null, Seed);

        Assert.Equal([SlotSetting.For(SpeciesId.HouseSpider)], outcome.Options.TypeSlots);
        Assert.Equal(12, outcome.Options.BugCount);
        Assert.True(outcome.StartAtLogin);
    }

    [Fact]
    public void A_seed_that_switches_startup_off_switches_it_off()
    {
        Assert.False(FirstRunSeed.Decide(null, """{"StartAtLogin":false}""").StartAtLogin);
    }

    [Fact]
    public void A_seed_that_says_nothing_about_startup_leaves_it_alone()
    {
        Assert.Null(FirstRunSeed.Decide(null, """{"BugCount":3}""").StartAtLogin);
    }

    [Fact]
    public void With_neither_file_the_app_starts_on_its_own_defaults()
    {
        var outcome = FirstRunSeed.Decide(null, null);

        Assert.Equal(BugOptions.Default, outcome.Options);
        Assert.Null(outcome.StartAtLogin);
    }

    [Fact]
    public void A_malformed_seed_yields_the_defaults_and_leaves_startup_alone()
    {
        var outcome = FirstRunSeed.Decide(null, "not json");

        Assert.Equal(BugOptions.Default, outcome.Options);
        Assert.Null(outcome.StartAtLogin);
    }

    [Fact]
    public void A_corrupt_saved_file_is_still_not_a_first_run()
    {
        var outcome = FirstRunSeed.Decide("not json", Seed);

        Assert.Equal(BugOptions.Default, outcome.Options);
        Assert.Null(outcome.StartAtLogin);
    }

    [Fact]
    public void An_empty_saved_file_is_still_not_a_first_run()
    {
        var outcome = FirstRunSeed.Decide("", Seed);

        Assert.Equal(BugOptions.Default, outcome.Options);
        Assert.Null(outcome.StartAtLogin);
    }
}
