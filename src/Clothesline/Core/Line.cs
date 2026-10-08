using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clothesline.Interop;

namespace Clothesline.Core;

/// <summary>One screenshot hanging on the line.</summary>
public sealed class Pegged
{
    private static readonly Random Rng = new();

    public Guid Id { get; } = Guid.NewGuid();
    public string Path { get; }
    public BitmapSource Thumb { get; set; }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    /// <summary>Every photo hangs a little crooked, like on a real line.</summary>
    public double Tilt { get; } = Rng.NextDouble() * 5 - 2.5;
    public bool Falling { get; set; }
    /// <summary>Still flying in from where it was captured; the card waits hidden.</summary>
    public bool Flying { get; set; }
    /// <summary>Copied, dragged out, opened or edited at least once. Restored photos count as used.</summary>
    public bool Used { get; set; }
    /// <summary>Kept on purpose: never pushed off by newer captures.</summary>
    public bool Pinned { get; set; }
    public DateTime HungAt { get; } = DateTime.Now;

    public Pegged(string path, Thumbnail thumb)
    {
        Path = path;
        Thumb = thumb.Image;
        PixelWidth = thumb.PixelWidth;
        PixelHeight = thumb.PixelHeight;
    }
}

/// <summary>
/// The line itself: what hangs on it and what you can do with each item.
/// The files never move. The line is only a view onto them.
/// </summary>
public sealed class Line
{
    private readonly List<Pegged> _items = new();
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Random _rng = new();

    public IReadOnlyList<Pegged> Items => _items;

    /// <summary>The list changed: something hung, fell or was removed.</summary>
    public event Action? ItemsChanged;
    /// <summary>One item changed in place: landed, new thumbnail.</summary>
    public event Action<Pegged>? ItemUpdated;
    /// <summary>A little wind.</summary>
    public event Action? Gust;
    /// <summary>Called just before a photo starts falling, so the fall can be drawn over the whole screen.</summary>
    public event Action<Pegged>? Fall;
    public event Action? StateChanged;

    private Guid? _copiedId, _draggingId, _pressedId;
    public Guid? CopiedId { get => _copiedId; set { if (_copiedId != value) { _copiedId = value; StateChanged?.Invoke(); } } }
    /// <summary>What the badge under the card says: "Copied", or "Text copied".</summary>
    public string CopiedLabel { get; set; } = UI.Strings.Copied;
    public Guid? DraggingId { get => _draggingId; set { if (_draggingId != value) { _draggingId = value; StateChanged?.Invoke(); } } }
    public Guid? PressedId { get => _pressedId; set { if (_pressedId != value) { _pressedId = value; StateChanged?.Invoke(); } } }

    public int MaxItems { get; set; } = 8;

    public bool SoundOn
    {
        get => Settings.Current.SoundOn;
        set { Settings.Current.SoundOn = value; Sounds.Enabled = value; Settings.Current.Save(); }
    }

    public int LiveCount => _items.Count(i => !i.Falling);
    /// <summary>Whether something hangs that has not been copied, dragged out or opened yet.</summary>
    public bool HasUnused => _items.Any(i => !i.Falling && !i.Used);

    private readonly bool _persist;

    public Line(bool persist = true)
    {
        _persist = persist;
        Sounds.Enabled = persist && SoundOn;
        if (persist) Restore();
        ScheduleGust();
    }

    public Pegged? Find(Guid id) => _items.FirstOrDefault(i => i.Id == id);
    public int IndexOf(Guid id) => _items.FindIndex(i => i.Id == id);

    // MARK: Hanging and dropping

    public Guid? Hang(string path, bool quietly = false, bool flying = false, Thumbnail? preloaded = null)
    {
        if (_items.Any(i => !i.Falling && string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase))) return null;
        var thumb = preloaded ?? Thumbnails.Load(path);
        if (thumb is null) return null;
        var item = new Pegged(path, thumb.Value) { Flying = flying };
        _items.Add(item);
        // A full line lets the oldest photo fall off the far end.
        while (LiveCount > MaxItems)
        {
            var oldest = _items.FirstOrDefault(i => !i.Falling && !i.Pinned);
            if (oldest is null) break; // everything left is kept on purpose
            Drop(oldest.Id, quietly: true);
        }
        Save();
        ItemsChanged?.Invoke();
        if (!quietly) Sounds.PlayTink();
        return item.Id;
    }

    /// <summary>The capture has reached the line: the real card takes over.</summary>
    public void Land(Guid id)
    {
        var item = Find(id);
        if (item is null || !item.Flying) return;
        item.Flying = false;
        ItemUpdated?.Invoke(item);
    }

    public void Drop(Guid id, bool quietly = false)
    {
        var item = Find(id);
        if (item is null || item.Falling) return;
        Fall?.Invoke(item);
        item.Falling = true;
        Save();
        ItemsChanged?.Invoke();
        if (!quietly) Sounds.PlayPop();
        Later(0.6, () =>
        {
            if (_items.Remove(item)) ItemsChanged?.Invoke();
        });
    }

    public void Clear()
    {
        var live = _items.Where(i => !i.Falling).ToList();
        for (int n = 0; n < live.Count; n++)
        {
            var id = live[n].Id;
            bool quiet = n > 0;
            Later(0.06 * n, () => Drop(id, quiet));
        }
    }

    /// <summary>Photos whose file was deleted or moved away fall off by themselves.</summary>
    public void Prune()
    {
        foreach (var item in _items.Where(i => !i.Falling && !File.Exists(i.Path)).ToList())
            Drop(item.Id, quietly: true);
    }

    // MARK: Actions on one photo

    public void Copy(Guid id)
    {
        var item = Find(id);
        if (item is null) return;
        item.Used = true;
        CopiedLabel = UI.Strings.Copied;
        try
        {
            var data = new DataObject();
            var png = Thumbnails.PngBytes(item.Path);
            if (png is not null)
            {
                data.SetData("PNG", new MemoryStream(png), false);
                var full = Thumbnails.Load(item.Path, int.MaxValue, 3);
                if (full is not null) data.SetImage(full.Value.Image);
            }
            data.SetFileDropList(new System.Collections.Specialized.StringCollection { item.Path });
            Clipboard.SetDataObject(data, true);
        }
        catch (Exception e)
        {
            Log.Error($"Could not copy: {e.Message}");
            System.Media.SystemSounds.Beep.Play();
            return;
        }
        CopiedId = id;
        Later(1.2, () => { if (CopiedId == id) CopiedId = null; });
    }

    public void Open(Guid id)
    {
        var item = Find(id);
        if (item is null) return;
        item.Used = true;
        if (!Shell.Open(item.Path)) System.Media.SystemSounds.Beep.Play();
    }

    /// <summary>Long press: open the photo in an editor. When saved, the line shows the new version.</summary>
    public void Edit(Guid id)
    {
        var item = Find(id);
        if (item is null) return;
        item.Used = true;
        if (!Shell.Edit(item.Path)) System.Media.SystemSounds.Beep.Play();
    }

    /// <summary>Moves the file to the Recycle Bin and takes the photo off the line.</summary>
    public void Trash(Guid id)
    {
        var item = Find(id);
        if (item is null) return;
        if (File.Exists(item.Path) && !Shell.Recycle(item.Path))
        {
            Log.Error($"Could not recycle {item.Path}");
            System.Media.SystemSounds.Beep.Play();
            return;
        }
        Log.Notice($"Recycled {Path.GetFileName(item.Path)}");
        Sounds.PlayWhoosh();
        Drop(id, quietly: true);
    }

    /// <summary>Whether the file lives in Clothesline's own folder.</summary>
    public bool IsInInbox(Guid id)
    {
        var item = Find(id);
        return item is not null && Inbox.Contains(item.Path);
    }

    /// <summary>The corner cross and "Take down" both end up here.</summary>
    public void Discard(Guid id)
    {
        if (IsInInbox(id)) Trash(id); else Drop(id);
    }

    /// <summary>Keep a caught capture by moving it to the Desktop.</summary>
    public void SaveToDesktop(Guid id) => MoveTo(id, Shell.Desktop());

    /// <summary>Keep a caught capture by moving it to Pictures\Screenshots.</summary>
    public void SaveToScreenshots(Guid id) => MoveTo(id, Shell.ScreenshotsFolder());

    private void MoveTo(Guid id, string folder)
    {
        var item = Find(id);
        if (item is null) return;
        try
        {
            Directory.CreateDirectory(folder);
            var target = Inbox.UniquePath(folder, Path.GetFileName(item.Path));
            File.Move(item.Path, target);
            Drop(id, quietly: true);
        }
        catch (Exception e)
        {
            Log.Error($"Could not move to {folder}: {e.Message}");
            System.Media.SystemSounds.Beep.Play();
        }
    }

    /// <summary>After editing, the photo on the line shows the new version.</summary>
    public void ReloadThumbnail(string path)
    {
        var item = _items.FirstOrDefault(i => !i.Falling && string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;
        var thumb = Thumbnails.Load(path);
        if (thumb is null) return;
        item.Thumb = thumb.Value.Image;
        item.PixelWidth = thumb.Value.PixelWidth;
        item.PixelHeight = thumb.Value.PixelHeight;
        ItemUpdated?.Invoke(item);
    }

    public void Reveal(Guid id)
    {
        var item = Find(id);
        if (item is not null) Shell.ShowInExplorer(item.Path);
    }

    /// <summary>Keeps a photo on the line: newer captures never push it off.</summary>
    public void TogglePin(Guid id)
    {
        var item = Find(id);
        if (item is null) return;
        item.Pinned = !item.Pinned;
        Save();
        ItemUpdated?.Invoke(item);
    }

    /// <summary>Reads the text in the screenshot and puts it on the clipboard.</summary>
    public async void CopyText(Guid id)
    {
        var item = Find(id);
        if (item is null) return;
        item.Used = true;
        string? text;
        try { text = await Ocr.ReadAsync(item.Path); }
        catch (Exception e) { Log.Error($"OCR failed: {e.Message}"); text = null; }
        if (string.IsNullOrWhiteSpace(text))
        {
            System.Media.SystemSounds.Beep.Play();
            return;
        }
        try { Clipboard.SetText(text); }
        catch (Exception e) { Log.Error($"Could not copy text: {e.Message}"); return; }
        CopiedLabel = UI.Strings.TextCopied;
        CopiedId = id;
        Later(1.4, () => { if (CopiedId == id) CopiedId = null; });
    }

    // MARK: Breeze

    /// <summary>Every so often a little wind moves the line. It is what makes it feel like an object and not a widget.</summary>
    private void ScheduleGust()
    {
        Later(7 + _rng.NextDouble() * 9, () =>
        {
            if (_items.Count > 0 && DraggingId is null) Gust?.Invoke();
            ScheduleGust();
        });
    }

    // MARK: Persistence

    private void Save()
    {
        if (!_persist) return;
        Settings.Current.Pegged = _items.Where(i => !i.Falling).Select(i => i.Path).ToList();
        Settings.Current.PinnedPaths = _items.Where(i => !i.Falling && i.Pinned).Select(i => i.Path).ToList();
        Settings.Current.Save();
    }

    private void Restore()
    {
        foreach (var path in Settings.Current.Pegged.ToList())
            if (File.Exists(path) && Hang(path, quietly: true) is { } id && Find(id) is { } item)
            {
                item.Used = true;
                item.Pinned = Settings.Current.PinnedPaths.Contains(path, StringComparer.OrdinalIgnoreCase);
            }
    }

    private void Later(double seconds, Action action)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher) { Interval = TimeSpan.FromSeconds(Math.Max(0.001, seconds)) };
        timer.Tick += (_, _) => { timer.Stop(); action(); };
        timer.Start();
    }
}
