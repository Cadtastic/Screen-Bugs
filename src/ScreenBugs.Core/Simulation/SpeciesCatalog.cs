namespace ScreenBugs.Core.Simulation;

/// <summary>The nine species and their tuning (spec 5.1 table).</summary>
public static class SpeciesCatalog
{
    public static IReadOnlyList<BugSpecies> All { get; } =
    [
        //             Id,                          Body, Hit, Walk, Flee, Turn, FleeR, ReactMin, ReactMax, Pause/s, PauseMin, PauseMax
        new(SpeciesId.HissingCockroach,  44f, 26f, 110f, 330f, 5.0f, 180f, 0.10f, 0.25f, 0.20f, 0.5f, 2.0f),
        // The ants' hit radius is generous relative to their bodies: at 16 DIPs long they are the
        // hardest to click, so their target is sized closer to the spider's.
        new(SpeciesId.BlackGardenAnt,    16f, 22f,  70f, 175f, 6.0f, 120f, 0.10f, 0.25f, 0.50f, 0.3f, 1.2f),
        new(SpeciesId.RedFireAnt,        15f, 22f,  80f, 200f, 6.0f, 120f, 0.10f, 0.25f, 0.50f, 0.3f, 1.2f),
        new(SpeciesId.PrayingMantis,     56f, 24f,  25f,  50f, 2.0f,  90f, 0.20f, 0.40f, 0.80f, 1.0f, 4.0f),
        new(SpeciesId.SevenSpotLadybug,  22f, 16f,  40f,  80f, 3.0f, 100f, 0.10f, 0.25f, 0.30f, 0.5f, 2.0f),
        new(SpeciesId.StagBeetle,        40f, 22f,  30f,  55f, 2.0f,  90f, 0.10f, 0.25f, 0.30f, 0.5f, 2.0f),
        new(SpeciesId.HouseSpider,       34f, 24f,  90f, 270f, 8.0f, 150f, 0.05f, 0.15f, 1.00f, 0.8f, 3.0f),
        new(SpeciesId.Centipede,         50f, 22f,  60f, 150f, 3.0f, 130f, 0.10f, 0.25f, 0.15f, 0.5f, 2.0f),
        new(SpeciesId.StinkBug,          28f, 18f,  35f,  70f, 2.5f, 100f, 0.10f, 0.25f, 0.40f, 0.5f, 2.0f),
    ];

    public static BugSpecies Get(SpeciesId id) => All.First(s => s.Id == id);
}
