using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Clothesline.Interop;
using static Clothesline.Interop.Native;

namespace Clothesline.UI;

/// <summary>
/// The capture lifting off the screen and flying up to the line. It turns
/// into the hanging card on the way: it shrinks, tilts into place and grows
/// its glass frame and clip, so there is nothing left to change on landing.
/// Also the discarded card falling off the line, drawn over the whole screen
/// so it is never cut by the strip.
/// </summary>
public sealed class FlightWindow : Window
{
    public const double Duration = 0.65;
    private const double Arc = 30;

    private readonly Canvas _canvas = new();
    private readonly Grid _container = new();
    private readonly Border _glass;
    private readonly Image _photo;
    private readonly Border _edge;
    private readonly Border _clip;
    private readonly RotateTransform _rotate = new();
    private readonly DropShadowEffect _shadow;

    private readonly Rect _from, _to;   // in window DIPs
    private readonly double _tilt;
    private readonly bool _falling;
    private readonly double _duration;
    private readonly Stopwatch _clock = new();
    private readonly Action? _completion;
    private readonly double _scale;
    private static readonly List<FlightWindow> Current = new();

    /// <summary>A capture flying from where it was taken to its place on the line. Rects in physical pixels.</summary>
    public static void Fly(BitmapSource image, RECT from, RECT to, double tilt, Display monitor, Action completion)
    {
        var w = new FlightWindow(image, from, to, tilt, monitor, falling: false, Duration, completion);
        Current.Add(w);
        w.Run();
    }

    /// <summary>A discarded card falling off the line: 520 points down, tilting further, fading, 0.55 s ease in.</summary>
    public static void Fall(BitmapSource image, RECT card, double tilt, Display monitor)
    {
        var w = new FlightWindow(image, card, card, tilt, monitor, falling: true, 0.55, null);
        Current.Add(w);
        w.Run();
    }

    private FlightWindow(BitmapSource image, RECT from, RECT to, double tilt, Display monitor, bool falling, double duration, Action? completion)
    {
        _tilt = tilt;
        _falling = falling;
        _duration = duration;
        _completion = completion;
        _scale = monitor.Scale;

        // The window covers just what the flight needs, with room for the arc and the shadow.
        int pad = (int)(80 * _scale);
        int left = Math.Min(from.Left, to.Left) - pad, top = Math.Min(from.Top, to.Top) - pad;
        int right = Math.Max(from.Right, to.Right) + pad;
        int bottom = Math.Max(from.Bottom, to.Bottom) + pad + (falling ? (int)(560 * _scale) : 0);
        left = Math.Max(left, monitor.Bounds.Left); top = Math.Max(top, monitor.Bounds.Top);
        right = Math.Min(right, monitor.Bounds.Right); bottom = Math.Min(bottom, monitor.Bounds.Bottom);
        _windowRect = new RECT(left, top, right, bottom);

        _from = new Rect((from.Left - left) / _scale, (from.Top - top) / _scale, from.Width / _scale, from.Height / _scale);
        _to = new Rect((to.Left - left) / _scale, (to.Top - top) / _scale, to.Width / _scale, to.Height / _scale);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left / _scale; Top = top / _scale; Width = (right - left) / _scale; Height = (bottom - top) / _scale;
        Content = _canvas;

        _shadow = new DropShadowEffect { Color = Colors.Black, Opacity = 0.24, BlurRadius = 20, ShadowDepth = 5, Direction = 270 };
        _glass = new Border { Background = Theme.Freeze(new SolidColorBrush(Theme.GlassFill)), Effect = _shadow };
        _photo = new Image { Source = image, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(_photo, BitmapScalingMode.HighQuality);
        _edge = new Border
        {
            BorderThickness = new Thickness(0.75),
            BorderBrush = Theme.Freeze(new LinearGradientBrush(Theme.GlassEdgeTop, Theme.GlassEdgeBottom, 90)),
        };
        _clip = new Border
        {
            Width = Layout.PinWidth, Height = Layout.PinHeight, CornerRadius = new CornerRadius(3.5),
            Background = Glass.MetalBrush(),
            BorderThickness = new Thickness(0.6), BorderBrush = Theme.Freeze(new SolidColorBrush(Theme.Gray(1, 0.7))),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        _container.Children.Add(_glass);
        _container.Children.Add(_photo);
        _container.Children.Add(_edge);
        _container.Children.Add(_clip);
        _container.RenderTransform = _rotate;
        _canvas.Children.Add(_container);
    }

    private readonly RECT _windowRect;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        FullScreen.Own.Add(source.Handle);
        AddExStyle(source.Handle, WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT);
        source.CompositionTarget.BackgroundColor = Colors.Transparent;
        MakeGlassSheet(source.Handle);
        SetWindowPos(source.Handle, HWND_TOPMOST, _windowRect.Left, _windowRect.Top, _windowRect.Width, _windowRect.Height, SWP_NOACTIVATE);
    }

    private void Run()
    {
        if (_falling) { Update(1); UpdateFall(0); } else Update(0);
        Show();
        _clock.Start();
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        double k = Math.Min(1, _clock.Elapsed.TotalSeconds / _duration);
        if (_falling) UpdateFall(k); else Update(k);
        if (k < 1) return;
        CompositionTarget.Rendering -= OnRendering;
        _completion?.Invoke();
        Current.Remove(this);
        if (_falling)
        {
            Close();
            return;
        }
        // The real card fades in underneath; this one fades out over it.
        var fade = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromSeconds(0.16));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    private void UpdateFall(double raw)
    {
        double ease = raw * raw * raw;
        double angle = _tilt + (_tilt * 7 + 20) * ease;
        double w = _to.Width, h = _to.Height;
        Place(_to.Left + w / 2, _to.Top + 520 * ease, w, h, angle, Layout.FrameRadius, Layout.FrameInset, 1);
        _container.Opacity = 1 - ease;
    }

    private void Update(double raw)
    {
        double k = Ease.InOutCubic(raw);
        double chrome = Ease.Smooth(k, 0.35, 1);
        double Lerp(double a, double b) => a + (b - a) * k;

        double w = Lerp(_from.Width, _to.Width), h = Lerp(_from.Height, _to.Height);
        double topX = Lerp(_from.Left + _from.Width / 2, _to.Left + _to.Width / 2);
        double topY = Lerp(_from.Top, _to.Top) - Math.Sin(Math.PI * k) * Arc;
        double inset = Layout.FrameInset * k;
        double radius = Lerp(0, Layout.FrameRadius);
        Place(topX, topY, w, h, _tilt * k, radius, inset, chrome);
    }

    private void Place(double topX, double topY, double w, double h, double angle, double radius, double inset, double chrome)
    {
        _container.Width = w; _container.Height = h;
        Canvas.SetLeft(_container, topX - w / 2);
        Canvas.SetTop(_container, topY);
        _rotate.CenterX = w / 2; _rotate.CenterY = 0;
        _rotate.Angle = angle;

        _glass.CornerRadius = new CornerRadius(radius);
        _glass.Opacity = chrome;
        _edge.CornerRadius = new CornerRadius(radius);
        _edge.Opacity = chrome;
        _shadow.Opacity = 0.24 * Math.Max(chrome, 0.35);

        _photo.Margin = new Thickness(inset);
        double pr = Math.Max(0, radius - inset);
        _photo.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, w - 2 * inset), Math.Max(0, h - 2 * inset)), pr, pr);

        // The clip grips the top edge: 26 points tall, 12 of them over the card.
        _clip.Margin = new Thickness(w / 2 - Layout.PinWidth / 2, -(Layout.PinHeight - 12), 0, 0);
        _clip.Opacity = chrome;
    }
}
