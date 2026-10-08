using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clothesline.Core;

/// <summary>Everything the app remembers, in one small JSON file.</summary>
public sealed class Settings
{
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clothesline");

    private static readonly string FilePath = Path.Combine(Folder, "settings.json");

    // Declared before Current: static fields run in order, and Load needs the options.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static Settings Current { get; } = Load();

    /// <summary>Files hanging on the line, oldest first.</summary>
    public List<string> Pegged { get; set; } = new();
    public List<string> PinnedPaths { get; set; } = new();
    public bool SoundOn { get; set; } = true;
    public bool CatchClipboard { get; set; } = true;
    /// <summary>Keep the line down while a capture has not been used yet.</summary>
    public bool StayDownWhileUnused { get; set; }
    /// <summary>A photo dragged into an app leaves the line, as if it had been dropped into a folder.</summary>
    public bool TakeDownAfterDrag { get; set; }
    /// <summary>Bring the line down when the pointer rests still at the top edge of the screen.</summary>
    public bool RevealAtTopEdge { get; set; } = true;
    /// <summary>How long the pointer rests at the edge before the line comes down, in seconds.</summary>
    public double RevealDelay { get; set; } = 0.5;
    /// <summary>How far below the top of the work area the line hangs, in device independent pixels.</summary>
    public double LineOffset { get; set; }
    /// <summary>A preset name like "bronze", or a hex colour like "#8C5A2E".</summary>
    public string RopeColor { get; set; } = "bronze";
    /// <summary>wood, metal, mixed, red, blue, green or yellow.</summary>
    public string PegStyle { get; set; } = "wood";
    public bool Bows { get; set; } = true;
    /// <summary>Spread the photos evenly along the line. Off, each stays where you put it.</summary>
    public bool AutoArrange { get; set; }
    /// <summary>Where each photo hangs, as a fraction of the width of the screen, by file path.</summary>
    public Dictionary<string, double> Spots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>auto, light or dark.</summary>
    public string Appearance { get; set; } = "auto";
    /// <summary>auto, en or ar.</summary>
    public string Language { get; set; } = "auto";
    public bool Welcomed { get; set; }
    public string HotKey { get; set; } = "Ctrl+Alt+T";
    /// <summary>Extra folders to watch, for ShareX, Greenshot and friends.</summary>
    public List<string> WatchFolders { get; set; } = new();
    /// <summary>Where clipboard captures are written. Defaults to Clothesline's own folder.</summary>
    public string? InboxFolder { get; set; }
    /// <summary>Monitor DPI scale aware maximum; null means as many as fit.</summary>
    public int? MaxItems { get; set; }

    private static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Options) ?? new Settings();
        }
        catch (Exception e)
        {
            Log.Error($"Could not read settings: {e.Message}");
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception e)
        {
            Log.Error($"Could not save settings: {e.Message}");
        }
    }
}

/// <summary>A tiny log in %LOCALAPPDATA%\Clothesline\log.txt, for when something is off.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static readonly string FilePath = Path.Combine(Settings.Folder, "log.txt");

    public static void Notice(string message) => Write("notice", message);
    public static void Error(string message) => Write("error", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Settings.Folder);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 512 * 1024) File.Delete(FilePath);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch { }
        System.Diagnostics.Debug.WriteLine($"[{level}] {message}");
    }
}
