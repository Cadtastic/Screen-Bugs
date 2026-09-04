namespace ScreenBugs.Overlay;

/// <summary>Sets WS_EX_TRANSPARENT (click-through) except while the cursor is over a bug.</summary>
public sealed class ClickThroughController(IntPtr hwnd)
{
    private bool? clickThrough;

    public void Update(bool cursorOverBug)
    {
        bool wanted = !cursorOverBug;
        if (clickThrough == wanted)
        {
            return;
        }

        int style = NativeMethods.GetExtendedStyle(hwnd);
        style = wanted
            ? style | NativeMethods.WS_EX_TRANSPARENT
            : style & ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetExtendedStyle(hwnd, style);
        clickThrough = wanted;
    }
}
