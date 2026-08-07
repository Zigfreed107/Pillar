// IndexedMeshValidator.cs
// Enforces renderer-independent position and triangle-index invariants for authoritative mesh payloads.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Core.Geometry;

/// <summary>
/// Validates the structural invariants shared by imported models and generated geometry.
/// </summary>
public static class IndexedMeshValidator
{
    /// <summary>
    /// Validates finite positions, complete triangles, and in-range position indices.
    /// </summary>
    public static void Validate(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> triangleIndices,
        bool allowEmpty = false)
    {
        if (positions == null)
        {
            throw new ArgumentNullException(nameof(positions));
        }

        if (triangleIndices == null)
        {
            throw new ArgumentNullException(nameof(triangleIndices));
        }

        if (positions.Count == 0 || triangleIndices.Count == 0)
        {
            if (allowEmpty && positions.Count == 0 && triangleIndices.Count == 0)
            {
                return;
            }

            throw new ArgumentException("An indexed mesh must contain both positions and triangle indices.");
        }

        if (triangleIndices.Count % 3 != 0)
        {
            throw new ArgumentException("Triangle indices must be supplied in groups of three.", nameof(triangleIndices));
        }

        for (int positionIndex = 0; positionIndex < positions.Count; positionIndex++)
        {
            Vector3 position = positions[positionIndex];

            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                throw new ArgumentException($"Position {positionIndex} contains a non-finite component.", nameof(positions));
            }
        }

        for (int indexPosition = 0; indexPosition < triangleIndices.Count; indexPosition++)
        {
            int positionIndex = triangleIndices[indexPosition];

            if (positionIndex < 0 || positionIndex >= positions.Count)
            {
                throw new ArgumentException(
                    $"Triangle index at position {indexPosition} references position {positionIndex}, but the mesh has {positions.Count} positions.",
                    nameof(triangleIndices));
            }
        }
    }
}
