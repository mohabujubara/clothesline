using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Clothesline.Core;
using Clothesline.Interop;
using Clothesline.UI;
using static Clothesline.Interop.Native;

namespace Clothesline;

/// <summary>
/// Runs the show: the tray icon, the shortcut, revealing and tucking away the
/// line, and the captures flying to it.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly Line _line = new();
    private readonly LineWindow _panel;
    private readonly MessageWindow _messages = new();
    private readonly TrayIcon _tray;
    private readonly List<ScreenshotWatcher> _watchers = new();
    private readonly ClipboardWatcher _clipboard;
    private HotKey? _hotKey;
    private readonly DispatcherTimer _mouseTimer;
    private readonly DispatcherTimer _themeTimer;

    /// <summary>Whether the panel is shown. It can be shown and still tucked away above the top edge.</summary>
    private bool _isPresent;
    /// <summary>Whether the line has slid down into view.</summary>
    private bool _isRevealed;
    /// <summary>Opened on purpose with the shortcut or the menu: it stays down until the pointer has visited it and left.</summary>
    private bool _pinned;
    /// <summary>A new screenshot shows itself for a moment, then tucks away.</summary>
    private DateTime _peekUntil = DateTime.MinValue;
    private DateTime? _hotZoneSince;
    private DateTime? _awaySince;
    /// <summary>Whether the line should be up, if nothing prevents it. A full screen app on that screen does.</summary>
    private bool _wanted;
    /// <summary>Set when you open the line on purpose, so it stays up while empty.</summary>
    private bool _keepOpen;
    private int _lastLiveCount;
    /// <summary>The screen a new capture was taken on: the line goes there.</summary>
    private Display? _pendingMonitor;
    /// <summary>Captures that just hung from a file, so the clipboard copy of the same one is skipped.</summary>
    private readonly List<(DateTime when, int w, int h)> _recentFileCaptures = new();

    private static readonly TimeSpan RetractDelay = TimeSpan.FromSeconds(0.5);
    /// <summary>A click near the top edge means work in the window there, not a wish for the line. Stays set until the pointer leaves the edge.</summary>
    private bool _edgeSuppressed;
    private POINT _restingAt;
    private bool _buttonWasDown;

    public AppController()
    {
        OleInitialize(IntPtr.Zero);
        Theme.ApplySetting();
        Theme.Apply();
        Strings.Refresh();

        _panel = new LineWindow(_line);
        _panel.PlaceOn();
        _panel.Show();
        _panel.OrderOut();
        _panel.PlaceOn();
        UpdateCapacity();

        _tray = new TrayIcon(_messages) { MenuProvider = BuildMenu };
        _tray.LeftClick += Toggle;

        _clipboard = new ClipboardWatcher(_messages, OnClipboardCapture, RecentlyHungFromFile)
        {
            Enabled = Settings.Current.CatchClipboard,
        };
        _clipboard.Start();
        StartWatchers();
        RegisterHotKey();

        _line.Fall += Fall;
        _line.ItemsChanged += ItemsChanged;
        _lastLiveCount = _line.LiveCount;

        _mouseTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1.0 / 30) };
        _mouseTimer.Tick += (_, _) => Tick();

        _themeTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _themeTimer.Tick += (_, _) => Theme.Refresh();
        _themeTimer.Start();

        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => _panel.Dispatcher.BeginInvoke(() =>
        {
            _panel.PlaceOn(_panel.Display is { } m && Displays.All().Contains(m) ? m : null);
            UpdateCapacity();
        });
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, _) => _panel.Dispatcher.BeginInvoke(Theme.Refresh);

        if (_line.LiveCount > 0)
        {
            _wanted = true;
            Refresh();
        }

        if (!Settings.Current.Welcomed)
        {
            Settings.Current.Welcomed = true;
            Settings.Current.Save();
            Welcome();
        }
    }

    // MARK: Watching for captures

    private void StartWatchers()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
        var folders = new List<string> { Shell.ScreenshotsFolder(), Inbox.Folder };
        folders.AddRange(Settings.Current.WatchFolders.Select(Environment.ExpandEnvironmentVariables));
        foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var watcher = new ScreenshotWatcher(folder,
                onNew: path => HangCapture(path, fromFile: true),
                onModified: path => _line.ReloadThumbnail(path),
                onChange: () => _line.Prune());
            watcher.Start();
            _watchers.Add(watcher);
            Log.Notice($"Watching {folder}");
        }
    }

    private void RegisterHotKey()
    {
        _hotKey?.Dispose();
        _hotKey = new HotKey(_messages, Settings.Current.HotKey, Toggle);
        if (!_hotKey.Registered) Log.Error($"Could not register the shortcut {Settings.Current.HotKey}");
    }

    private bool RecentlyHungFromFile(int w, int h)
    {
        var cutoff = DateTime.Now.AddSeconds(-4);
        _recentFileCaptures.RemoveAll(c => c.when < cutoff);
        return _recentFileCaptures.Any(c => c.w == w && c.h == h);
    }

    private void OnClipboardCapture(string path, int w, int h) => HangCapture(path, fromFile: false);

    // MARK: The capture flying to the line

    /// <summary>A new screenshot lifts off from where it was taken and flies to its place on the line.</summary>
    private void HangCapture(string path, bool fromFile)
    {
        var thumb = Thumbnails.Load(path);
        if (thumb is null) return;
        if (fromFile) _recentFileCaptures.Add((DateTime.Now, thumb.Value.PixelWidth, thumb.Value.PixelHeight));

        var from = CaptureRect.Infer(thumb.Value.PixelWidth, thumb.Value.PixelHeight);
        if (from is not null) _pendingMonitor = from.Value.monitor;
        var id = _line.Hang(path, flying: from is not null, preloaded: thumb);
        if (id is null || from is null) return;
        // Let the line come down and lay out before measuring the landing spot.
        Later(0.03, () => Fly(id.Value, from.Value.rect, from.Value.monitor));
    }

    private void Fly(Guid id, RECT from, Display monitor)
    {
        var item = _line.Find(id);
        if (item is null) return;
        if (!_isPresent || !_isRevealed || _panel.Display is null || !_panel.Display.Equals(monitor) || CardFrame(id) is not { } to)
        {
            _line.Land(id);
            return;
        }
        int pixels = (int)Math.Max(from.Width, from.Height);
        var image = Thumbnails.Load(item.Path, Math.Min(3000, Math.Max(400, pixels)), 2)?.Image ?? item.Thumb;
        FlightWindow.Fly(image, from, to, item.Tilt, monitor, Seed(item), () => _line.Land(id));
    }

    /// <summary>A discarded card falls over the whole screen, from where it hangs.</summary>
    private void Fall(Pegged item)
    {
        if (!_isPresent || !_isRevealed || item.Flying || _panel.Display is null) return;
        if (CardFrame(item.Id) is not { } card) return;
        FlightWindow.Fall(item.Thumb, card, item.Tilt, _panel.Display, Seed(item));
    }

    private static int Seed(Pegged item) => item.Path.Aggregate(17, (h, c) => h * 31 + c);

    /// <summary>Where a card hangs, in screen pixels, using the same layout as the canvas.</summary>
    private RECT? CardFrame(Guid id)
    {
        var card = _panel.Canvas.Cards.FirstOrDefault(c => c.Item.Id == id);
        if (card is null || _panel.Display is null) return null;
        var item = card.Item;
        var live = _line.Items.Where(i => !i.Falling).ToList();
        int index = live.IndexOf(item);
        if (index < 0) return null;
        double width = _panel.Canvas.ActualWidth;
        double x = card.TargetX > 0 ? card.TargetX : Layout.X(index, live.Count, width);
        double viewTop = Layout.RopeY(x, width) - Layout.PinAbove;
        double cardTop = viewTop + Layout.CardOffsetBelowTop;
        var size = Layout.CardSize(item.PixelWidth, item.PixelHeight);
        var frame = _panel.Frame;
        double s = _panel.Display.Scale;
        int l = frame.Left + (int)Math.Round((x - size.Width / 2) * s);
        int t = frame.Top + (int)Math.Round(cardTop * s);
        return new RECT(l, t, l + (int)Math.Round(size.Width * s), t + (int)Math.Round(size.Height * s));
    }

    // MARK: Showing and hiding

    private void ItemsChanged()
    {
        int live = _line.LiveCount;
        if (live > _lastLiveCount)
        {
            _panel.PlaceOn(_pendingMonitor);
            _pendingMonitor = null;
            UpdateCapacity();
            _wanted = true;
            Refresh();
            Reveal(peekFor: 2.5, reason: "new capture");
        }
        else if (live == 0 && !_keepOpen)
        {
            Later(0.7, () =>
            {
                if (_line.LiveCount != 0 || _keepOpen) return;
                _wanted = false;
                Refresh();
            });
        }
        _lastLiveCount = live;
    }

    /// <summary>Decides whether the panel is shown at all: something to show, and no full screen app on that screen.</summary>
    private void Refresh()
    {
        var monitor = _panel.Display ?? Displays.UnderPointer();
        bool blocked = FullScreen.IsActive(monitor);
        if (_wanted && !blocked) Present(); else Dismiss();
        // The pointer is watched while there is a line, even tucked away, to notice it pushing against the top edge.
        if (_wanted) _mouseTimer.Start(); else StopMouseTracking();
    }

    private void Present()
    {
        if (_isPresent) return;
        _isPresent = true;
        _panel.OrderFront();
    }

    private void Dismiss()
    {
        if (!_isPresent) return;
        _isPresent = false;
        SetRevealed(false);
        Later(0.4, () => { if (!_isPresent) _panel.OrderOut(); });
    }

    private void Reveal(bool pinned = false, double peekFor = 0, string reason = "edge")
    {
        if (!_isPresent) return;
        if (!_isRevealed) Log.Notice($"Reveal: {reason}");
        if (pinned) _pinned = true;
        if (peekFor > 0) _peekUntil = DateTime.Now.AddSeconds(peekFor);
        _awaySince = null;
        SetRevealed(true);
    }

    private void SetRevealed(bool on)
    {
        if (on == _isRevealed) return;
        _isRevealed = on;
        _panel.Canvas.Revealed = on;
        if (!on)
        {
            _pinned = false;
            _peekUntil = DateTime.MinValue;
            _panel.Canvas.UpdateHover(null);
        }
    }

    public void Toggle()
    {
        if (_isRevealed)
        {
            SetRevealed(false);
            if (_line.LiveCount == 0)
            {
                _keepOpen = false;
                _wanted = false;
                Refresh();
            }
        }
        else
        {
            _keepOpen = true;
            _wanted = true;
            _panel.PlaceOn();
            UpdateCapacity();
            Refresh();
            Reveal(pinned: true, reason: "toggle");
        }
    }

    private void StopMouseTracking()
    {
        _mouseTimer.Stop();
        _panel.Canvas.UpdateHover(null);
    }

    private static bool ButtonDown() => (GetAsyncKeyState(0x01) < 0) || (GetAsyncKeyState(0x02) < 0) || (GetAsyncKeyState(0x04) < 0);

    private void Tick()
    {
        var mouse = Displays.Cursor();
        var now = DateTime.Now;
        var displayUnderPointer = Displays.Containing(mouse);
        bool inHotBand = displayUnderPointer is not null && displayUnderPointer.HotBand.Contains(mouse);
        bool buttonDown = ButtonDown();
        bool justPressed = buttonDown && !_buttonWasDown;
        _buttonWasDown = buttonDown;
        if (!inHotBand) _edgeSuppressed = false;

        if (_isPresent && _panel.Display is { } current && FullScreen.IsActive(current))
        {
            Dismiss();
            return;
        }
        if (!_isPresent && _wanted) Refresh();

        if (!_isRevealed)
        {
            // The top edge is where every maximised window keeps its tabs, so the
            // line only comes down when the pointer rests there, still, with no
            // button pressed. A click up there says you are working, not asking.
            if (buttonDown && inHotBand) _edgeSuppressed = true;
            bool resting = Settings.Current.RevealAtTopEdge && displayUnderPointer is not null && inHotBand && !buttonDown
                           && !_edgeSuppressed && !FullScreen.IsActive(displayUnderPointer);
            if (resting)
            {
                if (_hotZoneSince is null || Math.Abs(mouse.X - _restingAt.X) > 6 || Math.Abs(mouse.Y - _restingAt.Y) > 6)
                {
                    _hotZoneSince = now;
                    _restingAt = mouse;
                }
                if (now - _hotZoneSince.Value >= TimeSpan.FromSeconds(Math.Clamp(Settings.Current.RevealDelay, 0.15, 3)))
                {
                    _hotZoneSince = null;
                    if (_panel.Display is null || !_panel.Display.Equals(displayUnderPointer))
                    {
                        _panel.PlaceOn(displayUnderPointer);
                        UpdateCapacity();
                    }
                    _wanted = true;
                    Refresh();
                    Reveal();
                }
            }
            else
            {
                _hotZoneSince = null;
            }
            return;
        }

        bool dragging = DragSource.IsDragging || _line.DraggingId is not null;
        bool holding = _line.PressedId is not null || _panel.Canvas.RopeDragging || _panel.Canvas.Cards.Any(c => c.Reordering);
        _panel.HoldMouse = dragging || holding;
        var local = _panel.ToCanvas(mouse);
        _panel.Canvas.UpdateHover(dragging ? null : local);

        // A click that goes through the strip into the window underneath puts
        // the line away at once: you are working there.
        if (justPressed && !dragging && !holding && !_line.MenuOpen && !_panel.IsOverPhoto(mouse))
        {
            _edgeSuppressed = inHotBand;
            SetRevealed(false);
            return;
        }

        // The line's zone runs from its lowest point up to the top of the screen, so moving up never hides it.
        var frame = _panel.Frame;
        var zone = frame;
        if (_panel.Display is { } m) zone.Top = m.Bounds.Top;
        bool inside = zone.Contains(mouse);
        if (inside && _pinned) _pinned = false;

        bool pendingHold = Settings.Current.StayDownWhileUnused && _line.HasUnused;
        bool busy = _pinned || dragging || holding || _line.MenuOpen || now < _peekUntil || pendingHold;
        if (inside || busy)
        {
            _awaySince = null;
        }
        else
        {
            _awaySince ??= now;
            if (now - _awaySince.Value >= RetractDelay)
            {
                _awaySince = null;
                SetRevealed(false);
            }
        }
    }

    private void UpdateCapacity()
    {
        double usable = _panel.Canvas.Width - 200;
        if (double.IsNaN(usable) || usable <= 0) usable = _panel.ActualWidth - 200;
        int fit = Math.Max(3, Math.Min(12, (int)(usable / Layout.Spacing)));
        _line.MaxItems = Settings.Current.MaxItems is { } max && max > 0 ? Math.Min(max, fit) : fit;
    }

    // MARK: Welcome

    private void Welcome()
    {
        _keepOpen = true;
        _wanted = true;
        Refresh();
        Reveal(pinned: true, reason: "welcome");
        _tray.Balloon(Strings.WelcomeTitle, string.Format(Strings.WelcomeBody, Settings.Current.HotKey));
        Later(6, () =>
        {
            if (_line.LiveCount != 0) return;
            _keepOpen = false;
            _wanted = false;
            Refresh();
        });
    }

    /// <summary>Settings changed: the shortcut, the folders and the clipboard watcher follow.</summary>
    public void ApplySettings()
    {
        Theme.ApplySetting();
        Strings.Refresh();
        Pegs.RaiseLookChanged();
        _tray.Repaint();
        if (_panel.Display is { } d) _panel.PlaceOn(d);
        if (_hotKey is null || _hotKey.Text != Settings.Current.HotKey) RegisterHotKey();
        _clipboard.Enabled = Settings.Current.CatchClipboard;
        var wanted = new List<string> { Shell.ScreenshotsFolder(), Inbox.Folder };
        wanted.AddRange(Settings.Current.WatchFolders.Select(Environment.ExpandEnvironmentVariables));
        var current = _watchers.Select(w => w.Folder).ToList();
        if (!wanted.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).SequenceEqual(current.OrderBy(x => x), StringComparer.OrdinalIgnoreCase)) StartWatchers();
    }

    // MARK: Menu

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu { FlowDirection = Strings.Flow };
        menu.Opened += (_, _) => _line.MenuOpen = true;
        menu.Closed += (_, _) => _line.MenuOpen = false;
        menu.Items.Add(PeggedControl.MenuItemFor(_isRevealed ? Strings.HideLine : Strings.ShowLine, Toggle, Settings.Current.HotKey));
        var clear = PeggedControl.MenuItemFor(Strings.TakeEverythingDown, () => _line.Clear());
        clear.IsEnabled = _line.LiveCount > 0;
        menu.Items.Add(clear);
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.NewCapture, Shell.Snip, "Win+Shift+S"));
        var note = new MenuItem { Header = Strings.NewNote };
        foreach (var key in Notes.Colors)
        {
            var k = key;
            var item = new MenuItem { Header = Strings.NoteName(k), Icon = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(Notes.Paper(k)) } };
            item.Click += (_, _) => { _keepOpen = true; _wanted = true; _panel.PlaceOn(); UpdateCapacity(); Refresh(); _line.NewNote(k); Reveal(pinned: true, reason: "note"); };
            note.Items.Add(item);
        }
        menu.Items.Add(note);
        menu.Items.Add(new Separator());

        var catchItem = PeggedControl.MenuItemFor(Strings.CatchClipboard, () =>
        {
            Settings.Current.CatchClipboard = !Settings.Current.CatchClipboard;
            Settings.Current.Save();
            _clipboard.Enabled = Settings.Current.CatchClipboard;
        });
        catchItem.IsChecked = Settings.Current.CatchClipboard;
        catchItem.ToolTip = Strings.CatchClipboardTip;
        menu.Items.Add(catchItem);
        var stay = PeggedControl.MenuItemFor(Strings.StayDownWhileUnused, () =>
        {
            Settings.Current.StayDownWhileUnused = !Settings.Current.StayDownWhileUnused;
            Settings.Current.Save();
        });
        stay.IsChecked = Settings.Current.StayDownWhileUnused;
        stay.ToolTip = Strings.StayDownWhileUnusedTip;
        menu.Items.Add(stay);
        var afterDrag = PeggedControl.MenuItemFor(Strings.TakeDownAfterDrag, () =>
        {
            Settings.Current.TakeDownAfterDrag = !Settings.Current.TakeDownAfterDrag;
            Settings.Current.Save();
        });
        afterDrag.IsChecked = Settings.Current.TakeDownAfterDrag;
        afterDrag.ToolTip = Strings.TakeDownAfterDragTip;
        menu.Items.Add(afterDrag);
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.OpenScreenshotsFolder, () => Shell.OpenFolder(Shell.ScreenshotsFolder())));
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.OpenInboxFolder, () => Shell.OpenFolder(Inbox.Folder)));
        menu.Items.Add(new Separator());

        var sound = PeggedControl.MenuItemFor(Strings.Sounds, () => _line.SoundOn = !_line.SoundOn);
        sound.IsChecked = _line.SoundOn;
        menu.Items.Add(sound);
        var login = PeggedControl.MenuItemFor(Strings.StartWithWindows, () =>
        {
            try { Shell.StartsWithWindows = !Shell.StartsWithWindows; }
            catch (Exception e) { Log.Error($"Could not change the startup setting: {e.Message}"); }
        });
        login.IsChecked = Shell.StartsWithWindows;
        menu.Items.Add(login);
        menu.Items.Add(new Separator());
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.Settings, () => SettingsWindow.Open(ApplySettings)));
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.About, ShowAbout));
        menu.Items.Add(PeggedControl.MenuItemFor(Strings.Quit, () => Application.Current.Shutdown()));
        return menu;
    }

    private void ShowAbout()
    {
        var version = typeof(AppController).Assembly.GetName().Version?.ToString(3) ?? "1.0";
        MessageBox.Show(
            $"{Strings.AppName} {version}\n{Strings.Tagline}\n\n" +
            string.Format(Strings.AboutBody, Settings.Current.HotKey) + "\n\n" +
            $"Caught captures live in\n{Inbox.Folder}\n\nSettings: {Path.Combine(Settings.Folder, "settings.json")}",
            Strings.About, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK,
            Strings.IsRtl ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);
    }

    private static void Later(double seconds, Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(0.001, seconds)) };
        timer.Tick += (_, _) => { timer.Stop(); action(); };
        timer.Start();
    }

    public void Dispose()
    {
        _mouseTimer.Stop();
        _themeTimer.Stop();
        foreach (var w in _watchers) w.Dispose();
        _clipboard.Dispose();
        _hotKey?.Dispose();
        _tray.Dispose();
        _messages.Dispose();
    }
}
