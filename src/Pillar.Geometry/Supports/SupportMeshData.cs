// SupportMeshData.cs
// Carries generated support triangle buffers so rendering and export can consume the same procedural geometry.
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
    public SupportMeshData(IReadOnlyList<Vector3> positions, IReadOnlyList<int> triangleIndices, IReadOnlyList<Vector3> normals)
    {
        if (positions == null)
        {
            throw new ArgumentNullException(nameof(positions));
        }

        if (triangleIndices == null)
        {
            throw new ArgumentNullException(nameof(triangleIndices));
        }

        if (normals == null)
        {
            throw new ArgumentNullException(nameof(normals));
        }

        Positions = new ReadOnlyCollection<Vector3>(new List<Vector3>(positions));
        TriangleIndices = new ReadOnlyCollection<int>(new List<int>(triangleIndices));
        Normals = new ReadOnlyCollection<Vector3>(new List<Vector3>(normals));
    }

    /// <summary>
    /// Gets the mesh positions.
    /// </summary>
    public IReadOnlyList<Vector3> Positions { get; }

    /// <summary>
    /// Gets the triangle index buffer.
    /// </summary>
    public IReadOnlyList<int> TriangleIndices { get; }

    /// <summary>
    /// Gets one normal per position.
    /// </summary>
    public IReadOnlyList<Vector3> Normals { get; }
}
