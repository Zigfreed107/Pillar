// RingSupportSettings.cs
// Stores the editable parametric definition used to regenerate a Ring Support group.
using System;
using System.Numerics;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes the persistent settings used by the Ring Support tool to regenerate one support group.
/// </summary>
public sealed class RingSupportSettings
{
    /// <summary>
    /// Creates validated Ring Support generator settings.
    /// </summary>
    public RingSupportSettings(Vector3 firstPoint, Vector3 secondPoint, Vector3 thirdPoint, float spacing)
    {
        FirstPoint = firstPoint;
        SecondPoint = secondPoint;
        ThirdPoint = thirdPoint;
        Spacing = ValidateSpacing(spacing);
    }

    /// <summary>
    /// Gets the first picked circumference point, which also locks the Ring Support construction plane.
    /// </summary>
    public Vector3 FirstPoint { get; }

    /// <summary>
    /// Gets the second picked circumference point projected onto the locked construction plane.
    /// </summary>
    public Vector3 SecondPoint { get; }

    /// <summary>
    /// Gets the third picked circumference point projected onto the locked construction plane.
    /// </summary>
    public Vector3 ThirdPoint { get; }

    /// <summary>
    /// Gets the requested distance between supports around the circumference.
    /// </summary>
    public float Spacing { get; }

    /// <summary>
    /// Creates a defensive copy for ownership boundaries and undo snapshots.
    /// </summary>
    public RingSupportSettings Clone()
    {
        return new RingSupportSettings(FirstPoint, SecondPoint, ThirdPoint, Spacing);
    }

    /// <summary>
    /// Rejects invalid spacing before generator settings reach document state.
    /// </summary>
    private static float ValidateSpacing(float spacing)
    {
        if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), "Ring Support spacing must be finite and positive.");
        }

        return spacing;
    }
}
