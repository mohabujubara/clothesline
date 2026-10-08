using System.Diagnostics;
using System.IO;
using Snapline.Core;
using Microsoft.Win32;
using static Snapline.Interop.Native;

namespace Snapline.Interop;

/// <summary>Files, folders and apps, the way Explorer would do it.</summary>
public static class Shell
{
    /// <summary>Moves a file to the Recycle Bin, silently.</summary>
    public static bool Recycle(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0\0",
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI),
        };
        return SHFileOperation(ref op) == 0 && !op.fAnyOperationsAborted;
    }

    /// <summary>Opens a file with its default app, like a double click in Explorer.</summary>
    public static bool Open(string path) => Execute(path, "open");

    /// <summary>Opens a file in its editor: Paint for images, unless the user chose another.</summary>
    public static bool Edit(string path)
    {
        if (Execute(path, "edit")) return true;
        try { Process.Start(new ProcessStartInfo("mspaint.exe", $"\"{path}\"") { UseShellExecute = true }); return true; }
        catch { return false; }
    }

    public static void ShowInExplorer(string path)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); }
        catch { }
    }

    /// <summary>Starts a new screen snip, the same as Win+Shift+S.</summary>
    public static void Snip()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", "ms-screenclip:") { UseShellExecute = true }); }
        catch (Exception e) { Log.Error($"Could not start a snip: {e.Message}"); }
    }

    public static void OpenFolder(string path)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true }); }
        catch { }
    }

    private static bool Execute(string file, string verb)
    {
        var info = new SHELLEXECUTEINFO
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<SHELLEXECUTEINFO>(),
            fMask = SEE_MASK_FLAG_NO_UI,
            lpVerb = verb,
            lpFile = file,
            nShow = 1,
        };
        return ShellExecuteEx(ref info);
    }

    /// <summary>Where Windows saves screenshots: Pictures\Screenshots, OneDrive aware.</summary>
    public static string ScreenshotsFolder()
    {
        var known = KnownFolder(FOLDERID_Screenshots);
        if (!string.IsNullOrEmpty(known)) return known;
        var pictures = KnownFolder(FOLDERID_Pictures)
            ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(pictures, "Screenshots");
    }

    public static string Desktop()
        => KnownFolder(FOLDERID_Desktop) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    // MARK: Start with Windows

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "Snapline";

    /// <summary>A Run entry left by the old name points at an exe that no longer exists; it becomes ours.</summary>
    public static void MigrateStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue("Clothesline") is string)
            {
                key.DeleteValue("Clothesline", false);
                key.SetValue(RunName, $"\"{ExePath()}\"");
            }
        }
        catch { }
    }

    public static bool StartsWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunName) is string s && s.Contains(ExePath(), StringComparison.OrdinalIgnoreCase);
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(RunName, $"\"{ExePath()}\"");
            else key.DeleteValue(RunName, false);
        }
    }

    public static string ExePath() => Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;
}
