namespace Snapline.UI;

/// <summary>
/// A damped spring, stepped every frame. The same model SwiftUI uses, so the
/// swing of a card and the drop of the line feel the way they do on the Mac.
/// </summary>
public sealed class Spring
{
    public double Value;
    public double Velocity;
    public double Target;
    public double Stiffness;
    public double Damping;
    public double RestThreshold = 0.01;

    private Spring(double stiffness, double damping) { Stiffness = stiffness; Damping = damping; }

    /// <summary>SwiftUI's interpolatingSpring(stiffness:damping:), with unit mass.</summary>
    public static Spring Raw(double stiffness, double damping, double value = 0) => new(stiffness, damping) { Value = value, Target = value };

    /// <summary>SwiftUI's spring(response:dampingFraction:).</summary>
    public static Spring FromResponse(double response, double dampingFraction, double value = 0)
    {
        double w = 2 * Math.PI / response;
        return new Spring(w * w, 2 * dampingFraction * w) { Value = value, Target = value };
    }

    public bool Resting => Math.Abs(Value - Target) < RestThreshold && Math.Abs(Velocity) < RestThreshold * 4;

    public void Snap(double value) { Value = Target = value; Velocity = 0; }

    public void Step(double dt)
    {
        if (Resting) { Value = Target; Velocity = 0; return; }
        const double h = 1.0 / 240;
        while (dt > 0)
        {
            double step = Math.Min(h, dt);
            double a = -Stiffness * (Value - Target) - Damping * Velocity;
            Velocity += a * step;
            Value += Velocity * step;
            dt -= step;
        }
    }
}

/// <summary>A value easing from where it is to a target over a fixed time.</summary>
public sealed class Tween
{
    public double Value;
    private double _from, _to, _duration, _elapsed = double.MaxValue;
    private Func<double, double> _ease = Ease.OutCubic;

    public Tween(double value) { Value = _from = _to = value; }

    public bool Done => _elapsed >= _duration;
    public double Target => _to;

    public void Snap(double value) { Value = _from = _to = value; _elapsed = double.MaxValue; _duration = 0; }

    public void Go(double to, double seconds, Func<double, double>? ease = null)
    {
        if (Math.Abs(to - Value) < 1e-6 && Done) { Snap(to); return; }
        _from = Value; _to = to; _duration = Math.Max(0.0001, seconds); _elapsed = 0;
        _ease = ease ?? Ease.OutCubic;
    }

    public void Step(double dt)
    {
        if (Done) { Value = _to; return; }
        _elapsed += dt;
        double k = Math.Clamp(_elapsed / _duration, 0, 1);
        Value = _from + (_to - _from) * _ease(k);
    }
}

public static class Ease
{
    public static double Linear(double x) => x;
    public static double OutCubic(double x) => 1 - Math.Pow(1 - x, 3);
    public static double InCubic(double x) => x * x * x;
    public static double InOutCubic(double x) => x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2;
    public static double InOutSine(double x) => -(Math.Cos(Math.PI * x) - 1) / 2;
    public static double OutQuad(double x) => 1 - (1 - x) * (1 - x);
    public static double InQuad(double x) => x * x;
    public static double Smooth(double x, double a, double b)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
