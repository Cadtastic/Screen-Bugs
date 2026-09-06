namespace ScreenBugs.Rendering;

/// <summary>
/// The undulation of one species in its specimen space: which Y is the head, which is the tail,
/// how far each end swings, and how many wavelengths fit along the body. Amplitude grows
/// linearly from head to tail so the head tracks straight while the rear swings widest.
/// <paramref name="HeadY"/> and <paramref name="TailY"/> must differ; equal ends would divide by
/// zero.
/// </summary>
public sealed record BodyWave(double HeadY, double TailY, double HeadAmplitudeDips, double TailAmplitudeDips, double Wavelengths)
{
    /// <summary>
    /// Sideways offset in specimen units for a body point at specimen <paramref name="y"/>.
    /// Y beyond either end is clamped, so antennae move with the head and hind legs with the tail.
    /// </summary>
    public double OffsetAt(double y, float legPhase, double scale)
    {
        double along = Math.Clamp((y - HeadY) / (TailY - HeadY), 0.0, 1.0);
        double amplitude = HeadAmplitudeDips + ((TailAmplitudeDips - HeadAmplitudeDips) * along);
        return BodyMotion.Undulate(legPhase, scale, along, amplitude, Wavelengths);
    }
}
