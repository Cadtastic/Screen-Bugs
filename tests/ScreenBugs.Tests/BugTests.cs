namespace ScreenBugs.Tests;

public sealed class BugTests
{
    private static readonly BugSpecies Ant = SpeciesCatalog.Get(SpeciesId.BlackGardenAnt);

    [Fact]
    public void New_bug_is_wandering_and_alive()
    {
        var bug = new Bug(1, Ant, seed: 42);

        Assert.Equal(BugState.Wandering, bug.State);
        Assert.True(bug.IsAlive);
        Assert.Equal(1, bug.Id);
        Assert.Same(Ant, bug.Species);
    }

    [Fact]
    public void HitTest_uses_the_species_hit_radius()
    {
        var bug = new Bug(1, Ant, seed: 42) { Position = new Vector2(100, 100) };

        Assert.True(bug.HitTest(new Vector2(100, 100)));
        Assert.True(bug.HitTest(new Vector2(100 + Ant.HitRadius, 100)));
        Assert.False(bug.HitTest(new Vector2(100 + Ant.HitRadius + 0.5f, 100)));
    }

    [Fact]
    public void Squashed_bug_is_not_alive_and_never_hit()
    {
        var bug = new Bug(1, Ant, seed: 42) { Position = new Vector2(100, 100), State = BugState.Squashed };

        Assert.False(bug.IsAlive);
        Assert.False(bug.HitTest(new Vector2(100, 100)));
    }

    [Fact]
    public void SpeedFactor_is_in_range_and_determined_by_seed()
    {
        var a = new Bug(1, Ant, seed: 7);
        var b = new Bug(2, Ant, seed: 7);
        var c = new Bug(3, Ant, seed: 8);

        Assert.InRange(a.SpeedFactor, 0.85f, 1.15f);
        Assert.Equal(a.SpeedFactor, b.SpeedFactor);
        Assert.NotEqual(a.SpeedFactor, c.SpeedFactor);
    }
}
