// SupportMeshData.cs
// Carries generated support triangle buffers so rendering and export can consume the same procedural geometry.
using Pillar.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Geometry.Supports;

/// <summary>
/// Represents one generated triangle mesh for a procedural support.
/// </summary>
public sealed class SupportMeshData
{
    /// <summary>
    /// Creates one immutable mesh payload.
    /// </summary>
    public SupportMeshData(IReadOnlyList<Vector3> positions, IReadOnlyList<int> triangleIndices)
    {
        if (positions == null)
        {
            throw new ArgumentNullException(nameof(positions));
        }

        if (triangleIndices == null)
        {
            throw new ArgumentNullException(nameof(triangleIndices));
        }

        IndexedMeshValidator.Validate(positions, triangleIndices, allowEmpty: true);
        Positions = new ReadOnlyCollection<Vector3>(new List<Vector3>(positions));
        TriangleIndices = new ReadOnlyCollection<int>(new List<int>(triangleIndices));
    }

    /// <summary>
    /// Gets the mesh positions.
    /// </summary>
    public IReadOnlyList<Vector3> Positions { get; }

    /// <summary>
    /// Gets the triangle index buffer.
    /// </summary>
    public IReadOnlyList<int> TriangleIndices { get; }
}
