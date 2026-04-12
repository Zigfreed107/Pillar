using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace CadApp.Core.Entities;

/// <summary>
/// Represents an imported triangle mesh as CAD document data without any rendering dependencies.
/// </summary>
public class MeshEntity : CadEntity
{
    public string Name { get; }
    public string? SourcePath { get; }
    public IReadOnlyList<Vector3> Vertices { get; }
    public IReadOnlyList<int> TriangleIndices { get; }
    public IReadOnlyList<Vector3> Normals { get; }

    public MeshEntity(
        string name,
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices,
        IReadOnlyList<Vector3> normals,
        string? sourcePath = null)
    {
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

        Name = string.IsNullOrWhiteSpace(name) ? "Imported mesh" : name;
        SourcePath = sourcePath;
        Vertices = new ReadOnlyCollection<Vector3>(new List<Vector3>(vertices));
        TriangleIndices = new ReadOnlyCollection<int>(new List<int>(triangleIndices));
        Normals = new ReadOnlyCollection<Vector3>(new List<Vector3>(normals));
    }

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
