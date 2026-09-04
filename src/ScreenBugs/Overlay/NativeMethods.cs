using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ScreenBugs.Overlay;

/// <summary>The Win32 calls the overlay needs (spec 7.6).</summary>
internal static class NativeMethods
{
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    private const int GWL_EXSTYLE = -20;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT point);

    /// <summary>Reads the window's extended style bits.</summary>
    public static int GetExtendedStyle(IntPtr hwnd)
    {
        Marshal.SetLastSystemError(0);
        IntPtr result = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        ThrowIfZeroWithError(result);
        return (int)result.ToInt64();
    }

    /// <summary>Replaces the window's extended style bits.</summary>
    public static void SetExtendedStyle(IntPtr hwnd, int style)
    {
        Marshal.SetLastSystemError(0);
        IntPtr result = SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
        ThrowIfZeroWithError(result);
    }

    /// <summary>Moves the window to the top of the topmost band without activating, moving or resizing it.</summary>
    public static void BringToTopmost(IntPtr hwnd)
    {
        if (!SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>Cursor position in physical screen pixels; false when Windows will not report it.</summary>
    public static bool TryGetCursorPosition(out int x, out int y)
    {
        if (GetCursorPos(out POINT point))
        {
            x = point.X;
            y = point.Y;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    /// <summary>
    /// <c>SetWindowLongPtr</c> returns 0 both on failure and when the previous value was 0,
    /// so a zero result is only an error when the last error code is non-zero.
    /// </summary>
    private static void ThrowIfZeroWithError(IntPtr result)
    {
        int error = Marshal.GetLastPInvokeError();
        if (result == IntPtr.Zero && error != 0)
        {
            throw new Win32Exception(error);
        }
    }
}
