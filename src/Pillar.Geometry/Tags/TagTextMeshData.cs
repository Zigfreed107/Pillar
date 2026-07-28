// TagTextMeshData.cs
// Stores a reusable local-space solid text mesh and its measured line width.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Geometry.Tags;

/// <summary>
/// Represents extruded text centered at the origin before raft-edge placement.
/// </summary>
public sealed class TagTextMeshData
{
    /// <summary>
    /// Creates one immutable local-space text mesh.
    /// </summary>
    public TagTextMeshData(
        float measuredWidth,
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> triangleIndices)
    {
        if (!float.IsFinite(measuredWidth) || measuredWidth < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(measuredWidth));
        }

        MeasuredWidth = measuredWidth;
        Positions = new ReadOnlyCollection<Vector3>(
            new List<Vector3>(positions ?? throw new ArgumentNullException(nameof(positions))));
        TriangleIndices = new ReadOnlyCollection<int>(
            new List<int>(triangleIndices ?? throw new ArgumentNullException(nameof(triangleIndices))));

        if (Positions.Count == 0)
        {
            MinimumY = 0.0f;
            MaximumY = 0.0f;
        }
        else
        {
            float minimumY = Positions[0].Y;
            float maximumY = Positions[0].Y;

            for (int positionIndex = 1; positionIndex < Positions.Count; positionIndex++)
            {
                minimumY = MathF.Min(minimumY, Positions[positionIndex].Y);
                maximumY = MathF.Max(maximumY, Positions[positionIndex].Y);
            }

            MinimumY = minimumY;
            MaximumY = maximumY;
        }
    }

    public float MeasuredWidth { get; }
    public IReadOnlyList<Vector3> Positions { get; }
    public IReadOnlyList<int> TriangleIndices { get; }
    public float MinimumY { get; }
    public float MaximumY { get; }
}
