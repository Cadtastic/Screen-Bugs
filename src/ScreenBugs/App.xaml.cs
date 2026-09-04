using System.Windows;
using ScreenBugs.Overlay;

namespace ScreenBugs;

public partial class App : Application
{
    private const int InitialBugCount = 3;

    private OverlayWindow? overlay;
    private FrameLoop? frameLoop;
    private TopmostKeeper? topmostKeeper;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var bounds = new Bounds((float)SystemParameters.PrimaryScreenWidth, (float)SystemParameters.PrimaryScreenHeight);
        var simulation = new BugSimulation(bounds, new SystemRandomSource()) { TargetCount = InitialBugCount };

        var window = new OverlayWindow();
        window.Surface.Simulation = simulation;
        window.Show();
        overlay = window;

        var clickThrough = new ClickThroughController(window.Handle);
        topmostKeeper = new TopmostKeeper(window.Handle);
        frameLoop = new FrameLoop(dt =>
        {
            Vector2? cursor = CursorTracker.GetCursorDips(window);
            simulation.Step(dt, cursor);
            clickThrough.Update(cursor is { } c && simulation.HitTest(c) is not null);
            window.Surface.Redraw();
        });

        frameLoop.Start();
        topmostKeeper.Start();
    }
}
