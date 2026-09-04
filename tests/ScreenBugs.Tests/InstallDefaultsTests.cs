namespace ScreenBugs.Tests;

public sealed class InstallDefaultsTests
{
    private const string FullSeed = """
        {
          "TypeSlots": [ { "Type": "HouseSpider", "Speed": 1 } ],
          "BugCount": 12,
          "FrameRate": 60,
          "OnTypeChange": "RespawnAll",
          "StartAtLogin": true
        }
        """;

    [Fact]
    public void A_full_seed_parses_to_its_options_and_startup_choice()
    {
        var defaults = InstallDefaults.Parse(FullSeed);

        Assert.Equal([SlotSetting.For(SpeciesId.HouseSpider)], defaults.Options.TypeSlots);
        Assert.Equal(12, defaults.Options.BugCount);
        Assert.Equal(60, defaults.Options.FrameRate);
        Assert.Equal(TypeChangeBehavior.RespawnAll, defaults.Options.OnTypeChange);
        Assert.True(defaults.StartAtLogin);
    }

    [Fact]
    public void Startup_false_is_distinct_from_the_field_being_absent()
    {
        Assert.False(InstallDefaults.Parse("""{"StartAtLogin": false}""").StartAtLogin);
    }

    [Fact]
    public void An_absent_startup_field_leaves_startup_alone()
    {
        Assert.Null(InstallDefaults.Parse("""{"BugCount": 3}""").StartAtLogin);
    }

    [Theory]
    [InlineData("""{"StartAtLogin": "yes"}""")]
    [InlineData("""{"StartAtLogin": "true"}""")]
    [InlineData("""{"StartAtLogin": 1}""")]
    [InlineData("""{"StartAtLogin": null}""")]
    public void A_non_boolean_startup_field_leaves_startup_alone(string json)
    {
        Assert.Null(InstallDefaults.Parse(json).StartAtLogin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("null")]
    public void Unusable_input_yields_the_defaults(string json)
    {
        Assert.Equal(InstallDefaults.Default, InstallDefaults.Parse(json));
    }

    [Fact]
    public void Random_reads_as_a_random_slot_at_the_default_speed()
    {
        var defaults = InstallDefaults.Parse("""{"TypeSlots":[{"Type":"Random"}]}""");

        Assert.Equal([new SlotSetting(BugTypeSlot.Random, SlotSetting.DefaultSpeed)], defaults.Options.TypeSlots);
    }

    [Fact]
    public void An_unknown_species_falls_back_to_the_default_slots()
    {
        var defaults = InstallDefaults.Parse("""{"TypeSlots":[{"Type":"Wasp","Speed":1}]}""");

        Assert.Equal(BugOptions.Default.TypeSlots, defaults.Options.TypeSlots);
    }

    [Fact]
    public void Out_of_range_numbers_are_repaired_by_the_settings_serializer()
    {
        var defaults = InstallDefaults.Parse("""{"BugCount": 999, "FrameRate": 7}""");

        Assert.Equal(50, defaults.Options.BugCount);
        Assert.Equal(60, defaults.Options.FrameRate);
    }

    [Fact]
    public void The_file_name_is_the_one_the_installer_writes()
    {
        Assert.Equal("install-defaults.json", InstallDefaults.FileName);
    }
}
