using ScreenBugs.Rendering.Painters;

namespace ScreenBugs.Rendering;

/// <summary>Maps each <see cref="SpeciesId"/> to its painter.</summary>
public sealed class BugPainterRegistry
{
    private readonly Dictionary<SpeciesId, IBugPainter> painters = new()
    {
        [SpeciesId.HissingCockroach] = new HissingCockroachPainter(),
        [SpeciesId.BlackGardenAnt] = new BlackGardenAntPainter(),
        [SpeciesId.RedFireAnt] = new RedFireAntPainter(),
        [SpeciesId.PrayingMantis] = new PrayingMantisPainter(),
        [SpeciesId.SevenSpotLadybug] = new SevenSpotLadybugPainter(),
        [SpeciesId.StagBeetle] = new StagBeetlePainter(),
        [SpeciesId.HouseSpider] = new HouseSpiderPainter(),
        [SpeciesId.Centipede] = new CentipedePainter(),
        [SpeciesId.StinkBug] = new StinkBugPainter(),
    };

    public IBugPainter Get(SpeciesId id) => painters[id];
}
