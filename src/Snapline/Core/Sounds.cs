using System.IO;
using System.Media;

namespace Snapline.Core;

/// <summary>
/// Three small sounds, synthesised at launch so the app ships nothing but
/// itself: a glass tink when a photo hangs, a soft pop when one is taken
/// down, and a short whoosh when one goes to the Recycle Bin.
/// </summary>
public static class Sounds
{
    private const int Rate = 44100;
    private static readonly Lazy<SoundPlayer> Tink = new(() => Make(0.14, t =>
        (Math.Sin(2 * Math.PI * 2093 * t) * 0.55 + Math.Sin(2 * Math.PI * 4186 * t) * 0.25 + Math.Sin(2 * Math.PI * 6271 * t) * 0.10)
        * Math.Exp(-t * 34) * 0.30));

    private static readonly Lazy<SoundPlayer> Pop = new(() => Make(0.10, t =>
        Math.Sin(2 * Math.PI * (320 - 200 * Math.Min(1, t * 14)) * t) * Math.Exp(-t * 40) * 0.28));

    private static readonly Lazy<SoundPlayer> Whoosh = new(() =>
    {
        var rng = new Random(7);
        double last = 0;
        return Make(0.26, t =>
        {
            // Low-passed noise with a swell and a tail.
            double n = rng.NextDouble() * 2 - 1;
            last += (n - last) * 0.18;
            double env = Math.Sin(Math.PI * Math.Min(1, t / 0.26));
            return last * env * env * 0.5;
        });
    });

    public static bool Enabled { get; set; } = true;

    public static void PlayTink() => Play(Tink);
    public static void PlayPop() => Play(Pop);
    public static void PlayWhoosh() => Play(Whoosh);

    private static void Play(Lazy<SoundPlayer> player)
    {
        if (!Enabled) return;
        try { player.Value.Play(); } catch { }
    }

    private static SoundPlayer Make(double seconds, Func<double, double> wave)
    {
        int count = (int)(Rate * seconds);
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        int dataBytes = count * 2;
        w.Write("RIFF"u8); w.Write(36 + dataBytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataBytes);
        for (int i = 0; i < count; i++)
        {
            double t = i / (double)Rate;
            // A short fade at the end avoids a click.
            double tail = Math.Min(1, (seconds - t) / 0.01);
            double v = Math.Clamp(wave(t) * tail, -1, 1);
            w.Write((short)(v * short.MaxValue));
        }
        w.Flush();
        ms.Position = 0;
        var player = new SoundPlayer(ms);
        player.Load();
        return player;
    }
}
