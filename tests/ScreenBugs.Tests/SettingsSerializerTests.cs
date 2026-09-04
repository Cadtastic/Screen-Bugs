namespace ScreenBugs.Tests;

public sealed class SettingsSerializerTests
{
    private static readonly SlotSetting Ant = SlotSetting.For(SpeciesId.BlackGardenAnt);
    private static readonly SlotSetting Centipede = SlotSetting.For(SpeciesId.Centipede);

    [Fact]
    public void Round_trip_preserves_every_field_including_speeds()
    {
        var original = new BugOptions(
            [
                SlotSetting.For(SpeciesId.BlackGardenAnt, 2.5f),
                new SlotSetting(BugTypeSlot.Random, 0.5f),
                SlotSetting.For(SpeciesId.PrayingMantis),
            ],
            BugCount: 7,
            FrameRate: 120,
            TypeChangeBehavior.AgeOut);

        Assert.Equal(original, SettingsSerializer.Deserialize(SettingsSerializer.Serialize(original)));
    }

    [Fact]
    public void A_bare_type_name_from_an_older_file_loads_at_the_default_speed()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["Centipede","Random"]}""");

        Assert.Equal([Centipede.Type, BugTypeSlot.Random], options.TypeSlots.Select(s => s.Type));
        Assert.All(options.TypeSlots, slot => Assert.Equal(SlotSetting.DefaultSpeed, slot.SpeedMultiplier));
    }

    [Fact]
    public void An_out_of_range_or_missing_speed_is_repaired()
    {
        var options = SettingsSerializer.Deserialize(
            """{"TypeSlots":[{"Type":"Centipede","Speed":99},{"Type":"Random"},{"Type":"StagBeetle","Speed":"fast"}]}""");

        Assert.Equal(SlotSetting.MaxSpeed, options.TypeSlots[0].SpeedMultiplier);
        Assert.Equal(SlotSetting.DefaultSpeed, options.TypeSlots[1].SpeedMultiplier);
        Assert.Equal(SlotSetting.DefaultSpeed, options.TypeSlots[2].SpeedMultiplier);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("{")]
    [InlineData("{}")]
    public void Unusable_input_yields_the_defaults(string json)
    {
        Assert.Equal(BugOptions.Default, SettingsSerializer.Deserialize(json));
    }

    [Fact]
    public void A_wrong_typed_field_does_not_discard_the_others()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["Centipede"],"BugCount":"5"}""");

        Assert.Equal([Centipede.Type], options.TypeSlots.Select(s => s.Type));
        Assert.Equal(5, options.BugCount);
    }

    [Fact]
    public void Unknown_and_null_slot_names_are_dropped()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["Centipede","99","Unicorn",null,5]}""");

        Assert.Equal([Centipede.Type], options.TypeSlots.Select(s => s.Type));
    }

    [Fact]
    public void Slot_names_are_case_insensitive()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["random","blackgardenant"]}""");

        Assert.Equal([BugTypeSlot.Random, Ant.Type], options.TypeSlots.Select(s => s.Type));
    }

    [Fact]
    public void An_all_unknown_slot_list_falls_back_to_the_default_slots()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["Unicorn"]}""");

        Assert.Equal(BugOptions.Default.TypeSlots, options.TypeSlots);
    }

    [Fact]
    public void Duplicate_slots_are_sanitized_away()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["Centipede","Centipede"]}""");

        Assert.Equal([Centipede.Type], options.TypeSlots.Select(s => s.Type));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(999, 50)]
    [InlineData(7, 7)]
    public void Bug_count_is_clamped(int written, int expected)
    {
        Assert.Equal(expected, SettingsSerializer.Deserialize($$"""{"BugCount":{{written}}}""").BugCount);
    }

    [Theory]
    [InlineData(45, 60)]
    [InlineData(30, 30)]
    [InlineData(120, 120)]
    public void Only_the_three_allowed_frame_rates_survive(int written, int expected)
    {
        Assert.Equal(expected, SettingsSerializer.Deserialize($$"""{"FrameRate":{{written}}}""").FrameRate);
    }

    [Fact]
    public void An_unknown_type_change_becomes_respawn_all()
    {
        var options = SettingsSerializer.Deserialize("""{"OnTypeChange":"Sideways"}""");

        Assert.Equal(TypeChangeBehavior.RespawnAll, options.OnTypeChange);
    }

    [Fact]
    public void An_unknown_property_is_ignored()
    {
        var options = SettingsSerializer.Deserialize("""{"BugCount":9,"FutureSetting":true}""");

        Assert.Equal(9, options.BugCount);
    }
}
