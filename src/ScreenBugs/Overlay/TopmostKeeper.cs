using System.Windows.Threading;

namespace ScreenBugs.Overlay;

/// <summary>Re-asserts HWND_TOPMOST every two seconds so windows that become topmost later do not bury the overlay.</summary>
public sealed class TopmostKeeper(IntPtr hwnd)
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool subscribed;

    public void Start()
    {
        if (!subscribed)
        {
            timer.Tick += (_, _) => NativeMethods.BringToTopmost(hwnd);
            subscribed = true;
        }

        timer.Start();
    }

    public void Stop() => timer.Stop();
}
