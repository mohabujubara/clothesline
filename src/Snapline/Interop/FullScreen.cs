using static Snapline.Interop.Native;

namespace Snapline.Interop;

/// <summary>Knows when a screen is showing a full screen app, so the line stays away.</summary>
public static class FullScreen
{
    /// <summary>Our own windows never count as full screen apps.</summary>
    public static readonly HashSet<IntPtr> Own = new();

    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
    };

    public static bool IsActive(Display monitor)
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || Own.Contains(fg)) return false;
        if (MonitorFromWindow(fg, MONITOR_DEFAULTTONULL) != monitor.Handle) return false;
        if (ShellClasses.Contains(ClassNameOf(fg))) return false;
        if (IsZoomed(fg) || IsIconic(fg) || IsCloaked(fg)) return false;
        if (!GetWindowRect(fg, out var r)) return false;
        var b = monitor.Bounds;
        bool covers = r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        if (covers) return true;

        // Exclusive full screen games and presentations are reported by the shell.
        if (SHQueryUserNotificationState(out var state) == 0 &&
            state is QUERY_USER_NOTIFICATION_STATE.RunningD3dFullScreen or QUERY_USER_NOTIFICATION_STATE.PresentationMode)
            return true;
        return false;
    }
}
