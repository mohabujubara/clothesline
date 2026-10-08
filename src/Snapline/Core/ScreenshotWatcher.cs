using System.IO;
using System.Windows.Threading;

namespace Snapline.Core;

/// <summary>
/// Watches a folder screenshots land in and reports new ones. Snapline
/// never takes screenshots itself: you keep Win+Shift+S, PrtScn, ShareX or
/// whatever you use, and the line just picks them up.
/// </summary>
public sealed class ScreenshotWatcher : IDisposable
{
    public string Folder { get; }

    private readonly Action<string> _onNew;
    private readonly Action<string> _onModified;
    private readonly Action _onChange;
    private readonly Dispatcher _dispatcher;
    private readonly DateTime _launch = DateTime.Now;
    private readonly Dictionary<string, DateTime> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _debounce;
    private readonly DispatcherTimer _poll;
    private FileSystemWatcher? _watcher;
    private readonly HashSet<string> _dirty = new(StringComparer.OrdinalIgnoreCase);

    public ScreenshotWatcher(string folder, Action<string> onNew, Action<string> onModified, Action onChange)
    {
        Folder = folder;
        _onNew = onNew;
        _onModified = onModified;
        _onChange = onChange;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _debounce = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(220) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Scan(); };
        // A safety net: file notifications can be missed on synced folders.
        _poll = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromSeconds(3) };
        _poll.Tick += (_, _) => Scan();
    }

    public void Start()
    {
        try { Directory.CreateDirectory(Folder); }
        catch (Exception e) { Log.Error($"Cannot create {Folder}: {e.Message}"); return; }

        // Anything already there is old news, unless it landed since launch.
        foreach (var (path, written) in Listing())
        {
            if (written >= _launch && Thumbnails.IsImage(path)) _onNew(path);
            _known[path] = written;
        }

        try
        {
            _watcher = new FileSystemWatcher(Folder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                InternalBufferSize = 64 * 1024,
            };
            _watcher.Created += (_, e) => Touched(e.FullPath);
            _watcher.Changed += (_, e) => Touched(e.FullPath);
            _watcher.Renamed += (_, e) => Touched(e.FullPath);
            _watcher.Deleted += (_, _) => Touched(null);
            _watcher.Error += (_, e) => Log.Error($"Watcher error on {Folder}: {e.GetException().Message}");
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception e)
        {
            Log.Error($"Cannot watch {Folder}: {e.Message}");
        }
        _poll.Start();
    }

    private void Touched(string? path)
    {
        _dispatcher.BeginInvoke(() =>
        {
            if (path is not null) _dirty.Add(path);
            // Windows writes a temp file and renames it; give it a moment.
            _debounce.Stop();
            _debounce.Start();
        });
    }

    private void Scan()
    {
        var current = Listing();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, written) in current)
        {
            seen.Add(path);
            if (!_known.TryGetValue(path, out var before))
            {
                _known[path] = written;
                if (Thumbnails.IsImage(path) && written >= _launch.AddSeconds(-2)) _onNew(path);
            }
            else if (written > before)
            {
                _known[path] = written;
                if (Thumbnails.IsImage(path) && _dirty.Contains(path)) _onModified(path);
            }
        }
        foreach (var gone in _known.Keys.Where(k => !seen.Contains(k)).ToList()) _known.Remove(gone);
        _dirty.Clear();
        _onChange();
    }

    private List<(string path, DateTime written)> Listing()
    {
        try
        {
            var dir = new DirectoryInfo(Folder);
            if (!dir.Exists) return new();
            return dir.EnumerateFiles()
                .Where(f => (f.Attributes & (FileAttributes.Hidden | FileAttributes.Temporary)) == 0 && !f.Name.StartsWith('~'))
                .Select(f => (f.FullName, Max(f.CreationTime, f.LastWriteTime)))
                .OrderBy(t => t.Item2)
                .ToList();
        }
        catch (Exception e)
        {
            Log.Error($"Cannot list {Folder}: {e.Message}");
            return new();
        }
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    public void Dispose()
    {
        _debounce.Stop();
        _poll.Stop();
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }
}
