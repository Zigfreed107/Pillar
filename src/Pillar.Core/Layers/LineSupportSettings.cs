// LineSupportSettings.cs
// Stores the editable parametric definition used to regenerate a Line Support group.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes the persistent settings used by the Line Support tool to regenerate one support group.
/// </summary>
public sealed class LineSupportSettings
{
    private readonly List<Vector3> _points;

    /// <summary>
    /// Creates validated Line Support generator settings.
    /// </summary>
    public LineSupportSettings(IReadOnlyList<Vector3> points, float spacing)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (points.Count < 2)
        {
            throw new ArgumentException("Line Support settings require at least two polyline points.", nameof(points));
        }

        _points = new List<Vector3>(points.Count);

        for (int i = 0; i < points.Count; i++)
        {
            ValidatePoint(points[i], nameof(points));
            _points.Add(points[i]);
        }

        Spacing = ValidateSpacing(spacing);
    }

    /// <summary>
    /// Gets the selected model-surface points that define the Line Support polyline.
    /// </summary>
    public IReadOnlyList<Vector3> Points
    {
        get { return _points; }
    }

    /// <summary>
    /// Gets the requested maximum distance between generated supports along the line.
    /// </summary>
    public float Spacing { get; }

    /// <summary>
    /// Creates a defensive copy for ownership boundaries and undo snapshots.
    /// </summary>
    public LineSupportSettings Clone()
    {
        return new LineSupportSettings(_points, Spacing);
    }

    /// <summary>
    /// Rejects invalid spacing before generator settings reach document state.
    /// </summary>
    private static float ValidateSpacing(float spacing)
    {
        if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), "Line Support spacing must be finite and positive.");
        }

        return spacing;
    }

    /// <summary>
    /// Rejects non-finite polyline coordinates before they become saved generator metadata.
    /// </summary>
    private static void ValidatePoint(Vector3 point, string parameterName)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
        {
            throw new ArgumentException("Line Support points must be finite.", parameterName);
        }
    }
}
