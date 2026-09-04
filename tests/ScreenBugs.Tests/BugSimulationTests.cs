namespace ScreenBugs.Tests;

public sealed class BugSimulationTests
{
    [Fact]
    public void Walking_bug_moves_along_its_heading_and_advances_its_legs()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);

        SimulationSteps.StepFor(sim, 0.5f);

        Assert.True(bug.Position.X > 960 + 20, $"moved to {bug.Position}");
        Assert.InRange(bug.Position.Y, 530f, 550f);
        Assert.NotEqual(0f, bug.LegPhase);
    }

    [Fact]
    public void Step_clamps_dt_to_a_tenth_of_a_second()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);

        sim.Step(5f, null);

        float maxTravel = SimulationSteps.Walker.WalkSpeed * 1.15f * 0.1f;
        Assert.True(Vector2.Distance(bug.Position, new Vector2(960, 540)) <= maxTravel + 0.01f);
    }

    [Fact]
    public void Bug_entering_from_outside_is_flagged_and_then_kept_inside()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(-10, 540), 0f);
        Assert.False(bug.HasEnteredScreen);

        SimulationSteps.StepFor(sim, 1f);

        Assert.True(bug.HasEnteredScreen);
        Assert.True(bug.Position.X >= 2f);
    }

    [Fact]
    public void Bugs_that_entered_the_screen_stay_inside_the_inset_bounds()
    {
        var sim = SimulationSteps.Create(10);

        for (int i = 0; i < 20_000; i++)
        {
            sim.Step(SimulationSteps.Dt, null);
            foreach (var bug in sim.Bugs)
            {
                if (!bug.IsAlive || !bug.HasEnteredScreen)
                {
                    continue;
                }

                Assert.InRange(bug.Position.X, 2f, SimulationSteps.Screen.Width - 2f);
                Assert.InRange(bug.Position.Y, 2f, SimulationSteps.Screen.Height - 2f);
            }
        }
    }

    [Fact]
    public void Bug_heading_at_an_edge_is_steered_back_toward_the_screen()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(30, 540), MathF.PI);

        for (int i = 0; i < 120; i++)
        {
            sim.Step(SimulationSteps.Dt, null);
            Assert.True(bug.Position.X >= 2f, $"left the screen at step {i}: {bug.Position}");
        }

        Assert.True(bug.Position.X > 30f, $"did not move back in from the edge: {bug.Position}");
        Assert.True(MathF.Cos(bug.TargetHeading) > 0f, $"wander target {bug.TargetHeading} still points off screen");
    }

    [Fact]
    public void Pausing_bug_is_completely_still_and_its_legs_do_not_move()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(500, 500), 0.4f);
        SimulationSteps.StepFor(sim, 0.2f);
        bug.State = BugState.Pausing;
        bug.StateTime = 0f;
        bug.PauseDuration = 5f;
        var position = bug.Position;
        float heading = bug.Heading;
        float legPhase = bug.LegPhase;

        SimulationSteps.StepFor(sim, 1f);

        Assert.Equal(BugState.Pausing, bug.State);
        Assert.Equal(position, bug.Position);
        Assert.Equal(heading, bug.Heading);
        Assert.Equal(legPhase, bug.LegPhase);
        Assert.Equal(0f, bug.Speed);
    }

    [Fact]
    public void Pausing_bug_returns_to_wandering_when_its_pause_ends()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(500, 500), 0f);
        bug.State = BugState.Pausing;
        bug.StateTime = 0f;
        bug.PauseDuration = 0.5f;

        SimulationSteps.StepFor(sim, 0.4f);
        Assert.Equal(BugState.Pausing, bug.State);

        SimulationSteps.StepFor(sim, 0.2f);
        Assert.Equal(BugState.Wandering, bug.State);
    }

    [Fact]
    public void Wandering_bug_with_pause_chance_eventually_pauses()
    {
        var sim = SimulationSteps.Create(0);
        var ant = SpeciesCatalog.Get(SpeciesId.BlackGardenAnt);
        var bug = sim.AddBug(ant, new Vector2(960, 540), 0f);

        bool paused = false;
        for (int i = 0; i < 60 * 30 && !paused; i++)
        {
            sim.Step(SimulationSteps.Dt, null);
            paused = bug.State == BugState.Pausing;
        }

        Assert.True(paused, "ant never paused in 30 s at 0.5 pauses per second");
        Assert.InRange(bug.PauseDuration, ant.PauseMin, ant.PauseMax);
    }
}
