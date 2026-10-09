using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Snapline.UI;

/// <summary>The mark, the wordmark and the colours that make Snapline look like itself.</summary>
public static class Brand
{
    // Cobalt and charcoal on white.
    public static readonly Color Cobalt = Color.FromRgb(0x1E, 0x6F, 0xFF);
    public static readonly Color Charcoal = Color.FromRgb(0x1C, 0x1F, 0x2A);
    public static readonly Color Slate = Color.FromRgb(0x6B, 0x72, 0x80);

    /// <summary>Manrope ExtraBold, embedded, the face of the wordmark.</summary>
    public static readonly FontFamily Wordmark = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Manrope");
    public static readonly FontWeight WordmarkWeight = FontWeights.ExtraBold;

    /// <summary>The designed lockup, mark and name together, as drawn.</summary>
    public static BitmapSource? LockupImage(bool dark = false) => Load(dark ? "LockupDark.png" : "Lockup.png");

    private static readonly Dictionary<string, BitmapSource?> Cache = new();
    private static BitmapSource? Load(string name)
    {
        if (Cache.TryGetValue(name, out var cached)) return cached;
        BitmapSource? result = null;
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/" + name));
            if (info is not null) result = BitmapDecoder.Create(info.Stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        }
        catch { }
        Cache[name] = result;
        return result;
    }

    public static SolidColorBrush Ink(bool dark = false) => Freeze(new SolidColorBrush(dark ? Colors.White : Charcoal));
    public static SolidColorBrush Accent() => Freeze(new SolidColorBrush(Cobalt));
    public static SolidColorBrush Muted(bool dark = false) => Freeze(new SolidColorBrush(dark ? Color.FromRgb(0x9A, 0xA0, 0xA8) : Slate));
    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    private static BitmapFrame? _icon;

    /// <summary>The app icon, for window title bars: the mark on a white tile.</summary>
    public static BitmapFrame? Icon
    {
        get
        {
            if (_icon is not null) return _icon;
            try
            {
                var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Snapline.ico"));
                if (info is null) return null;
                var decoder = BitmapDecoder.Create(info.Stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                _icon = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
            }
            catch { }
            return _icon;
        }
    }

    private static BitmapSource? _mark;

    /// <summary>The bare mark, transparent, for banners, title cards and headers.</summary>
    public static BitmapSource? Mark
    {
        get
        {
            if (_mark is not null) return _mark;
            try
            {
                var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Mark.png"));
                if (info is null) return null;
                var decoder = BitmapDecoder.Create(info.Stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                _mark = decoder.Frames[0];
            }
            catch { }
            return _mark;
        }
    }

    /// <summary>The mark beside the name, as one piece.</summary>
    public static FrameworkElement Lockup(double nameSize, bool dark = false)
    {
        var row = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        if (Mark is { } mark)
            row.Children.Add(new System.Windows.Controls.Image { Source = mark, Width = nameSize * 1.05, Height = nameSize * 1.05, Margin = new Thickness(0, 0, nameSize * 0.22, 0), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = Strings.AppName, FontFamily = Wordmark, FontWeight = WordmarkWeight, FontSize = nameSize,
            Foreground = Ink(dark), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -nameSize * 0.08, 0, 0),
        });
        return row;
    }
}
