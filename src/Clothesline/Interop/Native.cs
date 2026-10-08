using System.Runtime.InteropServices;
using System.Text;

namespace Clothesline.Interop;

/// <summary>Win32, DWM and shell entry points the app needs. Kept in one place.</summary>
public static class Native
{
    // Window styles
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_MAXIMIZE = 0x01000000;

    // Messages
    public const int WM_NCHITTEST = 0x0084;
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int WM_HOTKEY = 0x0312;
    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_SETTINGCHANGE = 0x001A;
    public const int WM_DPICHANGED = 0x02E0;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_RBUTTONDOWN = 0x0204;

    public const int HTTRANSPARENT = -1;
    public const int HTCLIENT = 1;
    public const int MA_NOACTIVATE = 3;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_MESSAGE = new(-3);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public const int SW_SHOWNOACTIVATE = 4;
    public const int SW_HIDE = 0;

    public const uint MONITOR_DEFAULTTONULL = 0;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MONITORINFOF_PRIMARY = 1;

    // Hot key modifiers
    public const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008, MOD_NOREPEAT = 0x4000;

    // DWM
    public const int DWMWA_CLOAKED = 14;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_DONOTROUND = 1;
    public const int DWMWCP_ROUND = 2;
    public const int DWMWA_BORDER_COLOR = 34;
    public const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public RECT(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; }
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public bool Contains(POINT p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;
        public override string ToString() => $"({Left},{Top})-({Right},{Bottom})";
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public int cx, cy;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int Left, Right, Top, Bottom;
    }

    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] public static extern IntPtr SetCursor(IntPtr hCursor);
    [DllImport("user32.dll")] public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int attrValue, int attrSize);

    [DllImport("ole32.dll")] public static extern int OleInitialize(IntPtr pvReserved);
    [DllImport("ole32.dll")] public static extern void ReleaseStgMedium(ref System.Runtime.InteropServices.ComTypes.STGMEDIUM pmedium);
    [DllImport("ole32.dll")] public static extern int DoDragDrop(System.Runtime.InteropServices.ComTypes.IDataObject pDataObj, IDropSource pDropSource, uint dwOKEffects, out uint pdwEffect);

    [DllImport("kernel32.dll")] public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll")] public static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] public static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")] public static extern IntPtr GlobalFree(IntPtr hMem);
    [DllImport("kernel32.dll")] public static extern UIntPtr GlobalSize(IntPtr hMem);
    public const uint GHND = 0x0042;

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
    [DllImport("ole32.dll")] public static extern void CoTaskMemFree(IntPtr pv);

    public enum QUERY_USER_NOTIFICATION_STATE { NotPresent = 1, Busy = 2, RunningD3dFullScreen = 3, PresentationMode = 4, AcceptsNotifications = 5, QuietTime = 6, App = 7 }
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE pquns);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }
    public const uint FO_DELETE = 3;
    public const ushort FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40, FOF_NOERRORUI = 0x400;
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public string? lpVerb;
        public string lpFile;
        public string? lpParameters;
        public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }
    public const uint SEE_MASK_FLAG_NO_UI = 0x400;
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    public static string ClassNameOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool IsCloaked(IntPtr hwnd)
        => DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    public static void AddExStyle(IntPtr hwnd, int bits)
    {
        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex | (uint)bits));
    }

    public static void RemoveExStyle(IntPtr hwnd, int bits)
    {
        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex & ~(long)(uint)bits));
    }

    public static bool HasExStyle(IntPtr hwnd, int bits)
        => (GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64() & (uint)bits) != 0;

    /// <summary>Makes a WPF window render on the DWM with a truly transparent background.</summary>
    public static void MakeGlassSheet(IntPtr hwnd)
    {
        var m = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref m);
        int pref = DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        // Windows 11 draws a one pixel border around top level windows; not on a sheet of glass.
        int none = unchecked((int)DWMWA_COLOR_NONE);
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref none, sizeof(int));
    }

    public static string? KnownFolder(Guid id)
    {
        if (SHGetKnownFolderPath(id, 0, IntPtr.Zero, out var p) != 0 || p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUni(p); }
        finally { CoTaskMemFree(p); }
    }

    public static readonly Guid FOLDERID_Screenshots = new("b7bede81-df94-4682-a7d8-57a52620b86f");
    public static readonly Guid FOLDERID_Pictures = new("33E28130-4E1E-4676-835A-98395C3BC3BB");
    public static readonly Guid FOLDERID_Desktop = new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
}

[ComImport, Guid("00000121-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDropSource
{
    [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool fEscapePressed, uint grfKeyState);
    [PreserveSig] int GiveFeedback(uint dwEffect);
}
