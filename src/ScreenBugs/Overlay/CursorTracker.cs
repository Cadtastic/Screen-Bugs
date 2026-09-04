using System.Windows;

namespace ScreenBugs.Overlay;

/// <summary>Global cursor position in the overlay's DIP coordinates, or null when unavailable.</summary>
public static class CursorTracker
{
    public static Vector2? GetCursorDips(Window window)
    {
        if (!NativeMethods.TryGetCursorPosition(out int x, out int y))
        {
            return null;
        }

        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is null)
        {
            return null;
        }

        Point dips = source.CompositionTarget.TransformFromDevice.Transform(new Point(x, y));
        return new Vector2((float)dips.X, (float)dips.Y);
    }
}
