using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Clothesline.Core;
using Clothesline.Interop;
using Line = Clothesline.Core.Line;
using Path = System.Windows.Shapes.Path;

namespace Clothesline.UI;

/// <summary>
/// The screenshot, enlarged, with a pen in your hand. Ballpoint, highlighter,
/// circle, box and arrow; undo and redo; every mark is saved into the file as
/// you go, and the untouched original is kept so you can always go back.
/// </summary>
public sealed class MarkupWindow : Window
{
    private static readonly Dictionary<string, MarkupWindow> Open = new(StringComparer.OrdinalIgnoreCase);

    public static void Show(string path, Line line)
    {
        if (Open.TryGetValue(path, out var existing)) { existing.Activate(); return; }
        var full = Thumbnails.Load(path, int.MaxValue, 4);
        if (full is null) { System.Media.SystemSounds.Beep.Play(); return; }
        var w = new MarkupWindow(path, full.Value, line);
        Open[path] = w;
        w.Closed += (_, _) => Open.Remove(path);
        w.Show();
        w.Activate();
    }

    private enum Tool { Pen, Highlighter, Circle, Box, Arrow }

    private readonly string _path;
    private readonly Line _line;
    private readonly BitmapSource _image;
    private readonly InkCanvas _ink;
    private readonly ShapeLayer _shapes;
    private readonly Grid _page;
    private readonly double _scale;      // display units per image pixel
    private readonly Stack<object> _undo = new();
    private readonly Stack<object> _redo = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly TextBlock _status = new();
    private readonly List<ToggleButton> _toolButtons = new();
    private readonly List<Border> _swatches = new();
    private readonly List<ToggleButton> _sizeButtons = new();
    private Tool _tool = Tool.Pen;
    private Color _color = Color.FromRgb(0xE5, 0x39, 0x35);
    private int _size = 1; // 0 thin, 1 medium, 2 thick
    private bool _dirty;
    private Button _undoButton = null!, _redoButton = null!;

    private static readonly Color[] Palette =
    {
        Color.FromRgb(0xE5, 0x39, 0x35), Color.FromRgb(0xFF, 0xC1, 0x07), Color.FromRgb(0x1E, 0x88, 0xE5),
        Color.FromRgb(0x43, 0xA0, 0x47), Color.FromRgb(0x12, 0x12, 0x12), Color.FromRgb(0xFF, 0xFF, 0xFF),
    };

    private MarkupWindow(string path, Thumbnail full, Line line)
    {
        _path = path;
        _line = line;
        _image = full.Image;

        Title = $"{System.IO.Path.GetFileName(path)} · {Strings.MarkUp}";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = true;
        Topmost = true;
        FlowDirection = Strings.Flow;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 12;
        UseLayoutRounding = true;

        // Fit the picture comfortably into the screen it was taken on.
        var display = Displays.UnderPointer();
        double availW = display.Work.Width / display.Scale * 0.86, availH = display.Work.Height / display.Scale * 0.86 - 56;
        _scale = Math.Min(1.0 * Math.Max(1, availW / full.PixelWidth), availH / full.PixelHeight);
        _scale = Math.Min(_scale, Math.Max(1, Math.Min(availW / full.PixelWidth, availH / full.PixelHeight)));
        if (full.PixelWidth * _scale < 360) _scale = Math.Min(360.0 / full.PixelWidth, availW / full.PixelWidth);
        double imgW = full.PixelWidth * _scale, imgH = full.PixelHeight * _scale;

        bool dark = Theme.AppsDark;
        var chrome = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(dark ? Color.FromRgb(0x20, 0x20, 0x22) : Color.FromRgb(0xF4, 0xF4, 0xF6)),
            BorderBrush = new SolidColorBrush(dark ? Theme.Gray(1, 0.12) : Theme.Gray(0, 0.14)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(24),
            Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 10, Direction = 270, Opacity = 0.35, Color = Colors.Black },
        };
        var root = new DockPanel();
        chrome.Child = root;
        Content = chrome;
        Foreground = new SolidColorBrush(dark ? Theme.Gray(0.95) : Theme.Gray(0.12));

        var toolbar = BuildToolbar(dark);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        // The page: the picture, the ink above it, the shapes above that.
        _page = new Grid { Width = imgW, Height = imgH, Margin = new Thickness(12, 4, 12, 12), ClipToBounds = true };
        _page.Children.Add(new Border
        {
            Child = new Image { Source = _image, Stretch = Stretch.Fill },
            CornerRadius = new CornerRadius(6),
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Direction = 270, Opacity = 0.25, Color = Colors.Black },
        });
        _ink = new InkCanvas { Background = Brushes.Transparent, EditingMode = InkCanvasEditingMode.Ink, Cursor = Cursors.Pen };
        _ink.StrokeCollected += (_, e) => Commit(e.Stroke);
        _page.Children.Add(_ink);
        _shapes = new ShapeLayer(this) { IsHitTestVisible = false };
        _page.Children.Add(_shapes);
        root.Children.Add(new Viewbox { Child = _page, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Center });

        Width = imgW + 24 + 48; Height = imgH + 16 + 48 + 48;
        WindowStartupLocation = WindowStartupLocation.Manual;
        var work = display.Work;
        Left = (work.Left + (work.Width - Width * display.Scale) / 2) / display.Scale;
        Top = (work.Top + (work.Height - Height * display.Scale) / 2) / display.Scale;

        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(); };
        KeyDown += OnKey;
        Closing += (_, _) => { if (_saveTimer.IsEnabled) { _saveTimer.Stop(); Save(); } };
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == chrome || e.OriginalSource == toolbar) DragMove(); };

        ApplyTool();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        FullScreen.Own.Add(hwnd);
    }

    // MARK: Toolbar

    private UIElement BuildToolbar(bool dark)
    {
        var bar = new WrapPanel { Margin = new Thickness(12, 10, 12, 6), VerticalAlignment = VerticalAlignment.Center };

        void AddTool(Tool t, string glyph, string label)
        {
            var b = ToolButton(glyph, label, dark);
            b.Click += (_, _) => { _tool = t; ApplyTool(); };
            _toolButtons.Add(b);
            bar.Children.Add(b);
        }
        AddTool(Tool.Pen, "M3,13 L11,5 L13,7 L5,15 Z M2,16 L5,15", Strings.Pen);
        AddTool(Tool.Highlighter, "M3,12 L10,5 L13,8 L6,15 Z M2,15 H8 M13,8 L15,6", Strings.Highlighter);
        AddTool(Tool.Circle, "M9,3 A6,6 0 1 1 8.9,3", Strings.Circle);
        AddTool(Tool.Box, "M3,4 H15 V14 H3 Z", Strings.Box);
        AddTool(Tool.Arrow, "M3,15 L15,3 M9,3 H15 V9", Strings.Arrow);
        bar.Children.Add(Gap());

        for (int i = 0; i < Palette.Length; i++)
        {
            var c = Palette[i];
            var swatch = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Margin = new Thickness(3, 0, 3, 0),
                Background = new SolidColorBrush(c), Cursor = Cursors.Hand, ToolTip = Strings.Colour,
                BorderBrush = new SolidColorBrush(Theme.Gray(dark ? 1 : 0, 0.25)), BorderThickness = new Thickness(1),
            };
            swatch.MouseLeftButtonDown += (_, e) => { e.Handled = true; _color = c; ApplyTool(); };
            _swatches.Add(swatch);
            bar.Children.Add(swatch);
        }
        bar.Children.Add(Gap());

        for (int i = 0; i < 3; i++)
        {
            int size = i;
            double d = 5 + i * 4;
            var b = new ToggleButton
            {
                Content = new Ellipse { Width = d, Height = d, Fill = Foreground },
                Width = 30, Height = 30, Margin = new Thickness(1, 0, 1, 0), ToolTip = Strings.Thickness,
                Style = ToolStyle(dark),
            };
            b.Click += (_, _) => { _size = size; ApplyTool(); };
            _sizeButtons.Add(b);
            bar.Children.Add(b);
        }
        bar.Children.Add(Gap());

        _undoButton = ActionButton("M6,4 L2,8 L6,12 M2,8 H11 A4,4 0 0 1 11,16 H8", Strings.Undo + " (Ctrl+Z)", dark);
        _undoButton.Click += (_, _) => Undo();
        _redoButton = ActionButton("M12,4 L16,8 L12,12 M16,8 H7 A4,4 0 0 0 7,16 H10", Strings.Redo + " (Ctrl+Y)", dark);
        _redoButton.Click += (_, _) => Redo();
        bar.Children.Add(_undoButton);
        bar.Children.Add(_redoButton);
        bar.Children.Add(Gap());

        var revert = TextButton(Strings.Revert, dark);
        revert.Click += (_, _) => Revert();
        var copy = TextButton(Strings.Copy, dark);
        copy.Click += (_, _) => { Save(); _line.CopyPath(_path); _status.Text = Strings.Copied; };
        var done = TextButton(Strings.Done, dark, accent: true);
        done.Click += (_, _) => Close();
        bar.Children.Add(revert);
        bar.Children.Add(copy);
        bar.Children.Add(done);

        _status.Margin = new Thickness(12, 0, 0, 0);
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.Opacity = 0.6;
        bar.Children.Add(_status);
        return bar;
    }

    private static FrameworkElement Gap() => new Rectangle { Width = 1, Height = 20, Fill = new SolidColorBrush(Theme.Gray(0.5, 0.35)), Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };

    private Style ToolStyle(bool dark)
    {
        var style = new Style(typeof(ToggleButton));
        var template = new ControlTemplate(typeof(ToggleButton));
        var border = new FrameworkElementFactory(typeof(Border), "Bd");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Theme.Gray(dark ? 1 : 0, 0.08)), "Bd"));
        var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        on.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(dark ? Color.FromArgb(0x50, 0x4A, 0x8C, 0xFF) : Color.FromArgb(0x30, 0x1E, 0x66, 0xE5)), "Bd"));
        template.Triggers.Add(hover);
        template.Triggers.Add(on);
        style.Setters.Add(new Setter(TemplateProperty, template));
        style.Setters.Add(new Setter(ForegroundProperty, Foreground));
        return style;
    }

    private ToggleButton ToolButton(string glyph, string label, bool dark)
    {
        return new ToggleButton
        {
            Content = Glyph(glyph), Width = 32, Height = 30, Margin = new Thickness(1, 0, 1, 0), ToolTip = label, Style = ToolStyle(dark),
        };
    }

    private Button ActionButton(string glyph, string label, bool dark)
    {
        var b = new Button { Content = Glyph(glyph), Width = 32, Height = 30, Margin = new Thickness(1, 0, 1, 0), ToolTip = label };
        b.Style = ButtonStyle(dark, accent: false);
        return b;
    }

    private Button TextButton(string text, bool dark, bool accent = false)
    {
        var b = new Button { Content = text, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(3, 0, 3, 0), FontWeight = accent ? FontWeights.SemiBold : FontWeights.Normal };
        b.Style = ButtonStyle(dark, accent);
        return b;
    }

    private Style ButtonStyle(bool dark, bool accent)
    {
        var style = new Style(typeof(Button));
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "Bd");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));
        border.SetValue(Border.BackgroundProperty, accent
            ? new SolidColorBrush(Color.FromRgb(0x1E, 0x66, 0xE5))
            : Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, accent ? new SolidColorBrush(Color.FromRgb(0x2A, 0x74, 0xF0)) : new SolidColorBrush(Theme.Gray(dark ? 1 : 0, 0.08)), "Bd"));
        var disabled = new Trigger { Property = IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(OpacityProperty, 0.35));
        template.Triggers.Add(hover);
        template.Triggers.Add(disabled);
        style.Setters.Add(new Setter(TemplateProperty, template));
        style.Setters.Add(new Setter(ForegroundProperty, accent ? Brushes.White : Foreground));
        return style;
    }

    private Path Glyph(string data) => new()
    {
        Data = Geometry.Parse(data), Stroke = Foreground, StrokeThickness = 1.6, Width = 18, Height = 18, Stretch = Stretch.None,
        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
    };

    // MARK: Tools

    private double Thickness => (_size switch { 0 => 2.2, 1 => 4, _ => 7 }) * Math.Max(0.6, Math.Min(1.6, 1 / _scale));

    private void ApplyTool()
    {
        for (int i = 0; i < _toolButtons.Count; i++) _toolButtons[i].IsChecked = (int)_tool == i;
        for (int i = 0; i < _sizeButtons.Count; i++) _sizeButtons[i].IsChecked = _size == i;
        foreach (var s in _swatches)
        {
            bool on = ((SolidColorBrush)s.Background).Color == _color;
            s.BorderThickness = new Thickness(on ? 2.5 : 1);
            s.BorderBrush = on ? new SolidColorBrush(Theme.AppsDark ? Colors.White : Color.FromRgb(0x1E, 0x66, 0xE5)) : new SolidColorBrush(Theme.Gray(Theme.AppsDark ? 1 : 0, 0.25));
        }

        bool inking = _tool is Tool.Pen or Tool.Highlighter;
        _ink.EditingMode = inking ? InkCanvasEditingMode.Ink : InkCanvasEditingMode.None;
        _shapes.IsHitTestVisible = !inking;
        _shapes.Cursor = Cursors.Cross;
        bool hi = _tool == Tool.Highlighter;
        _ink.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = hi ? Color.FromArgb(0x70, _color.R, _color.G, _color.B) : _color,
            Width = hi ? Thickness * 4 : Thickness,
            Height = hi ? Thickness * 4 : Thickness,
            IsHighlighter = hi,
            StylusTip = hi ? StylusTip.Rectangle : StylusTip.Ellipse,
            FitToCurve = true,
            IgnorePressure = true,
        };
        _undoButton.IsEnabled = _undo.Count > 0;
        _redoButton.IsEnabled = _redo.Count > 0;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (e.Key == Key.Escape) { Close(); return; }
        if (ctrl && e.Key == Key.Z) { Undo(); return; }
        if (ctrl && (e.Key == Key.Y || (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))) { Redo(); return; }
        if (ctrl && e.Key == Key.C) { Save(); _line.CopyPath(_path); _status.Text = Strings.Copied; return; }
        if (ctrl) return;
        switch (e.Key)
        {
            case Key.P: _tool = Tool.Pen; break;
            case Key.H: _tool = Tool.Highlighter; break;
            case Key.C: _tool = Tool.Circle; break;
            case Key.B: case Key.R: _tool = Tool.Box; break;
            case Key.A: _tool = Tool.Arrow; break;
            case Key.D1: _size = 0; break;
            case Key.D2: _size = 1; break;
            case Key.D3: _size = 2; break;
            default: return;
        }
        ApplyTool();
    }

    // MARK: Marks, undo, redo

    private void Commit(object mark)
    {
        _undo.Push(mark);
        _redo.Clear();
        _dirty = true;
        ApplyTool();
        _status.Text = "";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Undo()
    {
        if (_undo.Count == 0) return;
        var mark = _undo.Pop();
        if (mark is Stroke s) _ink.Strokes.Remove(s); else if (mark is ShapeMark m) _shapes.Remove(m);
        _redo.Push(mark);
        _dirty = true;
        ApplyTool();
        _saveTimer.Stop(); _saveTimer.Start();
    }

    private void Redo()
    {
        if (_redo.Count == 0) return;
        var mark = _redo.Pop();
        if (mark is Stroke s) _ink.Strokes.Add(s); else if (mark is ShapeMark m) _shapes.Add(m);
        _undo.Push(mark);
        _dirty = true;
        ApplyTool();
        _saveTimer.Stop(); _saveTimer.Start();
    }

    // MARK: Saving

    private static string OriginalsFolder => System.IO.Path.Combine(Settings.Folder, "Originals");

    private string BackupPath => System.IO.Path.Combine(OriginalsFolder, System.IO.Path.GetFileName(_path));

    private void Save()
    {
        if (!_dirty) return;
        try
        {
            // The untouched picture is kept once, so Revert is always possible.
            Directory.CreateDirectory(OriginalsFolder);
            if (!File.Exists(BackupPath)) File.Copy(_path, BackupPath);

            int pw = _image.PixelWidth, ph = _image.PixelHeight;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(_image, new Rect(0, 0, pw, ph));
                dc.PushTransform(new ScaleTransform(1 / _scale, 1 / _scale));
                _ink.Strokes.Draw(dc);
                _shapes.Draw(dc);
                dc.Pop();
            }
            var rtb = new RenderTargetBitmap(pw, ph, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            BitmapEncoder encoder = System.IO.Path.GetExtension(_path).ToLowerInvariant() is ".jpg" or ".jpeg"
                ? new JpegBitmapEncoder { QualityLevel = 92 }
                : new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            var tmp = _path + ".tmp";
            using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)) encoder.Save(stream);
            File.Move(tmp, _path, true);
            _dirty = false;
            _status.Text = Strings.Saved;
            _line.ReloadThumbnail(_path);
        }
        catch (Exception e)
        {
            Log.Error($"Could not save the markup: {e.Message}");
            _status.Text = Strings.CouldNotSave;
        }
    }

    private void Revert()
    {
        _saveTimer.Stop();
        _ink.Strokes.Clear();
        _shapes.Clear();
        _undo.Clear();
        _redo.Clear();
        try
        {
            if (File.Exists(BackupPath))
            {
                File.Copy(BackupPath, _path, true);
                File.Delete(BackupPath);
                _line.ReloadThumbnail(_path);
            }
            // The editor keeps showing the picture it opened with, which is the original if it was never marked before.
            var fresh = Thumbnails.Load(_path, int.MaxValue, 3);
            if (fresh is not null) ((Image)((Border)_page.Children[0]).Child).Source = fresh.Value.Image;
        }
        catch (Exception e) { Log.Error($"Could not revert: {e.Message}"); }
        _dirty = false;
        _status.Text = Strings.Reverted;
        ApplyTool();
    }

    /// <summary>Draws a stroke and a circle by code, saves, undoes one, saves again. For checking the pipeline without a desktop.</summary>
    public static bool SelfTest(string path, Line line)
    {
        Show(path, line);
        if (!Open.TryGetValue(path, out var w)) return false;
        try
        {
            long before = new FileInfo(path).Length;
            var points = new StylusPointCollection();
            for (int i = 0; i < 40; i++) points.Add(new StylusPoint(20 + i * 4, 30 + Math.Sin(i / 4.0) * 12));
            var stroke = new Stroke(points, w._ink.DefaultDrawingAttributes);
            w._ink.Strokes.Add(stroke);
            w.Commit(stroke);
            var circle = new ShapeMark(Tool.Circle, new Point(60, 40), new Point(160, 110), w._color, w.Thickness);
            w._shapes.Add(circle);
            w.Commit(circle);
            w._saveTimer.Stop();
            w.Save();
            bool savedOnce = !w._dirty && File.Exists(w.BackupPath);
            w.Undo();
            w._saveTimer.Stop();
            w.Save();
            bool undone = w._undo.Count == 1 && w._redo.Count == 1;
            var reread = Thumbnails.Load(path, int.MaxValue, 2);
            bool sameSize = reread is not null && reread.Value.PixelWidth == w._image.PixelWidth && reread.Value.PixelHeight == w._image.PixelHeight;
            Log.Notice($"Markup self test: savedOnce={savedOnce} undone={undone} sameSize={sameSize} bytes {before}->{new FileInfo(path).Length}");
            return savedOnce && undone && sameSize;
        }
        catch (Exception e)
        {
            Log.Error($"Markup self test failed: {e}");
            return false;
        }
        finally { w.Close(); }
    }

    // MARK: Shapes

    private sealed record ShapeMark(Tool Kind, Point From, Point To, Color Color, double Thickness);

    /// <summary>Circles, boxes and arrows, drawn by dragging. They live here, above the ink.</summary>
    private sealed class ShapeLayer : FrameworkElement
    {
        private readonly MarkupWindow _owner;
        private readonly List<ShapeMark> _marks = new();
        private ShapeMark? _live;
        private Point _start;

        public ShapeLayer(MarkupWindow owner) { _owner = owner; }

        public void Add(ShapeMark m) { _marks.Add(m); InvalidateVisual(); }
        public void Remove(ShapeMark m) { _marks.Remove(m); InvalidateVisual(); }
        public void Clear() { _marks.Clear(); InvalidateVisual(); }

        protected override HitTestResult HitTestCore(PointHitTestParameters p) => new PointHitTestResult(this, p.HitPoint);

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            _start = e.GetPosition(this);
            _live = new ShapeMark(_owner._tool, _start, _start, _owner._color, _owner.Thickness);
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_live is null) return;
            _live = _live with { To = e.GetPosition(this) };
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_live is null) return;
            ReleaseMouseCapture();
            var mark = _live with { To = e.GetPosition(this) };
            _live = null;
            if ((mark.To - mark.From).Length >= 4) { _marks.Add(mark); _owner.Commit(mark); }
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            Draw(dc);
        }

        public void Draw(DrawingContext dc)
        {
            foreach (var m in _marks) DrawMark(dc, m);
            if (_live is not null) DrawMark(dc, _live);
        }

        private static void DrawMark(DrawingContext dc, ShapeMark m)
        {
            var pen = new Pen(new SolidColorBrush(m.Color), m.Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            var rect = new Rect(m.From, m.To);
            switch (m.Kind)
            {
                case Tool.Circle:
                    dc.DrawEllipse(null, pen, new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), rect.Width / 2, rect.Height / 2);
                    break;
                case Tool.Box:
                    dc.DrawRoundedRectangle(null, pen, rect, m.Thickness, m.Thickness);
                    break;
                case Tool.Arrow:
                {
                    dc.DrawLine(pen, m.From, m.To);
                    var dir = m.To - m.From;
                    if (dir.Length < 1) break;
                    dir.Normalize();
                    double head = Math.Max(10, m.Thickness * 4);
                    var left = m.To - dir * head + new Vector(-dir.Y, dir.X) * head * 0.55;
                    var right = m.To - dir * head - new Vector(-dir.Y, dir.X) * head * 0.55;
                    dc.DrawLine(pen, m.To, left);
                    dc.DrawLine(pen, m.To, right);
                    break;
                }
            }
        }
    }
}
