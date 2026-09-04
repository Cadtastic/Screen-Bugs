namespace ScreenBugs.Core.Settings;

/// <summary>What a launch starts with, and what to do about startup registration.</summary>
/// <param name="StartAtLogin">Null to leave the Run key alone; otherwise the state to apply.</param>
public readonly record struct SeedOutcome(BugOptions Options, bool? StartAtLogin);
