// RingSupportPattern.cs
// Converts Ring Support tool spacing settings into stable support positions without depending on WPF or Helix rendering.
using Pillar.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Supports;

/// <summary>
/// Provides renderer-agnostic support distribution helpers for circular support patterns.
/// </summary>
public static class RingSupportPattern
{
    public const int MinimumSupportCount = 3;
    public const int MaximumSupportCount = 512;
    public const float DefaultSpacing = 5.0f;

    /// <summary>
    /// Converts a requested circumference spacing into an even count of supports around the circle.
    /// </summary>
    public static int CalculateSupportCount(Circle3D circle, float spacing)
    {
        float validatedSpacing = ValidateSpacing(spacing);
        int count = (int)MathF.Ceiling(circle.Circumference / validatedSpacing);

        if (count < MinimumSupportCount)
        {
            count = MinimumSupportCount;
        }

        if (count > MaximumSupportCount)
        {
            count = MaximumSupportCount;
        }

        return count;
    }

    /// <summary>
    /// Appends evenly spaced guide points around the supplied circle into a caller-owned buffer.
    /// </summary>
    public static void FillGuidePoints(Circle3D circle, float spacing, IList<Vector3> guidePoints)
    {
        if (guidePoints == null)
        {
            throw new ArgumentNullException(nameof(guidePoints));
        }

        guidePoints.Clear();
        int supportCount = CalculateSupportCount(circle, spacing);

        for (int i = 0; i < supportCount; i++)
        {
            float angle = (float)(i * Math.PI * 2.0 / supportCount);
            guidePoints.Add(circle.GetPoint(angle));
        }
    }

    /// <summary>
    /// Replaces invalid spacing with the current tool default before support count calculation.
    /// </summary>
    private static float ValidateSpacing(float spacing)
    {
        if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0.0f)
        {
            return DefaultSpacing;
        }

        return spacing;
    }
}
