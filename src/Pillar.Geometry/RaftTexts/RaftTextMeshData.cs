// RaftTextMeshData.cs
// Carries renderer-neutral triangle buffers produced for local or placed raft text.
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Geometry.RaftTexts;

/// <summary>
/// Represents one immutable raft text mesh.
/// </summary>
public sealed class RaftTextMeshData
{
    /// <summary>
    /// Creates one triangle-buffer snapshot.
    /// </summary>
    public RaftTextMeshData(IReadOnlyList<Vector3> positions, IReadOnlyList<int> triangleIndices)
    {
        Positions = new ReadOnlyCollection<Vector3>(new List<Vector3>(positions));
        TriangleIndices = new ReadOnlyCollection<int>(new List<int>(triangleIndices));
    }

    public IReadOnlyList<Vector3> Positions { get; }
    public IReadOnlyList<int> TriangleIndices { get; }
}
