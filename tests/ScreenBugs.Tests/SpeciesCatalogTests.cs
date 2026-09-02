namespace ScreenBugs.Tests;

public sealed class SpeciesCatalogTests
{
    [Fact]
    public void Catalog_has_nine_distinct_species()
    {
        Assert.Equal(9, SpeciesCatalog.All.Count);
        Assert.Equal(9, SpeciesCatalog.All.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void Get_returns_the_species_with_that_id()
    {
        foreach (var id in Enum.GetValues<SpeciesId>())
        {
            Assert.Equal(id, SpeciesCatalog.Get(id).Id);
        }
    }

    [Fact]
    public void Every_species_has_sane_positive_tuning()
    {
        foreach (var s in SpeciesCatalog.All)
        {
            Assert.True(s.BodyLength > 0, s.Id.ToString());
            Assert.True(s.HitRadius > 0, s.Id.ToString());
            Assert.True(s.WalkSpeed > 0, s.Id.ToString());
            Assert.True(s.FleeSpeed > s.WalkSpeed, s.Id.ToString());
            Assert.True(s.TurnRate > 0, s.Id.ToString());
            Assert.True(s.FleeRadius > 0, s.Id.ToString());
            Assert.True(s.ReactionDelayMin > 0, s.Id.ToString());
            Assert.True(s.ReactionDelayMax >= s.ReactionDelayMin, s.Id.ToString());
            Assert.True(s.PauseChancePerSecond > 0, s.Id.ToString());
            Assert.True(s.PauseMin > 0, s.Id.ToString());
            Assert.True(s.PauseMax >= s.PauseMin, s.Id.ToString());
            Assert.Equal(0.6f * s.BodyLength, s.StrideLength);
        }
    }
}
