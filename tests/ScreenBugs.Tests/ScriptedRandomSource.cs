namespace ScreenBugs.Tests;

/// <summary>An <see cref="IRandomSource"/> returning queued integers, so a test can force exact draws.</summary>
internal sealed class ScriptedRandomSource(params int[] values) : IRandomSource
{
    private readonly Queue<int> queued = new(values);

    public float NextFloat() => 0f;

    public float NextFloat(float min, float max) => min;

    public int NextInt(int maxExclusive)
    {
        if (queued.Count == 0)
        {
            throw new InvalidOperationException("ScriptedRandomSource ran out of queued values.");
        }

        return Math.Clamp(queued.Dequeue(), 0, maxExclusive - 1);
    }
}
