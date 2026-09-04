using ScreenBugs.Overlay;

namespace ScreenBugs.Settings;

/// <summary>Applies the difference between two option sets to the running overlay.</summary>
public sealed class OptionsApplier(BugSimulation simulation, SlotSpeciesSource species, FrameLoop frameLoop)
{
    /// <summary>
    /// Applies what differs, in the order slots, count, frame rate, so bugs spawned by a count
    /// increase already use the new slots. Returns true if the population was respawned.
    /// </summary>
    public bool Apply(BugOptions previous, BugOptions next, TypeChangeBehavior onSlotChange)
    {
        bool respawned = false;

        if (!previous.TypeSlots.SequenceEqual(next.TypeSlots))
        {
            species.Slots = next.TypeSlots;
            if (onSlotChange == TypeChangeBehavior.RespawnAll)
            {
                simulation.RespawnAll();
                respawned = true;
            }
        }

        if (previous.BugCount != next.BugCount)
        {
            simulation.TargetCount = next.BugCount;
        }

        if (previous.FrameRate != next.FrameRate)
        {
            frameLoop.TargetFrameRate = next.FrameRate;
        }

        return respawned;
    }
}
