using System.Windows;
using System.Windows.Threading;
using ScreenBugs.Diagnostics;
using ScreenBugs.Overlay;
using ScreenBugs.Tray;

namespace ScreenBugs;

public partial class App : Application
{
    private const int InitialBugCount = 3;

    private SingleInstanceGuard? instanceGuard;
    private TrayIcon? trayIcon;
    private OverlayWindow? overlay;
    private FrameLoop? frameLoop;
    private TopmostKeeper? topmostKeeper;
    private bool paused;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Must be wired before the first tray window exists: WinForms decides how to treat
        // handler exceptions when its first window callback runs.
        TrayIcon.RouteThreadExceptions(HandleFatalException);

        instanceGuard = new SingleInstanceGuard();
        if (!instanceGuard.TryAcquire())
        {
            Shutdown();
            return;
        }

        var bounds = new Bounds((float)SystemParameters.PrimaryScreenWidth, (float)SystemParameters.PrimaryScreenHeight);
        var rng = new SystemRandomSource();
        var speciesSource = new SlotSpeciesSource(rng);
        var simulation = new BugSimulation(bounds, rng, speciesSource) { TargetCount = InitialBugCount };

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
            bool squashable = trayIcon?.IsMenuOpen != true && cursor is { } c && simulation.HitTest(c) is not null;
            clickThrough.Update(squashable);
            window.Surface.Redraw();
        });

        trayIcon = new TrayIcon();
        trayIcon.PauseToggled += TogglePause;
        trayIcon.ExitRequested += () => Shutdown();

        frameLoop.Start();
        topmostKeeper.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        trayIcon?.Dispose();
        instanceGuard?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Hiding the window keeps its native handle, so the extended styles survive a pause.</summary>
    private void TogglePause()
    {
        paused = !paused;
        trayIcon?.SetPaused(paused);
        if (paused)
        {
            frameLoop?.Stop();
            topmostKeeper?.Stop();
            overlay?.Hide();
        }
        else
        {
            overlay?.Show();
            frameLoop?.Start();
            topmostKeeper?.Start();
        }
    }

    private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        HandleFatalException(e.Exception);
    }

    /// <summary>Logs the exception, tears everything down, and exits. Shared by the WPF dispatcher and WinForms tray paths.</summary>
    private void HandleFatalException(Exception exception)
    {
        CrashLog.Write(exception);

        // Stop the loop before the posted Shutdown runs, so a failing tick cannot log again.
        frameLoop?.Stop();
        topmostKeeper?.Stop();
        overlay?.Hide();
        trayIcon?.Dispose();
        trayIcon = null;
        Shutdown();
    }
}
