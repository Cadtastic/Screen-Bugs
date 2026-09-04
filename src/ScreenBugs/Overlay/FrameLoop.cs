using System.Windows.Media;

namespace ScreenBugs.Overlay;

/// <summary>Calls <paramref name="tick"/> with the elapsed seconds, at most 60 times per second, from WPF's rendering callback.</summary>
public sealed class FrameLoop(Action<float> tick)
{
    private const double Interval = 1.0 / 60.0;

    private TimeSpan? lastRenderingTime;
    private TimeSpan lastTickTime;
    private double accumulator;

    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        lastRenderingTime = null;
        accumulator = 0;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        TimeSpan now = ((RenderingEventArgs)e).RenderingTime;
        if (lastRenderingTime is not { } last)
        {
            lastRenderingTime = now;
            lastTickTime = now;
            return;
        }

        accumulator += (now - last).TotalSeconds;
        lastRenderingTime = now;
        if (accumulator < Interval)
        {
            return;
        }

        // Carry the remainder rather than resetting, so 120 Hz and 144 Hz monitors
        // both settle at a steady 60 ticks per second.
        accumulator = Math.Min(accumulator - Interval, Interval);
        float dt = (float)(now - lastTickTime).TotalSeconds;
        lastTickTime = now;
        tick(dt);
    }
}
