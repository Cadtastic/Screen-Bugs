namespace ScreenBugs.Core.Simulation;

/// <summary>One row of the options: which bug type, and how fast that row's bugs move.</summary>
public readonly record struct SlotSetting(BugTypeSlot Type, float SpeedMultiplier)
{
    public const float DefaultSpeed = 1f;
    public const float MinSpeed = 0.25f;
    public const float MaxSpeed = 3f;

    public SlotSetting(BugTypeSlot type)
        : this(type, DefaultSpeed)
    {
    }

    public static SlotSetting Random => new(BugTypeSlot.Random);

    public static SlotSetting For(SpeciesId species, float speed = DefaultSpeed) =>
        new(new BugTypeSlot(species), speed);

    public static float ClampSpeed(float speed) => Math.Clamp(speed, MinSpeed, MaxSpeed);
}
