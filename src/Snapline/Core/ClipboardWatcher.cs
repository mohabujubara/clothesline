using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Snapline.Interop;

namespace Snapline.Core;

/// <summary>
/// Catches captures that only reach the clipboard: Win+Shift+S when the
/// Snipping Tool is not saving files, PrtScn, Alt+PrtScn. A pure image on
/// the clipboard, with no text, HTML or files beside it, is written to the
/// inbox folder and hung. Anything we put on the clipboard ourselves carries
/// a file beside the image, so it is never caught.
/// </summary>
public sealed class ClipboardWatcher : IDisposable
{
    private readonly MessageWindow _window;
    private readonly Dispatcher _dispatcher;
    private readonly Action<string, int, int> _onCapture;
    private readonly Func<int, int, bool> _recentlyHungFromFile;
    private bool _listening;
    private uint _lastSequence;

    public bool Enabled { get; set; } = true;

    /// <param name="recentlyHungFromFile">Whether a file with these pixel dimensions just hung, so the clipboard copy of the same capture is skipped.</param>
    public ClipboardWatcher(MessageWindow window, Action<string, int, int> onCapture, Func<int, int, bool> recentlyHungFromFile)
    {
        _window = window;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _onCapture = onCapture;
        _recentlyHungFromFile = recentlyHungFromFile;
        _window.Message += OnMessage;
    }

    public void Start()
    {
        if (_listening) return;
        _listening = Native.AddClipboardFormatListener(_window.Handle);
        _lastSequence = Native.GetClipboardSequenceNumber();
    }

    private void OnMessage(int msg, IntPtr w, IntPtr l)
    {
        if (msg != Native.WM_CLIPBOARDUPDATE || !Enabled) return;
        var seq = Native.GetClipboardSequenceNumber();
        if (seq == _lastSequence) return;
        _lastSequence = seq;

        // The Snipping Tool may also be saving a file. Let that path hang the
        // capture first; then only catch what never reached a file.
        var timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(1200) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (Native.GetClipboardSequenceNumber() != seq) return; // already replaced
            TryCatch();
        };
        timer.Start();
    }

    private static readonly string[] NotPureImage =
    {
        DataFormats.Text, DataFormats.UnicodeText, DataFormats.Html, DataFormats.Rtf, DataFormats.FileDrop,
        "Object Descriptor", "Link Source", "Embed Source", "Shell IDList Array", "FileName", "FileNameW",
    };

    private void TryCatch()
    {
        try
        {
            IDataObject data;
            try { data = Clipboard.GetDataObject(); }
            catch { return; }
            if (data is null) return;
            var formats = data.GetFormats(false);
            if (formats is null || formats.Length == 0) return;
            if (!formats.Contains(DataFormats.Bitmap) && !formats.Contains(DataFormats.Dib) && !formats.Contains("PNG")) return;
            if (NotPureImage.Any(f => formats.Contains(f))) return;

            BitmapSource? image = null;
            if (formats.Contains("PNG") && data.GetData("PNG") is Stream png)
            {
                try
                {
                    var decoder = new PngBitmapDecoder(png, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                    image = decoder.Frames[0];
                }
                catch { image = null; }
            }
            image ??= Clipboard.GetImage();
            if (image is null || image.PixelWidth < 8 || image.PixelHeight < 8) return;
            if (_recentlyHungFromFile(image.PixelWidth, image.PixelHeight)) return;

            var path = Inbox.NewCapturePath();
            Thumbnails.SavePng(image, path);
            Log.Notice($"Caught a clipboard capture: {Path.GetFileName(path)}");
            _onCapture(path, image.PixelWidth, image.PixelHeight);
        }
        catch (Exception e)
        {
            Log.Error($"Clipboard capture failed: {e.Message}");
        }
    }

    public void Dispose()
    {
        _window.Message -= OnMessage;
        if (_listening) Native.RemoveClipboardFormatListener(_window.Handle);
        _listening = false;
    }
}
