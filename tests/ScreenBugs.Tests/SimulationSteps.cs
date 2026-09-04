namespace ScreenBugs.Tests;

internal static class SimulationSteps
{
    public const float Dt = 1f / 60f;

    public static readonly Bounds Screen = new(1920, 1080);

    /// <summary>A black garden ant that never pauses by chance, for deterministic movement tests.</summary>
    public static readonly BugSpecies Walker =
        SpeciesCatalog.Get(SpeciesId.BlackGardenAnt) with { PauseChancePerSecond = 0f };

    public static BugSimulation Create(int count, int seed = 1234)
    {
        // A default SlotSpeciesSource holds one Random slot, reproducing the pre-options uniform choice.
        var rng = new SystemRandomSource(seed);
        return new BugSimulation(Screen, rng, new SlotSpeciesSource(rng)) { TargetCount = count };
    }

    /// <summary>Steps the simulation at 60 Hz for at least <paramref name="seconds"/>.</summary>
    public static void StepFor(BugSimulation sim, float seconds, Vector2? cursor = null)
    {
        int steps = (int)MathF.Ceiling(seconds / Dt);
        for (int i = 0; i < steps; i++)
        {
            sim.Step(Dt, cursor);
        }
    }

    public static Vector2 Direction(float heading) => new(MathF.Cos(heading), MathF.Sin(heading));

    public static int AliveCount(BugSimulation sim) => sim.Bugs.Count(b => b.IsAlive);
}
