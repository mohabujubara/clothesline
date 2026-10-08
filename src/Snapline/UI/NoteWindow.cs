using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Snapline.Core;
using Snapline.Interop;
using Line = Snapline.Core.Line;

namespace Snapline.UI;

/// <summary>A sheet of coloured paper to write on. What you type is drawn onto the note on the line as you go.</summary>
public sealed class NoteWindow : Window
{
    private static readonly Dictionary<string, NoteWindow> Open = new(StringComparer.OrdinalIgnoreCase);

    public static void Show(string path, Line line)
    {
        if (Open.TryGetValue(path, out var existing)) { existing.Activate(); return; }
        var data = Notes.Get(path);
        if (data is null) return;
        var w = new NoteWindow(path, data, line);
        Open[path] = w;
        w.Closed += (_, _) => Open.Remove(path);
        w.Show();
        w.Activate();
    }

    private readonly string _path;
    private readonly Line _line;
    private readonly TextBox _text;
    private readonly Border _sheet;
    private readonly List<Border> _swatches = new();
    private string _color;
    private readonly DispatcherTimer _save = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private bool _dirty;

    private NoteWindow(string path, NoteData data, Line line)
    {
        _path = path;
        _line = line;
        _color = data.Color;
        Title = Strings.EditNote;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;
        Topmost = true;
        Width = 420; Height = 340;
        FlowDirection = Strings.Flow;
        FontFamily = new FontFamily("Segoe UI");
        WindowStartupLocation = WindowStartupLocation.Manual;
        var display = Displays.UnderPointer();
        Left = (display.Work.Left + (display.Work.Width - Width * display.Scale) / 2) / display.Scale;
        Top = (display.Work.Top + (display.Work.Height - Height * display.Scale) / 2) / display.Scale;

        _sheet = new Border { Padding = new Thickness(22, 18, 22, 14) };
        var root = new DockPanel();
        _sheet.Child = root;
        Content = _sheet;

        // Colours and Done along the bottom.
        var bottom = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        var done = new Button { Content = Strings.Done, Padding = new Thickness(14, 5, 14, 5), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontWeight = FontWeights.SemiBold };
        done.Click += (_, _) => Close();
        DockPanel.SetDock(done, Dock.Right);
        bottom.Children.Add(done);
        var colours = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var key in Notes.Colors)
        {
            var k = key;
            var swatch = new Border
            {
                Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Notes.Paper(key)), Cursor = Cursors.Hand, ToolTip = Strings.NoteName(key),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0, 0, 0)), BorderThickness = new Thickness(1),
            };
            swatch.MouseLeftButtonDown += (_, e) => { e.Handled = true; _color = k; Paint(); Touch(); };
            _swatches.Add(swatch);
            colours.Children.Add(swatch);
        }
        bottom.Children.Add(colours);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        _text = new TextBox
        {
            Text = data.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 20,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x30, 0x1E)), CaretBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x30, 0x1E)),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0),
        };
        _text.TextChanged += (_, _) => Touch();
        root.Children.Add(_text);

        _save.Tick += (_, _) => { _save.Stop(); Save(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Closing += (_, _) => { _save.Stop(); Save(); };
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is Border or DockPanel) DragMove(); };
        Loaded += (_, _) => { _text.Focus(); _text.CaretIndex = _text.Text.Length; };
        Paint();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        FullScreen.Own.Add(hwnd);
        int round = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    private void Paint()
    {
        var paper = Notes.Paper(_color);
        _sheet.Background = new LinearGradientBrush(Lighten(paper, 0.1), Darken(paper, 0.06), 90);
        Background = _sheet.Background;
        foreach (var s in _swatches)
        {
            bool on = ((SolidColorBrush)s.Background).Color == paper;
            s.BorderThickness = new Thickness(on ? 2.5 : 1);
            s.BorderBrush = new SolidColorBrush(on ? Color.FromRgb(0x3A, 0x30, 0x1E) : Color.FromArgb(0x50, 0, 0, 0));
        }
    }

    private void Touch()
    {
        _dirty = true;
        _save.Stop();
        _save.Start();
    }

    /// <summary>The note editor as a plain element the film can type into.</summary>
    public static (FrameworkElement content, Action<string> type, Action save) ForDemo(string path, Line line)
    {
        var data = Notes.Get(path) ?? new NoteData();
        var w = new NoteWindow(path, data, line);
        var content = (FrameworkElement)w.Content;
        w.Content = null;
        return (content, text => { w._text.Text = text; w._dirty = true; }, () => { w._save.Stop(); w.Save(); });
    }

    private void Save()
    {
        if (!_dirty) return;
        _dirty = false;
        try
        {
            Notes.Update(_path, _text.Text, _color);
            _line.ReloadThumbnail(_path);
        }
        catch (Exception e) { Log.Error($"Could not save the note: {e.Message}"); }
    }

    private static Color Lighten(Color c, double k) => Color.FromRgb((byte)(c.R + (255 - c.R) * k), (byte)(c.G + (255 - c.G) * k), (byte)(c.B + (255 - c.B) * k));
    private static Color Darken(Color c, double k) => Color.FromRgb((byte)(c.R * (1 - k)), (byte)(c.G * (1 - k)), (byte)(c.B * (1 - k)));
}
