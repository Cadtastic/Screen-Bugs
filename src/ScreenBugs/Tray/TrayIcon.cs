using System.Windows.Forms;

namespace ScreenBugs.Tray;

/// <summary>
/// System tray icon with Pause/Resume, a Bugs count submenu, and Exit.
/// Inside this file <c>Application</c> means the WinForms one, which is why the
/// <c>ThreadException</c> plumbing lives here rather than in <c>App</c>.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private static readonly int[] CountChoices = [1, 3, 5, 10];

    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip menu;
    private readonly ToolStripMenuItem pauseItem;
    private readonly ToolStripMenuItem[] countItems;

    public event Action? PauseToggled;

    public event Action<int>? BugCountChanged;

    public event Action? ExitRequested;

    // Explicit constructor: it wires WinForms components and handlers that need `this`.
    public TrayIcon(int initialCount)
    {
        pauseItem = new ToolStripMenuItem("Pause");
        pauseItem.Click += (_, _) => PauseToggled?.Invoke();

        countItems = CountChoices
            .Select(count => new ToolStripMenuItem(count.ToString()) { Checked = count == initialCount, Tag = count })
            .ToArray();
        var bugsMenu = new ToolStripMenuItem("Bugs");
        foreach (var item in countItems)
        {
            item.Click += (_, _) => SelectCount(item);
            bugsMenu.DropDownItems.Add(item);
        }

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        menu = new ContextMenuStrip();
        menu.Items.Add(pauseItem);
        menu.Items.Add(bugsMenu);
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

    private void SelectCount(ToolStripMenuItem selected)
    {
        foreach (var item in countItems)
        {
            item.Checked = item == selected;
        }

        BugCountChanged?.Invoke((int)selected.Tag!);
    }
}
