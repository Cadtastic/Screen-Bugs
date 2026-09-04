namespace ScreenBugs.Core.Simulation;

/// <summary>Decides what each new bug is. Lets the simulation stay ignorant of the options.</summary>
public interface ISpeciesSource
{
    /// <summary>Picks a species and reports the slot it came from.</summary>
    SpawnChoice Next();

    /// <summary>
    /// The current speed multiplier for a slot, read every frame so a slider drag takes effect
    /// on bugs already on screen. Out-of-range indices fall back to the default.
    /// </summary>
    float SpeedFor(int slotIndex);
}
