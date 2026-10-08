using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clothesline.Core;

/// <summary>
/// Sticky notes. A note is a small picture of coloured paper with your words
/// on it, written into the caught-captures folder, so it hangs, swings,
/// copies and drags out exactly like a screenshot. What it says and what
/// colour it is live in the settings, so it can be edited again.
/// </summary>
public static class Notes
{
    public static readonly string[] Colors = { "yellow", "pink", "blue", "green", "orange" };

    public static Color Paper(string key) => key switch
    {
        "pink" => Color.FromRgb(0xFB, 0xC4, 0xD6),
        "blue" => Color.FromRgb(0xBF, 0xDC, 0xFB),
        "green" => Color.FromRgb(0xC9, 0xEA, 0xC6),
        "orange" => Color.FromRgb(0xFF, 0xD6, 0xA3),
        _ => Color.FromRgb(0xFF, 0xF1, 0x8E),
    };

    public static bool IsNote(string path) => Settings.Current.Notes.ContainsKey(path);

    public static NoteData? Get(string path) => Settings.Current.Notes.TryGetValue(path, out var d) ? d : null;

    /// <summary>A fresh, empty note of the given colour. Returns the path of its picture.</summary>
    public static string Create(string color)
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(Inbox.Folder, $"Note {stamp}.png");
        int n = 2;
        while (File.Exists(path)) path = Path.Combine(Inbox.Folder, $"Note {stamp} ({n++}).png");
        var data = new NoteData { Text = "", Color = color };
        Settings.Current.Notes[path] = data;
        Render(data, path);
        Settings.Current.Save();
        return path;
    }

    /// <summary>New words or a new colour: the picture is drawn again.</summary>
    public static void Update(string path, string text, string color)
    {
        var data = Get(path) ?? new NoteData();
        data.Text = text;
        data.Color = color;
        Settings.Current.Notes[path] = data;
        Render(data, path);
        Settings.Current.Save();
    }

    public static void Forget(string path)
    {
        if (Settings.Current.Notes.Remove(path)) Settings.Current.Save();
    }

    private const double W = 360, H = 270, Curl = 48;

    /// <summary>Coloured paper with a lifted corner and the words on it, drawn at twice the size for crisp text.</summary>
    public static void Render(NoteData note, string path)
    {
        var paper = Paper(note.Color);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // The sheet, minus the corner that curls up.
            var sheet = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(0, 0), IsClosed = true };
            fig.Segments.Add(new LineSegment(new Point(W, 0), true));
            fig.Segments.Add(new LineSegment(new Point(W, H - Curl), true));
            fig.Segments.Add(new LineSegment(new Point(W - Curl, H), true));
            fig.Segments.Add(new LineSegment(new Point(0, H), true));
            sheet.Figures.Add(fig);
            var fill = new LinearGradientBrush(Lighten(paper, 0.12), Darken(paper, 0.08), 90);
            dc.DrawGeometry(fill, null, sheet);
            // A faint band along the top, where the glue would be.
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)), null, new Rect(0, 0, W, 34));
            // The curl: the back of the paper, lighter, with a shadow under it.
            var curl = new PathGeometry();
            var cf = new PathFigure { StartPoint = new Point(W, H - Curl), IsClosed = true };
            cf.Segments.Add(new BezierSegment(new Point(W - Curl * 0.15, H - Curl * 0.15), new Point(W - Curl * 0.55, H - Curl * 0.05), new Point(W - Curl, H), true));
            cf.Segments.Add(new BezierSegment(new Point(W - Curl * 0.3, H - Curl * 0.45), new Point(W - Curl * 0.15, H - Curl * 0.7), new Point(W, H - Curl), true));
            curl.Figures.Add(cf);
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(0x38, 0, 0, 0)), null, Geometry.Combine(curl, curl, GeometryCombineMode.Union, new TranslateTransform(-3, 3)));
            dc.DrawGeometry(new LinearGradientBrush(Lighten(paper, 0.45), Darken(paper, 0.02), 45), null, curl);

            // The words.
            var text = string.IsNullOrWhiteSpace(note.Text) ? "" : note.Text;
            bool rtl = text.Length > 0 && IsArabic(text);
            var ft = new FormattedText(text, CultureInfo.CurrentUICulture, rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 24,
                new SolidColorBrush(Color.FromRgb(0x3A, 0x30, 0x1E)), 1.0)
            {
                MaxTextWidth = W - 52, MaxTextHeight = H - 60, Trimming = TextTrimming.WordEllipsis,
            };
            dc.DrawText(ft, new Point(rtl ? W - 26 : 26, 30));
        }
        var rtb = new RenderTargetBitmap((int)(W * 2), (int)(H * 2), 192, 192, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        var tmp = path + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)) encoder.Save(stream);
        File.Move(tmp, path, true);
    }

    private static bool IsArabic(string s) => s.Any(c => c >= '؀' && c <= 'ۿ');
    private static Color Lighten(Color c, double k) => Color.FromRgb((byte)(c.R + (255 - c.R) * k), (byte)(c.G + (255 - c.G) * k), (byte)(c.B + (255 - c.B) * k));
    private static Color Darken(Color c, double k) => Color.FromRgb((byte)(c.R * (1 - k)), (byte)(c.G * (1 - k)), (byte)(c.B * (1 - k)));
}
