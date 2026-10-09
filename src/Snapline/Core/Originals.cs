using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Snapline.Core;

/// <summary>
/// The untouched copy of a screenshot that is being marked up, so "Revert to
/// original" always has something to go back to. One copy per file, named
/// after the file's full path, so two files with the same name never share
/// one. The copy lives only while the photo hangs on the line: taking it
/// down, trashing it or discarding it removes the copy too, so no unblurred
/// version of anything outlives the picture it came from.
/// </summary>
public static class Originals
{
    public static string Folder => Path.Combine(Settings.Folder, "Originals");

    public static string BackupPath(string path)
    {
        var full = Path.GetFullPath(path);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(full.ToUpperInvariant())))[..16].ToLowerInvariant();
        return Path.Combine(Folder, $"{digest}-{Path.GetFileName(full)}");
    }

    public static bool Exists(string path) => File.Exists(BackupPath(path));

    /// <summary>Keeps a copy of the file as it is now, unless one is already kept.</summary>
    public static void Keep(string path)
    {
        if (Settings.ReadOnly) return; // renders never touch the user's folder
        var target = BackupPath(path);
        if (File.Exists(target)) return;
        Directory.CreateDirectory(Folder);
        File.Copy(path, target);
    }

    /// <summary>Puts the kept copy back in place of the file. The copy stays until the file leaves the line.</summary>
    public static bool Revert(string path)
    {
        var source = BackupPath(path);
        if (!File.Exists(source)) return false;
        // Copy first, replace second: the file is never gone, even if something fails halfway.
        var tmp = path + ".revert";
        File.Copy(source, tmp, true);
        File.Move(tmp, path, true);
        return true;
    }

    public static void Forget(string path)
    {
        try { File.Delete(BackupPath(path)); } catch { }
    }

    /// <summary>At launch: only copies of photos still on the line are kept. Anything else, older name-keyed copies included, goes.</summary>
    public static void Sweep(IEnumerable<string> hanging)
    {
        if (Settings.ReadOnly || !Directory.Exists(Folder)) return;
        var wanted = new HashSet<string>(hanging.Select(BackupPath), StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(Folder))
        {
            if (wanted.Contains(file)) continue;
            try { File.Delete(file); } catch { }
        }
    }
}
