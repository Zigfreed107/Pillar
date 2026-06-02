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
        float baseBottomRadius,
        float baseHeight,
        float stemBottomDiameter,
        float stemTopDiameter,
        float headHeight,
        float headPenetrationDepth,
        float headTopDiameter,
        float maxHeadAngleFromVerticalDegrees)
    {
        BaseBottomRadius = ValidatePositiveDimension(baseBottomRadius, nameof(baseBottomRadius));
        BaseHeight = ValidatePositiveDimension(baseHeight, nameof(baseHeight));
        StemBottomDiameter = ValidatePositiveDimension(stemBottomDiameter, nameof(stemBottomDiameter));
        StemTopDiameter = ValidatePositiveDimension(stemTopDiameter, nameof(stemTopDiameter));
        HeadHeight = ValidatePositiveDimension(headHeight, nameof(headHeight));
        HeadPenetrationDepth = ValidatePositiveDimension(headPenetrationDepth, nameof(headPenetrationDepth));
        HeadTopDiameter = ValidatePositiveDimension(headTopDiameter, nameof(headTopDiameter));
        MaxHeadAngleFromVerticalDegrees = ValidateAngle(maxHeadAngleFromVerticalDegrees, nameof(maxHeadAngleFromVerticalDegrees));
    }

    /// <summary>
    /// Gets the radius of the base footprint where it touches the build plate.
    /// </summary>
    public float BaseBottomRadius { get; }

    /// <summary>
    /// Gets the axial height of the base section.
    /// </summary>
    public float BaseHeight { get; }

    /// <summary>
    /// Gets the stem diameter where the stem attaches to the base.
    /// </summary>
    public float StemBottomDiameter { get; }

    /// <summary>
    /// Gets the stem diameter where the stem attaches to the head.
    /// </summary>
    public float StemTopDiameter { get; }

    /// <summary>
    /// Gets the head diameter where the head attaches to the stem or directly to the base.
    /// </summary>
    public float HeadBottomDiameter
    {
        get { return StemTopDiameter; }
    }

    /// <summary>
    /// Gets the desired head height from the model intersection down to the stem or base.
    /// </summary>
    public float HeadHeight { get; }

    /// <summary>
    /// Gets how far the head penetrates into the model past the intersection point.
    /// </summary>
    public float HeadPenetrationDepth { get; }

    /// <summary>
    /// Gets the head diameter at the model intersection point.
    /// </summary>
    public float HeadTopDiameter { get; }

    /// <summary>
    /// Gets the maximum angle the head may lean away from vertical in degrees.
    /// </summary>
    public float MaxHeadAngleFromVerticalDegrees { get; }

    /// <summary>
    /// Creates a defensive copy of this profile for immutable ownership boundaries.
    /// </summary>
    public SupportProfile Clone()
    {
        return new SupportProfile(
            BaseBottomRadius,
            BaseHeight,
            StemBottomDiameter,
            StemTopDiameter,
            HeadHeight,
            HeadPenetrationDepth,
            HeadTopDiameter,
            MaxHeadAngleFromVerticalDegrees);
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

    /// <summary>
    /// Rejects invalid head angle limits before support placement uses them.
    /// </summary>
    private static float ValidateAngle(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0.0f || value > 90.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Support head angle must be between 0 and 90 degrees.");
        }

        return value;
    }
}
