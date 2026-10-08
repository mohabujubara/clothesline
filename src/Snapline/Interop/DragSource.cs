using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Snapline.Interop.Native;

namespace Snapline.Interop;

/// <summary>
/// Drags a file out of the app the way Explorer does: a real OLE drag with the
/// shell's drag image, so the photo follows the pointer into Explorer, the
/// Desktop, the Recycle Bin and any app that accepts files.
/// </summary>
public static class DragSource
{
    public const uint DROPEFFECT_NONE = 0, DROPEFFECT_COPY = 1, DROPEFFECT_MOVE = 2, DROPEFFECT_LINK = 4;

    public static bool IsDragging { get; private set; }

    /// <summary>
    /// Runs a drag of one file. Returns the effect the target performed.
    /// `image` is drawn under the pointer, with `grab` the point inside it the
    /// pointer holds, both in physical pixels.
    /// </summary>
    public static uint Drag(string path, BitmapSource image, Point grab, uint allowed = DROPEFFECT_COPY | DROPEFFECT_MOVE)
    {
        using var data = new ShellDataObject();
        data.SetFileDrop(path);

        try
        {
            var helper = (IDragSourceHelper)new DragDropHelper();
            var shdi = new SHDRAGIMAGE
            {
                sizeDragImage = new SIZE { cx = image.PixelWidth, cy = image.PixelHeight },
                ptOffset = new POINT((int)grab.X, (int)grab.Y),
                hbmpDragImage = CreateHBitmap(image),
                crColorKey = 0xFFFFFFFF,
            };
            helper.InitializeFromBitmap(ref shdi, data);
        }
        catch
        {
            // Without the helper the drag still works, with the plain cursor.
        }

        IsDragging = true;
        try
        {
            int hr = DoDragDrop(data, new DropSource(), allowed, out uint effect);
            if (hr == DRAGDROP_S_DROP) return effect;
            return DROPEFFECT_NONE;
        }
        finally
        {
            IsDragging = false;
        }
    }

    private const int DRAGDROP_S_DROP = 0x00040100;
    private const int DRAGDROP_S_CANCEL = 0x00040101;
    private const int DRAGDROP_S_USEDEFAULTCURSORS = 0x00040102;
    private const uint MK_LBUTTON = 1, MK_RBUTTON = 2;

    [ComVisible(true)]
    private sealed class DropSource : IDropSource
    {
        public int QueryContinueDrag(bool fEscapePressed, uint grfKeyState)
        {
            if (fEscapePressed) return DRAGDROP_S_CANCEL;
            if ((grfKeyState & (MK_LBUTTON | MK_RBUTTON)) == 0) return DRAGDROP_S_DROP;
            return 0;
        }

        public int GiveFeedback(uint dwEffect) => DRAGDROP_S_USEDEFAULTCURSORS;
    }

    /// <summary>A 32-bit premultiplied DIB from a WPF bitmap, for the shell drag image.</summary>
    private static IntPtr CreateHBitmap(BitmapSource source)
    {
        var bmp = source.Format == PixelFormats.Pbgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        int w = bmp.PixelWidth, h = bmp.PixelHeight;
        var info = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32, biCompression = 0,
        };
        var hbm = CreateDIBSection(IntPtr.Zero, ref info, 0, out var bits, IntPtr.Zero, 0);
        if (hbm == IntPtr.Zero) throw new InvalidOperationException("CreateDIBSection failed");
        bmp.CopyPixels(Int32Rect.Empty, bits, w * h * 4, w * 4);
        return hbm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SHDRAGIMAGE
    {
        public SIZE sizeDragImage;
        public POINT ptOffset;
        public IntPtr hbmpDragImage;
        public uint crColorKey;
    }

    [ComImport, Guid("DE5BF786-477A-11d2-839D-00C04FD918D0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDragSourceHelper
    {
        void InitializeFromBitmap(ref SHDRAGIMAGE pshdi, System.Runtime.InteropServices.ComTypes.IDataObject pDataObject);
        void InitializeFromWindow(IntPtr hwnd, ref POINT ppt, System.Runtime.InteropServices.ComTypes.IDataObject pDataObject);
    }

    [ComImport, Guid("4657278A-411B-11d2-839A-00C04FD918D0")]
    private class DragDropHelper { }
}

/// <summary>
/// A data object that stores whatever the shell puts in it. The drag image
/// helper writes several private formats, so the usual WPF DataObject, which
/// refuses SetData from COM, cannot be used.
/// </summary>
[ComVisible(true)]
internal sealed class ShellDataObject : System.Runtime.InteropServices.ComTypes.IDataObject, IDisposable
{
    private const int DV_E_FORMATETC = unchecked((int)0x80040064);
    private const int DV_E_TYMED = unchecked((int)0x80040069);
    private const int OLE_E_ADVISENOTSUPPORTED = unchecked((int)0x80040003);
    private const int DATA_S_SAMEFORMATETC = 0x00040130;
    private const int E_NOTIMPL = unchecked((int)0x80004001);
    private const short CF_HDROP = 15;

    private readonly List<(FORMATETC format, STGMEDIUM medium)> _entries = new();

    public void SetFileDrop(params string[] paths)
    {
        var text = string.Join("\0", paths) + "\0\0";
        var bytes = System.Text.Encoding.Unicode.GetBytes(text);
        const int headerSize = 20; // DROPFILES
        var h = GlobalAlloc(GHND, (UIntPtr)(headerSize + bytes.Length));
        var p = GlobalLock(h);
        try
        {
            Marshal.WriteInt32(p, 0, headerSize);           // pFiles
            Marshal.WriteInt32(p, 4, 0);                    // pt.x
            Marshal.WriteInt32(p, 8, 0);                    // pt.y
            Marshal.WriteInt32(p, 12, 0);                   // fNC
            Marshal.WriteInt32(p, 16, 1);                   // fWide
            Marshal.Copy(bytes, 0, p + headerSize, bytes.Length);
        }
        finally { GlobalUnlock(h); }

        var fmt = new FORMATETC { cfFormat = CF_HDROP, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
        var med = new STGMEDIUM { tymed = TYMED.TYMED_HGLOBAL, unionmember = h };
        Store(fmt, med);
    }

    /// <summary>Reads a DWORD format the target wrote back, like "Performed DropEffect".</summary>
    public uint? ReadDword(string formatName)
    {
        short cf = (short)System.Windows.DataFormats.GetDataFormat(formatName).Id;
        foreach (var (f, m) in _entries)
        {
            if (f.cfFormat != cf || m.tymed != TYMED.TYMED_HGLOBAL || m.unionmember == IntPtr.Zero) continue;
            var p = GlobalLock(m.unionmember);
            try { return (uint)Marshal.ReadInt32(p); }
            finally { GlobalUnlock(m.unionmember); }
        }
        return null;
    }

    private void Store(FORMATETC fmt, STGMEDIUM med)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var f = _entries[i].format;
            if (f.cfFormat == fmt.cfFormat && f.dwAspect == fmt.dwAspect && f.lindex == fmt.lindex && (f.tymed & fmt.tymed) != 0)
            {
                var old = _entries[i].medium;
                ReleaseStgMedium(ref old);
                _entries.RemoveAt(i);
            }
        }
        _entries.Add((fmt, med));
    }

    private int Find(ref FORMATETC fmt)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var f = _entries[i].format;
            if (f.cfFormat == fmt.cfFormat && (f.tymed & fmt.tymed) != 0 && f.dwAspect == fmt.dwAspect &&
                (fmt.lindex == -1 || f.lindex == fmt.lindex))
                return i;
        }
        return -1;
    }

    private static IntPtr CopyGlobal(IntPtr src)
    {
        if (src == IntPtr.Zero) return IntPtr.Zero;
        var size = GlobalSize(src);
        var dst = GlobalAlloc(GHND, size);
        var s = GlobalLock(src); var d = GlobalLock(dst);
        try
        {
            unsafe { Buffer.MemoryCopy((void*)s, (void*)d, (ulong)size, (ulong)size); }
        }
        finally { GlobalUnlock(src); GlobalUnlock(dst); }
        return dst;
    }

    // MARK: IDataObject

    public void GetData(ref FORMATETC format, out STGMEDIUM medium)
    {
        int i = Find(ref format);
        if (i < 0) throw new COMException("Format not present", DV_E_FORMATETC);
        var stored = _entries[i].medium;
        medium = new STGMEDIUM { tymed = stored.tymed, pUnkForRelease = null };
        switch (stored.tymed)
        {
            case TYMED.TYMED_HGLOBAL:
                medium.unionmember = CopyGlobal(stored.unionmember);
                break;
            case TYMED.TYMED_ISTREAM:
            case TYMED.TYMED_ISTORAGE:
                medium.unionmember = stored.unionmember;
                if (stored.unionmember != IntPtr.Zero) Marshal.AddRef(stored.unionmember);
                break;
            default:
                throw new COMException("Unsupported medium", DV_E_TYMED);
        }
    }

    public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium)
        => throw new COMException("Not supported", E_NOTIMPL);

    public int QueryGetData(ref FORMATETC format) => Find(ref format) >= 0 ? 0 : DV_E_FORMATETC;

    public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut)
    {
        formatOut = formatIn;
        formatOut.ptd = IntPtr.Zero;
        return DATA_S_SAMEFORMATETC;
    }

    public void SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release)
    {
        var med = medium;
        if (!release)
        {
            if (medium.tymed == TYMED.TYMED_HGLOBAL) med.unionmember = CopyGlobal(medium.unionmember);
            else if (medium.unionmember != IntPtr.Zero && medium.tymed is TYMED.TYMED_ISTREAM or TYMED.TYMED_ISTORAGE)
                Marshal.AddRef(medium.unionmember);
            med.pUnkForRelease = null;
        }
        Store(formatIn, med);
    }

    public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
    {
        if (direction != DATADIR.DATADIR_GET) throw new COMException("Not supported", E_NOTIMPL);
        return new FormatEnumerator(_entries.Select(e => e.format).ToArray());
    }

    public int DAdvise(ref FORMATETC pFormatetc, ADVF advf, IAdviseSink adviseSink, out int connection)
    {
        connection = 0;
        return OLE_E_ADVISENOTSUPPORTED;
    }

    public void DUnadvise(int connection) => throw new COMException("Not supported", OLE_E_ADVISENOTSUPPORTED);

    public int EnumDAdvise(out IEnumSTATDATA? enumAdvise)
    {
        enumAdvise = null;
        return OLE_E_ADVISENOTSUPPORTED;
    }

    public void Dispose()
    {
        foreach (var e in _entries)
        {
            var m = e.medium;
            try { ReleaseStgMedium(ref m); } catch { }
        }
        _entries.Clear();
    }

    [ComVisible(true)]
    private sealed class FormatEnumerator : IEnumFORMATETC
    {
        private readonly FORMATETC[] _formats;
        private int _index;

        public FormatEnumerator(FORMATETC[] formats) { _formats = formats; }

        public int Next(int celt, FORMATETC[] rgelt, int[] pceltFetched)
        {
            int n = 0;
            while (n < celt && _index < _formats.Length) rgelt[n++] = _formats[_index++];
            if (pceltFetched is { Length: > 0 }) pceltFetched[0] = n;
            return n == celt ? 0 : 1;
        }

        public int Skip(int celt)
        {
            _index = Math.Min(_formats.Length, _index + celt);
            return _index < _formats.Length ? 0 : 1;
        }

        public int Reset() { _index = 0; return 0; }

        public void Clone(out IEnumFORMATETC newEnum) => newEnum = new FormatEnumerator(_formats) { _index = _index };
    }
}
