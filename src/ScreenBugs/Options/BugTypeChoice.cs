namespace ScreenBugs.Options;

/// <summary>A slot value plus the label shown for it in the dialog's dropdowns.</summary>
public sealed record BugTypeChoice(BugTypeSlot Slot, string Label)
{
    public static BugTypeChoice From(BugTypeSlot slot) => new(slot, LabelFor(slot));

    private static string LabelFor(BugTypeSlot slot) => slot.Species switch
    {
        null => "Random",
        SpeciesId.HissingCockroach => "Hissing cockroach",
        SpeciesId.BlackGardenAnt => "Black garden ant",
        SpeciesId.RedFireAnt => "Red fire ant",
        SpeciesId.PrayingMantis => "Praying mantis",
        SpeciesId.SevenSpotLadybug => "Seven-spot ladybug",
        SpeciesId.StagBeetle => "Stag beetle",
        SpeciesId.HouseSpider => "House spider",
        SpeciesId.Centipede => "Centipede",
        SpeciesId.StinkBug => "Stink bug",
        _ => slot.Species.Value.ToString(),
    };
}
