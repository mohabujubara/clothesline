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

    public static Settings Current { get; } = Load();

    /// <summary>Files hanging on the line, oldest first.</summary>
    public List<string> Pegged { get; set; } = new();
    public bool SoundOn { get; set; } = true;
    public bool CatchClipboard { get; set; } = true;
    public bool Welcomed { get; set; }
    public string HotKey { get; set; } = "Ctrl+Alt+T";
    /// <summary>Extra folders to watch, for ShareX, Greenshot and friends.</summary>
    public List<string> WatchFolders { get; set; } = new();
    /// <summary>Where clipboard captures are written. Defaults to Clothesline's own folder.</summary>
    public string? InboxFolder { get; set; }
    /// <summary>Monitor DPI scale aware maximum; null means as many as fit.</summary>
    public int? MaxItems { get; set; }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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
