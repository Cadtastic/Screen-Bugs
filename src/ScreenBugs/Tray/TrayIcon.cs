using System.Windows.Forms;

namespace ScreenBugs.Tray;

/// <summary>
/// System tray icon with Pause/Resume, Options and Exit.
/// Inside this file <c>Application</c> means the WinForms one, which is why the
/// <c>ThreadException</c> plumbing lives here rather than in <c>App</c>.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip menu;
    private readonly ToolStripMenuItem pauseItem;

    public event Action? PauseToggled;

    public event Action? OptionsRequested;

    public event Action? ExitRequested;

    // Explicit constructor: it wires WinForms components and handlers that need `this`.
    public TrayIcon()
    {
        pauseItem = new ToolStripMenuItem("Pause");
        pauseItem.Click += (_, _) => PauseToggled?.Invoke();

        var optionsItem = new ToolStripMenuItem("Options...");
        optionsItem.Click += (_, _) => OptionsRequested?.Invoke();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        menu = new ContextMenuStrip();
        menu.Items.Add(pauseItem);
        menu.Items.Add(optionsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        menu.Opening += (_, _) => IsMenuOpen = true;
        menu.Closed += (_, _) => IsMenuOpen = false;

        notifyIcon = new NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Text = "Screen Bugs",
            ContextMenuStrip = menu,
            Visible = true,
        };
        notifyIcon.DoubleClick += (_, _) => OptionsRequested?.Invoke();
    }

    /// <summary>
    /// Routes exceptions thrown inside menu handlers to <paramref name="handler"/>.
    /// Menu clicks run inside a WinForms window procedure, which catches exceptions and raises
    /// them on <c>Application.ThreadException</c> instead of letting them reach WPF's
    /// <c>DispatcherUnhandledException</c>. Without this, a failing menu handler shows the
    /// WinForms error dialog whose Quit button calls <c>Environment.Exit</c>, skipping
    /// <c>App.OnExit</c> and leaving a ghost tray icon. Call this before creating a
    /// <see cref="TrayIcon"/>.
    /// </summary>
    public static void RouteThreadExceptions(Action<Exception> handler) =>
        Application.ThreadException += (_, args) => handler(args.Exception);

    /// <summary>
    /// True while the context menu is on screen. The overlay stays click-through in that case,
    /// so a bug drawn over the menu cannot swallow a click meant for a menu item.
    /// </summary>
    public bool IsMenuOpen { get; private set; }

    /// <summary>Swaps the first menu item between "Pause" and "Resume".</summary>
    public void SetPaused(bool paused) => pauseItem.Text = paused ? "Resume" : "Pause";

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        menu.Dispose();
    }
}
