using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Clothesline.Core;
using Clothesline.Interop;
using static Clothesline.Interop.Native;

namespace Clothesline.UI;

/// <summary>
/// A transparent strip along the top of the screen that floats over every
/// app, never takes focus, and lets clicks pass through everywhere except
/// over the photos.
/// </summary>
public sealed class LineWindow : Window
{
    public LineCanvas Canvas { get; }
    public Display? Display { get; private set; }
    public IntPtr Handle { get; private set; }
    private HwndSource? _source;
    private double _ropeStartOffset, _ropeStartY;
    private readonly System.Windows.Threading.DispatcherTimer _ropeFollow = new() { Interval = TimeSpan.FromMilliseconds(16) };

    /// <summary>While a drag or a press is in progress the whole strip stays solid, so events keep coming.</summary>
    public bool HoldMouse { get; set; }

    public LineWindow(Line line)
    {
        Canvas = new LineCanvas(line);
        Title = Strings.AppName;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0; Top = 0; Width = 800; Height = Layout.PanelHeight;
        Content = Canvas;
        Focusable = false;

        // Holding the rope moves the whole line up or down the screen.
        Canvas.RopeDragStarted += () =>
        {
            _ropeStartOffset = Settings.Current.LineOffset;
            _ropeStartY = Displays.Cursor().Y;
            _ropeFollow.Start();
        };
        _ropeFollow.Tick += (_, _) =>
        {
            if (Display is null) return;
            double dy = (Displays.Cursor().Y - _ropeStartY) / Display.Scale;
            double offset = Math.Max(0, _ropeStartOffset + dy);
            if (Math.Abs(offset - Settings.Current.LineOffset) < 0.5) return;
            Settings.Current.LineOffset = offset;
            PlaceOn(Display);
        };
        Canvas.RopeDragEnded += () => { _ropeFollow.Stop(); Settings.Current.Save(); };
        SnapsToDevicePixels = false;
        UseLayoutRounding = false;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = (HwndSource)PresentationSource.FromVisual(this)!;
        Handle = _source.Handle;
        FullScreen.Own.Add(Handle);
        AddExStyle(Handle, WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        _source.CompositionTarget.BackgroundColor = Colors.Transparent;
        MakeGlassSheet(Handle);
        _source.AddHook(Hook);
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
            {
                // The strip spans the whole width of the screen, so it only takes
                // the mouse while the pointer is over a photo. Everywhere else,
                // clicks go to whatever is underneath.
                handled = true;
                if (HoldMouse) return new IntPtr(HTCLIENT);
                int x = unchecked((short)(long)lParam), y = unchecked((short)((long)lParam >> 16));
                return new IntPtr(IsOverPhoto(new POINT(x, y)) ? HTCLIENT : HTTRANSPARENT);
            }
            case WM_MOUSEACTIVATE:
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }

    /// <summary>Screen pixels to canvas coordinates.</summary>
    public Point? ToCanvas(POINT screen)
    {
        if (_source is null || !GetWindowRect(Handle, out var r)) return null;
        var m = _source.CompositionTarget.TransformFromDevice;
        var p = m.Transform(new Point(screen.X - r.Left, screen.Y - r.Top));
        // The canvas slides; undo its offset.
        p.Y -= ((TranslateTransform)Canvas.RenderTransform).Y;
        return p;
    }

    public bool IsOverPhoto(POINT screen)
    {
        if (!Canvas.Revealed) return false;
        var p = ToCanvas(screen);
        if (p is null) return false;
        foreach (var (_, rect) in Canvas.HitRects())
            if (rect.Contains(p.Value)) return true;
        if (Canvas.HintRect is { } hint && hint.Contains(p.Value)) return true;
        if (Canvas.TagRect is { } tag && tag.Contains(p.Value)) return true;
        return Canvas.RopeDragging || Canvas.IsOverRope(p.Value);
    }

    /// <summary>The line hangs on the screen you are using, under the taskbar if it is at the top.</summary>
    public void PlaceOn(Display? monitor = null)
    {
        var m = monitor ?? Displays.UnderPointer();
        Display = m;
        int height = (int)Math.Round(Layout.PanelHeight * m.Scale);
        var work = m.Work;
        // The line can hang lower than the very top, below the tabs of a maximised window.
        double maxOffset = Math.Max(0, work.Height / m.Scale - Layout.PanelHeight);
        double offset = Math.Clamp(Settings.Current.LineOffset, 0, maxOffset);
        Settings.Current.LineOffset = offset;
        int top = work.Top + (int)Math.Round(offset * m.Scale);
        if (Handle == IntPtr.Zero)
        {
            Left = work.Left; Top = top; Width = work.Width / m.Scale; Height = Layout.PanelHeight;
            return;
        }
        const uint flags = SWP_NOACTIVATE | SWP_NOZORDER;
        SetWindowPos(Handle, IntPtr.Zero, work.Left, top, work.Width, height, flags);
        // Crossing to a screen with another DPI makes Windows suggest a rescaled
        // frame first. Say it again, now that the DPI has settled.
        if (GetWindowRect(Handle, out var r) && (r.Left != work.Left || r.Top != top || r.Width != work.Width || r.Height != height))
            SetWindowPos(Handle, IntPtr.Zero, work.Left, top, work.Width, height, flags);
        Canvas.Width = work.Width / m.Scale;
        Canvas.UpdateLayout();
    }

    public void OrderFront()
    {
        if (!IsVisible) Show();
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    public void OrderOut()
    {
        if (Handle != IntPtr.Zero) ShowWindow(Handle, SW_HIDE);
    }

    /// <summary>The panel's frame in physical pixels.</summary>
    public RECT Frame
    {
        get
        {
            GetWindowRect(Handle, out var r);
            return r;
        }
    }
}
