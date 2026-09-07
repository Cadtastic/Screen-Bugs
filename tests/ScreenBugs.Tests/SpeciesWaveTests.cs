using ScreenBugs.Rendering.Painters;

namespace ScreenBugs.Tests;

/// <summary>
/// The shipped waves, not the maths: issue #1 asks for the tail to swing wider than the head, and
/// nothing else would notice if the two amplitudes were swapped.
/// </summary>
public sealed class SpeciesWaveTests
{
    public static TheoryData<string, BodyWave> Waves => new()
    {
        { "centipede", CentipedePainter.Wave },
        { "ant", AntGeometry.Wave },
    };

    [Theory]
    [MemberData(nameof(Waves))]
    public void Tail_swings_wider_than_head_and_lies_behind_it(string species, BodyWave wave)
    {
        Assert.True(wave.TailY > wave.HeadY, $"{species}: the tail must be behind the head (larger Y).");
        Assert.True(wave.TailAmplitudeDips > wave.HeadAmplitudeDips, $"{species}: the tail must swing wider than the head.");
    }
}
