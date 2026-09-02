using System.Numerics;

namespace ScreenBugs.Core.Simulation;

/// <summary>One bug's mutable state. Owned and stepped by <see cref="BugSimulation"/>.</summary>
public sealed class Bug(int id, BugSpecies species, int seed)
{
    public int Id => id;

    public BugSpecies Species => species;

    /// <summary>
    /// Stable per-bug seed for visual variation and the splat shape. An initialized property
    /// (not <c>=> seed</c>) because <c>seed</c> is also used in the <see cref="SpeedFactor"/>
    /// initializer; capturing it as well would trigger CS9124.
    /// </summary>
    public int Seed { get; } = seed;

    /// <summary>Multiplies walk and flee speed; in [0.85, 1.15] and fixed by <see cref="Seed"/>.</summary>
    public float SpeedFactor { get; } = 0.85f + 0.30f * new Random(seed).NextSingle();

    public Vector2 Position { get; internal set; }

    /// <summary>Radians; 0 points right (+X), positive turns clockwise on screen (Y is down).</summary>
    public float Heading { get; internal set; }

    public float TargetHeading { get; internal set; }

    /// <summary>Current speed in DIPs per second.</summary>
    public float Speed { get; internal set; }

    public BugState State { get; internal set; } = BugState.Wandering;

    /// <summary>Seconds spent in the current state.</summary>
    public float StateTime { get; internal set; }

    /// <summary>Leg cycle position in [0, 1); advances with distance traveled.</summary>
    public float LegPhase { get; internal set; }

    /// <summary>Seconds until the bug reacts to a nearby cursor; null when the cursor is not close.</summary>
    public float? ReactionTimer { get; internal set; }

    /// <summary>Seconds the cursor has been far away while fleeing.</summary>
    public float FleeSafeTime { get; internal set; }

    /// <summary>0 to 1 while squashed; the bug is removed at 1.</summary>
    public float SquashProgress { get; internal set; }

    /// <summary>Seconds until the next wander retarget.</summary>
    public float RetargetTimer { get; internal set; }

    /// <summary>Length of the current pause in seconds.</summary>
    public float PauseDuration { get; internal set; }

    /// <summary>Radians added to the flee direction; redrawn every 0.3 s.</summary>
    public float FleeJitter { get; internal set; }

    public float FleeJitterTimer { get; internal set; }

    /// <summary>Seconds since spawn.</summary>
    public float Age { get; internal set; }

    /// <summary>True once the bug has been inside the screen; enables the edge clamp.</summary>
    public bool HasEnteredScreen { get; internal set; }

    public bool IsAlive => State != BugState.Squashed;

    public bool HitTest(Vector2 point) =>
        IsAlive && Vector2.Distance(point, Position) <= species.HitRadius;
}
