using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clothesline.Core;
using Clothesline.Interop;

namespace Clothesline.UI;

/// <summary>Everything you can change, on one small page. Changes apply as you make them.</summary>
public sealed class SettingsWindow : Window
{
    private static SettingsWindow? _open;
    private readonly Action _apply;
    private readonly TextBox _hotKey;
    private readonly ListBox _folders;
    private readonly TextBlock _inbox;

    public static void Open(Action apply)
    {
        if (_open is not null) { _open.Activate(); return; }
        _open = new SettingsWindow(apply);
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    private SettingsWindow(Action apply)
    {
        _apply = apply;
        Title = $"{Strings.AppName} {Strings.SettingsTitle}";
        Width = 460; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Icon = Application.Current.MainWindow?.Icon;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 13;
        Background = (Brush)Application.Current.Resources["MenuBg"];
        Foreground = (Brush)Application.Current.Resources["MenuFg"];
        UseLayoutRounding = true;

        var s = Settings.Current;
        var stack = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };

        stack.Children.Add(Heading(Strings.SectionLine));
        stack.Children.Add(Check(Strings.StayDownWhileUnused, Strings.StayDownWhileUnusedTip, s.StayDownWhileUnused, v => s.StayDownWhileUnused = v));
        stack.Children.Add(Check(Strings.TakeDownAfterDrag, Strings.TakeDownAfterDragTip, s.TakeDownAfterDrag, v => s.TakeDownAfterDrag = v));
        stack.Children.Add(Check(Strings.Sounds, null, s.SoundOn, v => { s.SoundOn = v; Sounds.Enabled = v; }));
        stack.Children.Add(Check(Strings.StartWithWindows, null, Shell.StartsWithWindows, v =>
        {
            try { Shell.StartsWithWindows = v; } catch (Exception e) { Log.Error($"Could not change the startup setting: {e.Message}"); }
        }));

        // Shortcut
        var row = new DockPanel { Margin = new Thickness(0, 8, 0, 2) };
        row.Children.Add(new TextBlock { Text = Strings.Shortcut, VerticalAlignment = VerticalAlignment.Center, Width = 150 });
        _hotKey = new TextBox
        {
            Text = s.HotKey, IsReadOnly = true, Width = 160, Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Left, ToolTip = Strings.ShortcutTip,
        };
        _hotKey.PreviewKeyDown += OnHotKeyPressed;
        row.Children.Add(_hotKey);
        stack.Children.Add(row);

        stack.Children.Add(Heading(Strings.SectionCaptures));
        stack.Children.Add(Check(Strings.CatchClipboard, Strings.CatchClipboardTip, s.CatchClipboard, v => s.CatchClipboard = v));

        var inboxRow = new DockPanel { Margin = new Thickness(0, 6, 0, 2) };
        var change = new Button { Content = Strings.Change, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(change, Dock.Right);
        change.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.OpenInboxFolder, InitialDirectory = Inbox.Folder };
            if (dialog.ShowDialog(this) == true) { s.InboxFolder = dialog.FolderName; Changed(); _inbox.Text = Inbox.Folder; }
        };
        inboxRow.Children.Add(change);
        _inbox = new TextBlock { Text = Inbox.Folder, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.75 };
        var inboxLabel = new StackPanel();
        inboxLabel.Children.Add(new TextBlock { Text = Strings.CaughtCapturesFolder });
        inboxLabel.Children.Add(_inbox);
        inboxRow.Children.Add(inboxLabel);
        stack.Children.Add(inboxRow);

        stack.Children.Add(new TextBlock { Text = Strings.WatchFoldersLabel, Margin = new Thickness(0, 12, 0, 4) });
        _folders = new ListBox { Height = 84, Margin = new Thickness(0, 0, 0, 6) };
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

        var footer = new TextBlock
        {
            Text = string.Format(Strings.SettingsFooter, Path.Combine(Settings.Folder, "settings.json")),
            Margin = new Thickness(0, 16, 0, 0), Opacity = 0.6, FontSize = 11, TextWrapping = TextWrapping.Wrap,
        };
        stack.Children.Add(footer);
        Content = stack;
    }

    private TextBlock Heading(string text) => new()
    {
        Text = text, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 10, 0, 6),
    };

    private CheckBox Check(string label, string? tip, bool value, Action<bool> set)
    {
        var box = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 4, 0, 4), ToolTip = tip, Foreground = Foreground };
        box.Checked += (_, _) => { set(true); Changed(); };
        box.Unchecked += (_, _) => { set(false); Changed(); };
        return box;
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
        Settings.Current.Save();
        _apply();
    }
}
