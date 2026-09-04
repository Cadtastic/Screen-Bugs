namespace ScreenBugs.Core.Simulation;

public sealed class SystemRandomSource(int? seed = null) : IRandomSource
{
    private readonly Random random = seed is null ? new Random() : new Random(seed.Value);

    public float NextFloat() => random.NextSingle();

    public float NextFloat(float min, float max) => min + (max - min) * random.NextSingle();

    public int NextInt(int maxExclusive) => random.Next(maxExclusive);
}
