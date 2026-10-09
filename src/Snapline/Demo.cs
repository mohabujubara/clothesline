using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Snapline.Core;
using Snapline.UI;
using Line = Snapline.Core.Line;
using Path = System.Windows.Shapes.Path;

namespace Snapline;

/// <summary>
/// Renders the demo film and the hero banner without a desktop. The line,
/// its physics, its cards, the markup editor, the note editor and the
/// settings page are the real ones; a Windows 11 desktop, a pointer and the
/// captions are drawn around them.
///
///   Snapline --demo out\frames        writes frame_0000.png … at 30 fps, 1600 x 900
///   Snapline --hero out.png           writes the README banner
///   Snapline --hero-dark out.png      the same, for dark mode
/// The title on the banner and the title card follows --title "Name" if given.
/// </summary>
public static class Demo
{
    private const int W = 1600, H = 900, Fps = 30;
    public static string Title = Strings.AppName;
    public static string Link = "github.com/mohabujubara/snapline";

    private static string Sample(string name)
    {
        foreach (var dir in new[] { System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "samples"), System.IO.Path.Combine("docs", "samples") })
        {
            var p = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, name));
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException(name);
    }

    private static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    private static readonly FontFamily Text = new("Segoe UI Variable Text, Segoe UI");

    // MARK: A Windows 11 desktop

    private sealed class Scene
    {
        public readonly Line Line = new(persist: false) { MaxItems = 12 };
        public readonly LineCanvas Canvas;
        public readonly Grid Root = new() { Width = W, Height = H, ClipToBounds = true };
        public readonly Canvas Overlay = new() { Width = W, Height = H, IsHitTestVisible = false };
        public readonly Path Cursor;
        public readonly Border Caption;
        public readonly TextBlock CaptionText;
        public readonly Image PageImage;
        public Point Pointer = new(W / 2, H * 0.6);

        public Scene()
        {
            // A real Windows 11 desktop, taskbar and all, as the backdrop.
            Root.Children.Add(new Image { Source = Thumbnails.Load(Sample("desktop.png"), int.MaxValue, 2)!.Value.Image, Stretch = Stretch.UniformToFill });
            Root.Children.Add(BrowserWindow(out PageImage));

            Canvas = new LineCanvas(Line) { Width = W, Height = Layout.PanelHeight, VerticalAlignment = VerticalAlignment.Top };
            Root.Children.Add(Canvas);
            Root.Children.Add(Overlay);

            CaptionText = new TextBlock { FontFamily = Display, FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x22)) };
            Caption = new Border
            {
                Child = CaptionText, Padding = new Thickness(22, 10, 22, 12), CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), BorderThickness = new Thickness(0.75),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 78),
                Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 4, Direction = 270, Opacity = 0.22, Color = Colors.Black },
                Opacity = 0,
            };
            Root.Children.Add(Caption);

            Cursor = new Path
            {
                Data = Geometry.Parse("M0,0 L0,17 L4.5,13 L7.5,20 L10.5,18.8 L7.5,12 L13,12 Z"),
                Fill = Brushes.White, Stroke = Brushes.Black, StrokeThickness = 1.2, StrokeLineJoin = PenLineJoin.Round,
                Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Opacity = 0.4, Color = Colors.Black },
                RenderTransform = new ScaleTransform(1.25, 1.25),
            };
            Overlay.Children.Add(Cursor);
            Panel.SetZIndex(Cursor, 100);
        }

        /// <summary>The Windows 11 bloom: a deep blue field with soft petals of light.</summary>
        private static UIElement Wallpaper()
        {
            var g = new Grid();
            g.Children.Add(new Rectangle
            {
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
                    GradientStops = { new GradientStop(Color.FromRgb(0x08, 0x1C, 0x4A), 0), new GradientStop(Color.FromRgb(0x0F, 0x3A, 0x8C), 0.55), new GradientStop(Color.FromRgb(0x17, 0x5C, 0xC8), 1) },
                },
            });
            void Petal(double cx, double cy, double w, double h, double angle, Color c, double opacity)
            {
                g.Children.Add(new Ellipse
                {
                    Width = w, Height = h, Opacity = opacity,
                    Fill = new RadialGradientBrush(c, Color.FromArgb(0, c.R, c.G, c.B)) { RadiusX = 0.5, RadiusY = 0.5 },
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(cx - w / 2, cy - h / 2, 0, 0),
                    RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(angle),
                });
            }
            var light = Color.FromRgb(0x6F, 0xC3, 0xFF);
            var pale = Color.FromRgb(0xC9, 0xEA, 0xFF);
            Petal(1120, 520, 900, 420, -28, light, 0.75);
            Petal(980, 640, 760, 360, 22, light, 0.6);
            Petal(1240, 420, 620, 300, -62, pale, 0.55);
            Petal(860, 500, 520, 260, 8, Color.FromRgb(0x2E, 0x8A, 0xF2), 0.8);
            Petal(1100, 560, 300, 140, -20, Colors.White, 0.5);
            return g;
        }

        private static UIElement DesktopIcons()
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(18, 14, 0, 0) };
            UIElement Icon(string label, UIElement glyph)
            {
                var s = new StackPanel { Width = 84, Margin = new Thickness(0, 0, 0, 14) };
                var box = new Grid { Width = 46, Height = 46, HorizontalAlignment = HorizontalAlignment.Center };
                box.Children.Add(glyph);
                s.Children.Add(box);
                s.Children.Add(new TextBlock
                {
                    Text = label, Foreground = Brushes.White, FontFamily = Text, FontSize = 12, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0),
                    Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Opacity = 0.8, Color = Colors.Black },
                });
                return s;
            }
            var bin = new Grid();
            bin.Children.Add(new Border { Width = 30, Height = 34, Margin = new Thickness(0, 8, 0, 0), CornerRadius = new CornerRadius(3, 3, 6, 6), Background = new LinearGradientBrush(Color.FromArgb(0xD0, 0xE8, 0xF2, 0xFF), Color.FromArgb(0xA0, 0xA8, 0xC4, 0xE0), 90), BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Top });
            bin.Children.Add(new Border { Width = 36, Height = 6, Margin = new Thickness(0, 4, 0, 0), CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromRgb(0xC8, 0xDA, 0xEE)), VerticalAlignment = VerticalAlignment.Top });
            stack.Children.Add(Icon("Recycle Bin", bin));
            var pc = new Grid();
            pc.Children.Add(new Border { Width = 40, Height = 28, Margin = new Thickness(0, 6, 0, 0), CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x8A, 0xF2)), BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE8, 0xF8)), BorderThickness = new Thickness(2), VerticalAlignment = VerticalAlignment.Top });
            pc.Children.Add(new Border { Width = 18, Height = 4, Margin = new Thickness(0, 38, 0, 0), Background = new SolidColorBrush(Color.FromRgb(0xDD, 0xE8, 0xF8)), VerticalAlignment = VerticalAlignment.Top });
            stack.Children.Add(Icon("This PC", pc));
            return stack;
        }

        /// <summary>A browser window with a page in it, so the line has something to hang over.</summary>
        private static UIElement BrowserWindow(out Image pageImage)
        {
            var win = new Border
            {
                Width = 1240, Height = 690, CornerRadius = new CornerRadius(8), ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(180, 128, 0, 0),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0, 0, 0)), BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { BlurRadius = 36, ShadowDepth = 8, Direction = 270, Opacity = 0.35, Color = Colors.Black },
            };
            var dock = new DockPanel();
            var tabs = new DockPanel { Height = 42, Background = new SolidColorBrush(Color.FromRgb(0xDE, 0xE1, 0xE6)) };
            var winButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 14, 0) };
            foreach (var t in new[] { "—", "☐", "✕" }) winButtons.Children.Add(new TextBlock { Text = t, FontSize = 12, Margin = new Thickness(16, 6, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x45)) });
            DockPanel.SetDock(winButtons, Dock.Right);
            tabs.Children.Add(winButtons);
            var tab = new Border { Width = 230, Height = 34, Margin = new Thickness(10, 8, 0, 0), CornerRadius = new CornerRadius(8, 8, 0, 0), Background = Brushes.White, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left };
            var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            tabRow.Children.Add(new Ellipse { Width = 14, Height = 14, Fill = new SolidColorBrush(Color.FromRgb(0x2E, 0x8A, 0xF2)), Margin = new Thickness(0, 0, 8, 0) });
            tabRow.Children.Add(new TextBlock { Text = "Yahoo Finance", FontFamily = Text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center });
            tab.Child = tabRow;
            DockPanel.SetDock(tab, Dock.Left);
            tabs.Children.Add(tab);
            tabs.Children.Add(new TextBlock { Text = "+", FontSize = 18, Margin = new Thickness(12, 8, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x55)), HorizontalAlignment = HorizontalAlignment.Left });
            DockPanel.SetDock(tabs, Dock.Top);
            dock.Children.Add(tabs);
            var bar = new DockPanel { Height = 44, Background = Brushes.White };
            var nav = new TextBlock { Text = "←   →   ⟳", FontSize = 14, Margin = new Thickness(16, 12, 10, 0), Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x66)) };
            DockPanel.SetDock(nav, Dock.Left);
            bar.Children.Add(nav);
            var address = new Border { Height = 30, Margin = new Thickness(0, 7, 16, 7), CornerRadius = new CornerRadius(15), Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF3, 0xF4)) };
            address.Child = new TextBlock { Text = "finance.yahoo.com", FontFamily = Text, FontSize = 13, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x35)) };
            bar.Children.Add(address);
            DockPanel.SetDock(bar, Dock.Top);
            dock.Children.Add(bar);
            var rule = new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromRgb(0xE4, 0xE4, 0xE8)) };
            DockPanel.SetDock(rule, Dock.Top);
            dock.Children.Add(rule);
            // The page itself is a real screenshot, top aligned and cropped to the window.
            pageImage = new Image { Stretch = Stretch.UniformToFill, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
            var page = new Border { Background = Brushes.White, ClipToBounds = true, Child = pageImage };
            dock.Children.Add(page);
            win.Child = dock;
            return win;
        }

        private static UIElement Taskbar()
        {
            var bar = new Grid { Height = 48, VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(Color.FromArgb(0xE8, 0xF3, 0xF3, 0xF9)) };
            bar.Children.Add(new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), VerticalAlignment = VerticalAlignment.Top });
            var centre = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var logo = new Grid { Width = 20, Height = 20, Margin = new Thickness(10, 0, 10, 0) };
            foreach (var (x, yy) in new[] { (0, 0), (11, 0), (0, 11), (11, 11) })
                logo.Children.Add(new Rectangle { Width = 9, Height = 9, Fill = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), Margin = new Thickness(x, yy, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
            centre.Children.Add(logo);
            centre.Children.Add(Glyph("M7,7 m-5,0 a5,5 0 1,0 10,0 a5,5 0 1,0 -10,0 M11,11 L16,16", Color.FromRgb(0x30, 0x30, 0x35)));
            centre.Children.Add(Glyph("M2,5 H12 V13 H2 Z M6,2 H16 V10", Color.FromRgb(0x30, 0x30, 0x35)));
            var widgets = new Grid { Width = 18, Height = 18, Margin = new Thickness(10, 0, 10, 0) };
            foreach (var (x, yy, c) in new[] { (0, 0, "#2E8AF2"), (10, 0, "#F2B134"), (0, 10, "#4CB85C"), (10, 10, "#8A7AF0") })
                widgets.Children.Add(new Rectangle { Width = 8, Height = 8, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)), Margin = new Thickness(x, yy, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
            centre.Children.Add(widgets);
            var folder = new Grid { Width = 22, Height = 20, Margin = new Thickness(10, 0, 10, 0) };
            folder.Children.Add(new Border { Width = 22, Height = 16, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xC0, 0x3E)), VerticalAlignment = VerticalAlignment.Bottom });
            folder.Children.Add(new Border { Width = 10, Height = 5, CornerRadius = new CornerRadius(2, 2, 0, 0), Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xA8, 0x1E)), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left });
            centre.Children.Add(folder);
            centre.Children.Add(new Ellipse { Width = 22, Height = 22, Margin = new Thickness(10, 0, 10, 0), Fill = new LinearGradientBrush(Color.FromRgb(0x2A, 0xB7, 0x66), Color.FromRgb(0x0B, 0x6B, 0xD8), 45) });
            centre.Children.Add(new Border { Width = 22, Height = 22, Margin = new Thickness(10, 0, 10, 0), CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x6C, 0xBD)) });
            bar.Children.Add(centre);
            var tray = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            tray.Children.Add(new TextBlock { Text = "⌃", FontSize = 12, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x35)) });
            tray.Children.Add(new TextBlock { Text = "ENG", FontFamily = Text, FontSize = 11, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x35)) });
            tray.Children.Add(Glyph("M2,6 Q8,1 14,6 M4,9 Q8,6 12,9 M6,12 Q8,10.5 10,12", Color.FromRgb(0x30, 0x30, 0x35)));
            tray.Children.Add(Glyph("M2,6 H5 L9,2 V14 L5,10 H2 Z M11,5 Q14,8 11,11", Color.FromRgb(0x30, 0x30, 0x35)));
            tray.Children.Add(Glyph("M1,4 H13 V12 H1 Z M13,7 H15 V9 H13 M3,6 H11 V10 H3 Z", Color.FromRgb(0x30, 0x30, 0x35)));
            var clock = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            clock.Children.Add(new TextBlock { Text = "10:32", FontFamily = Text, FontSize = 12, TextAlignment = TextAlignment.Right, Foreground = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x25)) });
            clock.Children.Add(new TextBlock { Text = "09/10/2026", FontFamily = Text, FontSize = 12, TextAlignment = TextAlignment.Right, Foreground = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x25)) });
            tray.Children.Add(clock);
            bar.Children.Add(tray);
            return bar;
        }

        private static Path Glyph(string data, Color color) => new()
        {
            Data = Geometry.Parse(data), Stroke = new SolidColorBrush(color), StrokeThickness = 1.6, Width = 18, Height = 18, Stretch = Stretch.Uniform,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
        };

        public void Relayout()
        {
            Root.Measure(new Size(W, H));
            Root.Arrange(new Rect(0, 0, W, H));
            Root.UpdateLayout();
        }

        public BitmapSource Render()
        {
            Relayout();
            var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(Root);
            rtb.Freeze();
            return rtb;
        }

        public void PlaceCursor() { System.Windows.Controls.Canvas.SetLeft(Cursor, Pointer.X); System.Windows.Controls.Canvas.SetTop(Cursor, Pointer.Y); }

        /// <summary>A window of the app, shown over the desktop: rounded, shadowed, fading in and out.</summary>
        public Border Host(FrameworkElement content, double x, double y, double? width = null)
        {
            var host = new Border
            {
                Child = width is { } w ? new Viewbox { Child = content, Width = w, Stretch = Stretch.Uniform } : content,
                CornerRadius = new CornerRadius(10), ClipToBounds = true,
                Effect = new DropShadowEffect { BlurRadius = 44, ShadowDepth = 10, Direction = 270, Opacity = 0.4, Color = Colors.Black },
                Opacity = 0, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(0.96, 0.96),
            };
            System.Windows.Controls.Canvas.SetLeft(host, x);
            System.Windows.Controls.Canvas.SetTop(host, y);
            Overlay.Children.Add(host);
            Panel.SetZIndex(host, 70);
            return host;
        }
    }

    private static void Fade(Border host, double k)
    {
        k = Math.Clamp(k, 0, 1);
        host.Opacity = k;
        var s = (ScaleTransform)host.RenderTransform;
        s.ScaleX = s.ScaleY = 0.96 + 0.04 * Ease.OutCubic(k);
    }

    private static void ApplyLook()
    {
        Settings.ReadOnly = true;
        Settings.Current.LineOffset = 0;
        Settings.Current.HotKey = "Ctrl+Alt+T";
        Settings.Current.Language = "en";
        Strings.Refresh();
        Settings.Current.AutoArrange = false;
        Settings.Current.Bows = true;
        Settings.Current.RopeColor = "bronze";
        Settings.Current.PegStyle = "wood";
        Settings.Current.Appearance = "light";
        Theme.ApplySetting();
    }

    // MARK: The film

    public static int RunDemo(string outDir)
    {
        Directory.CreateDirectory(outDir);
        ApplyLook();

        var scene = new Scene();
        var line = scene.Line;
        var canvas = scene.Canvas;
        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "snapline-demo");
        Directory.CreateDirectory(temp);
        string Copy(string name) { var p = System.IO.Path.Combine(temp, name); File.Copy(Sample(name), p, true); return p; }
        var docPath = Copy("arabic-site.png");
        var dashPath = Copy("arabic-dashboard.png");
        var notePath = System.IO.Path.Combine(temp, "Note demo.png");
        // The page in the browser, and the piece of it that gets snipped.
        var yahoo = Thumbnails.Load(Sample("yahoo.png"), int.MaxValue, 2)!.Value.Image;
        scene.PageImage.Source = yahoo;
        var snipInPage = new Int32Rect(420, 118, 420, 212);
        var sunsetPath = System.IO.Path.Combine(temp, "snip.png");
        Thumbnails.SavePng(new CroppedBitmap(yahoo, snipInPage), sunsetPath);
        scene.Relayout();

        var docId = line.Hang(docPath, quietly: true)!.Value;
        var dashId = line.Hang(dashPath, quietly: true)!.Value;
        scene.Relayout();
        line.SetSpot(docId, 0.33, save: false);
        line.SetSpot(dashId, 0.47, save: false);
        canvas.RelayoutNow();
        canvas.Advance(2.0);
        var dashCross = canvas.Cards.First(c => c.Item.Id == dashId).CardRect;
        var crossPoint = new Point(dashCross.Left + 13, dashCross.Top + 13);
        var docRect = canvas.Cards.First(c => c.Item.Id == docId).CardRect;
        var docPoint = new Point(docRect.Left + docRect.Width / 2, docRect.Top + docRect.Height / 2);

        double dt = 1.0 / Fps;
        var tl = new Timeline(scene, dt);

        // The line comes down.
        tl.Move(new Point(900, 600), new Point(760, 2), 0.0, 1.1);
        tl.At(1.35, () => canvas.Revealed = true);
        tl.Caption("Rest the pointer at the top edge. The line comes down.", 1.4, 3.7);
        tl.Move(new Point(760, 2), new Point(760, 150), 1.8, 2.4);

        // A snip of the page flies to the line. The page is scaled to the window's inner width.
        double pageScale = 1238.0 / yahoo.PixelWidth;
        var snipRect = new Rect(181 + snipInPage.X * pageScale, 215 + snipInPage.Y * pageScale, snipInPage.Width * pageScale, snipInPage.Height * pageScale);
        Flight? flight = null;
        tl.Caption("Take a screenshot. It hangs the moment you let go.", 3.9, 6.6);
        tl.Move(new Point(760, 150), snipRect.TopLeft, 3.7, 4.2);
        tl.Snip(snipRect, 4.2, 5.1);
        tl.Move(snipRect.TopLeft, snipRect.BottomRight, 4.25, 5.05);
        Guid sunsetId = Guid.Empty;
        tl.At(5.15, () =>
        {
            sunsetId = line.Hang(sunsetPath, quietly: true, flying: true)!.Value;
            scene.Relayout();
            line.SetSpot(sunsetId, 0.61, save: false);
            canvas.RelayoutNow();
            canvas.Advance(0.001);
            scene.Relayout();
            var card = canvas.Cards.First(c => c.Item.Id == sunsetId);
            flight = new Flight(scene, card.Item.Thumb, snipRect, card.CardRect, card.Item.Tilt, 5.15, () => line.Land(sunsetId));
        });

        // Click to copy.
        tl.Move(snipRect.BottomRight, new Point(976, 120), 6.3, 6.9);
        tl.Click(7.1);
        tl.At(7.1, () => { line.CopiedLabel = Strings.Copied; line.CopiedId = sunsetId; });
        tl.Caption("Click to copy. Paste it anywhere.", 6.9, 9.2);
        tl.At(8.6, () => line.CopiedId = null);

        // Keep on the line.
        tl.Caption("Keep on the line: newer captures never push it off.", 9.4, 11.9);
        tl.At(9.8, () => line.TogglePin(sunsetId));

        // Press and hold: the markup editor.
        MarkupWindow.DemoHandle? editor = null;
        Border? editorHost = null;
        tl.Move(new Point(976, 120), docPoint, 11.6, 12.2);
        tl.Caption("Press and hold to mark it up.", 12.2, 14.4);
        tl.Click(12.5);
        tl.At(13.2, () =>
        {
            editor = MarkupWindow.ForDemo(docPath, line);
            editorHost = scene.Host(editor.Content, 230, 95, 1140);
        });
        tl.Fade(() => editorHost, 13.2, 13.55, true);
        tl.Caption("Pen, highlighter, circle, box, arrow, text. Six colours, three sizes.", 14.6, 18.4);
        tl.At(14.4, () => { editor?.Tool("Pen"); editor?.Colour(0); editor?.Size(1); });
        // The gestures are written in the picture's own pixels (1030 x 427) and the pointer follows them.
        Point OnScreen(double ix, double iy) => editor is null ? new Point(800, 450) : new Point(230 + editor.PagePoint(ix, iy).X * 1140 / editor.Width + 13, 95 + 48 + editor.PagePoint(ix, iy).Y * 1140 / editor.Width + 4);
        var penPts = new List<Point>();
        tl.MoveTo(() => OnScreen(1000, 122), () => OnScreen(640, 122), 14.6, 15.6, k =>
        {
            if (editor is null) return;
            double ix = 1000 - 360 * k;
            penPts.Add(editor.PagePoint(ix, 122 + Math.Sin(k * 14) * 5));
            editor.LiveStroke(penPts);
        });
        tl.At(15.65, () => editor?.EndLiveStroke());
        tl.At(15.9, () => { editor?.Tool("Highlighter"); editor?.Colour(1); });
        var hiPts = new List<Point>();
        tl.MoveTo(() => OnScreen(1010, 152), () => OnScreen(230, 152), 16.0, 16.9, k =>
        {
            if (editor is null) return;
            hiPts.Add(editor.PagePoint(1010 - 780 * k, 152));
            editor.LiveStroke(hiPts);
        });
        tl.At(16.95, () => editor?.EndLiveStroke());
        tl.At(17.2, () => { editor?.Tool("Circle"); editor?.Colour(0); editor?.Size(1); });
        tl.MoveTo(() => OnScreen(640, 268), () => OnScreen(935, 385), 17.3, 18.1, k =>
        {
            if (editor is null) return;
            editor.LiveShape(editor.PagePoint(640, 268), editor.PagePoint(640 + 295 * k, 268 + 117 * k));
        });
        tl.At(18.15, () => editor?.Shape(editor.PagePoint(640, 268), editor.PagePoint(935, 385)));
        tl.Caption("Blur hides what should not be shared.", 18.6, 20.6);
        tl.At(18.7, () => editor?.Tool("Blur"));
        tl.MoveTo(() => OnScreen(1018, 390), () => OnScreen(220, 424), 18.8, 19.5, k =>
        {
            if (editor is null) return;
            editor.LiveShape(editor.PagePoint(1018, 390), editor.PagePoint(1018 - 798 * k, 390 + 34 * k));
        });
        tl.At(19.55, () => editor?.Shape(editor.PagePoint(1018, 390), editor.PagePoint(220, 424)));
        tl.At(20.0, () => { editor?.Tool("Text"); editor?.Colour(0); editor?.Size(2); });
        tl.MoveTo(() => OnScreen(220, 424), () => OnScreen(60, 300), 19.8, 20.3);
        tl.Click(20.4);
        tl.At(20.5, () => editor?.Text(editor.PagePoint(60, 300), "Check this before Monday"));
        tl.Caption("Every mark saves into the file as you go. Undo, redo, revert to the original.", 20.8, 24.2);
        tl.At(22.0, () => editor?.Undo());
        tl.At(22.9, () => editor?.Text(editor.PagePoint(60, 300), "Check this before Monday"));
        tl.At(24.0, () => editor?.Save());
        tl.Fade(() => editorHost, 24.3, 24.6, false);
        tl.At(24.7, () => { if (editorHost is not null) scene.Overlay.Children.Remove(editorHost); editorHost = null; });

        // Copy text.
        tl.MoveTo(() => OnScreen(60, 300), () => docPoint, 24.6, 25.2);
        tl.Caption("Copy text reads the words off a screenshot with Windows OCR.", 25.3, 28.2);
        tl.Click(25.6);
        tl.At(25.6, () => { line.CopiedLabel = Strings.TextCopied; line.CopiedId = docId; });
        tl.At(27.4, () => line.CopiedId = null);

        // Drag along the line.
        PeggedControl? held = null;
        tl.Caption("Drag a photo along the line. It stays where you put it.", 28.4, 31.2);
        tl.At(28.6, () => { held = canvas.Cards.First(c => c.Item.Id == docId); canvas.BeginReorder(held); });
        tl.Move(docPoint, new Point(300, docPoint.Y), 28.7, 30.1, p => { if (held is not null) canvas.ReorderTo(held, p.X); });
        tl.At(30.2, () => { if (held is not null) { canvas.EndReorder(held); held = null; } });

        // The cross lets one go.
        Fall? fall = null;
        tl.Move(new Point(300, docPoint.Y), crossPoint, 30.6, 31.3);
        tl.Caption("The cross lets it go.", 31.3, 33.4);
        tl.Click(31.6);
        tl.At(31.6, () =>
        {
            var card = canvas.Cards.First(c => c.Item.Id == dashId);
            fall = new Fall(scene, card.Item.Thumb, card.CardRect, card.Item.Tilt, 31.6);
            line.Drop(dashId, quietly: true);
        });

        // A sticky note, written in its little window.
        Guid noteId = Guid.Empty;
        Border? noteHost = null;
        Action<string>? typeNote = null; Action? saveNote = null;
        const string noteText = "Send the deck to Sara before 3";
        tl.Caption("Sticky notes hang on the line too.", 33.6, 37.8);
        tl.At(33.8, () =>
        {
            Settings.Current.Notes[notePath] = new NoteData { Text = "", Color = "yellow" };
            Notes.Render(Settings.Current.Notes[notePath], notePath);
            noteId = line.Hang(notePath, quietly: true)!.Value;
            scene.Relayout();
            line.SetSpot(noteId, 0.47, save: false);
            canvas.RelayoutNow();
            var (content, type, save) = NoteWindow.ForDemo(notePath, line);
            typeNote = type; saveNote = save;
            noteHost = scene.Host(content, 590, 330);
        });
        tl.Fade(() => noteHost, 34.2, 34.5, true);
        tl.Move(crossPoint, new Point(700, 420), 33.9, 34.5);
        tl.Type(noteText, 34.8, 36.9, s => { typeNote?.Invoke(s); saveNote?.Invoke(); });
        tl.Fade(() => noteHost, 37.4, 37.7, false);
        tl.At(37.8, () => { if (noteHost is not null) scene.Overlay.Children.Remove(noteHost); noteHost = null; });

        // Settings: the look changes live.
        Border? settingsHost = null;
        tl.Caption("Your line, your way: ten colours, three kinds of pegs, light or dark, English or Arabic.", 38.2, 44.6);
        tl.At(38.4, () => settingsHost = scene.Host(SettingsWindow.ForDemo(() => { }), 1090, 40, 470));
        tl.Fade(() => settingsHost, 38.4, 38.75, true);
        tl.Move(new Point(700, 420), new Point(1400, 330), 38.6, 39.4);
        tl.Click(39.8);
        tl.At(39.8, () => { Settings.Current.RopeColor = "blue"; Pegs.RaiseLookChanged(); RefreshSettings(settingsHost); });
        tl.Click(41.2);
        tl.At(41.2, () => { Settings.Current.PegStyle = "mixed"; Pegs.RaiseLookChanged(); RefreshSettings(settingsHost); });
        tl.Click(42.6);
        tl.At(42.6, () => { Settings.Current.RopeColor = "bronze"; Settings.Current.PegStyle = "wood"; Pegs.RaiseLookChanged(); RefreshSettings(settingsHost); });
        tl.Fade(() => settingsHost, 44.2, 44.5, false);
        tl.At(44.6, () => { if (settingsHost is not null) scene.Overlay.Children.Remove(settingsHost); settingsHost = null; });

        // Move away: the line tucks itself up.
        tl.Caption("Move away and it's gone. Clicks go straight through to your windows.", 44.8, 47.6);
        tl.Move(new Point(1400, 330), new Point(1120, 620), 44.8, 45.8);
        tl.At(45.4, () => canvas.Revealed = false);

        // Title card.
        tl.Title(47.8, 51.5);

        double total = 51.5;
        int frame = 0;
        for (double t = 0; t < total; t += dt, frame++)
        {
            tl.Step(t);
            canvas.Advance(dt);
            if (frame % 100 == 50) foreach (var c in canvas.Cards) c.Breeze(now: true);
            canvas.UpdateHover(canvas.Revealed && editorHost is null && noteHost is null && settingsHost is null ? scene.Pointer : null);
            flight?.Update(t);
            fall?.Update(t);
            scene.PlaceCursor();
            var bmp = scene.Render();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using var stream = File.Create(System.IO.Path.Combine(outDir, $"frame_{frame:0000}.png"));
            encoder.Save(stream);
        }
        Log.Notice($"Demo: wrote {frame} frames to {outDir}");
        return 0;
    }

    private static void RefreshSettings(Border? host)
    {
        if (host is null) return;
        host.Child = SettingsWindow.ForDemo(() => { });
    }

    // MARK: The banner

    public static int RunHero(string outPath, bool dark)
    {
        ApplyLook();
        if (dark) { Settings.Current.Appearance = "dark"; Theme.ApplySetting(); }

        const int bw = 2400, bh = 1280;
        var root = new Grid { Width = bw, Height = bh, Background = new SolidColorBrush(dark ? Color.FromRgb(0x0D, 0x11, 0x17) : Colors.White) };
        root.Children.Add(new TextBlock
        {
            Text = Title, FontFamily = Display, FontSize = 150, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(dark ? Colors.White : Color.FromRgb(0x1C, 0x1C, 0x1E)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 110, 0, 0),
        });
        root.Children.Add(new TextBlock
        {
            Text = "Screenshots, hung out to dry. For Windows.", FontFamily = Display, FontSize = 54,
            Foreground = new SolidColorBrush(dark ? Color.FromRgb(0x9A, 0xA0, 0xA8) : Color.FromRgb(0x6E, 0x6E, 0x73)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 300, 0, 0),
        });

        var scene = new Scene();
        var yahoo = Thumbnails.Load(Sample("yahoo.png"), int.MaxValue, 2)!.Value.Image;
        scene.PageImage.Source = yahoo;
        var snipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "snapline-hero-snip.png");
        Thumbnails.SavePng(new CroppedBitmap(yahoo, new Int32Rect(420, 118, 420, 212)), snipPath);
        var desk = new Border
        {
            Width = 2160, Height = 700, CornerRadius = new CornerRadius(40), ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 480, 0, 0),
            Child = new Viewbox { Child = scene.Root, Stretch = Stretch.UniformToFill },
            Effect = new DropShadowEffect { BlurRadius = 60, ShadowDepth = 14, Direction = 270, Opacity = 0.22, Color = Colors.Black },
        };
        scene.Root.Height = 520;
        scene.Caption.Visibility = Visibility.Collapsed;
        root.Children.Add(desk);

        var line = scene.Line;
        var a = line.Hang(Sample("arabic-site.png"), quietly: true)!.Value;
        var b = line.Hang(snipPath, quietly: true)!.Value;
        var c = line.Hang(Sample("arabic-dashboard.png"), quietly: true)!.Value;
        scene.Relayout();
        line.SetSpot(a, 0.36, save: false); line.SetSpot(b, 0.5, save: false); line.SetSpot(c, 0.64, save: false);
        scene.Canvas.RelayoutNow();
        scene.Canvas.Revealed = true;
        scene.Canvas.Advance(1.3);
        foreach (var card in scene.Canvas.Cards) card.Breeze(now: true);
        scene.Canvas.Advance(0.5);
        scene.Cursor.Visibility = Visibility.Collapsed;

        root.Measure(new Size(bw, bh));
        root.Arrange(new Rect(0, 0, bw, bh));
        root.UpdateLayout();
        var rtb = new RenderTargetBitmap(bw, bh, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = File.Create(outPath);
        encoder.Save(stream);
        return 0;
    }

    // MARK: Pieces of the film

    private sealed class Timeline
    {
        private readonly Scene _scene;
        private readonly double _dt;
        private readonly List<(double at, Action act, bool done)> _events = new();
        private readonly List<(Point from, Point to, double t0, double t1, Action<Point>? each)> _moves = new();
        private readonly List<(string text, double t0, double t1)> _captions = new();
        private readonly List<(Rect rect, double t0, double t1)> _snips = new();
        private readonly List<(Func<Border?> host, double t0, double t1, bool In)> _fades = new();
        private readonly List<(string text, double t0, double t1, Action<string> each, int shown)> _typings = new();
        private readonly List<double> _clicks = new();
        private (double t0, double t1)? _title;
        private Border? _snipMask;
        private Ellipse? _clickRing;
        private Grid? _titleCard;

        public Timeline(Scene scene, double dt) { _scene = scene; _dt = dt; }

        public void At(double t, Action act) => _events.Add((t, act, false));
        public void Move(Point from, Point to, double t0, double t1, Action<Point>? each = null) => _moves.Add((from, to, t0, t1, each));

        /// <summary>A move whose ends are only known when it starts, with progress 0..1 reported along the way.</summary>
        private readonly List<(Func<Point> from, Func<Point> to, double t0, double t1, Action<double>? each)> _lateMoves = new();
        public void MoveTo(Func<Point> from, Func<Point> to, double t0, double t1, Action<double>? each = null) => _lateMoves.Add((from, to, t0, t1, each));
        public void Caption(string text, double t0, double t1) => _captions.Add((text, t0, t1));
        public void Snip(Rect rect, double t0, double t1) => _snips.Add((rect, t0, t1));
        public void Click(double t) => _clicks.Add(t);
        public void Fade(Func<Border?> host, double t0, double t1, bool In) => _fades.Add((host, t0, t1, In));
        public void Type(string text, double t0, double t1, Action<string> each) => _typings.Add((text, t0, t1, each, 0));
        public void Title(double t0, double t1) => _title = (t0, t1);

        public void Step(double t)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                var (at, act, done) = _events[i];
                if (!done && t >= at) { _events[i] = (at, act, true); act(); }
            }
            foreach (var (from, to, t0, t1, each) in _moves)
            {
                if (t < t0 || t > t1 + _dt) continue;
                double k = Ease.InOutCubic(Math.Clamp((t - t0) / (t1 - t0), 0, 1));
                _scene.Pointer = new Point(from.X + (to.X - from.X) * k, from.Y + (to.Y - from.Y) * k);
                each?.Invoke(_scene.Pointer);
            }
            foreach (var (from, to, t0, t1, each) in _lateMoves)
            {
                if (t < t0 || t > t1 + _dt) continue;
                double k = Ease.InOutCubic(Math.Clamp((t - t0) / (t1 - t0), 0, 1));
                var a = from(); var b = to();
                _scene.Pointer = new Point(a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k);
                each?.Invoke(k);
            }
            foreach (var (host, t0, t1, In) in _fades)
            {
                if (t < t0 || t > t1 + _dt) continue;
                var h = host();
                if (h is null) continue;
                double k = Math.Clamp((t - t0) / (t1 - t0), 0, 1);
                Demo.Fade(h, In ? k : 1 - k);
            }
            for (int i = 0; i < _typings.Count; i++)
            {
                var (text, t0, t1, each, shown) = _typings[i];
                if (t < t0 || t > t1 + _dt) continue;
                int n = (int)Math.Round(text.Length * Math.Clamp((t - t0) / (t1 - t0), 0, 1));
                if (n != shown) { _typings[i] = (text, t0, t1, each, n); each(text[..n]); }
            }

            double opacity = 0; string? caption = null;
            foreach (var (s, t0, t1) in _captions)
            {
                if (t < t0 || t > t1) continue;
                caption = s;
                opacity = Math.Min(1, Math.Min((t - t0) / 0.3, (t1 - t) / 0.3));
            }
            if (caption is not null) _scene.CaptionText.Text = caption;
            _scene.Caption.Opacity = Math.Clamp(opacity, 0, 1);

            var snip = _snips.FirstOrDefault(s => t >= s.t0 && t <= s.t1 + 0.25);
            if (snip.rect.Width > 0)
            {
                _snipMask ??= MakeSnipMask();
                double k = Math.Clamp((t - snip.t0) / (snip.t1 - snip.t0), 0, 1);
                double fade = t > snip.t1 ? 1 - (t - snip.t1) / 0.25 : 1;
                var r = new Rect(snip.rect.X, snip.rect.Y, snip.rect.Width * Math.Max(0.02, k), snip.rect.Height * Math.Max(0.02, k));
                UpdateSnipMask(r, Math.Clamp(fade, 0, 1));
            }
            else if (_snipMask is not null) { _scene.Overlay.Children.Remove(_snipMask); _snipMask = null; }

            var click = _clicks.FirstOrDefault(c => t >= c && t <= c + 0.35);
            if (click > 0)
            {
                _clickRing ??= Ring();
                double k = (t - click) / 0.35;
                double d = 10 + 40 * k;
                _clickRing.Width = _clickRing.Height = d;
                _clickRing.Opacity = 1 - k;
                System.Windows.Controls.Canvas.SetLeft(_clickRing, _scene.Pointer.X - d / 2 + 2);
                System.Windows.Controls.Canvas.SetTop(_clickRing, _scene.Pointer.Y - d / 2 + 2);
            }
            else if (_clickRing is not null) { _scene.Overlay.Children.Remove(_clickRing); _clickRing = null; }

            if (_title is { } tt && t >= tt.t0)
            {
                _titleCard ??= TitleCard();
                _titleCard.Opacity = Math.Clamp((t - tt.t0) / 0.6, 0, 1);
                _scene.Cursor.Opacity = 1 - _titleCard.Opacity;
            }
        }

        private Border MakeSnipMask()
        {
            var mask = new Border { Width = W, Height = H, Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)) };
            _scene.Overlay.Children.Add(mask);
            Panel.SetZIndex(mask, 50);
            return mask;
        }

        private void UpdateSnipMask(Rect r, double opacity)
        {
            if (_snipMask is null) return;
            _snipMask.Opacity = opacity;
            _snipMask.Clip = Geometry.Combine(new RectangleGeometry(new Rect(0, 0, W, H)), new RectangleGeometry(r), GeometryCombineMode.Exclude, null);
            if (_snipMask.Tag is not Rectangle frame)
            {
                frame = new Rectangle { Stroke = Brushes.White, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 6, 4 } };
                _scene.Overlay.Children.Add(frame);
                Panel.SetZIndex(frame, 51);
                _snipMask.Tag = frame;
            }
            frame.Opacity = opacity;
            frame.Width = r.Width; frame.Height = r.Height;
            System.Windows.Controls.Canvas.SetLeft(frame, r.X); System.Windows.Controls.Canvas.SetTop(frame, r.Y);
            if (opacity <= 0.01) { _scene.Overlay.Children.Remove(frame); _snipMask.Tag = null; }
        }

        private Ellipse Ring()
        {
            var ring = new Ellipse { Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x66, 0xE5)), StrokeThickness = 3 };
            _scene.Overlay.Children.Add(ring);
            Panel.SetZIndex(ring, 90);
            return ring;
        }

        private Grid TitleCard()
        {
            var g = new Grid { Width = W, Height = H, Background = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF)) };
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = Demo.Title, FontFamily = Display, FontSize = 110, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)), HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new TextBlock { Text = "Screenshots, hung out to dry. For Windows 10 and 11.", FontFamily = Display, FontSize = 40, Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x73)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
            stack.Children.Add(new TextBlock { Text = Demo.Link, FontFamily = Text, FontSize = 30, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x66, 0xE5)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
            stack.Children.Add(new TextBlock { Text = "Free and open source", FontFamily = Text, FontSize = 24, Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) });
            g.Children.Add(stack);
            _scene.Overlay.Children.Add(g);
            Panel.SetZIndex(g, 200);
            return g;
        }
    }

    private sealed class Flight
    {
        private readonly Scene _scene;
        private readonly Grid _container = new();
        private readonly Border _glass, _edge;
        private readonly Image _photo;
        private readonly Grid _clip;
        private readonly RotateTransform _rotate = new();
        private readonly Rect _from, _to;
        private readonly double _tilt, _start;
        private readonly Action _landed;
        private bool _done;

        public Flight(Scene scene, BitmapSource image, Rect from, Rect to, double tilt, double start, Action landed)
        {
            _scene = scene; _from = from; _to = to; _tilt = tilt; _start = start; _landed = landed;
            _glass = new Border { Background = new SolidColorBrush(Theme.GlassFill), Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.24, BlurRadius = 20, ShadowDepth = 5, Direction = 270 } };
            _photo = new Image { Source = image, Stretch = Stretch.Fill };
            _edge = new Border { BorderThickness = new Thickness(0.75), BorderBrush = new LinearGradientBrush(Theme.GlassEdgeTop, Theme.GlassEdgeBottom, 90) };
            _clip = Pegs.Create(seed: 7);
            _clip.HorizontalAlignment = HorizontalAlignment.Left; _clip.VerticalAlignment = VerticalAlignment.Top;
            _container.Children.Add(_glass); _container.Children.Add(_photo); _container.Children.Add(_edge); _container.Children.Add(_clip);
            _container.RenderTransform = _rotate;
            scene.Overlay.Children.Add(_container);
            Panel.SetZIndex(_container, 60);
            Update(start);
        }

        public void Update(double t)
        {
            if (_done) return;
            double raw = Math.Min(1, (t - _start) / FlightWindow.Duration);
            double k = Ease.InOutCubic(raw);
            double chrome = Ease.Smooth(k, 0.35, 1);
            double L(double a, double b) => a + (b - a) * k;
            double w = L(_from.Width, _to.Width), h = L(_from.Height, _to.Height);
            double topX = L(_from.Left + _from.Width / 2, _to.Left + _to.Width / 2);
            double topY = L(_from.Top, _to.Top) - Math.Sin(Math.PI * k) * 30;
            double inset = Layout.FrameInset * k, radius = L(0, Layout.FrameRadius);
            _container.Width = w; _container.Height = h;
            System.Windows.Controls.Canvas.SetLeft(_container, topX - w / 2);
            System.Windows.Controls.Canvas.SetTop(_container, topY);
            _rotate.CenterX = w / 2; _rotate.CenterY = 0; _rotate.Angle = _tilt * k;
            _glass.CornerRadius = _edge.CornerRadius = new CornerRadius(radius);
            _glass.Opacity = _edge.Opacity = _clip.Opacity = chrome;
            _photo.Margin = new Thickness(inset);
            double pr = Math.Max(0, radius - inset);
            _photo.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, w - 2 * inset), Math.Max(0, h - 2 * inset)), pr, pr);
            _clip.Margin = new Thickness(w / 2 - _clip.Width / 2, -(Layout.PinHeight - 12), 0, 0);
            if (raw >= 1) { _done = true; _landed(); _scene.Overlay.Children.Remove(_container); }
        }
    }

    private sealed class Fall
    {
        private readonly Scene _scene;
        private readonly Grid _container = new();
        private readonly RotateTransform _rotate = new();
        private readonly Rect _card;
        private readonly double _tilt, _start;
        private bool _done;

        public Fall(Scene scene, BitmapSource image, Rect card, double tilt, double start)
        {
            _scene = scene; _card = card; _tilt = tilt; _start = start;
            var glass = new Border { Background = new SolidColorBrush(Theme.GlassFill), CornerRadius = new CornerRadius(Layout.FrameRadius), Effect = new DropShadowEffect { Color = Colors.Black, Opacity = 0.24, BlurRadius = 20, ShadowDepth = 5, Direction = 270 } };
            var photo = new Image { Source = image, Stretch = Stretch.Fill, Margin = new Thickness(Layout.FrameInset) };
            double pr = Layout.FrameRadius - Layout.FrameInset;
            photo.Clip = new RectangleGeometry(new Rect(0, 0, card.Width - 2 * Layout.FrameInset, card.Height - 2 * Layout.FrameInset), pr, pr);
            var clip = Pegs.Create(seed: 3);
            clip.HorizontalAlignment = HorizontalAlignment.Left; clip.VerticalAlignment = VerticalAlignment.Top;
            clip.Margin = new Thickness(card.Width / 2 - clip.Width / 2, -(Layout.PinHeight - 12), 0, 0);
            _container.Width = card.Width; _container.Height = card.Height;
            _container.Children.Add(glass); _container.Children.Add(photo); _container.Children.Add(clip);
            _container.RenderTransform = _rotate;
            scene.Overlay.Children.Add(_container);
            Panel.SetZIndex(_container, 60);
            Update(start);
        }

        public void Update(double t)
        {
            if (_done) return;
            double raw = Math.Min(1, (t - _start) / 0.55);
            double e = raw * raw * raw;
            System.Windows.Controls.Canvas.SetLeft(_container, _card.Left);
            System.Windows.Controls.Canvas.SetTop(_container, _card.Top + 520 * e);
            _rotate.CenterX = _card.Width / 2; _rotate.CenterY = 0;
            _rotate.Angle = _tilt + (_tilt * 7 + 20) * e;
            _container.Opacity = 1 - e;
            if (raw >= 1) { _done = true; _scene.Overlay.Children.Remove(_container); }
        }
    }
}
