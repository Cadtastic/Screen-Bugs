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

    [Fact]
    public void After_a_squash_the_population_is_restored_within_eight_and_a_half_seconds()
    {
        var sim = SimulationSteps.Create(3);
        int maxIdBefore = sim.Bugs.Max(b => b.Id);
        Assert.True(sim.TrySquashAt(sim.Bugs[0].Position));
        Assert.Equal(2, SimulationSteps.AliveCount(sim));

        SimulationSteps.StepFor(sim, 8.5f);

        Assert.Equal(3, SimulationSteps.AliveCount(sim));
        Assert.True(sim.Bugs.Max(b => b.Id) > maxIdBefore);
    }

    [Fact]
    public void Respawn_timer_starts_after_a_death_and_is_cancelled_by_a_count_change()
    {
        var sim = SimulationSteps.Create(3);
        sim.TrySquashAt(sim.Bugs[0].Position);
        Assert.Null(sim.RespawnTimer);

        sim.Step(SimulationSteps.Dt, null);
        Assert.NotNull(sim.RespawnTimer);
        Assert.InRange(sim.RespawnTimer!.Value, 3f, 8f);

        sim.TargetCount = 5;

        Assert.Null(sim.RespawnTimer);
        Assert.Equal(5, SimulationSteps.AliveCount(sim));
        for (int i = 0; i < 600; i++)
        {
            sim.Step(SimulationSteps.Dt, null);
            Assert.True(SimulationSteps.AliveCount(sim) <= 5, $"exceeded target at step {i}");
        }
    }

    [Fact]
    public void Raising_the_target_spawns_immediately_and_lowering_removes_alive_bugs_only()
    {
        var sim = SimulationSteps.Create(3);

        sim.TargetCount = 10;
        Assert.Equal(10, SimulationSteps.AliveCount(sim));

        sim.TrySquashAt(sim.Bugs[0].Position);
        sim.TargetCount = 1;

        Assert.Equal(1, SimulationSteps.AliveCount(sim));
        Assert.Contains(sim.Bugs, b => b.State == BugState.Squashed);
        Assert.Equal(1, sim.TargetCount);
    }

    [Fact]
    public void Bug_that_never_enters_the_screen_is_removed_after_ten_seconds_and_replaced()
    {
        var sim = SimulationSteps.Create(0);
        var straggler = sim.AddBug(SimulationSteps.Walker, new Vector2(-30, 540), MathF.PI);
        straggler.State = BugState.Pausing;
        straggler.StateTime = 0f;
        straggler.PauseDuration = 100f;
        sim.TargetCount = 1;
        Assert.Single(sim.Bugs);

        SimulationSteps.StepFor(sim, 10.5f);
        Assert.DoesNotContain(straggler, sim.Bugs);

        SimulationSteps.StepFor(sim, 8.5f);
        Assert.Single(sim.Bugs);
        Assert.NotSame(straggler, sim.Bugs[0]);
    }
}
