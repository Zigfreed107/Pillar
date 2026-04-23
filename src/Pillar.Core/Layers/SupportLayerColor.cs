// SupportLayerColor.cs
// Defines the renderer-agnostic RGB color value used by support layer groups and inherited by their support entities.
using System;

namespace Pillar.Core.Layers;

/// <summary>
/// Represents one immutable support layer color using 8-bit RGB channels.
/// </summary>
public readonly struct SupportLayerColor : IEquatable<SupportLayerColor>
{
    /// <summary>
    /// Creates one support layer color from 8-bit RGB channels.
    /// </summary>
    public SupportLayerColor(byte red, byte green, byte blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    /// <summary>
    /// Gets the red channel.
    /// </summary>
    public byte Red { get; }

    /// <summary>
    /// Gets the green channel.
    /// </summary>
    public byte Green { get; }

    /// <summary>
    /// Gets the blue channel.
    /// </summary>
    public byte Blue { get; }

    /// <summary>
    /// Compares two support layer colors by their channel values.
    /// </summary>
    public bool Equals(SupportLayerColor other)
    {
        return Red == other.Red
            && Green == other.Green
            && Blue == other.Blue;
    }

    /// <summary>
    /// Compares this color with another object when boxed.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is SupportLayerColor other && Equals(other);
    }

    /// <summary>
    /// Gets the hash code for dictionary and set usage.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Red, Green, Blue);
    }

    /// <summary>
    /// Tests two support layer colors for channel equality.
    /// </summary>
    public static bool operator ==(SupportLayerColor left, SupportLayerColor right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Tests two support layer colors for channel inequality.
    /// </summary>
    public static bool operator !=(SupportLayerColor left, SupportLayerColor right)
    {
        return !left.Equals(right);
    }
}
