namespace ScreenBugs.Core.Simulation;

/// <summary>Source of randomness for the simulation. Seeded implementations make runs reproducible.</summary>
public interface IRandomSource
{
    /// <summary>Uniform value in [0, 1).</summary>
    float NextFloat();

    /// <summary>Uniform value in [min, max).</summary>
    float NextFloat(float min, float max);

    /// <summary>Uniform integer in [0, maxExclusive).</summary>
    int NextInt(int maxExclusive);
}
