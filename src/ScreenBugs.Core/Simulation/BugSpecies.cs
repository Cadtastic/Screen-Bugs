namespace ScreenBugs.Core.Simulation;

public sealed record BugSpecies(
    SpeciesId Id,
    float BodyLength,
    float HitRadius,
    float WalkSpeed,
    float FleeSpeed,
    float TurnRate,
    float FleeRadius,
    float ReactionDelayMin,
    float ReactionDelayMax,
    float PauseChancePerSecond,
    float PauseMin,
    float PauseMax)
{
    /// <summary>DIPs traveled per full leg cycle while walking (spec 5.1).</summary>
    public float StrideLength => 0.6f * BodyLength;
}
