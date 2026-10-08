using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Clothesline.Core;
using Clothesline.UI;
using Line = Clothesline.Core.Line;
using Path = System.Windows.Shapes.Path;

namespace Clothesline;

/// <summary>
/// Renders the demo film and the hero banner without a desktop. The line,
/// its physics and its cards are the real ones; the desktop, the pointer and
/// the captions are drawn around them.
///
///   Clothesline --demo out\frames   writes frame_0000.png … at 30 fps, 1600 x 900
///   Clothesline --hero out.png      writes the README banner
/// </summary>
public static class Demo
{
    private const int W = 1600, H = 900, Fps = 30;
    private static readonly string Samples = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "samples"));

    private static string Sample(string name)
    {
        var local = System.IO.Path.Combine(Samples, name);
        if (File.Exists(local)) return local;
        return System.IO.Path.GetFullPath(System.IO.Path.Combine("docs", "samples", name));
    }

    // MARK: Scene

    private sealed class Scene
    {
        public readonly Line Line = new(persist: false) { MaxItems = 12 };
        public readonly LineCanvas Canvas;
        public readonly Grid Root = new() { Width = W, Height = H, ClipToBounds = true };
        public readonly Canvas Overlay = new() { Width = W, Height = H, IsHitTestVisible = false };
        public readonly Path Cursor;
        public readonly Border Caption;
        public readonly TextBlock CaptionText;
        public Point Pointer = new(W / 2, H * 0.6);

        public Scene()
        {
            // A clean desktop: a soft wallpaper and a quiet taskbar.
            var wallpaper = new Rectangle
            {
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
                    GradientStops = { new GradientStop(Color.FromRgb(0xDC, 0xE4, 0xFF), 0), new GradientStop(Color.FromRgb(0xEC, 0xE0, 0xF6), 0.5), new GradientStop(Color.FromRgb(0xFB, 0xDF, 0xE4), 1) },
                },
            };
            Root.Children.Add(wallpaper);
            var glow = new Ellipse
            {
                Width = 1100, Height = 700, Margin = new Thickness(250, 150, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Fill = new RadialGradientBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF)),
            };
            Root.Children.Add(glow);

            Canvas = new LineCanvas(Line) { Width = W, Height = Layout.PanelHeight, VerticalAlignment = VerticalAlignment.Top };
            Root.Children.Add(Canvas);

            Root.Children.Add(Taskbar());
            Root.Children.Add(Overlay);

            CaptionText = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"), FontSize = 26, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x22)),
            };
            Caption = new Border
            {
                Child = CaptionText, Padding = new Thickness(22, 10, 22, 12), CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(0xD8, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), BorderThickness = new Thickness(0.75),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 90),
                Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 4, Direction = 270, Opacity = 0.18, Color = Colors.Black },
                Opacity = 0,
            };
            Root.Children.Add(Caption);

            Cursor = new Path
            {
                Data = Geometry.Parse("M0,0 L0,17 L4.5,13 L7.5,20 L10.5,18.8 L7.5,12 L13,12 Z"),
                Fill = Brushes.White, Stroke = Brushes.Black, StrokeThickness = 1.2, StrokeLineJoin = PenLineJoin.Round,
                Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Opacity = 0.4, Color = Colors.Black },
                RenderTransform = new ScaleTransform(1.25, 1.25),
            };
            Overlay.Children.Add(Cursor);
            Panel.SetZIndex(Cursor, 100);
        }

        private static UIElement Taskbar()
        {
            var bar = new Border
            {
                Height = 56, VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(0xC8, 0xF3, 0xF3, 0xF9)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), BorderThickness = new Thickness(0, 0.75, 0, 0),
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            // Start: four blue squares.
            var start = new Grid { Width = 22, Height = 22, Margin = new Thickness(8, 0, 10, 0) };
            foreach (var (x, y) in new[] { (0, 0), (12, 0), (0, 12), (12, 12) })
                start.Children.Add(new Rectangle { Width = 10, Height = 10, Fill = new SolidColorBrush(Color.FromRgb(0x2E, 0x8A, 0xF2)), Margin = new Thickness(x, y, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, RadiusX = 1.5, RadiusY = 1.5 });
            row.Children.Add(start);
            var tints = new[] { Color.FromRgb(0x4A, 0x90, 0xE2), Color.FromRgb(0xF2, 0xB1, 0x34), Color.FromRgb(0x4C, 0xB8, 0x5C), Color.FromRgb(0x8A, 0x7A, 0xF0), Color.FromRgb(0x5A, 0x5A, 0x66) };
            foreach (var c in tints)
                row.Children.Add(new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(c), Margin = new Thickness(9, 0, 9, 0), Opacity = 0.85 });
            bar.Child = row;
            return bar;
        }

        public void Relayout()
        {
            Root.Measure(new Size(W, H));
            Root.Arrange(new Rect(0, 0, W, H));
            Root.UpdateLayout();
        }

        public BitmapSource Render()
        {
            Relayout();
            var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(Root);
            rtb.Freeze();
            return rtb;
        }

        public void PlaceCursor() { System.Windows.Controls.Canvas.SetLeft(Cursor, Pointer.X); System.Windows.Controls.Canvas.SetTop(Cursor, Pointer.Y); }
    }

    // MARK: The film

    public static int RunDemo(string outDir)
    {
        Directory.CreateDirectory(outDir);
        Theme.Override(false);
        Settings.Current.Language = "en";
        Strings.Refresh();
        Settings.Current.AutoArrange = false;
        Settings.Current.Bows = true;
        Settings.Current.RopeColor = "bronze";
        Settings.Current.PegStyle = "wood";

        var scene = new Scene();
        var line = scene.Line;
        var canvas = scene.Canvas;
        scene.Relayout();

        // Two photos already hang; the canvas lays them out around the middle.
        var docId = line.Hang(Sample("document.png"), quietly: true)!.Value;
        var dashId = line.Hang(Sample("dashboard.png"), quietly: true)!.Value;
        scene.Relayout();
        line.SetSpot(docId, 0.33, save: false);
        line.SetSpot(dashId, 0.47, save: false);
        canvas.RelayoutNow();
        canvas.Advance(2.0);   // settle
        var dashCross = canvas.Cards.First(c => c.Item.Id == dashId).CardRect;
        var crossPoint = new Point(dashCross.Left + 13, dashCross.Top + 13);

        var notePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "clothesline-demo-note.png");
        Notes.Render(new NoteData { Text = "Send the deck to Sara before 3", Color = "yellow" }, notePath);

        double dt = 1.0 / Fps;
        int frame = 0;
        var timeline = new Timeline(scene, dt);

        // 0.0  Desktop, pointer wanders up to the top edge.
        timeline.Move(new Point(880, 560), new Point(760, 2), 0.0, 1.1);
        timeline.At(1.35, () => canvas.Revealed = true);
        timeline.Caption("Rest the pointer at the top edge. The line comes down.", 1.4, 3.6);
        timeline.Move(new Point(760, 2), new Point(760, 150), 1.8, 2.4);

        // 3.8  A snip is taken and flies to the line.
        var snipRect = new Rect(590, 330, 420, 262);
        Flight? flight = null;
        timeline.Caption("Take a screenshot. It hangs the moment you let go.", 3.9, 6.6);
        timeline.Move(new Point(760, 150), new Point(590, 330), 3.7, 4.2);
        timeline.Snip(snipRect, 4.2, 5.1);
        timeline.Move(new Point(590, 330), new Point(1010, 592), 4.25, 5.05);
        Guid sunsetId = Guid.Empty;
        timeline.At(5.15, () =>
        {
            sunsetId = line.Hang(Sample("sunset.png"), quietly: true, flying: true)!.Value;
            scene.Relayout();
            line.SetSpot(sunsetId, 0.61, save: false);
            canvas.Advance(0.001);
            scene.Relayout();
            var card = canvas.Cards.First(c => c.Item.Id == sunsetId);
            var to = card.CardRect;
            to.Offset(0, 0);
            flight = new Flight(scene, card.Item.Thumb, snipRect, to, card.Item.Tilt, 5.15, () => line.Land(sunsetId));
        });

        // 6.8  Click to copy.
        timeline.Move(new Point(1010, 592), new Point(976, 120), 6.3, 6.9);
        timeline.At(7.1, () => line.CopiedId = sunsetId);
        timeline.Click(7.1);
        timeline.Caption("Click to copy. Paste it anywhere.", 6.9, 9.3);
        timeline.At(8.5, () => line.CopiedId = null);

        // 9.4  Drag along the line: it stays where you put it.
        PeggedControl? held = null;
        timeline.Move(new Point(976, 120), new Point(528, 118), 9.0, 9.6);
        timeline.Caption("Drag a photo along the line. It stays where you put it.", 9.6, 12.4);
        timeline.At(9.8, () => { held = canvas.Cards.First(c => c.Item.Id == docId); canvas.BeginReorder(held); });
        timeline.Move(new Point(528, 118), new Point(300, 118), 9.9, 11.3, p => { if (held is not null) canvas.ReorderTo(held, p.X); });
        timeline.At(11.4, () => { if (held is not null) { canvas.EndReorder(held); held = null; } });

        // 12.5  The cross lets one go.
        Fall? fall = null;
        timeline.Move(new Point(300, 118), crossPoint, 12.1, 12.8);
        timeline.Caption("The cross lets it go.", 12.6, 14.8);
        timeline.Click(13.1);
        timeline.At(13.1, () =>
        {
            var card = canvas.Cards.First(c => c.Item.Id == dashId);
            fall = new Fall(scene, card.Item.Thumb, card.CardRect, card.Item.Tilt, 13.1);
            line.Drop(dashId, quietly: true);
        });

        // 15.0  A sticky note.
        Guid noteId = Guid.Empty;
        timeline.Caption("Sticky notes hang too.", 15.0, 17.4);
        timeline.At(15.2, () =>
        {
            Settings.Current.Notes[notePath] = new NoteData { Text = "Send the deck to Sara before 3", Color = "yellow" };
            noteId = line.Hang(notePath, quietly: true)!.Value;
            scene.Relayout();
            line.SetSpot(noteId, 0.47, save: false);
        });
        timeline.Move(crossPoint, new Point(1200, 520), 15.6, 16.6);

        // 17.6  Move away: the line tucks itself up.
        timeline.Caption("Move away and it's gone.", 17.6, 19.4);
        timeline.At(18.0, () => canvas.Revealed = false);
        timeline.Move(new Point(1200, 520), new Point(1120, 600), 18.0, 19.0);

        // 19.6  Title card.
        timeline.Title(19.6, 23.0);

        double total = 23.0;
        for (double t = 0; t < total; t += dt, frame++)
        {
            timeline.Step(t);
            canvas.Advance(dt);
            if (frame % 90 == 45) foreach (var c in canvas.Cards) c.Breeze(now: true);
            canvas.UpdateHover(canvas.Revealed ? new Point(scene.Pointer.X, scene.Pointer.Y) : null);
            flight?.Update(t);
            fall?.Update(t);
            scene.PlaceCursor();
            var bmp = scene.Render();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using var stream = File.Create(System.IO.Path.Combine(outDir, $"frame_{frame:0000}.png"));
            encoder.Save(stream);
        }
        Log.Notice($"Demo: wrote {frame} frames to {outDir}");
        return 0;
    }

    private static double dashX(LineCanvas canvas, Guid id)
    {
        var card = canvas.Cards.FirstOrDefault(c => c.Item.Id == id);
        return card is null ? W * 0.47 : card.CenterX + Layout.CardWidth / 2 - 10;
    }

    // MARK: The banner

    public static int RunHero(string outPath, bool dark)
    {
        Theme.Override(dark);
        Settings.Current.Language = "en";
        Strings.Refresh();
        Settings.Current.AutoArrange = false;
        Settings.Current.Bows = true;
        Settings.Current.RopeColor = "bronze";
        Settings.Current.PegStyle = "wood";

        const int bw = 2400, bh = 1280;
        var root = new Grid { Width = bw, Height = bh, Background = new SolidColorBrush(dark ? Color.FromRgb(0x0D, 0x11, 0x17) : Colors.White) };

        var title = new TextBlock
        {
            Text = Strings.AppName, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"), FontSize = 150, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(dark ? Colors.White : Color.FromRgb(0x1C, 0x1C, 0x1E)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 110, 0, 0),
        };
        var tagline = new TextBlock
        {
            Text = "Screenshots, hung out to dry. For Windows.", FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"), FontSize = 54,
            Foreground = new SolidColorBrush(dark ? Color.FromRgb(0x9A, 0xA0, 0xA8) : Color.FromRgb(0x6E, 0x6E, 0x73)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 300, 0, 0),
        };
        root.Children.Add(title);
        root.Children.Add(tagline);

        // A slice of a desktop, with the line across its top.
        var scene = new Scene();
        var desk = new Border
        {
            Width = 2160, Height = 700, CornerRadius = new CornerRadius(40), ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 480, 0, 0),
            Child = new Viewbox { Child = scene.Root, Stretch = Stretch.UniformToFill, StretchDirection = StretchDirection.Both },
            Effect = new DropShadowEffect { BlurRadius = 60, ShadowDepth = 14, Direction = 270, Opacity = 0.22, Color = Colors.Black },
        };
        scene.Root.Height = 520;    // only the top of the desktop shows
        scene.Caption.Visibility = Visibility.Collapsed;
        root.Children.Add(desk);

        var line = scene.Line;
        var a = line.Hang(Sample("document.png"), quietly: true)!.Value;
        var b = line.Hang(Sample("sunset.png"), quietly: true)!.Value;
        var c = line.Hang(Sample("dashboard.png"), quietly: true)!.Value;
        scene.Relayout();
        line.SetSpot(a, 0.36, save: false); line.SetSpot(b, 0.5, save: false); line.SetSpot(c, 0.64, save: false);
        scene.Canvas.Revealed = true;
        scene.Canvas.Advance(1.3);
        foreach (var card in scene.Canvas.Cards) card.Breeze(now: true);
        scene.Canvas.Advance(0.5);
        scene.Pointer = new Point(1180, 20);
        scene.PlaceCursor();

        root.Measure(new Size(bw, bh));
        root.Arrange(new Rect(0, 0, bw, bh));
        root.UpdateLayout();
        var rtb = new RenderTargetBitmap(bw, bh, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = File.Create(outPath);
        encoder.Save(stream);
        return 0;
    }

    // MARK: Pieces of the film

    /// <summary>Scripted events and pointer moves against the clock.</summary>
    private sealed class Timeline
    {
        private readonly Scene _scene;
        private readonly double _dt;
        private readonly List<(double at, Action act, bool done)> _events = new();
        private readonly List<(Point from, Point to, double t0, double t1, Action<Point>? each)> _moves = new();
        private readonly List<(string text, double t0, double t1)> _captions = new();
        private readonly List<(Rect rect, double t0, double t1)> _snips = new();
        private readonly List<double> _clicks = new();
        private (double t0, double t1)? _title;
        private Border? _snipMask;
        private Ellipse? _clickRing;
        private Grid? _titleCard;

        public Timeline(Scene scene, double dt) { _scene = scene; _dt = dt; }

        public void At(double t, Action act) => _events.Add((t, act, false));
        public void Move(Point from, Point to, double t0, double t1, Action<Point>? each = null) => _moves.Add((from, to, t0, t1, each));
        public void Caption(string text, double t0, double t1) => _captions.Add((text, t0, t1));
        public void Snip(Rect rect, double t0, double t1) => _snips.Add((rect, t0, t1));
        public void Click(double t) => _clicks.Add(t);
        public void Title(double t0, double t1) => _title = (t0, t1);

        public void Step(double t)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                var (at, act, done) = _events[i];
                if (!done && t >= at) { _events[i] = (at, act, true); act(); }
            }
            foreach (var (from, to, t0, t1, each) in _moves)
            {
                if (t < t0 || t > t1 + _dt) continue;
                double k = Ease.InOutCubic(Math.Clamp((t - t0) / (t1 - t0), 0, 1));
                _scene.Pointer = new Point(from.X + (to.X - from.X) * k, from.Y + (to.Y - from.Y) * k);
                each?.Invoke(_scene.Pointer);
            }

            // Captions fade in and out.
            double opacity = 0; string? text = null;
            foreach (var (s, t0, t1) in _captions)
            {
                if (t < t0 || t > t1) continue;
                text = s;
                opacity = Math.Min(1, Math.Min((t - t0) / 0.3, (t1 - t) / 0.3));
            }
            if (text is not null) _scene.CaptionText.Text = text;
            _scene.Caption.Opacity = Math.Clamp(opacity, 0, 1);

            // The snip: the screen dims, a bright rectangle is drawn out.
            var snip = _snips.FirstOrDefault(s => t >= s.t0 && t <= s.t1 + 0.25);
            if (snip.rect.Width > 0)
            {
                _snipMask ??= MakeSnipMask();
                double k = Math.Clamp((t - snip.t0) / (snip.t1 - snip.t0), 0, 1);
                double fade = t > snip.t1 ? 1 - (t - snip.t1) / 0.25 : 1;
                var r = new Rect(snip.rect.X, snip.rect.Y, snip.rect.Width * Math.Max(0.02, k), snip.rect.Height * Math.Max(0.02, k));
                UpdateSnipMask(r, Math.Clamp(fade, 0, 1));
            }
            else if (_snipMask is not null) { _scene.Overlay.Children.Remove(_snipMask); _snipMask = null; }

            // A click: a ring blooms under the pointer.
            var click = _clicks.FirstOrDefault(c => t >= c && t <= c + 0.35);
            if (click > 0)
            {
                _clickRing ??= Ring();
                double k = (t - click) / 0.35;
                double d = 10 + 40 * k;
                _clickRing.Width = _clickRing.Height = d;
                _clickRing.Opacity = 1 - k;
                System.Windows.Controls.Canvas.SetLeft(_clickRing, _scene.Pointer.X - d / 2 + 2);
                System.Windows.Controls.Canvas.SetTop(_clickRing, _scene.Pointer.Y - d / 2 + 2);
            }
            else if (_clickRing is not null) { _scene.Overlay.Children.Remove(_clickRing); _clickRing = null; }

            // The title card at the end.
            if (_title is { } tt && t >= tt.t0)
            {
                _titleCard ??= TitleCard();
                _titleCard.Opacity = Math.Clamp((t - tt.t0) / 0.6, 0, 1);
                _scene.Cursor.Opacity = 1 - _titleCard.Opacity;
            }
        }

        private Border MakeSnipMask()
        {
            var mask = new Border { Width = W, Height = H, Background = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0)) };
            _scene.Overlay.Children.Add(mask);
            Panel.SetZIndex(mask, 50);
            return mask;
        }

        private void UpdateSnipMask(Rect r, double opacity)
        {
            if (_snipMask is null) return;
            _snipMask.Opacity = opacity;
            var whole = new RectangleGeometry(new Rect(0, 0, W, H));
            var hole = new RectangleGeometry(r);
            _snipMask.Clip = Geometry.Combine(whole, hole, GeometryCombineMode.Exclude, null);
            if (_snipMask.Tag is not Rectangle frame)
            {
                frame = new Rectangle { Stroke = Brushes.White, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 6, 4 } };
                _scene.Overlay.Children.Add(frame);
                Panel.SetZIndex(frame, 51);
                _snipMask.Tag = frame;
            }
            frame.Opacity = opacity;
            frame.Width = r.Width; frame.Height = r.Height;
            System.Windows.Controls.Canvas.SetLeft(frame, r.X); System.Windows.Controls.Canvas.SetTop(frame, r.Y);
            if (opacity <= 0.01) { _scene.Overlay.Children.Remove(frame); _snipMask.Tag = null; }
        }

        private Ellipse Ring()
        {
            var ring = new Ellipse { Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x66, 0xE5)), StrokeThickness = 3 };
            _scene.Overlay.Children.Add(ring);
            Panel.SetZIndex(ring, 90);
            return ring;
        }

        private Grid TitleCard()
        {
            var g = new Grid { Width = W, Height = H, Background = new SolidColorBrush(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF)) };
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = Strings.AppName, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"), FontSize = 110, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)), HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new TextBlock { Text = "Screenshots, hung out to dry. For Windows.", FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"), FontSize = 40, Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x73)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
            stack.Children.Add(new TextBlock { Text = "github.com/mohabujubara/clothesline", FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"), FontSize = 30, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x66, 0xE5)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
            stack.Children.Add(new TextBlock { Text = "Free and open source", FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"), FontSize = 24, Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) });
            g.Children.Add(stack);
            _scene.Overlay.Children.Add(g);
            Panel.SetZIndex(g, 200);
            return g;
        }
    }

    /// <summary>The capture lifting off and flying to the line, drawn the way FlightWindow draws it.</summary>
    private sealed class Flight
    {
        private readonly Scene _scene;
        private readonly Grid _container = new();
        private readonly Border _glass, _edge;
        private readonly Image _photo;
        private readonly Grid _clip;
        private readonly RotateTransform _rotate = new();
        private readonly Rect _from, _to;
        private readonly double _tilt, _start;
        private readonly Action _landed;
        private bool _done;

        public Flight(Scene scene, BitmapSource image, Rect from, Rect to, double tilt, double start, Action landed)
        {
            _scene = scene; _from = from; _to = to; _tilt = tilt; _start = start; _landed = landed;
            _glass = new Border { Background = new SolidColorBrush(Theme.GlassFill), Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.24, BlurRadius = 20, ShadowDepth = 5, Direction = 270 } };
            _photo = new Image { Source = image, Stretch = Stretch.Fill };
            _edge = new Border { BorderThickness = new Thickness(0.75), BorderBrush = new LinearGradientBrush(Theme.GlassEdgeTop, Theme.GlassEdgeBottom, 90) };
            _clip = Pegs.Create(seed: 7);
            _clip.HorizontalAlignment = HorizontalAlignment.Left; _clip.VerticalAlignment = VerticalAlignment.Top;
            _container.Children.Add(_glass); _container.Children.Add(_photo); _container.Children.Add(_edge); _container.Children.Add(_clip);
            _container.RenderTransform = _rotate;
            scene.Overlay.Children.Add(_container);
            Panel.SetZIndex(_container, 60);
            Update(start);
        }

        public void Update(double t)
        {
            if (_done) return;
            double raw = Math.Min(1, (t - _start) / FlightWindow.Duration);
            double k = Ease.InOutCubic(raw);
            double chrome = Ease.Smooth(k, 0.35, 1);
            double L(double a, double b) => a + (b - a) * k;
            double w = L(_from.Width, _to.Width), h = L(_from.Height, _to.Height);
            double topX = L(_from.Left + _from.Width / 2, _to.Left + _to.Width / 2);
            double topY = L(_from.Top, _to.Top) - Math.Sin(Math.PI * k) * 30;
            double inset = Layout.FrameInset * k, radius = L(0, Layout.FrameRadius);
            _container.Width = w; _container.Height = h;
            System.Windows.Controls.Canvas.SetLeft(_container, topX - w / 2);
            System.Windows.Controls.Canvas.SetTop(_container, topY);
            _rotate.CenterX = w / 2; _rotate.CenterY = 0; _rotate.Angle = _tilt * k;
            _glass.CornerRadius = _edge.CornerRadius = new CornerRadius(radius);
            _glass.Opacity = _edge.Opacity = _clip.Opacity = chrome;
            _photo.Margin = new Thickness(inset);
            double pr = Math.Max(0, radius - inset);
            _photo.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, w - 2 * inset), Math.Max(0, h - 2 * inset)), pr, pr);
            _clip.Margin = new Thickness(w / 2 - _clip.Width / 2, -(Layout.PinHeight - 12), 0, 0);
            if (raw >= 1)
            {
                _done = true;
                _landed();
                _scene.Overlay.Children.Remove(_container);
            }
        }
    }

    /// <summary>A discarded card falling off the screen.</summary>
    private sealed class Fall
    {
        private readonly Scene _scene;
        private readonly Grid _container = new();
        private readonly RotateTransform _rotate = new();
        private readonly Rect _card;
        private readonly double _tilt, _start;
        private bool _done;

        public Fall(Scene scene, BitmapSource image, Rect card, double tilt, double start)
        {
            _scene = scene; _card = card; _tilt = tilt; _start = start;
            var glass = new Border { Background = new SolidColorBrush(Theme.GlassFill), CornerRadius = new CornerRadius(Layout.FrameRadius), Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.24, BlurRadius = 20, ShadowDepth = 5, Direction = 270 } };
            var photo = new Image { Source = image, Stretch = Stretch.Fill, Margin = new Thickness(Layout.FrameInset) };
            double pr = Layout.FrameRadius - Layout.FrameInset;
            photo.Clip = new RectangleGeometry(new Rect(0, 0, card.Width - 2 * Layout.FrameInset, card.Height - 2 * Layout.FrameInset), pr, pr);
            var clip = Pegs.Create(seed: 3);
            clip.HorizontalAlignment = HorizontalAlignment.Left; clip.VerticalAlignment = VerticalAlignment.Top;
            clip.Margin = new Thickness(card.Width / 2 - clip.Width / 2, -(Layout.PinHeight - 12), 0, 0);
            _container.Width = card.Width; _container.Height = card.Height;
            _container.Children.Add(glass); _container.Children.Add(photo); _container.Children.Add(clip);
            _container.RenderTransform = _rotate;
            scene.Overlay.Children.Add(_container);
            Panel.SetZIndex(_container, 60);
            Update(start);
        }

        public void Update(double t)
        {
            if (_done) return;
            double raw = Math.Min(1, (t - _start) / 0.55);
            double e = raw * raw * raw;
            System.Windows.Controls.Canvas.SetLeft(_container, _card.Left);
            System.Windows.Controls.Canvas.SetTop(_container, _card.Top + 520 * e);
            _rotate.CenterX = _card.Width / 2; _rotate.CenterY = 0;
            _rotate.Angle = _tilt + (_tilt * 7 + 20) * e;
            _container.Opacity = 1 - e;
            if (raw >= 1) { _done = true; _scene.Overlay.Children.Remove(_container); }
        }
    }
}
