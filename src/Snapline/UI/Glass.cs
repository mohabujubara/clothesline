using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Snapline.UI;

/// <summary>The crisp glass frame and the aluminium clothespin, drawn in code.</summary>
public static class Glass
{
    /// <summary>A translucent sheet with a thin specular edge, lit from above.</summary>
    public static Border Frame(double radius)
    {
        var b = new Border
        {
            CornerRadius = new CornerRadius(radius),
            BorderThickness = new Thickness(0.75),
            SnapsToDevicePixels = false,
        };
        Paint(b);
        return b;
    }

    public static void Paint(Border b)
    {
        b.Background = Theme.Freeze(new SolidColorBrush(Theme.GlassFill));
        b.BorderBrush = Theme.Freeze(new LinearGradientBrush(Theme.GlassEdgeTop, Theme.GlassEdgeBottom, 90));
    }

    /// <summary>The faint dark outline just outside the specular edge.</summary>
    public static Border Outline(double radius)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(radius + 0.5),
            BorderThickness = new Thickness(0.5),
            BorderBrush = Theme.Freeze(new SolidColorBrush(Theme.GlassOutline)),
            Margin = new Thickness(-0.5),
            IsHitTestVisible = false,
        };
    }

    public static Brush MetalBrush()
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        g.GradientStops.Add(new GradientStop(Theme.Gray(0.70), 0));
        g.GradientStops.Add(new GradientStop(Theme.Gray(0.93), 0.35));
        g.GradientStops.Add(new GradientStop(Theme.Gray(0.82), 0.65));
        g.GradientStops.Add(new GradientStop(Theme.Gray(0.62), 1));
        g.Freeze();
        return g;
    }

    /// <summary>A warmer metal for a clip that keeps its photo on the line.</summary>
    public static Brush BrassBrush()
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        g.GradientStops.Add(new GradientStop(Theme.Rgba(0.72, 0.56, 0.22, 1), 0));
        g.GradientStops.Add(new GradientStop(Theme.Rgba(0.98, 0.88, 0.55, 1), 0.35));
        g.GradientStops.Add(new GradientStop(Theme.Rgba(0.88, 0.74, 0.38, 1), 0.65));
        g.GradientStops.Add(new GradientStop(Theme.Rgba(0.62, 0.46, 0.16, 1), 1));
        g.Freeze();
        return g;
    }

    /// <summary>A minimal aluminium clip: a brushed metal pill with a slot where it grips the line.</summary>
    public static Grid Clothespin()
    {
        var pin = new Grid { Width = Layout.PinWidth, Height = Layout.PinHeight, IsHitTestVisible = false };
        var body = new Border
        {
            CornerRadius = new CornerRadius(3.5),
            Background = MetalBrush(),
            BorderThickness = new Thickness(0.6),
            BorderBrush = Theme.Freeze(new LinearGradientBrush(Theme.Gray(1, 0.9), Theme.Gray(0, 0.18), 90)),
            Effect = new DropShadowEffect { BlurRadius = 5, ShadowDepth = 1.5, Direction = 270, Opacity = 0.30, Color = Colors.Black },
        };
        var slot = new Rectangle
        {
            Width = 5, Height = 1.4, RadiusX = 0.7, RadiusY = 0.7,
            Fill = Theme.Freeze(new SolidColorBrush(Theme.Gray(0, 0.32))),
            VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8.5, 0, 0),
        };
        pin.Children.Add(body);
        pin.Children.Add(slot);
        return pin;
    }

    /// <summary>The icon paths used on cards: a cross and a check mark.</summary>
    public static Path Cross(double size, Brush stroke)
    {
        return new Path
        {
            Data = Geometry.Parse("M0,0 L8,8 M8,0 L0,8"),
            Stroke = stroke, StrokeThickness = 1.8,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            Width = size, Height = size, Stretch = Stretch.Uniform,
            IsHitTestVisible = false,
        };
    }

    public static Path Check(double size, Brush stroke)
    {
        return new Path
        {
            Data = Geometry.Parse("M0,5 L3.6,8.6 L10,1"),
            Stroke = stroke, StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            Width = size, Height = size * 0.96, Stretch = Stretch.Uniform,
            IsHitTestVisible = false,
        };
    }
}
