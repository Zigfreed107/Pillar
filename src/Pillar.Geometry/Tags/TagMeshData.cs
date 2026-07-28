// TagMeshData.cs
// Carries renderer-neutral triangle buffers produced by complete raft-tag generation.
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Geometry.Tags;

/// <summary>
/// Represents one generated tag body and text mesh.
/// </summary>
public sealed class TagMeshData
{
    /// <summary>
    /// Creates one immutable mesh buffer snapshot.
    /// </summary>
    public TagMeshData(IReadOnlyList<Vector3> positions, IReadOnlyList<int> triangleIndices)
    {
        Positions = new ReadOnlyCollection<Vector3>(new List<Vector3>(positions));
        TriangleIndices = new ReadOnlyCollection<int>(new List<int>(triangleIndices));
    }

    public IReadOnlyList<Vector3> Positions { get; }
    public IReadOnlyList<int> TriangleIndices { get; }
}
