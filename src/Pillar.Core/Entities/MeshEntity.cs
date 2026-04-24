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
    private Transform3DData _importPlacementTransform;
    private Transform3DData _userTransform;

    public string? SourcePath { get; }
    public IReadOnlyList<Vector3> Vertices { get; }
    public IReadOnlyList<int> TriangleIndices { get; }
    public IReadOnlyList<Vector3> Normals { get; }

    /// <summary>
    /// Gets or sets the import-time placement transform that grounds raw geometry without editing vertices.
    /// </summary>
    public Transform3DData ImportPlacementTransform
    {
        get { return _importPlacementTransform; }
        set
        {
            if (_importPlacementTransform == value)
            {
                return;
            }

            _importPlacementTransform = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WorldTransform));
        }
    }

    /// <summary>
    /// Gets or sets the editable user transform applied after import placement.
    /// </summary>
    public Transform3DData UserTransform
    {
        get { return _userTransform; }
        set
        {
            if (_userTransform == value)
            {
                return;
            }

            _userTransform = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WorldTransform));
        }
    }

    /// <summary>
    /// Gets the composed world transform used consistently by rendering, framing, and selection logic.
    /// </summary>
    public Matrix4x4 WorldTransform
    {
        get { return ImportPlacementTransform.ToMatrix4x4() * UserTransform.ToMatrix4x4(); }
    }

    /// <summary>
    /// Creates an immutable mesh entity from imported vertex, index, and normal buffers.
    /// </summary>
    public MeshEntity(
        string name,
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices,
        IReadOnlyList<Vector3> normals,
        string? sourcePath = null,
        Transform3DData? importPlacementTransform = null,
        Transform3DData? userTransform = null)
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
        _importPlacementTransform = importPlacementTransform ?? Transform3DData.Identity;
        _userTransform = userTransform ?? Transform3DData.Identity;
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
        string? sourcePath,
        Transform3DData? importPlacementTransform = null,
        Transform3DData? userTransform = null)
    {
        MeshEntity mesh = new MeshEntity(name, vertices, triangleIndices, normals, sourcePath, importPlacementTransform, userTransform);
        mesh.Id = id;
        return mesh;
    }

    /// <summary>
    /// Calculates the axis-aligned bounds for the raw imported mesh vertices in local mesh space.
    /// </summary>
    public (Vector3 Min, Vector3 Max) GetLocalBounds()
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

    /// <summary>
    /// Calculates the axis-aligned world bounds by transforming the local mesh bounds corners.
    /// </summary>
    public override (Vector3 Min, Vector3 Max) GetBounds()
    {
        (Vector3 Min, Vector3 Max) localBounds = GetLocalBounds();
        Matrix4x4 worldTransform = WorldTransform;
        Vector3 min = Vector3.Transform(new Vector3(localBounds.Min.X, localBounds.Min.Y, localBounds.Min.Z), worldTransform);
        Vector3 max = min;

        ExpandWorldBounds(new Vector3(localBounds.Max.X, localBounds.Min.Y, localBounds.Min.Z), worldTransform, ref min, ref max);
        ExpandWorldBounds(new Vector3(localBounds.Min.X, localBounds.Max.Y, localBounds.Min.Z), worldTransform, ref min, ref max);
        ExpandWorldBounds(new Vector3(localBounds.Max.X, localBounds.Max.Y, localBounds.Min.Z), worldTransform, ref min, ref max);
        ExpandWorldBounds(new Vector3(localBounds.Min.X, localBounds.Min.Y, localBounds.Max.Z), worldTransform, ref min, ref max);
        ExpandWorldBounds(new Vector3(localBounds.Max.X, localBounds.Min.Y, localBounds.Max.Z), worldTransform, ref min, ref max);
        ExpandWorldBounds(new Vector3(localBounds.Min.X, localBounds.Max.Y, localBounds.Max.Z), worldTransform, ref min, ref max);
        ExpandWorldBounds(new Vector3(localBounds.Max.X, localBounds.Max.Y, localBounds.Max.Z), worldTransform, ref min, ref max);

        return (min, max);
    }

    /// <summary>
    /// Incorporates one transformed local-bounds corner into the world-space bounds accumulator.
    /// </summary>
    private static void ExpandWorldBounds(Vector3 localCorner, Matrix4x4 worldTransform, ref Vector3 min, ref Vector3 max)
    {
        Vector3 transformedCorner = Vector3.Transform(localCorner, worldTransform);
        min = Vector3.Min(min, transformedCorner);
        max = Vector3.Max(max, transformedCorner);
    }
}
