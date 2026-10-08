using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clothesline.Core;
using Clothesline.UI;

namespace Clothesline;

/// <summary>
/// Renders the line to a PNG without a window: `Clothesline --snapshot out.png [--width 1600] [--dark] [--hover 1] image1 image2 ...`.
/// Used for the README art and to check the drawing without a desktop.
/// </summary>
public static class Snapshot
{
    public static int Run(string[] args)
    {
        string? output = null;
        int width = 1600;
        bool dark = false;
        int hover = -1;
        var images = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--snapshot": output = args[++i]; break;
                case "--width": width = int.Parse(args[++i]); break;
                case "--dark": dark = true; break;
                case "--hover": hover = int.Parse(args[++i]); break;
                default: images.Add(args[i]); break;
            }
        }
        if (output is null) return 2;

        Theme.Override(dark);
        var line = new Line(persist: false) { MaxItems = 12 };
        foreach (var path in images) line.Hang(Path.GetFullPath(path), quietly: true);

        var canvas = new LineCanvas(line) { Width = width, Height = Layout.PanelHeight };
        var root = new Grid { Width = width, Height = Layout.PanelHeight + 40, ClipToBounds = true };
        var bg = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            GradientStops = dark
                ? new GradientStopCollection { new(Color.FromRgb(0x1E, 0x1F, 0x2E), 0), new(Color.FromRgb(0x2A, 0x22, 0x33), 0.5), new(Color.FromRgb(0x33, 0x24, 0x2C), 1) }
                : new GradientStopCollection { new(Color.FromRgb(0xE2, 0xE4, 0xFF), 0), new(Color.FromRgb(0xEE, 0xE2, 0xF6), 0.5), new(Color.FromRgb(0xFC, 0xE2, 0xE6), 1) },
        };
        root.Background = bg;
        root.Children.Add(canvas);
        canvas.VerticalAlignment = VerticalAlignment.Top;

        root.Measure(new Size(width, Layout.PanelHeight + 40));
        root.Arrange(new Rect(0, 0, width, Layout.PanelHeight + 40));
        root.UpdateLayout();

        canvas.Revealed = true;
        canvas.Advance(1.2);
        if (hover >= 0 && hover < canvas.Cards.Count)
        {
            canvas.Cards[hover].SetHovering(true);
            line.CopiedId = canvas.Cards[hover].Item.Id;
            canvas.Advance(0.4);
        }
        // Freeze mid-breeze, so the cards hang a little crooked and alive.
        foreach (var c in canvas.Cards) c.Breeze(now: true);
        canvas.Advance(0.55);
        root.UpdateLayout();

        var rtb = new RenderTargetBitmap(width * 2, (int)(Layout.PanelHeight + 40) * 2, 192, 192, PixelFormats.Pbgra32);
        rtb.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = File.Create(output);
        encoder.Save(stream);
        Console.WriteLine($"Wrote {output}");
        return 0;
    }
}
