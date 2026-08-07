// MeshEntity.cs
// Defines imported mesh document data without coupling the model to Helix or WPF rendering objects.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using Pillar.Core.Geometry;

namespace Pillar.Core.Entities;

/// <summary>
/// Represents an imported triangle mesh as CAD document data without any rendering dependencies.
/// </summary>
public class MeshEntity : CadEntity
{
    private readonly Vector3 _localBoundsMin;
    private readonly Vector3 _localBoundsMax;
    private Transform3DData _importPlacementTransform;
    private Transform3DData _userTransform;

    public string? SourcePath { get; }
    public string OriginalFileName { get; }
    public IReadOnlyList<Vector3> Vertices { get; }
    public IReadOnlyList<int> TriangleIndices { get; }

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
    /// Creates an immutable mesh entity from imported position and triangle-index buffers.
    /// </summary>
    public MeshEntity(
        string name,
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices,
        string? sourcePath = null,
        Transform3DData? importPlacementTransform = null,
        Transform3DData? userTransform = null,
        string? originalFileName = null)
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

        IndexedMeshValidator.Validate(vertices, triangleIndices);

        SourcePath = sourcePath;
        OriginalFileName = CreateOriginalFileName(originalFileName, sourcePath, Name);
        Vertices = new ReadOnlyCollection<Vector3>(new List<Vector3>(vertices));
        TriangleIndices = new ReadOnlyCollection<int>(new List<int>(triangleIndices));
        (_localBoundsMin, _localBoundsMax) = CalculateLocalBounds(Vertices);
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
        string? sourcePath,
        string? originalFileName,
        Transform3DData? importPlacementTransform = null,
        Transform3DData? userTransform = null)
    {
        MeshEntity mesh = new MeshEntity(name, vertices, triangleIndices, sourcePath, importPlacementTransform, userTransform, originalFileName);
        mesh.Id = id;
        return mesh;
    }

    /// <summary>
    /// Gets the cached axis-aligned bounds for the immutable imported mesh vertices.
    /// </summary>
    public (Vector3 Min, Vector3 Max) GetLocalBounds()
    {
        return (_localBoundsMin, _localBoundsMax);
    }

    /// <summary>
    /// Calculates immutable local bounds once when the imported mesh is created.
    /// </summary>
    private static (Vector3 Min, Vector3 Max) CalculateLocalBounds(IReadOnlyList<Vector3> vertices)
    {
        Vector3 min = vertices[0];
        Vector3 max = vertices[0];

        for (int i = 1; i < vertices.Count; i++)
        {
            min = Vector3.Min(min, vertices[i]);
            max = Vector3.Max(max, vertices[i]);
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

    /// <summary>
    /// Preserves the original imported filename, using legacy source path data when older projects do not have the dedicated field.
    /// </summary>
    private static string CreateOriginalFileName(string? originalFileName, string? sourcePath, string displayName)
    {
        string? normalizedOriginalFileName = GetFileNameOrNull(originalFileName);

        if (!string.IsNullOrWhiteSpace(normalizedOriginalFileName))
        {
            return normalizedOriginalFileName;
        }

        string? sourceFileName = GetFileNameOrNull(sourcePath);

        if (!string.IsNullOrWhiteSpace(sourceFileName))
        {
            return sourceFileName;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return "Imported mesh";
    }

    /// <summary>
    /// Extracts a filename from a possibly legacy path while tolerating malformed persisted path values.
    /// </summary>
    private static string? GetFileNameOrNull(string? pathOrFileName)
    {
        if (string.IsNullOrWhiteSpace(pathOrFileName))
        {
            return null;
        }

        try
        {
            string fileName = System.IO.Path.GetFileName(pathOrFileName.Trim());
            return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
