using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clothesline.Core;

public readonly record struct Thumbnail(BitmapSource Image, int PixelWidth, int PixelHeight);

/// <summary>Decodes screenshots down to the size the line needs, patiently if the file is still being written.</summary>
public static class Thumbnails
{
    public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".avif",
    };

    public static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    /// <summary>A thumbnail no larger than `maxPixels` on its longest side. Null if the file cannot be read.</summary>
    public static Thumbnail? Load(string path, int maxPixels = 480, int attempts = 8)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length == 0) throw new IOException("empty");
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                int w = frame.PixelWidth, h = frame.PixelHeight;
                if (w <= 0 || h <= 0) throw new IOException("no pixels");

                stream.Position = 0;
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.StreamSource = stream;
                if (Math.Max(w, h) > maxPixels)
                {
                    if (w >= h) image.DecodePixelWidth = maxPixels; else image.DecodePixelHeight = maxPixels;
                }
                image.EndInit();
                image.Freeze();
                return new Thumbnail(image, w, h);
            }
            catch (Exception e) when (i < attempts - 1 && e is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
            {
                // The capture is still being written. Give it a moment.
                Thread.Sleep(80 + i * 40);
            }
            catch (Exception e)
            {
                Log.Error($"Could not decode {path}: {e.Message}");
                return null;
            }
        }
        return null;
    }

    public static void SavePng(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    public static byte[]? PngBytes(string path)
    {
        try
        {
            if (string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                return File.ReadAllBytes(path);
            var loaded = Load(path, int.MaxValue, 3);
            if (loaded is null) return null;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(loaded.Value.Image));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch (Exception e)
        {
            Log.Error($"Could not read {path}: {e.Message}");
            return null;
        }
    }
}
