// SupportLayerColorGenerator.cs
// Centralizes default support-layer color generation so random and fallback colors are not scattered through the app.
using System;

namespace Pillar.Core.Layers;

/// <summary>
/// Creates support layer colors for new and legacy support groups.
/// </summary>
public static class SupportLayerColorGenerator
{
    private const double MinimumSaturation = 0.45d;
    private const double MaximumSaturation = 0.75d;
    private const double MinimumValue = 0.70d;
    private const double MaximumValue = 0.95d;

    /// <summary>
    /// Creates one random default color for a newly created support group.
    /// </summary>
    public static SupportLayerColor CreateRandom()
    {
        Random random = Random.Shared;
        double hue = random.NextDouble() * 360.0d;
        double saturation = Lerp(MinimumSaturation, MaximumSaturation, random.NextDouble());
        double value = Lerp(MinimumValue, MaximumValue, random.NextDouble());
        return CreateFromHsv(hue, saturation, value);
    }

    /// <summary>
    /// Creates a stable fallback color derived from one persisted identifier.
    /// </summary>
    public static SupportLayerColor CreateFromStableSeed(Guid seed)
    {
        byte[] bytes = seed.ToByteArray();
        uint accumulator = BitConverter.ToUInt32(bytes, 0) ^ BitConverter.ToUInt32(bytes, 4) ^ BitConverter.ToUInt32(bytes, 8) ^ BitConverter.ToUInt32(bytes, 12);
        double normalized = accumulator / (double)uint.MaxValue;
        double hue = normalized * 360.0d;
        double saturation = Lerp(MinimumSaturation, MaximumSaturation, 0.50d);
        double value = Lerp(MinimumValue, MaximumValue, 0.60d);
        return CreateFromHsv(hue, saturation, value);
    }

    /// <summary>
    /// Converts one HSV color into the RGB storage used by support layers.
    /// </summary>
    private static SupportLayerColor CreateFromHsv(double hue, double saturation, double value)
    {
        double normalizedHue = hue % 360.0d;

        if (normalizedHue < 0.0d)
        {
            normalizedHue += 360.0d;
        }

        double chroma = value * saturation;
        double secondary = chroma * (1.0d - Math.Abs(((normalizedHue / 60.0d) % 2.0d) - 1.0d));
        double match = value - chroma;

        double redPrime;
        double greenPrime;
        double bluePrime;

        if (normalizedHue < 60.0d)
        {
            redPrime = chroma;
            greenPrime = secondary;
            bluePrime = 0.0d;
        }
        else if (normalizedHue < 120.0d)
        {
            redPrime = secondary;
            greenPrime = chroma;
            bluePrime = 0.0d;
        }
        else if (normalizedHue < 180.0d)
        {
            redPrime = 0.0d;
            greenPrime = chroma;
            bluePrime = secondary;
        }
        else if (normalizedHue < 240.0d)
        {
            redPrime = 0.0d;
            greenPrime = secondary;
            bluePrime = chroma;
        }
        else if (normalizedHue < 300.0d)
        {
            redPrime = secondary;
            greenPrime = 0.0d;
            bluePrime = chroma;
        }
        else
        {
            redPrime = chroma;
            greenPrime = 0.0d;
            bluePrime = secondary;
        }

        return new SupportLayerColor(
            ToByte(redPrime + match),
            ToByte(greenPrime + match),
            ToByte(bluePrime + match));
    }

    /// <summary>
    /// Converts a normalized channel into an 8-bit RGB value.
    /// </summary>
    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value * 255.0d), 0, 255);
    }

    /// <summary>
    /// Interpolates between two scalar values.
    /// </summary>
    private static double Lerp(double minimum, double maximum, double amount)
    {
        return minimum + ((maximum - minimum) * amount);
    }
}
