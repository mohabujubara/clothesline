using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Snapline.Core;
using Snapline.Interop;

namespace Snapline.UI;

/// <summary>Everything you can change, on one page. Changes apply as you make them.</summary>
public sealed class SettingsWindow : Window
{
    private static SettingsWindow? _open;
    private readonly Action _apply;
    private TextBox _hotKey = null!;
    private ListBox _folders = null!;
    private TextBlock _inbox = null!;
    private TextBox _offset = null!;
    private bool _building;

    public static void Open(Action apply)
    {
        if (_open is not null) { _open.Activate(); return; }
        _open = new SettingsWindow(apply);
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    /// <summary>After a language change the page is rebuilt in place.</summary>
    public static void Rebuild()
    {
        if (_open is null) return;
        var apply = _open._apply;
        var left = _open.Left; var top = _open.Top;
        _open.Close();
        Open(apply);
        if (_open is not null) { _open.Left = left; _open.Top = top; }
    }

    /// <summary>The settings page as a plain element, for the film.</summary>
    public static FrameworkElement ForDemo(Action apply)
    {
        var w = new SettingsWindow(apply);
        var content = (FrameworkElement)w.Content;
        w.Content = null;
        if (content is ScrollViewer sv) { sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled; sv.MaxHeight = double.PositiveInfinity; }
        var border = new Border { Width = 500, Background = w.Background, Child = content };
        System.Windows.Documents.TextElement.SetForeground(border, w.Foreground);
        return border;
    }

    private SettingsWindow(Action apply)
    {
        _apply = apply;
        Title = $"{Strings.AppName} · {Strings.SettingsTitle}";
        Width = 500; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Icon = Brand.Icon;
        FlowDirection = Strings.Flow;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 13;
        UseLayoutRounding = true;
        Paint();
        Theme.Changed += Paint;
        Closed += (_, _) => Theme.Changed -= Paint;
        Content = Build();
    }

    private void Paint()
    {
        Background = (Brush)Application.Current.Resources["MenuBg"];
        Foreground = (Brush)Application.Current.Resources["MenuFg"];
    }

    private UIElement Build()
    {
        _building = true;
        var s = Settings.Current;
        var stack = new StackPanel { Margin = new Thickness(24, 16, 24, 18) };
        var header = Brand.Lockup(26, Theme.AppsDark);
        header.Margin = new Thickness(0, 0, 0, 6);
        stack.Children.Add(header);

        // The line
        stack.Children.Add(Heading(Strings.SectionLine));
        stack.Children.Add(Check(Strings.RevealAtTopEdge, Strings.RevealAtTopEdgeTip, s.RevealAtTopEdge, v => s.RevealAtTopEdge = v));
        stack.Children.Add(Check(Strings.StayDownWhileUnused, Strings.StayDownWhileUnusedTip, s.StayDownWhileUnused, v => s.StayDownWhileUnused = v));
        stack.Children.Add(Check(Strings.TakeDownAfterDrag, Strings.TakeDownAfterDragTip, s.TakeDownAfterDrag, v => s.TakeDownAfterDrag = v));

        _hotKey = new TextBox { Text = s.HotKey, IsReadOnly = true, Width = 170, Padding = new Thickness(8, 4, 8, 4), ToolTip = Strings.ShortcutTip, HorizontalAlignment = HorizontalAlignment.Left };
        _hotKey.PreviewKeyDown += OnHotKeyPressed;
        stack.Children.Add(Row(Strings.Shortcut, _hotKey));

        _offset = new TextBox { Text = ((int)s.LineOffset).ToString(), Width = 70, Padding = new Thickness(8, 4, 8, 4), ToolTip = Strings.LineDistanceTip, HorizontalAlignment = HorizontalAlignment.Left };
        _offset.LostFocus += (_, _) => { if (double.TryParse(_offset.Text, out var v)) { s.LineOffset = Math.Max(0, v); Changed(); } };
        _offset.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Keyboard.ClearFocus(); if (double.TryParse(_offset.Text, out var v)) { s.LineOffset = Math.Max(0, v); Changed(); } } };
        stack.Children.Add(Row(Strings.LineDistance, _offset, Strings.LineDistanceTip));

        // Look
        stack.Children.Add(Heading(Strings.SectionLook));
        var rope = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var key in Pegs.RopeColors) rope.Items.Add(ColorItem(key));
        rope.SelectedIndex = Math.Max(0, Array.IndexOf(Pegs.RopeColors, s.RopeColor));
        rope.SelectionChanged += (_, _) => { if (rope.SelectedIndex >= 0) { s.RopeColor = Pegs.RopeColors[rope.SelectedIndex]; Changed(); } };
        stack.Children.Add(Row(Strings.RopeColor, rope));

        var pegs = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var key in Pegs.Styles) pegs.Items.Add(PegItem(key));
        pegs.SelectedIndex = Math.Max(0, Array.IndexOf(Pegs.Styles, s.PegStyle));
        pegs.SelectionChanged += (_, _) => { if (pegs.SelectedIndex >= 0) { s.PegStyle = Pegs.Styles[pegs.SelectedIndex]; Changed(); } };
        stack.Children.Add(Row(Strings.PegStyle, pegs));

        stack.Children.Add(Check(Strings.Bows, null, s.Bows, v => s.Bows = v));
        stack.Children.Add(Check(Strings.AutoArrange, Strings.AutoArrangeTip, s.AutoArrange, v => s.AutoArrange = v));

        var appearance = Choice(new[] { ("auto", Strings.Auto), ("light", Strings.Light), ("dark", Strings.Dark) }, s.Appearance, v => s.Appearance = v);
        stack.Children.Add(Row(Strings.Appearance, appearance));

        var language = Choice(new[] { ("auto", Strings.Auto), ("en", "English"), ("ar", "العربية") }, s.Language, v => { s.Language = v; Dispatcher.BeginInvoke(Rebuild); });
        stack.Children.Add(Row(Strings.LanguageLabel, language));

        // Captures
        stack.Children.Add(Heading(Strings.SectionCaptures));
        stack.Children.Add(Check(Strings.CatchClipboard, Strings.CatchClipboardTip, s.CatchClipboard, v => s.CatchClipboard = v));

        var inboxRow = new DockPanel { Margin = new Thickness(0, 6, 0, 2) };
        var change = new Button { Content = Strings.Change, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(change, Dock.Right);
        change.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.OpenInboxFolder, InitialDirectory = Inbox.Folder };
            if (dialog.ShowDialog(this) != true) return;
            var guarded = new List<string> { Shell.ScreenshotsFolder(), Shell.Desktop(),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
            guarded.AddRange(s.WatchFolders.Select(Environment.ExpandEnvironmentVariables));
            if (!Inbox.IsSafeFolder(dialog.FolderName, guarded))
            {
                MessageBox.Show(this, Strings.InboxFolderRefused, Strings.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            s.InboxFolder = dialog.FolderName; Changed(); _inbox.Text = Inbox.Folder;
        };
        inboxRow.Children.Add(change);
        _inbox = new TextBlock { Text = Inbox.Folder, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = 0.7, FlowDirection = FlowDirection.LeftToRight, HorizontalAlignment = Strings.IsRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left };
        var inboxLabel = new StackPanel();
        inboxLabel.Children.Add(new TextBlock { Text = Strings.CaughtCapturesFolder });
        inboxLabel.Children.Add(_inbox);
        inboxRow.Children.Add(inboxLabel);
        stack.Children.Add(inboxRow);

        stack.Children.Add(new TextBlock { Text = Strings.WatchFoldersLabel, Margin = new Thickness(0, 12, 0, 4) });
        _folders = new ListBox { Height = 84, Margin = new Thickness(0, 0, 0, 6), FlowDirection = FlowDirection.LeftToRight };
        _folders.Items.Add(new ListBoxItem { Content = Shell.ScreenshotsFolder(), IsEnabled = false, Opacity = 0.6 });
        foreach (var f in s.WatchFolders) _folders.Items.Add(f);
        stack.Children.Add(_folders);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var add = new Button { Content = Strings.AddFolder, Padding = new Thickness(12, 4, 12, 4) };
        add.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.AddFolder };
            if (dialog.ShowDialog(this) == true && !s.WatchFolders.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase))
            {
                s.WatchFolders.Add(dialog.FolderName);
                _folders.Items.Add(dialog.FolderName);
                Changed();
            }
        };
        var remove = new Button { Content = Strings.RemoveFolder, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 0, 0) };
        remove.Click += (_, _) =>
        {
            if (_folders.SelectedItem is string f)
            {
                s.WatchFolders.RemoveAll(x => string.Equals(x, f, StringComparison.OrdinalIgnoreCase));
                _folders.Items.Remove(f);
                Changed();
            }
        };
        buttons.Children.Add(add);
        buttons.Children.Add(remove);
        stack.Children.Add(buttons);

        // General
        stack.Children.Add(Heading(Strings.SectionGeneral));
        stack.Children.Add(Check(Strings.Sounds, null, s.SoundOn, v => { s.SoundOn = v; Sounds.Enabled = v; }));
        stack.Children.Add(Check(Strings.StartWithWindows, null, Shell.StartsWithWindows, v =>
        {
            try { Shell.StartsWithWindows = v; } catch (Exception e) { Log.Error($"Could not change the startup setting: {e.Message}"); }
        }));

        stack.Children.Add(new TextBlock
        {
            Text = string.Format(Strings.SettingsFooter, System.IO.Path.Combine(Settings.Folder, "settings.json")),
            Margin = new Thickness(0, 16, 0, 0), Opacity = 0.6, FontSize = 11, TextWrapping = TextWrapping.Wrap,
        });
        _building = false;
        return new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = SystemParameters.WorkArea.Height - 80 };
    }

    private TextBlock Heading(string text) => new()
    {
        Text = text, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 12, 0, 6),
    };

    private static DockPanel Row(string label, UIElement control, string? tip = null)
    {
        var row = new DockPanel { Margin = new Thickness(0, 6, 0, 2), LastChildFill = false };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Width = 230, TextWrapping = TextWrapping.Wrap, ToolTip = tip };
        row.Children.Add(text);
        row.Children.Add(control);
        return row;
    }

    private CheckBox Check(string label, string? tip, bool value, Action<bool> set)
    {
        var box = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, IsChecked = value, Margin = new Thickness(0, 4, 0, 4), ToolTip = tip, Foreground = Foreground };
        box.Checked += (_, _) => { set(true); Changed(); };
        box.Unchecked += (_, _) => { set(false); Changed(); };
        return box;
    }

    private ComboBox Choice((string key, string label)[] options, string current, Action<string> set)
    {
        var box = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (_, label) in options) box.Items.Add(label);
        int index = Array.FindIndex(options, o => o.key == current);
        box.SelectedIndex = index < 0 ? 0 : index;
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) { set(options[box.SelectedIndex].key); Changed(); } };
        return box;
    }

    private static StackPanel ColorItem(string key)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(Pegs.RopeColorOf(key)), Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)), StrokeThickness = 0.5, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = Strings.ColorName(key), VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private static StackPanel PegItem(string key)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var peg = Pegs.Create(key, seed: 3);
        peg.Margin = new Thickness(0, 0, 8, 0);
        var box = new Grid { Width = 14, Height = 26, Margin = new Thickness(0, 0, 8, 0) };
        peg.Margin = new Thickness(1, -2, 0, 0);
        peg.LayoutTransform = new ScaleTransform(0.85, 0.85);
        box.Children.Add(peg);
        row.Children.Add(box);
        row.Children.Add(new TextBlock { Text = Strings.PegName(key), VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private void OnHotKeyPressed(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (key == Key.Escape) return;
        var parts = new List<string>();
        var mods = Keyboard.Modifiers;
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        if (parts.Count == 0 && key is not (>= Key.F1 and <= Key.F24)) return; // a bare letter would steal typing everywhere
        parts.Add(key.ToString());
        var text = string.Join("+", parts);
        if (!HotKey.Parse(text, out _, out _)) return;
        Settings.Current.HotKey = text;
        _hotKey.Text = text;
        Changed();
    }

    private void Changed()
    {
        if (_building) return;
        Settings.Current.Save();
        _apply();
    }
}
