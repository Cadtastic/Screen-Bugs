namespace ScreenBugs.Tests;

public sealed class SettingsSerializerTests
{
    private static readonly BugTypeSlot Ant = new(SpeciesId.BlackGardenAnt);
    private static readonly BugTypeSlot Centipede = new(SpeciesId.Centipede);

    [Fact]
    public void Round_trip_preserves_every_field()
    {
        var original = new BugOptions(
            [Ant, BugTypeSlot.Random, new BugTypeSlot(SpeciesId.PrayingMantis)],
            BugCount: 7,
            FrameRate: 120,
            TypeChangeBehavior.AgeOut);

        Assert.Equal(original, SettingsSerializer.Deserialize(SettingsSerializer.Serialize(original)));
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

        Assert.Equal([Centipede], options.TypeSlots);
        Assert.Equal(5, options.BugCount);
    }

    [Fact]
    public void Unknown_and_null_slot_names_are_dropped()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["Centipede","99","Unicorn",null,5]}""");

        Assert.Equal([Centipede], options.TypeSlots);
    }

    [Fact]
    public void Slot_names_are_case_insensitive()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["random","blackgardenant"]}""");

        Assert.Equal([BugTypeSlot.Random, Ant], options.TypeSlots);
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

        Assert.Equal([Centipede], options.TypeSlots);
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
