namespace ScreenBugs.Tests;

public sealed class BodyMotionTests
{
    // LegPhase is a float, so phase arithmetic is only good to about 1e-7.
    private const double Tolerance = 1e-6;

    [Fact]
    public void Bob_is_one_dip_at_twice_the_leg_frequency()
    {
        // Rigid species keep Bob exactly as it was: a full DIP at an eighth of a stride.
        Assert.Equal(1.0, BodyMotion.Bob(0.125f, 1.0), Tolerance);
        Assert.Equal(2.0, BodyMotion.Bob(0.125f, 0.5), Tolerance);
    }

    [Fact]
    public void Undulate_is_zero_at_the_head_at_phase_zero()
    {
        Assert.Equal(0.0, BodyMotion.Undulate(0f, 1.0, 0.0, 3.0, 1.0), Tolerance);
    }

    [Fact]
    public void Undulate_amplitude_is_in_dips_divided_by_the_painter_scale()
    {
        // A quarter stride is the crest at the head.
        Assert.Equal(3.0, BodyMotion.Undulate(0.25f, 1.0, 0.0, 3.0, 1.0), Tolerance);
        Assert.Equal(6.0, BodyMotion.Undulate(0.25f, 0.5, 0.0, 3.0, 1.0), Tolerance);
    }

    [Fact]
    public void Undulate_lags_toward_the_tail_so_the_crest_travels_head_to_tail()
    {
        // A point a quarter of the way down a one-wavelength body sees the wave a quarter
        // stride late: what the head did at phase 0.1, that point does at phase 0.35.
        double head = BodyMotion.Undulate(0.1f, 1.0, 0.0, 2.0, 1.0);
        double later = BodyMotion.Undulate(0.35f, 1.0, 0.25, 2.0, 1.0);
        Assert.Equal(head, later, Tolerance);
    }

    [Fact]
    public void Undulate_wavelengths_scale_the_lag()
    {
        // With two wavelengths along the body the same point lags half a stride.
        double head = BodyMotion.Undulate(0.1f, 1.0, 0.0, 2.0, 2.0);
        double later = BodyMotion.Undulate(0.6f, 1.0, 0.25, 2.0, 2.0);
        Assert.Equal(head, later, Tolerance);
    }

    [Fact]
    public void Undulate_peaks_at_the_amplitude_over_a_full_stride()
    {
        double peak = Enumerable.Range(0, 400)
            .Select(i => Math.Abs(BodyMotion.Undulate(i / 400f, 2.0, 0.6, 3.0, 1.0)))
            .Max();
        Assert.Equal(1.5, peak, 1e-3);
    }
}
