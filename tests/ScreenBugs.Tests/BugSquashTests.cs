namespace ScreenBugs.Tests;

public sealed class BugSquashTests
{
    [Fact]
    public void TrySquashAt_on_a_bug_squashes_it_and_returns_true()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);

        Assert.True(sim.TrySquashAt(new Vector2(965, 540)));
        Assert.Equal(BugState.Squashed, bug.State);
        Assert.False(bug.IsAlive);
        Assert.Equal(0f, bug.SquashProgress);
    }

    [Fact]
    public void TrySquashAt_on_empty_space_returns_false()
    {
        var sim = SimulationSteps.Create(0);
        sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);

        Assert.False(sim.TrySquashAt(new Vector2(100, 100)));
        Assert.Equal(1, SimulationSteps.AliveCount(sim));
    }

    [Fact]
    public void Squashed_bug_fades_and_is_removed_within_two_seconds()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);
        sim.TrySquashAt(bug.Position);

        SimulationSteps.StepFor(sim, 1f);
        Assert.Contains(bug, sim.Bugs);
        Assert.InRange(bug.SquashProgress, 0.6f, 0.7f);

        SimulationSteps.StepFor(sim, 1f);
        Assert.DoesNotContain(bug, sim.Bugs);
    }

    [Fact]
    public void Squashed_bug_cannot_be_squashed_again()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);
        sim.TrySquashAt(bug.Position);

        Assert.False(sim.TrySquashAt(bug.Position));
    }
}
