namespace ScreenBugs.Core.Simulation;

/// <summary>
/// What a spawning bug gets from the options: its species, and which slot it came from.
/// The slot index rather than a speed value, so a bug follows its row's speed slider live.
/// </summary>
public readonly record struct SpawnChoice(BugSpecies Species, int SlotIndex);
