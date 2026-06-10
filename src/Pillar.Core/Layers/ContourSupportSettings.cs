// ContourSupportSettings.cs
// Stores the editable parametric definition used to regenerate a Contour Support group.
using System;
using System.Numerics;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes the persistent settings used by the Contour Support tool to regenerate one support group.
/// </summary>
public sealed class ContourSupportSettings
{
    public const float DefaultSpacing = 5.0f;
    public const float DefaultCoplanarThresholdDegrees = 15.0f;
    public const float DefaultStartOffset = 0.0f;
    public const float DefaultFinalOffset = 0.0f;

    /// <summary>
    /// Creates validated Contour Support generator settings.
    /// </summary>
    public ContourSupportSettings(
        Vector3 seedPoint,
        int seedTriangleIndex,
        float zHeight,
        float coplanarThresholdDegrees,
        float spacing,
        float startOffset,
        float finalOffset)
    {
        ValidatePoint(seedPoint, nameof(seedPoint));

        if (seedTriangleIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seedTriangleIndex), "A Contour Support seed triangle must be non-negative.");
        }

        if (!float.IsFinite(zHeight))
        {
            throw new ArgumentOutOfRangeException(nameof(zHeight), "Contour Support Z height must be finite.");
        }

        SeedPoint = seedPoint;
        SeedTriangleIndex = seedTriangleIndex;
        ZHeight = zHeight;
        CoplanarThresholdDegrees = ValidateThreshold(coplanarThresholdDegrees);
        Spacing = ValidateSpacing(spacing);
        StartOffset = ValidateOffset(startOffset, nameof(startOffset));
        FinalOffset = ValidateOffset(finalOffset, nameof(finalOffset));
    }

    /// <summary>
    /// Gets the original model-surface click point used to choose the contour region and open-path start.
    /// </summary>
    public Vector3 SeedPoint { get; }

    /// <summary>
    /// Gets the triangle index, not the triangle-indices buffer offset, that seeds the connected face patch.
    /// </summary>
    public int SeedTriangleIndex { get; }

    /// <summary>
    /// Gets the horizontal contour plane height.
    /// </summary>
    public float ZHeight { get; }

    /// <summary>
    /// Gets the maximum allowed angle between adjacent face normals in degrees.
    /// </summary>
    public float CoplanarThresholdDegrees { get; }

    /// <summary>
    /// Gets the requested maximum distance between generated supports along the contour.
    /// </summary>
    public float Spacing { get; }

    /// <summary>
    /// Gets the distance from the open contour start before the first support is placed.
    /// </summary>
    public float StartOffset { get; }

    /// <summary>
    /// Gets the distance from the open contour end before the final support is placed.
    /// </summary>
    public float FinalOffset { get; }

    /// <summary>
    /// Creates a defensive copy for ownership boundaries and undo snapshots.
    /// </summary>
    public ContourSupportSettings Clone()
    {
        return new ContourSupportSettings(
            SeedPoint,
            SeedTriangleIndex,
            ZHeight,
            CoplanarThresholdDegrees,
            Spacing,
            StartOffset,
            FinalOffset);
    }

    /// <summary>
    /// Rejects invalid spacing before generator settings reach document state.
    /// </summary>
    private static float ValidateSpacing(float spacing)
    {
        if (!float.IsFinite(spacing) || spacing <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), "Contour Support spacing must be finite and positive.");
        }

        return spacing;
    }

    /// <summary>
    /// Rejects invalid coplanar threshold values before they reach document state.
    /// </summary>
    private static float ValidateThreshold(float threshold)
    {
        if (!float.IsFinite(threshold) || threshold < 0.0f || threshold > 180.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Contour Support coplanar threshold must be from 0 to 180 degrees.");
        }

        return threshold;
    }

    /// <summary>
    /// Rejects invalid open-contour offsets before they reach document state.
    /// </summary>
    private static float ValidateOffset(float offset, string parameterName)
    {
        if (!float.IsFinite(offset) || offset < 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Contour Support offsets must be finite and non-negative.");
        }

        return offset;
    }

    /// <summary>
    /// Rejects non-finite seed coordinates before they become saved generator metadata.
    /// </summary>
    private static void ValidatePoint(Vector3 point, string parameterName)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
        {
            throw new ArgumentException("Contour Support seed points must be finite.", parameterName);
        }
    }
}
