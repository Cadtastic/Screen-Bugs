namespace ScreenBugs.Tests;

/// <summary>The per-row speed multiplier reaching actual bug motion.</summary>
public sealed class BugSpeedTests
{
    private static (BugSimulation Sim, SlotSpeciesSource Source) Create(params SlotSetting[] slots)
    {
        var rng = new SystemRandomSource(1234);
        var source = new SlotSpeciesSource(rng) { Slots = slots };
        return (new BugSimulation(SimulationSteps.Screen, rng, source), source);
    }

    /// <summary>
    /// Total path length, not straight-line displacement: a bug curves as it walks, so
    /// displacement understates speed and does so unevenly at different speeds.
    /// </summary>
    private static float PathOverOneSecond(BugSimulation sim)
    {
        var bug = sim.Bugs[0];
        float travelled = 0f;
        for (int i = 0; i < 60; i++)
        {
            var before = bug.Position;
            sim.Step(SimulationSteps.Dt, null);
            travelled += Vector2.Distance(before, bug.Position);
        }

        return travelled;
    }

    /// <summary>Placed mid-screen so neither bug reaches the edge-steering margin within a second.</summary>
    private static BugSimulation WalkerAtCentre(float speed)
    {
        var (sim, _) = Create(SlotSetting.For(SpeciesId.BlackGardenAnt, speed));
        sim.AddBug(SpeciesCatalog.Get(SpeciesId.BlackGardenAnt), SimulationSteps.Screen.Center, 0f, slotIndex: 0);
        return sim;
    }

    [Fact]
    public void A_row_at_double_speed_moves_its_bugs_exactly_twice_as_far()
    {
        float slow = PathOverOneSecond(WalkerAtCentre(1f));
        float fast = PathOverOneSecond(WalkerAtCentre(2f));

        // Same seed and same start, so the heading sequence matches and the ratio is the multiplier.
        Assert.Equal(2f, fast / slow, 3);
    }

    [Fact]
    public void The_slowest_and_fastest_rows_differ_by_the_full_range()
    {
        float slowest = PathOverOneSecond(WalkerAtCentre(SlotSetting.MinSpeed));
        float fastest = PathOverOneSecond(WalkerAtCentre(SlotSetting.MaxSpeed));

        // Two places, not three: summing 60 steps at a 12x ratio drifts in the last float digit.
        Assert.Equal(SlotSetting.MaxSpeed / SlotSetting.MinSpeed, fastest / slowest, 2);
    }

    [Fact]
    public void Changing_a_rows_speed_reaches_bugs_already_on_screen()
    {
        var (sim, source) = Create(SlotSetting.For(SpeciesId.BlackGardenAnt, 1f));
        sim.AddBug(SpeciesCatalog.Get(SpeciesId.BlackGardenAnt), SimulationSteps.Screen.Center, 0f, slotIndex: 0);
        float atNormalSpeed = PathOverOneSecond(sim);

        source.Slots = [SlotSetting.For(SpeciesId.BlackGardenAnt, 3f)];
        float afterSpeedUp = PathOverOneSecond(sim);

        Assert.True(afterSpeedUp > atNormalSpeed * 2.5f, $"{atNormalSpeed} then {afterSpeedUp}");
    }

    [Fact]
    public void Each_row_keeps_its_own_speed()
    {
        var (sim, source) = Create(
            SlotSetting.For(SpeciesId.BlackGardenAnt, 0.25f),
            SlotSetting.For(SpeciesId.PrayingMantis, 3f));

        Assert.Equal(0.25f, source.SpeedFor(0));
        Assert.Equal(3f, source.SpeedFor(1));

        sim.TargetCount = 6;
        foreach (var bug in sim.Bugs)
        {
            Assert.InRange(bug.SlotIndex, 0, 1);
        }
    }

    [Fact]
    public void A_bug_placed_directly_by_a_test_runs_at_the_default_speed()
    {
        var (sim, _) = Create(SlotSetting.For(SpeciesId.BlackGardenAnt, 3f));

        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);

        Assert.Equal(-1, bug.SlotIndex);
        Assert.Equal(SimulationSteps.Walker.WalkSpeed * bug.SpeedFactor, bug.Speed, 3);
    }
}
