// FaceSelectionKey.cs
// Identifies one selected source mesh triangle without coupling face-selection workflows to rendering objects.
using System;

namespace Pillar.Core.Selection;

/// <summary>
/// Stores the stable in-document identity of one mesh face selected by a helper tool.
/// </summary>
public readonly struct FaceSelectionKey : IEquatable<FaceSelectionKey>
{
    /// <summary>
    /// Creates one face selection key for a mesh entity and triangle number.
    /// </summary>
    public FaceSelectionKey(Guid meshEntityId, int triangleIndex)
    {
        if (triangleIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(triangleIndex), "A selected triangle index must be non-negative.");
        }

        MeshEntityId = meshEntityId;
        TriangleIndex = triangleIndex;
    }

    /// <summary>
    /// Gets the document id of the mesh entity that owns the face.
    /// </summary>
    public Guid MeshEntityId { get; }

    /// <summary>
    /// Gets the zero-based triangle number within the mesh triangle buffer.
    /// </summary>
    public int TriangleIndex { get; }

    /// <summary>
    /// Compares two face keys by mesh id and triangle number.
    /// </summary>
    public bool Equals(FaceSelectionKey other)
    {
        return MeshEntityId.Equals(other.MeshEntityId) && TriangleIndex == other.TriangleIndex;
    }

    /// <summary>
    /// Compares this key to another object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is FaceSelectionKey other && Equals(other);
    }

    /// <summary>
    /// Combines the mesh id and triangle number for hash-based selection sets.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(MeshEntityId, TriangleIndex);
    }
}
