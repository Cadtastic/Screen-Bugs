namespace ScreenBugs.Tests;

public sealed class BodyWaveTests
{
    // Scale 1 throughout, so every offset is in DIPs.
    private static readonly BodyWave Wave = new(HeadY: -70, TailY: 60, HeadAmplitudeDips: 0.5, TailAmplitudeDips: 3.0, Wavelengths: 1.0);

    private static double Peak(double y) => Enumerable.Range(0, 400)
        .Select(i => Math.Abs(Wave.OffsetAt(y, i / 400f, 1.0)))
        .Max();

    [Fact]
    public void Peak_swing_at_the_head_is_the_head_amplitude()
    {
        Assert.Equal(0.5, Peak(-70), 1e-3);
    }

    [Fact]
    public void Peak_swing_at_the_tail_is_the_tail_amplitude()
    {
        Assert.Equal(3.0, Peak(60), 1e-3);
    }

    [Fact]
    public void Peak_swing_halfway_is_the_mean_of_the_two()
    {
        Assert.Equal(1.75, Peak(-5), 1e-3);
    }

    [Fact]
    public void Points_beyond_either_end_move_with_that_end()
    {
        // Antennae sit ahead of the head and hind legs behind the tail; both must stay attached.
        Assert.Equal(Wave.OffsetAt(-70, 0.3f, 1.0), Wave.OffsetAt(-90, 0.3f, 1.0));
        Assert.Equal(Wave.OffsetAt(60, 0.3f, 1.0), Wave.OffsetAt(80, 0.3f, 1.0));
    }

    [Fact]
    public void Equal_head_and_tail_amplitude_reduces_to_undulate()
    {
        var flat = new BodyWave(HeadY: 0, TailY: 100, HeadAmplitudeDips: 2.0, TailAmplitudeDips: 2.0, Wavelengths: 1.5);
        Assert.Equal(
            BodyMotion.Undulate(0.4f, 0.5, 0.25, 2.0, 1.5),
            flat.OffsetAt(25, 0.4f, 0.5),
            1e-9);
    }
}
