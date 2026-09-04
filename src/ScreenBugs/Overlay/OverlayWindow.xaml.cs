using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ScreenBugs.Overlay;

/// <summary>Transparent, topmost, click-through window covering the primary monitor (spec 7.1).</summary>
public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
    }

    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        int style = NativeMethods.GetExtendedStyle(Handle);
        NativeMethods.SetExtendedStyle(
            Handle,
            style | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Point position = e.GetPosition(this);
        Surface.Simulation?.TrySquashAt(new Vector2((float)position.X, (float)position.Y));
        e.Handled = true;
    }
}
