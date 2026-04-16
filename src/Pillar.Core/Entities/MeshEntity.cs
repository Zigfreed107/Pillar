// MeshEntity.cs
// Defines imported mesh document data without coupling the model to Helix or WPF rendering objects.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Core.Entities;

/// <summary>
/// Represents an imported triangle mesh as CAD document data without any rendering dependencies.
/// </summary>
public class MeshEntity : CadEntity
{
    public string? SourcePath { get; }
    public IReadOnlyList<Vector3> Vertices { get; }
    public IReadOnlyList<int> TriangleIndices { get; }
    public IReadOnlyList<Vector3> Normals { get; }

    /// <summary>
    /// Creates an immutable mesh entity from imported vertex, index, and normal buffers.
    /// </summary>
    public MeshEntity(
        string name,
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices,
        IReadOnlyList<Vector3> normals,
        string? sourcePath = null)
        : base(string.IsNullOrWhiteSpace(name) ? "Imported mesh" : name)
    {
        if (vertices == null)
        {
            throw new ArgumentNullException(nameof(vertices));
        }

        if (triangleIndices == null)
        {
            throw new ArgumentNullException(nameof(triangleIndices));
        }

        if (normals == null)
        {
            throw new ArgumentNullException(nameof(normals));
        }

        if (vertices.Count == 0)
        {
            throw new ArgumentException("A mesh must contain at least one vertex.", nameof(vertices));
        }

        if (triangleIndices.Count == 0 || triangleIndices.Count % 3 != 0)
        {
            throw new ArgumentException("A mesh must contain triangle indices in groups of three.", nameof(triangleIndices));
        }

        if (normals.Count != 0 && normals.Count != vertices.Count)
        {
            throw new ArgumentException("Mesh normals must either be empty or match the vertex count.", nameof(normals));
        }

        for (int i = 0; i < triangleIndices.Count; i++)
        {
            int triangleIndex = triangleIndices[i];

            if (triangleIndex < 0 || triangleIndex >= vertices.Count)
            {
                throw new ArgumentException(
                    $"Triangle index at position {i} references vertex {triangleIndex}, but the mesh has {vertices.Count} vertices.",
                    nameof(triangleIndices));
            }
        }

        SourcePath = sourcePath;
        Vertices = new ReadOnlyCollection<Vector3>(new List<Vector3>(vertices));
        TriangleIndices = new ReadOnlyCollection<int>(new List<int>(triangleIndices));
        Normals = new ReadOnlyCollection<Vector3>(new List<Vector3>(normals));
    }

    /// <summary>
    /// Recreates a saved mesh while preserving the document identity and user-visible name.
    /// </summary>
    public static MeshEntity CreateLoaded(
        Guid id,
        string name,
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices,
        IReadOnlyList<Vector3> normals,
        string? sourcePath)
    {
        MeshEntity mesh = new MeshEntity(name, vertices, triangleIndices, normals, sourcePath);
        mesh.Id = id;
        return mesh;
    }

    /// <summary>
    /// Calculates the axis-aligned bounds for the imported mesh vertices.
    /// </summary>
    public override (Vector3 Min, Vector3 Max) GetBounds()
    {
        Vector3 min = Vertices[0];
        Vector3 max = Vertices[0];

        for (int i = 1; i < Vertices.Count; i++)
        {
            min = Vector3.Min(min, Vertices[i]);
            max = Vector3.Max(max, Vertices[i]);
        }

        return (min, max);
    }
}
