// SupportProfile.cs
// Defines the editable geometric dimensions that describe one procedural resin-print support in the domain layer.
using System;

namespace Pillar.Core.Supports;

/// <summary>
/// Represents the editable dimensional profile for one procedural support.
/// </summary>
public sealed class SupportProfile
{
    /// <summary>
    /// Creates one validated support profile.
    /// </summary>
    public SupportProfile(
        float tipDiameter,
        float tipLength,
        float bodyDiameter,
        float baseDiameter,
        float baseHeight)
    {
        TipDiameter = ValidatePositiveDimension(tipDiameter, nameof(tipDiameter));
        TipLength = ValidatePositiveDimension(tipLength, nameof(tipLength));
        BodyDiameter = ValidatePositiveDimension(bodyDiameter, nameof(bodyDiameter));
        BaseDiameter = ValidatePositiveDimension(baseDiameter, nameof(baseDiameter));
        BaseHeight = ValidatePositiveDimension(baseHeight, nameof(baseHeight));
    }

    /// <summary>
    /// Gets the diameter at the contact tip.
    /// </summary>
    public float TipDiameter { get; }

    /// <summary>
    /// Gets the axial length of the tapered tip segment.
    /// </summary>
    public float TipLength { get; }

    /// <summary>
    /// Gets the diameter of the straight body segment.
    /// </summary>
    public float BodyDiameter { get; }

    /// <summary>
    /// Gets the diameter of the base contact segment on the build plate.
    /// </summary>
    public float BaseDiameter { get; }

    /// <summary>
    /// Gets the axial height of the base segment.
    /// </summary>
    public float BaseHeight { get; }

    /// <summary>
    /// Creates a defensive copy of this profile for immutable ownership boundaries.
    /// </summary>
    public SupportProfile Clone()
    {
        return new SupportProfile(TipDiameter, TipLength, BodyDiameter, BaseDiameter, BaseHeight);
    }

    /// <summary>
    /// Rejects zero, negative, NaN, and infinity dimensions before they reach geometry code.
    /// </summary>
    private static float ValidatePositiveDimension(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Support dimensions must be finite positive values.");
        }

        return value;
    }
}
