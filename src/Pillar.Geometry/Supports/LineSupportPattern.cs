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
        FillGuidePoints(polylinePoints, spacing, true, guidePoints);
    }

    /// <summary>
    /// Appends guide points for the requested Line Support bend behavior into a caller-owned buffer.
    /// </summary>
    public static void FillGuidePoints(
        IReadOnlyList<Vector3> polylinePoints,
        float spacing,
        bool placeSupportsAtBends,
        IList<Vector3> guidePoints)
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

        if (!placeSupportsAtBends)
        {
            FillContinuousGuidePoints(polylinePoints, validatedSpacing, guidePoints);
            return;
        }

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
    /// Distributes supports evenly along the whole polyline without forcing interior vertices to become supports.
    /// </summary>
    private static void FillContinuousGuidePoints(IReadOnlyList<Vector3> polylinePoints, float spacing, IList<Vector3> guidePoints)
    {
        float totalLength = CalculatePolylineLength(polylinePoints);

        if (totalLength <= MinimumSegmentLength)
        {
            guidePoints.Add(polylinePoints[0]);
            return;
        }

        int intervalCount = (int)MathF.Ceiling(totalLength / spacing);

        if (intervalCount < 1)
        {
            intervalCount = 1;
        }

        int supportCount = intervalCount + 1;

        if (supportCount > MaximumSupportCount)
        {
            supportCount = MaximumSupportCount;
        }

        for (int supportIndex = 0; supportIndex < supportCount; supportIndex++)
        {
            float distanceAlongPolyline = supportCount == 1
                ? 0.0f
                : totalLength * (supportIndex / (float)(supportCount - 1));

            guidePoints.Add(EvaluatePolylineAtDistance(polylinePoints, distanceAlongPolyline));
        }
    }

    /// <summary>
    /// Calculates the combined length of all non-degenerate polyline segments.
    /// </summary>
    private static float CalculatePolylineLength(IReadOnlyList<Vector3> polylinePoints)
    {
        float totalLength = 0.0f;

        for (int segmentIndex = 1; segmentIndex < polylinePoints.Count; segmentIndex++)
        {
            float segmentLength = Vector3.Distance(polylinePoints[segmentIndex - 1], polylinePoints[segmentIndex]);

            if (segmentLength > MinimumSegmentLength)
            {
                totalLength += segmentLength;
            }
        }

        return totalLength;
    }

    /// <summary>
    /// Evaluates a point at a measured distance along the non-degenerate parts of the polyline.
    /// </summary>
    private static Vector3 EvaluatePolylineAtDistance(IReadOnlyList<Vector3> polylinePoints, float distanceAlongPolyline)
    {
        float traversedLength = 0.0f;
        Vector3 lastValidEnd = polylinePoints[0];

        for (int segmentIndex = 1; segmentIndex < polylinePoints.Count; segmentIndex++)
        {
            Vector3 start = polylinePoints[segmentIndex - 1];
            Vector3 end = polylinePoints[segmentIndex];
            float segmentLength = Vector3.Distance(start, end);

            if (segmentLength <= MinimumSegmentLength)
            {
                continue;
            }

            lastValidEnd = end;

            if (distanceAlongPolyline <= traversedLength + segmentLength)
            {
                float segmentDistance = distanceAlongPolyline - traversedLength;
                float t = Math.Clamp(segmentDistance / segmentLength, 0.0f, 1.0f);
                return Vector3.Lerp(start, end, t);
            }

            traversedLength += segmentLength;
        }

        return lastValidEnd;
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
