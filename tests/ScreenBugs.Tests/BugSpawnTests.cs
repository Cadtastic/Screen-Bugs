namespace ScreenBugs.Tests;

public sealed class BugSpawnTests
{
    [Fact]
    public void Setting_TargetCount_spawns_the_requested_number_of_bugs()
    {
        var sim = SimulationSteps.Create(5);

        Assert.Equal(5, sim.TargetCount);
        Assert.Equal(5, sim.Bugs.Count);
        Assert.Equal(5, SimulationSteps.AliveCount(sim));
        Assert.Equal(5, sim.Bugs.Select(b => b.Id).Distinct().Count());
    }

    [Fact]
    public void Spawned_bugs_start_outside_the_screen_heading_inward()
    {
        var sim = SimulationSteps.Create(20);

        foreach (var bug in sim.Bugs)
        {
            Assert.False(SimulationSteps.Screen.Contains(bug.Position));
            Assert.False(bug.HasEnteredScreen);
            var toCenter = SimulationSteps.Screen.Center - bug.Position;
            Assert.True(Vector2.Dot(SimulationSteps.Direction(bug.Heading), toCenter) > 0f);
        }
    }

    [Fact]
    public void Spawned_bugs_are_placed_one_body_length_outside_an_edge()
    {
        var sim = SimulationSteps.Create(20);

        foreach (var bug in sim.Bugs)
        {
            float off = bug.Species.BodyLength;
            bool onLeft = bug.Position.X == -off;
            bool onRight = bug.Position.X == SimulationSteps.Screen.Width + off;
            bool onTop = bug.Position.Y == -off;
            bool onBottom = bug.Position.Y == SimulationSteps.Screen.Height + off;
            Assert.True(onLeft || onRight || onTop || onBottom, $"bug {bug.Id} at {bug.Position}");
        }
    }

    [Fact]
    public void AddBug_places_a_wandering_bug_exactly_where_asked()
    {
        var sim = SimulationSteps.Create(0);

        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(300, 400), 1.5f);

        Assert.Single(sim.Bugs);
        Assert.Equal(new Vector2(300, 400), bug.Position);
        Assert.Equal(1.5f, bug.Heading);
        Assert.Equal(BugState.Wandering, bug.State);
        Assert.True(bug.HasEnteredScreen);
    }

    [Fact]
    public void HitTest_returns_the_nearest_overlapping_bug_or_null()
    {
        var sim = SimulationSteps.Create(0);
        var a = sim.AddBug(SimulationSteps.Walker, new Vector2(500, 500), 0f);
        var b = sim.AddBug(SimulationSteps.Walker, new Vector2(510, 500), 0f);

        Assert.Same(a, sim.HitTest(new Vector2(503, 500)));
        Assert.Same(b, sim.HitTest(new Vector2(507, 500)));
        Assert.Null(sim.HitTest(new Vector2(600, 600)));
    }
}
