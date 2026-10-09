using System.IO;

namespace Snapline.Core;

/// <summary>
/// Snapline's own folder. Captures caught from the clipboard are written
/// here, and anything hanging from here is discarded to the Recycle Bin, or
/// the folder would fill up with forgotten screenshots. Files anywhere else,
/// like Pictures\Screenshots, stay where they are when taken down.
/// </summary>
public static class Inbox
{
    public static string Folder
    {
        get
        {
            var custom = Settings.Current.InboxFolder;
            var folder = string.IsNullOrWhiteSpace(custom)
                ? Path.Combine(Settings.Folder, "Screenshots")
                : Environment.ExpandEnvironmentVariables(custom);
            try { Directory.CreateDirectory(folder); } catch { }
            return folder;
        }
    }

    public static bool Contains(string path)
    {
        var folder = Path.GetFullPath(Folder).TrimEnd('\\') + "\\";
        return Path.GetFullPath(path).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a folder may hold caught captures. Discarding a caught capture
    /// sends it to the Recycle Bin, so the folder must never be, contain or sit
    /// inside a place where the user's own screenshots live.
    /// </summary>
    public static bool IsSafeFolder(string candidate, IEnumerable<string> protectedFolders)
    {
        string Norm(string p) => Path.GetFullPath(p).TrimEnd('\\') + "\\";
        var c = Norm(candidate);
        foreach (var p in protectedFolders)
        {
            string n;
            try { n = Norm(p); } catch { continue; }
            if (c.StartsWith(n, StringComparison.OrdinalIgnoreCase) || n.StartsWith(c, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    /// <summary>A fresh file name for a capture, like the Snipping Tool's.</summary>
    public static string NewCapturePath()
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
        var path = Path.Combine(Folder, $"Screenshot {stamp}.png");
        int n = 2;
        while (File.Exists(path)) path = Path.Combine(Folder, $"Screenshot {stamp} ({n++}).png");
        return path;
    }

    public static string UniquePath(string folder, string name)
    {
        var baseName = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        var candidate = Path.Combine(folder, name);
        int n = 2;
        while (File.Exists(candidate)) candidate = Path.Combine(folder, $"{baseName} {n++}{ext}");
        return candidate;
    }
}
