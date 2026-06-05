// LineSupportPattern.cs
// Converts Line Support tool spacing settings into stable guide positions without depending on WPF or Helix rendering.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Supports;

/// <summary>
/// Provides renderer-agnostic support distribution helpers for polyline support patterns.
/// </summary>
public static class LineSupportPattern
{
    public const int MaximumSupportCount = 512;
    public const float DefaultSpacing = 5.0f;
    private const float MinimumSegmentLength = 0.0001f;

    /// <summary>
    /// Appends clicked vertices and evenly spaced guide points along each segment into a caller-owned buffer.
    /// </summary>
    public static void FillGuidePoints(IReadOnlyList<Vector3> polylinePoints, float spacing, IList<Vector3> guidePoints)
    {
        if (polylinePoints == null)
        {
            throw new ArgumentNullException(nameof(polylinePoints));
        }

        if (guidePoints == null)
        {
            throw new ArgumentNullException(nameof(guidePoints));
        }

        guidePoints.Clear();

        if (polylinePoints.Count == 0)
        {
            return;
        }

        float validatedSpacing = ValidateSpacing(spacing);
        guidePoints.Add(polylinePoints[0]);

        for (int segmentIndex = 1; segmentIndex < polylinePoints.Count; segmentIndex++)
        {
            Vector3 start = polylinePoints[segmentIndex - 1];
            Vector3 end = polylinePoints[segmentIndex];
            float segmentLength = Vector3.Distance(start, end);

            if (segmentLength <= MinimumSegmentLength)
            {
                continue;
            }

            int intervalCount = (int)MathF.Ceiling(segmentLength / validatedSpacing);

            if (intervalCount < 1)
            {
                intervalCount = 1;
            }

            for (int intervalIndex = 1; intervalIndex <= intervalCount; intervalIndex++)
            {
                if (guidePoints.Count >= MaximumSupportCount)
                {
                    return;
                }

                float t = intervalIndex / (float)intervalCount;
                guidePoints.Add(Vector3.Lerp(start, end, t));
            }
        }
    }

    /// <summary>
    /// Replaces invalid spacing with the current tool default before support distribution.
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
