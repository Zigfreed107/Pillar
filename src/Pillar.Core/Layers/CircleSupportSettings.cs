// CircleSupportSettings.cs
// Stores the editable parametric definition used to regenerate a Circle Support group.
using System;
using System.Numerics;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes the persistent settings used by the Circle Support tool to regenerate one support group.
/// </summary>
public sealed class CircleSupportSettings
{
    /// <summary>
    /// Creates validated Circle Support generator settings.
    /// </summary>
    public CircleSupportSettings(Vector3 firstDiameterPoint, Vector3 secondDiameterPoint, float spacing)
    {
        FirstDiameterPoint = firstDiameterPoint;
        SecondDiameterPoint = secondDiameterPoint;
        Spacing = ValidateSpacing(spacing);
    }

    /// <summary>
    /// Gets the first picked model-surface point defining the circle diameter.
    /// </summary>
    public Vector3 FirstDiameterPoint { get; }

    /// <summary>
    /// Gets the second picked model-surface point defining the circle diameter.
    /// </summary>
    public Vector3 SecondDiameterPoint { get; }

    /// <summary>
    /// Gets the requested distance between supports around the circumference.
    /// </summary>
    public float Spacing { get; }

    /// <summary>
    /// Creates a defensive copy for ownership boundaries and undo snapshots.
    /// </summary>
    public CircleSupportSettings Clone()
    {
        return new CircleSupportSettings(FirstDiameterPoint, SecondDiameterPoint, Spacing);
    }

    /// <summary>
    /// Rejects invalid spacing before generator settings reach document state.
    /// </summary>
    private static float ValidateSpacing(float spacing)
    {
        if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), "Circle Support spacing must be finite and positive.");
        }

        return spacing;
    }
}
