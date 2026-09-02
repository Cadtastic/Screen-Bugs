namespace ScreenBugs.Tests;

public sealed class BugFleeTests
{
    private static readonly Vector2 Start = new(960, 540);
    private static readonly Vector2 CursorBehind = new(920, 540);

    [Fact]
    public void Bug_does_not_flee_before_the_minimum_reaction_delay()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, Start, 0f);

        SimulationSteps.StepFor(sim, 0.08f, CursorBehind);

        Assert.NotEqual(BugState.Fleeing, bug.State);
        Assert.NotNull(bug.ReactionTimer);
    }

    [Fact]
    public void Bug_flees_within_half_a_second_and_gets_farther_from_the_cursor()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, Start, 0f);
        float startDistance = Vector2.Distance(CursorBehind, bug.Position);

        SimulationSteps.StepFor(sim, 0.5f, CursorBehind);

        Assert.Equal(BugState.Fleeing, bug.State);
        Assert.True(Vector2.Distance(CursorBehind, bug.Position) > startDistance);
        Assert.Equal(SimulationSteps.Walker.FleeSpeed * bug.SpeedFactor, bug.Speed);
    }

    [Fact]
    public void Cursor_leaving_before_the_reaction_fires_cancels_the_reaction()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, Start, 0f);

        SimulationSteps.StepFor(sim, 0.05f, CursorBehind);
        Assert.NotNull(bug.ReactionTimer);

        sim.Step(SimulationSteps.Dt, null);

        Assert.Null(bug.ReactionTimer);
        Assert.Equal(BugState.Wandering, bug.State);
    }

    [Fact]
    public void Fleeing_ends_with_a_pause_then_wandering_once_the_cursor_is_gone()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, Start, 0f);
        SimulationSteps.StepFor(sim, 0.5f, CursorBehind);
        Assert.Equal(BugState.Fleeing, bug.State);

        var states = new List<BugState>();
        for (int i = 0; i < 60 * 3; i++)
        {
            sim.Step(SimulationSteps.Dt, null);
            states.Add(bug.State);
        }

        int firstPausing = states.IndexOf(BugState.Pausing);
        int firstWandering = states.IndexOf(BugState.Wandering);
        Assert.True(firstPausing >= 0, "never paused after fleeing");
        Assert.True(firstWandering > firstPausing, "did not wander after the pause");
        Assert.Equal(BugState.Fleeing, states[0]);
    }

    [Fact]
    public void Fleeing_bug_ignores_pause_chance_and_keeps_running_while_the_cursor_follows()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SpeciesCatalog.Get(SpeciesId.BlackGardenAnt), Start, 0f);
        SimulationSteps.StepFor(sim, 0.5f, CursorBehind);
        Assert.Equal(BugState.Fleeing, bug.State);

        for (int i = 0; i < 60; i++)
        {
            var chasingCursor = bug.Position - SimulationSteps.Direction(bug.Heading) * 30f;
            sim.Step(SimulationSteps.Dt, chasingCursor);
            Assert.Equal(BugState.Fleeing, bug.State);
        }
    }

    [Fact]
    public void Squashed_bug_does_not_react_to_the_cursor()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, Start, 0f);
        sim.TrySquashAt(Start);
        var position = bug.Position;

        SimulationSteps.StepFor(sim, 0.5f, CursorBehind);

        Assert.Equal(BugState.Squashed, bug.State);
        Assert.Equal(position, bug.Position);
        Assert.Null(bug.ReactionTimer);
    }
}
