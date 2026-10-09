using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Snapline.Interop;

namespace Snapline.UI;

/// <summary>
/// Opens a context menu from windows that never activate: the line, the
/// paper tag and the tray icon. A WPF menu shown by a process that does not
/// own the foreground window opens, but its submenus ignore clicks (the New
/// note colours, for one) and it does not close on a click elsewhere. So a
/// hidden window takes the foreground for as long as the menu is open, and
/// whatever had it before gets it back, unless the menu opened a window.
/// </summary>
public static class Menus
{
    private static HwndSource? _focus;

    private static IntPtr Focus
    {
        get
        {
            _focus ??= new HwndSource(new HwndSourceParameters("Snapline.MenuFocus")
            {
                Width = 0, Height = 0, PositionX = 0, PositionY = 0,
                WindowStyle = 0,
                ParentWindow = new IntPtr(-3), // HWND_MESSAGE
            });
            return _focus.Handle;
        }
    }

    public static void Show(ContextMenu menu)
    {
        var before = Native.GetForegroundWindow();
        var focus = Focus;
        if (before != focus) Native.SetForegroundWindow(focus);
        menu.Closed += (_, _) =>
        {
            // Give the foreground back once the click's work is done, if nobody else took it.
            menu.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (before != IntPtr.Zero && Native.GetForegroundWindow() == focus) Native.SetForegroundWindow(before);
            });
        };
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }
}
