using System.Windows;

namespace Snapline.UI;

/// <summary>Where everything goes, in device independent pixels.</summary>
public static class Layout
{
    public const double PanelHeight = 240;
    public const double RopeTop = 10;
    public const double Spacing = 174;
    public const double CardWidth = 150;
    /// <summary>How far above the rope the top of the clip sits.</summary>
    public const double PinAbove = 9.5;
    public const double PinHeight = 26;
    public const double PinWidth = 9;
    /// <summary>Distance from the top of the hanging view (the clip) to the card.</summary>
    public const double CardOffsetBelowTop = PinHeight - 12;
    public const double FrameRadius = 16;
    public const double FrameInset = 4;
    public const double PhotoMaxHeight = 104;

    /// <summary>The rope hangs as a parabola from edge to edge of the screen.</summary>
    public static double Sag(double width) => Math.Min(58, width * 0.036);

    public static double RopeY(double x, double width)
    {
        if (width <= 0) return RopeTop;
        double f = x / width;
        return RopeTop + 4 * Sag(width) * f * (1 - f);
    }

    public static double X(int index, int count, double width)
    {
        double total = Math.Max(count - 1, 0) * Spacing;
        return width / 2 - total / 2 + index * Spacing;
    }

    /// <summary>The photo fits inside the card area keeping its proportions.</summary>
    public static Size PhotoSize(double pixelWidth, double pixelHeight)
    {
        double maxW = CardWidth - 14, maxH = PhotoMaxHeight;
        if (pixelWidth <= 0 || pixelHeight <= 0) return new Size(maxW, maxH);
        double scale = Math.Min(maxW / pixelWidth, maxH / pixelHeight);
        return new Size(pixelWidth * scale, pixelHeight * scale);
    }

    /// <summary>The card around the photo: the photo plus the glass inset.</summary>
    public static Size CardSize(double pixelWidth, double pixelHeight)
    {
        var p = PhotoSize(pixelWidth, pixelHeight);
        return new Size(p.Width + FrameInset * 2, p.Height + FrameInset * 2);
    }
}
