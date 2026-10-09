using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Controls;
using System.Windows.Interop;
using Snapline.Interop;
using Forms = System.Windows.Forms;

namespace Snapline.UI;

/// <summary>The icon in the notification area, and its menu.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon = new();
    private readonly MessageWindow _focusHelper;
    private Icon? _current;

    public event Action? LeftClick;
    public Func<ContextMenu>? MenuProvider { get; set; }

    public TrayIcon(MessageWindow focusHelper)
    {
        _focusHelper = focusHelper;
        _icon.Text = Strings.AppName;
        _icon.Visible = true;
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) LeftClick?.Invoke();
            else if (e.Button == Forms.MouseButtons.Right) ShowMenu();
        };
        Repaint();
        Theme.Changed += Repaint;
    }

    public void Repaint()
    {
        var old = _current;
        _current = LoadLogo() ?? Draw(Theme.SystemDark);
        _icon.Icon = _current;
        old?.Dispose();
    }

    /// <summary>The app icon, at the size the taskbar wants.</summary>
    private static Icon? LoadLogo()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Snapline.ico"));
            if (info is null) return null;
            using var stream = info.Stream;
            int size = (int)Math.Round(16 * (System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width ?? 1920) / 1920.0 * 1.0);
            return new Icon(stream, new Size(Math.Max(16, size), Math.Max(16, size)));
        }
        catch { return null; }
    }

    private void ShowMenu()
    {
        var menu = MenuProvider?.Invoke();
        if (menu is null) return;
        // Without a window of its own the menu would not close on a click
        // elsewhere. Giving a hidden window the foreground fixes that.
        Native.SetForegroundWindow(_focusHelper.Handle);
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    public void Balloon(string title, string text)
    {
        try { _icon.ShowBalloonTip(8000, title, text, Forms.ToolTipIcon.None); } catch { }
    }

    /// <summary>A rope with a photo hanging from it, white on a dark taskbar, black on a light one.</summary>
    private static Icon Draw(bool dark)
    {
        int size = 32;
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var ink = dark ? Color.White : Color.FromArgb(0x1C, 0x1C, 0x1C);
            using var rope = new Pen(ink, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var fill = new SolidBrush(ink);
            using var outline = new Pen(ink, 2.4f) { LineJoin = LineJoin.Round };
            // The line, sagging a little.
            var path = new GraphicsPath();
            path.AddBezier(new PointF(1, 7), new PointF(11, 10.5f), new PointF(21, 10.5f), new PointF(31, 7));
            g.DrawPath(rope, path);
            // The clip.
            g.FillRectangle(fill, new RectangleF(14, 5, 4, 8));
            // The photo, hanging slightly crooked.
            g.TranslateTransform(16, 12);
            g.RotateTransform(-4);
            var card = new RectangleF(-8.5f, 0, 17, 14);
            using var cardPath = Rounded(card, 3);
            g.DrawPath(outline, cardPath);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _current?.Dispose();
    }
}
