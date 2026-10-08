using System.Windows.Interop;
using static Clothesline.Interop.Native;

namespace Clothesline.Interop;

/// <summary>A hidden message-only window for hot keys and clipboard notifications.</summary>
public sealed class MessageWindow : IDisposable
{
    private readonly HwndSource _source;
    public IntPtr Handle => _source.Handle;

    public event Action<int, IntPtr, IntPtr>? Message;

    public MessageWindow()
    {
        var p = new HwndSourceParameters("Clothesline.Messages")
        {
            Width = 0, Height = 0, PositionX = 0, PositionY = 0,
            WindowStyle = 0,
            ParentWindow = HWND_MESSAGE,
        };
        _source = new HwndSource(p);
        _source.AddHook(Hook);
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        Message?.Invoke(msg, wParam, lParam);
        return IntPtr.Zero;
    }

    public void Dispose() => _source.Dispose();
}

/// <summary>A single global shortcut, through RegisterHotKey. No hooks, no permissions.</summary>
public sealed class HotKey : IDisposable
{
    private readonly MessageWindow _window;
    private readonly int _id;
    private static int _nextId = 0x4C49; // "LI"
    public bool Registered { get; }
    public string Text { get; }

    public HotKey(MessageWindow window, string text, Action action)
    {
        _window = window;
        _id = _nextId++;
        Text = text;
        if (!Parse(text, out var mods, out var vk)) { Registered = false; return; }
        Registered = RegisterHotKey(window.Handle, _id, mods | MOD_NOREPEAT, vk);
        window.Message += (msg, w, _) =>
        {
            if (msg == WM_HOTKEY && w.ToInt32() == _id) action();
        };
    }

    /// <summary>Parses "Ctrl+Alt+T", "Win+Shift+F9" and so on.</summary>
    public static bool Parse(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0; vk = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= MOD_CONTROL; break;
                case "alt": modifiers |= MOD_ALT; break;
                case "shift": modifiers |= MOD_SHIFT; break;
                case "win" or "windows" or "meta": modifiers |= MOD_WIN; break;
                default:
                    if (Enum.TryParse<System.Windows.Forms.Keys>(raw, true, out var key)) vk = (uint)key;
                    else if (raw.Length == 1) vk = (uint)char.ToUpperInvariant(raw[0]);
                    else return false;
                    break;
            }
        }
        return vk != 0;
    }

    public void Dispose()
    {
        if (Registered) UnregisterHotKey(_window.Handle, _id);
    }
}
