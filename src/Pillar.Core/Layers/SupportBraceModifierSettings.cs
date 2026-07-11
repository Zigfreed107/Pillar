// SupportBraceModifierSettings.cs
// Stores renderer-independent parameters for generated support bracing modifiers.
using Pillar.Core.Supports;
using System;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes saved parameters for a Brace support modifier.
/// </summary>
public sealed class SupportBraceModifierSettings
{
    public const float DefaultMinimumBraceAngleDegrees = 50.0f;
    public const float DefaultMaximumBraceAngleDegrees = 70.0f;
    public const float DefaultMaximumBraceLength = 10.0f;
    public const float DefaultBraceDiameter = SupportDefaults.DefaultStemTopDiameter;

    /// <summary>
    /// Creates one validated brace modifier parameter set.
    /// </summary>
    public SupportBraceModifierSettings(
        float minimumBraceAngleDegrees,
        float maximumBraceAngleDegrees,
        float maximumBraceLength,
        float braceDiameter)
    {
        MinimumBraceAngleDegrees = ValidateAngle(minimumBraceAngleDegrees, nameof(minimumBraceAngleDegrees));
        MaximumBraceAngleDegrees = ValidateAngle(maximumBraceAngleDegrees, nameof(maximumBraceAngleDegrees));

        if (MinimumBraceAngleDegrees > MaximumBraceAngleDegrees)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumBraceAngleDegrees), "Minimum Brace Angle cannot be greater than Maximum Brace Angle.");
        }

        MaximumBraceLength = ValidateNonNegativeFinite(maximumBraceLength, nameof(maximumBraceLength));
        BraceDiameter = ValidatePositiveFinite(braceDiameter, nameof(braceDiameter));
    }

    /// <summary>
    /// Gets the minimum allowed angle between a brace member and the XY plane.
    /// </summary>
    public float MinimumBraceAngleDegrees { get; }

    /// <summary>
    /// Gets the maximum allowed angle between a brace member and the XY plane.
    /// </summary>
    public float MaximumBraceAngleDegrees { get; }

    /// <summary>
    /// Gets the maximum generated brace member length.
    /// </summary>
    public float MaximumBraceLength { get; }

    /// <summary>
    /// Gets the generated brace member diameter.
    /// </summary>
    public float BraceDiameter { get; }

    /// <summary>
    /// Creates default settings for a new Brace tool session.
    /// </summary>
    public static SupportBraceModifierSettings CreateDefault()
    {
        return new SupportBraceModifierSettings(
            DefaultMinimumBraceAngleDegrees,
            DefaultMaximumBraceAngleDegrees,
            DefaultMaximumBraceLength,
            DefaultBraceDiameter);
    }

    /// <summary>
    /// Creates a defensive copy.
    /// </summary>
    public SupportBraceModifierSettings Clone()
    {
        return new SupportBraceModifierSettings(
            MinimumBraceAngleDegrees,
            MaximumBraceAngleDegrees,
            MaximumBraceLength,
            BraceDiameter);
    }

    /// <summary>
    /// Validates configured brace angles.
    /// </summary>
    private static float ValidateAngle(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 10.0f || value > 80.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Brace angles must be between 10 and 80 degrees.");
        }

        return value;
    }

    /// <summary>
    /// Validates finite non-negative dimensions.
    /// </summary>
    private static float ValidateNonNegativeFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Brace length must be finite and non-negative.");
        }

        return value;
    }

    /// <summary>
    /// Validates finite positive dimensions.
    /// </summary>
    private static float ValidatePositiveFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Brace diameter must be finite and positive.");
        }

        return value;
    }
}