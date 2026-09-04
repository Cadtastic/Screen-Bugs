namespace ScreenBugs.Core.Settings;

/// <summary>What happens to bugs already on screen when the selected types change.</summary>
public enum TypeChangeBehavior
{
    /// <summary>Clear the screen and walk a fresh population in.</summary>
    RespawnAll,

    /// <summary>Leave them; only replacements use the new types.</summary>
    AgeOut,
}
