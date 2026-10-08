using Snapline.Interop;
using static Snapline.Interop.Native;

namespace Snapline.Core;

/// <summary>
/// Guesses where on screen a capture was taken, so it can lift off from
/// there and fly to the line. Windows does not record the area, but the
/// size says a lot: a whole screen, a window, or a snip that ended under
/// the pointer.
/// </summary>
public static class CaptureRect
{
    /// <returns>The area in physical pixels, and the monitor it is on, or null to simply drop in.</returns>
    public static (RECT rect, Display monitor)? Infer(int width, int height)
    {
        if (width < 3 || height < 3) return null;
        var monitors = Displays.All();
        var cursor = Displays.Cursor();

        // A whole screen.
        foreach (var m in monitors)
            if (m.Bounds.Width == width && m.Bounds.Height == height) return (m.Bounds, m);

        // Every screen at once: fly from the one with the pointer.
        if (monitors.Count > 1)
        {
            int l = monitors.Min(m => m.Bounds.Left), t = monitors.Min(m => m.Bounds.Top);
            int r = monitors.Max(m => m.Bounds.Right), b = monitors.Max(m => m.Bounds.Bottom);
            if (r - l == width && b - t == height)
            {
                var m = Displays.Nearest(cursor);
                return (m.Bounds, m);
            }
        }

        // A window of exactly that size (Alt+PrtScn, or the window mode of the Snipping Tool).
        var window = FindWindow(width, height);
        if (window is not null)
        {
            var m = Displays.OfWindow(window.Value.hwnd) ?? Displays.Nearest(cursor);
            return (window.Value.rect, m);
        }

        // A region: the pointer let go at a corner of it, usually the bottom right.
        var monitor = Displays.Nearest(cursor);
        var bounds = monitor.Bounds;
        double scale = Math.Min(1, Math.Min(bounds.Width * 0.9 / width, bounds.Height * 0.9 / height));
        int w = (int)(width * scale), h = (int)(height * scale);
        int x = Math.Clamp(cursor.X - w, bounds.Left, Math.Max(bounds.Left, bounds.Right - w));
        int y = Math.Clamp(cursor.Y - h, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - h));
        return (new RECT(x, y, x + w, y + h), monitor);
    }

    private static (IntPtr hwnd, RECT rect)? FindWindow(int width, int height)
    {
        (IntPtr, RECT)? found = null;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd) || IsCloaked(hwnd) || FullScreen.Own.Contains(hwnd)) return true;
            if (!GetWindowRect(hwnd, out var r)) return true;
            // Window captures exclude the invisible resize borders of a top level window.
            if ((r.Width == width && r.Height == height) ||
                (r.Width - 14 == width && r.Height - 7 == height) ||
                (r.Width - 16 == width && r.Height - 8 == height))
            {
                found = (hwnd, r);
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
