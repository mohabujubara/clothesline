using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Clothesline.Core;
using Line = Clothesline.Core.Line;

namespace Clothesline.UI;

/// <summary>The line and where each photo hangs. Drives every animation from one clock.</summary>
public sealed class LineCanvas : Canvas
{
    private readonly Line _line;
    private readonly List<PeggedControl> _cards = new();
    private readonly Grid _rope;
    private readonly Path _ropeShadow, _ropeCore, _ropeHighlight;
    private readonly Border _hint;
    private readonly Tween _hintOpacity = new(0);
    private readonly TranslateTransform _reveal = new();
    private readonly Spring _revealSpring = Spring.FromResponse(0.42, 0.82, -(Layout.PanelHeight + 12));
    private readonly Tween _hideTween = new(-(Layout.PanelHeight + 12));
    private bool _revealed;
    private bool _usingSpring;
    private readonly Stopwatch _clock = new();
    private double _lastTime;
    private bool _ticking;
    private double _width;

    public LineCanvas(Line line)
    {
        _line = line;
        Background = null;
        ClipToBounds = false;
        Height = Layout.PanelHeight;
        RenderTransform = _reveal;
        _reveal.Y = -(Layout.PanelHeight + 12);

        // A thin, neutral line: a mid gray core with a faint highlight and a soft
        // shadow, so it reads on light and dark backgrounds alike. It fades out at
        // both ends so it seems to come from beyond the screen.
        _ropeShadow = new Path { Stroke = Theme.Freeze(new SolidColorBrush(Theme.Gray(0, 0.22))), StrokeThickness = 1.4, Effect = new BlurEffect { Radius = 2.4 }, RenderTransform = new TranslateTransform(0, 1.2) };
        _ropeCore = new Path { Stroke = Theme.Freeze(new SolidColorBrush(Theme.Gray(0.55))), StrokeThickness = 1.2 };
        _ropeHighlight = new Path { Stroke = Theme.Freeze(new SolidColorBrush(Theme.Gray(1, 0.45))), StrokeThickness = 0.4, RenderTransform = new TranslateTransform(0, -0.35) };
        _rope = new Grid { IsHitTestVisible = false };
        _rope.Children.Add(_ropeShadow);
        _rope.Children.Add(_ropeCore);
        _rope.Children.Add(_ropeHighlight);
        var mask = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
        mask.GradientStops.Add(new GradientStop(Colors.Black, 0.08));
        mask.GradientStops.Add(new GradientStop(Colors.Black, 0.92));
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        mask.Freeze();
        _rope.OpacityMask = mask;
        Children.Add(_rope);
        SetZIndex(_rope, 0);

        _hint = new Border
        {
            Child = new TextBlock
            {
                Text = Strings.Hint,
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"), FontSize = 12, FontWeight = FontWeights.Medium,
                Foreground = Theme.Freeze(new SolidColorBrush(Theme.Secondary)),
            },
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(14),
            Background = Theme.Freeze(new SolidColorBrush(Theme.HintFill)),
            BorderThickness = new Thickness(0.75),
            BorderBrush = Theme.Freeze(new LinearGradientBrush(Theme.GlassEdgeTop, Theme.GlassEdgeBottom, 90)),
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Direction = 270, Opacity = 0.14, Color = Colors.Black },
            IsHitTestVisible = false,
            Opacity = 0,
        };
        Children.Add(_hint);
        SetZIndex(_hint, 1);

        _line.ItemsChanged += Sync;
        _line.ItemUpdated += OnItemUpdated;
        _line.Gust += () => { foreach (var c in _cards) if (!c.Item.Falling) c.Breeze(); StartTicking(); };
        _line.StateChanged += () => { foreach (var c in _cards) c.StateChanged(); StartTicking(); };
        Theme.Changed += Repaint;
        SizeChanged += (_, _) => Relayout(snap: true);
        Sync();
    }

    public IReadOnlyList<PeggedControl> Cards => _cards;

    private void Repaint()
    {
        _hint.Background = Theme.Freeze(new SolidColorBrush(Theme.HintFill));
        ((TextBlock)_hint.Child).Foreground = Theme.Freeze(new SolidColorBrush(Theme.Secondary));
        // Cards are rebuilt with the new colours.
        foreach (var c in _cards.ToList()) Children.Remove(c);
        _cards.Clear();
        Sync();
    }

    public bool Revealed
    {
        get => _revealed;
        set
        {
            if (_revealed == value) return;
            _revealed = value;
            if (value)
            {
                // Tucked away, the whole line waits above the top edge and slides
                // out from under the taskbar, the way an auto-hiding taskbar does.
                _revealSpring.Value = _reveal.Y;
                _revealSpring.Velocity = 0;
                _revealSpring.Target = 0;
                _usingSpring = true;
            }
            else
            {
                _hideTween.Snap(_reveal.Y);
                _hideTween.Go(-(Layout.PanelHeight + 12), 0.22, Ease.InCubic);
                _usingSpring = false;
            }
            StartTicking();
        }
    }

    // MARK: Items

    private void Sync()
    {
        var items = _line.Items;
        // Remove what is gone.
        foreach (var card in _cards.Where(c => !items.Contains(c.Item)).ToList())
        {
            _cards.Remove(card);
            Children.Remove(card);
        }
        // Add what is new, in order.
        var known = _cards.Select(c => c.Item).ToHashSet();
        foreach (var item in items)
        {
            if (known.Contains(item)) continue;
            var card = new PeggedControl(item, _line);
            _cards.Add(card);
            Children.Add(card);
            SetZIndex(card, 2);
        }
        _cards.Sort((a, b) => items.ToList().IndexOf(a.Item).CompareTo(items.ToList().IndexOf(b.Item)));
        Relayout(snap: false);
        bool empty = _line.LiveCount == 0;
        _hintOpacity.Go(empty ? 1 : 0, 0.3);
        StartTicking();
    }

    private void OnItemUpdated(Pegged item)
    {
        var card = _cards.FirstOrDefault(c => c.Item == item);
        if (card is null) return;
        if (!item.Flying && card.Opacity == 0 && !item.Falling) card.Land();
        card.Refresh();
        StartTicking();
    }

    private void Relayout(bool snap)
    {
        _width = ActualWidth;
        if (_width <= 0) return;
        DrawRope();
        // Falling cards keep their slot until they are removed, so the others do not jump early.
        var live = _cards.Where(c => !c.Item.Falling).ToList();
        int count = live.Count;
        for (int i = 0; i < count; i++)
        {
            var card = live[i];
            double x = Layout.X(i, count, _width);
            double ropeY = Layout.RopeY(x, _width);
            bool fresh = double.IsNaN(GetTop(card));
            SetTop(card, ropeY - Layout.PinAbove);
            card.Height = Layout.PanelHeight - ropeY;
            card.SetTargetX(x, snap || fresh);
        }
        double mid = _width / 2;
        _hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(_hint, mid - _hint.DesiredSize.Width / 2);
        SetTop(_hint, Layout.RopeY(mid, _width) + 34 - _hint.DesiredSize.Height / 2);
    }

    private void DrawRope()
    {
        double top = Layout.RopeTop, w = _width;
        var fig = new PathFigure { StartPoint = new Point(-20, top) };
        fig.Segments.Add(new QuadraticBezierSegment(new Point(w / 2, top + 2 * Layout.Sag(w)), new Point(w + 20, top), true));
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        geo.Freeze();
        _ropeShadow.Data = _ropeCore.Data = _ropeHighlight.Data = geo;
        _rope.Width = w;
        _rope.Height = Layout.PanelHeight;
    }

    /// <summary>Where the pointer counts as being over a photo, in canvas coordinates.</summary>
    public IEnumerable<(PeggedControl card, Rect rect)> HitRects()
    {
        foreach (var c in _cards)
        {
            if (c.Item.Falling || c.Item.Flying) continue;
            var r = c.CardRect;
            r.Inflate(4, 4);
            yield return (c, r);
        }
    }

    public void UpdateHover(Point? pointInCanvas)
    {
        PeggedControl? over = null;
        if (pointInCanvas is { } p)
            foreach (var (card, rect) in HitRects())
                if (rect.Contains(p)) { over = card; break; }
        bool any = false;
        foreach (var c in _cards)
        {
            bool on = c == over && _line.DraggingId is null;
            if (c.Hovering != on) { c.SetHovering(on); any = true; }
        }
        if (any) StartTicking();
    }

    /// <summary>Steps every animation forward without a window, for offscreen rendering.</summary>
    public void Advance(double seconds, double step = 1.0 / 60)
    {
        _ticking = false;
        CompositionTarget.Rendering -= OnRendering;
        for (double t = 0; t < seconds; t += step) StepAll(step);
    }

    private bool StepAll(double dt)
    {
        bool active = false;
        if (_usingSpring) { _revealSpring.Step(dt); _reveal.Y = _revealSpring.Value; active |= !_revealSpring.Resting; }
        else { _hideTween.Step(dt); _reveal.Y = _hideTween.Value; active |= !_hideTween.Done; }
        _hintOpacity.Step(dt);
        _hint.Opacity = _hintOpacity.Value;
        active |= !_hintOpacity.Done;
        foreach (var card in _cards) { card.Tick(dt); active |= card.Active; }
        return active;
    }

    // MARK: The clock

    private void StartTicking()
    {
        if (_ticking) return;
        _ticking = true;
        _clock.Restart();
        _lastTime = 0;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalSeconds;
        double dt = Math.Clamp(now - _lastTime, 0, 1.0 / 20);
        _lastTime = now;
        if (dt <= 0) return;

        if (!StepAll(dt))
        {
            _ticking = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }
}
