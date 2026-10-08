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
    private Path? _bowLeft, _bowRight;
    private PeggedControl? _held;
    private int _topZ = 10;
    private bool _ropeDrag;
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
        Background = Brushes.Transparent;
        ClipToBounds = false;
        Height = Layout.PanelHeight;
        RenderTransform = _reveal;
        _reveal.Y = -(Layout.PanelHeight + 12);

        // A thin, neutral line: a mid gray core with a faint highlight and a soft
        // shadow, so it reads on light and dark backgrounds alike. It fades out at
        // both ends so it seems to come from beyond the screen.
        _ropeShadow = new Path { Stroke = Theme.Freeze(new SolidColorBrush(Theme.Gray(0, 0.22))), StrokeThickness = 1.4, Effect = new BlurEffect { Radius = 2.4 }, RenderTransform = new TranslateTransform(0, 1.2) };
        _ropeCore = new Path { StrokeThickness = 1.6 };
        _ropeHighlight = new Path { StrokeThickness = 0.5, RenderTransform = new TranslateTransform(0, -0.45) };
        PaintRope();
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
            Opacity = 0,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = Strings.NewCaptureTip,
        };
        // The hint is a button too: click it to start a snip.
        _hint.MouseLeftButtonUp += (_, e) => { e.Handled = true; Clothesline.Interop.Shell.Snip(); };
        Children.Add(_hint);
        SetZIndex(_hint, 1);

        _line.ItemsChanged += Sync;
        _line.ItemUpdated += OnItemUpdated;
        _line.Gust += () => { foreach (var c in _cards) if (!c.Item.Falling) c.Breeze(); StartTicking(); };
        _line.StateChanged += () => { foreach (var c in _cards) c.StateChanged(); StartTicking(); };
        Theme.Changed += Repaint;
        Pegs.LookChanged += Repaint;
        Strings.LanguageChanged += Repaint;
        MouseLeftButtonDown += OnRopeDown;
        MouseLeftButtonUp += OnRopeUp;
        LostMouseCapture += (_, _) => { if (_ropeDrag) { _ropeDrag = false; RopeDragEnded?.Invoke(); } };
        SizeChanged += (_, _) => Relayout(snap: true);
        Sync();
    }

    public IReadOnlyList<PeggedControl> Cards => _cards;

    private void PaintRope()
    {
        var color = Pegs.Rope();
        _ropeCore.Stroke = Theme.Freeze(new SolidColorBrush(color));
        _ropeHighlight.Stroke = Theme.Freeze(new SolidColorBrush(Pegs.Lighten(color, 0.55)));
        _ropeHighlight.Opacity = 0.7;
    }

    private void Repaint()
    {
        PaintRope();
        if (_width > 0) DrawRope();
        _hint.Background = Theme.Freeze(new SolidColorBrush(Theme.HintFill));
        ((TextBlock)_hint.Child).Foreground = Theme.Freeze(new SolidColorBrush(Theme.Secondary));
        ((TextBlock)_hint.Child).Text = Strings.Hint;
        _hint.ToolTip = Strings.NewCaptureTip;
        _hint.FlowDirection = Strings.Flow;
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
        bool even = Settings.Current.AutoArrange;
        for (int i = 0; i < count; i++)
        {
            var card = live[i];
            card.RopeYAt ??= x => Layout.RopeY(x, _width);
            // Spread evenly, or where you put it. A new photo takes the nearest free place to the middle.
            double x = even ? Layout.X(i, count, _width) : (card.Item.Spot ?? AssignSpot(card, live)) * _width;
            x = Math.Clamp(x, Layout.CardWidth / 2, Math.Max(Layout.CardWidth / 2, _width - Layout.CardWidth / 2));
            double ropeY = Layout.RopeY(x, _width);
            bool fresh = double.IsNaN(GetTop(card));
            SetTop(card, ropeY - Layout.PinAbove);
            card.Height = Layout.PanelHeight - ropeY;
            if (card != _held) card.SetTargetX(x, snap || fresh);
        }
        double mid = _width / 2;
        _hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(_hint, mid - _hint.DesiredSize.Width / 2);
        SetTop(_hint, Layout.RopeY(mid, _width) + 34 - _hint.DesiredSize.Height / 2);
    }

    /// <summary>The nearest place to the middle of the line with room for a card, remembered on the photo.</summary>
    private double AssignSpot(PeggedControl card, List<PeggedControl> live)
    {
        var taken = live.Where(c => c != card && c.Item.Spot is not null).Select(c => c.Item.Spot!.Value * _width).ToList();
        double half = Layout.CardWidth / 2;
        double best = _width / 2;
        for (int step = 0; step < 40; step++)
        {
            foreach (double candidate in new[] { _width / 2 + step * Layout.Spacing, _width / 2 - step * Layout.Spacing })
            {
                if (candidate < half || candidate > _width - half) continue;
                if (taken.All(t => Math.Abs(t - candidate) >= Layout.Spacing * 0.9)) { best = candidate; goto found; }
            }
        }
        found:
        double fraction = Math.Clamp(best / _width, 0, 1);
        _line.SetSpot(card.Item.Id, fraction, save: true);
        return fraction;
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

        // A bow near each end, where the cord is tied off.
        if (_bowLeft is not null) { Children.Remove(_bowLeft); Children.Remove(_bowRight); _bowLeft = _bowRight = null; }
        if (Settings.Current.Bows && w > 400)
        {
            var color = Pegs.Rope();
            _bowLeft = Pegs.Bow(color, mirrored: false);
            _bowRight = Pegs.Bow(color, mirrored: true);
            double xl = w * 0.055, xr = w * 0.945;
            SetLeft(_bowLeft, xl); SetTop(_bowLeft, Layout.RopeY(xl, w) + 1);
            SetLeft(_bowRight, xr); SetTop(_bowRight, Layout.RopeY(xr, w) + 1);
            Children.Add(_bowLeft); Children.Add(_bowRight);
            SetZIndex(_bowLeft, 1); SetZIndex(_bowRight, 1);
        }
    }

    // MARK: Reordering by hand

    public void BeginReorder(PeggedControl card)
    {
        _held = card;
        SetZIndex(card, 5);
        card.SetHovering(true);
        StartTicking();
    }

    /// <summary>The held card follows the pointer; the others make room.</summary>
    public void ReorderTo(PeggedControl card, double centerX)
    {
        if (_held != card) return;
        double half = Layout.CardWidth / 2;
        centerX = Math.Clamp(centerX, half, Math.Max(half, _width - half));
        card.SetTargetX(centerX, snap: true);
        if (!Settings.Current.AutoArrange)
        {
            // It stays where you put it; nothing else moves.
            _line.SetSpot(card.Item.Id, centerX / _width, save: false);
            StartTicking();
            return;
        }
        var live = _cards.Where(c => !c.Item.Falling).ToList();
        int from = live.IndexOf(card);
        if (from < 0) return;
        // Which slot is the pointer over now? The nearest one.
        int to = 0;
        double best = double.MaxValue;
        for (int i = 0; i < live.Count; i++)
        {
            double d = Math.Abs(centerX - Layout.X(i, live.Count, _width));
            if (d < best) { best = d; to = i; }
        }
        if (to != from) _line.Move(card.Item.Id, to);
        StartTicking();
    }

    public void EndReorder(PeggedControl card)
    {
        if (_held != card) return;
        _held = null;
        // The last one moved lies on top of any it was dropped against.
        SetZIndex(card, Settings.Current.AutoArrange ? 2 : ++_topZ);
        if (!Settings.Current.AutoArrange) _line.SetSpot(card.Item.Id, card.TargetX / _width, save: true);
        Relayout(snap: false);
        StartTicking();
    }

    // MARK: Moving the line itself

    public event Action? RopeDragStarted;
    public event Action? RopeDragEnded;
    public bool RopeDragging => _ropeDrag;

    /// <summary>Whether a canvas point lies on the rope, within a comfortable band, and not over a photo.</summary>
    public bool IsOverRope(Point p)
    {
        if (_width <= 0 || p.X < 0 || p.X > _width) return false;
        if (CardHitRects().Any(h => h.rect.Contains(p))) return false;
        return Math.Abs(p.Y - Layout.RopeY(p.X, _width)) <= 9;
    }

    private void OnRopeDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!IsOverRope(e.GetPosition(this))) return;
        e.Handled = true;
        _ropeDrag = true;
        CaptureMouse();
        RopeDragStarted?.Invoke();
    }

    private void OnRopeUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_ropeDrag) return;
        e.Handled = true;
        _ropeDrag = false;
        ReleaseMouseCapture();
        RopeDragEnded?.Invoke();
    }

    /// <summary>Where the pointer counts as being over a photo, in canvas coordinates.</summary>
    public IEnumerable<(PeggedControl card, Rect rect)> HitRects() => CardHitRects();

    /// <summary>The hint counts as well while the line is empty, so it can be clicked.</summary>
    public Rect? HintRect => _line.LiveCount == 0 && _hint.Opacity > 0.5
        ? new Rect(GetLeft(_hint), GetTop(_hint), _hint.ActualWidth, _hint.ActualHeight)
        : null;

    private IEnumerable<(PeggedControl card, Rect rect)> CardHitRects()
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
            foreach (var (card, rect) in CardHitRects())
                if (rect.Contains(p)) { over = card; break; }
        bool any = false;
        foreach (var c in _cards)
        {
            bool on = c == over && _line.DraggingId is null;
            if (c.Hovering != on) { c.SetHovering(on); any = true; }
        }
        if (any) StartTicking();
        Cursor = pointInCanvas is { } q && over is null && IsOverRope(q) ? System.Windows.Input.Cursors.SizeNS : null;
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
