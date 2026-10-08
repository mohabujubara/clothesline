using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Clothesline.Core;

/// <summary>
/// Reads the text in a screenshot with the OCR built into Windows. No
/// network, no service: the same engine the Snipping Tool uses.
/// </summary>
public static class Ocr
{
    public static bool Available => OcrEngine.AvailableRecognizerLanguages.Count > 0;

    /// <returns>The recognised lines joined by new lines, empty if nothing was read, null if OCR is unavailable.</returns>
    public static async Task<string?> ReadAsync(string path)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? OcrEngine.TryCreateFromLanguage(new Language("en-US"))
            ?? (OcrEngine.AvailableRecognizerLanguages.FirstOrDefault() is { } first ? OcrEngine.TryCreateFromLanguage(first) : null);
        if (engine is null) return null;

        var file = await StorageFile.GetFileFromPathAsync(path);
        using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);

        // The engine has a size limit; large captures are scaled to fit it.
        uint max = OcrEngine.MaxImageDimension;
        var transform = new BitmapTransform();
        if (decoder.PixelWidth > max || decoder.PixelHeight > max)
        {
            double scale = Math.Min((double)max / decoder.PixelWidth, (double)max / decoder.PixelHeight);
            transform.ScaledWidth = (uint)Math.Max(1, decoder.PixelWidth * scale);
            transform.ScaledHeight = (uint)Math.Max(1, decoder.PixelHeight * scale);
            transform.InterpolationMode = BitmapInterpolationMode.Fant;
        }
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

        var result = await engine.RecognizeAsync(bitmap);
        var lines = result.Lines.Select(l => l.Text.Trim()).Where(t => t.Length > 0);
        return string.Join(Environment.NewLine, lines);
    }
}
