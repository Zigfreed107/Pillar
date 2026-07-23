// RaftMeshData.cs
// Carries renderer-neutral triangle buffers produced by raft generation.
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Geometry.Rafts;

/// <summary>
/// Represents one generated raft mesh.
/// </summary>
public sealed class RaftMeshData
{
    public RaftMeshData(IReadOnlyList<Vector3> positions, IReadOnlyList<int> triangleIndices)
    {
        Positions = new ReadOnlyCollection<Vector3>(new List<Vector3>(positions));
        TriangleIndices = new ReadOnlyCollection<int>(new List<int>(triangleIndices));
    }

    public IReadOnlyList<Vector3> Positions { get; }
    public IReadOnlyList<int> TriangleIndices { get; }
}
