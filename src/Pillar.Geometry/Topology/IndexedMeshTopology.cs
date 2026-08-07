// IndexedMeshTopology.cs
// Builds reusable triangle adjacency and edge-ownership diagnostics from authoritative position indices.
using Pillar.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Topology;

/// <summary>
/// Identifies one undirected edge by its ordered authoritative position indices.
/// </summary>
public readonly struct IndexedEdgeKey : IEquatable<IndexedEdgeKey>
{
    /// <summary>
    /// Creates a deterministic undirected edge key.
    /// </summary>
    public IndexedEdgeKey(int firstPositionIndex, int secondPositionIndex)
    {
        if (firstPositionIndex <= secondPositionIndex)
        {
            FirstPositionIndex = firstPositionIndex;
            SecondPositionIndex = secondPositionIndex;
        }
        else
        {
            FirstPositionIndex = secondPositionIndex;
            SecondPositionIndex = firstPositionIndex;
        }
    }

    public int FirstPositionIndex { get; }

    public int SecondPositionIndex { get; }

    /// <summary>
    /// Checks whether two keys identify the same undirected indexed edge.
    /// </summary>
    public bool Equals(IndexedEdgeKey other)
    {
        return FirstPositionIndex == other.FirstPositionIndex
            && SecondPositionIndex == other.SecondPositionIndex;
    }

    /// <summary>
    /// Checks whether an object identifies the same undirected indexed edge.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is IndexedEdgeKey other && Equals(other);
    }

    /// <summary>
    /// Gets the hash code used by edge-ownership dictionaries.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(FirstPositionIndex, SecondPositionIndex);
    }
}

/// <summary>
/// Provides immutable topology derived from one indexed triangle mesh.
/// </summary>
public sealed class IndexedMeshTopology
{
    private const float DegenerateAreaToleranceSquared = 0.000000000001f;
    private readonly IReadOnlyList<int>[] _triangleAdjacency;
    private readonly Dictionary<IndexedEdgeKey, int[]> _edgeOwners;

    /// <summary>
    /// Stores completed topology and its diagnostics.
    /// </summary>
    private IndexedMeshTopology(
        IReadOnlyList<int>[] triangleAdjacency,
        Dictionary<IndexedEdgeKey, int[]> edgeOwners,
        int openEdgeCount,
        int nonManifoldEdgeCount,
        int degenerateTriangleCount)
    {
        _triangleAdjacency = triangleAdjacency;
        _edgeOwners = edgeOwners;
        OpenEdgeCount = openEdgeCount;
        NonManifoldEdgeCount = nonManifoldEdgeCount;
        DegenerateTriangleCount = degenerateTriangleCount;
    }

    public int TriangleCount
    {
        get { return _triangleAdjacency.Length; }
    }

    public int EdgeCount
    {
        get { return _edgeOwners.Count; }
    }

    public int OpenEdgeCount { get; }

    public int NonManifoldEdgeCount { get; }

    public int DegenerateTriangleCount { get; }

    /// <summary>
    /// Builds topology once from validated authoritative buffers.
    /// </summary>
    public static IndexedMeshTopology Create(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> triangleIndices)
    {
        return CreateCore(positions, triangleIndices, null);
    }

    /// <summary>
    /// Builds topology for a selected triangle subset while retaining authoritative triangle ordinals.
    /// </summary>
    public static IndexedMeshTopology CreateForTriangles(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> triangleIndices,
        IReadOnlyList<int> includedTriangleIndices)
    {
        if (includedTriangleIndices == null)
        {
            throw new ArgumentNullException(nameof(includedTriangleIndices));
        }

        return CreateCore(positions, triangleIndices, includedTriangleIndices);
    }

    /// <summary>
    /// Builds full or subset topology after validating the shared authoritative buffers.
    /// </summary>
    private static IndexedMeshTopology CreateCore(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> triangleIndices,
        IReadOnlyList<int>? includedTriangleIndices)
    {
        IndexedMeshValidator.Validate(positions, triangleIndices, allowEmpty: true);

        int triangleCount = triangleIndices.Count / 3;
        List<int>?[] mutableAdjacency = new List<int>?[triangleCount];
        Dictionary<IndexedEdgeKey, List<int>> mutableEdgeOwners = new Dictionary<IndexedEdgeKey, List<int>>();
        int degenerateTriangleCount = 0;

        if (includedTriangleIndices == null)
        {
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                ProcessTriangle(
                    positions,
                    triangleIndices,
                    triangleIndex,
                    mutableEdgeOwners,
                    mutableAdjacency,
                    ref degenerateTriangleCount);
            }
        }
        else
        {
            HashSet<int> processedTriangleIndices = new HashSet<int>();

            for (int includedIndex = 0; includedIndex < includedTriangleIndices.Count; includedIndex++)
            {
                int triangleIndex = includedTriangleIndices[includedIndex];

                if (triangleIndex < 0 || triangleIndex >= triangleCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(includedTriangleIndices),
                        $"Triangle {triangleIndex} is outside the mesh triangle range.");
                }

                if (!processedTriangleIndices.Add(triangleIndex))
                {
                    continue;
                }

                ProcessTriangle(
                    positions,
                    triangleIndices,
                    triangleIndex,
                    mutableEdgeOwners,
                    mutableAdjacency,
                    ref degenerateTriangleCount);
            }
        }

        Dictionary<IndexedEdgeKey, int[]> edgeOwners = new Dictionary<IndexedEdgeKey, int[]>(mutableEdgeOwners.Count);
        int openEdgeCount = 0;
        int nonManifoldEdgeCount = 0;

        foreach (KeyValuePair<IndexedEdgeKey, List<int>> edgeOwnersEntry in mutableEdgeOwners)
        {
            int[] owners = edgeOwnersEntry.Value.ToArray();
            edgeOwners.Add(edgeOwnersEntry.Key, owners);

            if (owners.Length == 1)
            {
                openEdgeCount++;
            }
            else if (owners.Length > 2)
            {
                nonManifoldEdgeCount++;
            }
        }

        IReadOnlyList<int>[] adjacency = new IReadOnlyList<int>[triangleCount];

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            adjacency[triangleIndex] = mutableAdjacency[triangleIndex]?.ToArray() ?? Array.Empty<int>();
        }

        return new IndexedMeshTopology(
            adjacency,
            edgeOwners,
            openEdgeCount,
            nonManifoldEdgeCount,
            degenerateTriangleCount);
    }

    /// <summary>
    /// Adds one triangle to topology construction without changing its authoritative ordinal.
    /// </summary>
    private static void ProcessTriangle(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> triangleIndices,
        int triangleIndex,
        Dictionary<IndexedEdgeKey, List<int>> mutableEdgeOwners,
        List<int>?[] mutableAdjacency,
        ref int degenerateTriangleCount)
    {
        mutableAdjacency[triangleIndex] = new List<int>(3);
        int baseIndex = triangleIndex * 3;
        int firstPositionIndex = triangleIndices[baseIndex];
        int secondPositionIndex = triangleIndices[baseIndex + 1];
        int thirdPositionIndex = triangleIndices[baseIndex + 2];

        if (IsDegenerate(
            positions[firstPositionIndex],
            positions[secondPositionIndex],
            positions[thirdPositionIndex]))
        {
            degenerateTriangleCount++;
        }

        AddEdge(firstPositionIndex, secondPositionIndex, triangleIndex, mutableEdgeOwners, mutableAdjacency);
        AddEdge(secondPositionIndex, thirdPositionIndex, triangleIndex, mutableEdgeOwners, mutableAdjacency);
        AddEdge(thirdPositionIndex, firstPositionIndex, triangleIndex, mutableEdgeOwners, mutableAdjacency);
    }

    /// <summary>
    /// Gets every triangle that owns the requested indexed edge.
    /// </summary>
    public IReadOnlyList<int> GetEdgeOwners(int firstPositionIndex, int secondPositionIndex)
    {
        IndexedEdgeKey edgeKey = new IndexedEdgeKey(firstPositionIndex, secondPositionIndex);
        return _edgeOwners.TryGetValue(edgeKey, out int[]? owners)
            ? owners
            : Array.Empty<int>();
    }

    /// <summary>
    /// Gets triangles that share an indexed edge with one triangle.
    /// </summary>
    public IReadOnlyList<int> GetAdjacentTriangles(int triangleIndex)
    {
        if (triangleIndex < 0 || triangleIndex >= _triangleAdjacency.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(triangleIndex));
        }

        return _triangleAdjacency[triangleIndex];
    }

    /// <summary>
    /// Adds one real indexed edge and links its distinct triangle owners.
    /// </summary>
    private static void AddEdge(
        int firstPositionIndex,
        int secondPositionIndex,
        int triangleIndex,
        Dictionary<IndexedEdgeKey, List<int>> edgeOwners,
        List<int>?[] triangleAdjacency)
    {
        if (firstPositionIndex == secondPositionIndex)
        {
            return;
        }

        IndexedEdgeKey edgeKey = new IndexedEdgeKey(firstPositionIndex, secondPositionIndex);

        if (!edgeOwners.TryGetValue(edgeKey, out List<int>? ownerTriangleIndices))
        {
            edgeOwners.Add(edgeKey, new List<int>(2) { triangleIndex });
            return;
        }

        if (ownerTriangleIndices.Contains(triangleIndex))
        {
            return;
        }

        for (int ownerIndex = 0; ownerIndex < ownerTriangleIndices.Count; ownerIndex++)
        {
            int ownerTriangleIndex = ownerTriangleIndices[ownerIndex];
            AddNeighbor(triangleAdjacency[triangleIndex]!, ownerTriangleIndex);
            AddNeighbor(triangleAdjacency[ownerTriangleIndex]!, triangleIndex);
        }

        ownerTriangleIndices.Add(triangleIndex);
    }

    /// <summary>
    /// Adds one unique adjacency entry while keeping duplicate-face meshes deterministic.
    /// </summary>
    private static void AddNeighbor(List<int> neighbors, int triangleIndex)
    {
        if (!neighbors.Contains(triangleIndex))
        {
            neighbors.Add(triangleIndex);
        }
    }

    /// <summary>
    /// Detects preserved zero-area and near-zero-area triangles for diagnostics.
    /// </summary>
    private static bool IsDegenerate(Vector3 first, Vector3 second, Vector3 third)
    {
        Vector3 areaVector = Vector3.Cross(second - first, third - first);
        return areaVector.LengthSquared() <= DegenerateAreaToleranceSquared;
    }
}
