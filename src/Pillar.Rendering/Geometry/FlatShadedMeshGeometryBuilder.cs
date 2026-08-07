// FlatShadedMeshGeometryBuilder.cs
// Expands renderer-independent indexed triangles into Helix buffers with one crisp normal per face.
using HelixToolkit;
using HelixToolkit.SharpDX;
using Pillar.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Rendering.Geometry;

/// <summary>
/// Creates flat-shaded render geometry without changing authoritative triangle ordering.
/// </summary>
public static class FlatShadedMeshGeometryBuilder
{
    private const float DegenerateNormalToleranceSquared = 0.00000001f;

    /// <summary>
    /// Expands every indexed triangle into three independent render positions and sequential render indices.
    /// </summary>
    public static MeshGeometry3D Create(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> triangleIndices)
    {
        if (positions == null)
        {
            throw new ArgumentNullException(nameof(positions));
        }

        if (triangleIndices == null)
        {
            throw new ArgumentNullException(nameof(triangleIndices));
        }

        if (triangleIndices.Count == 0)
        {
            return new MeshGeometry3D
            {
                Positions = new Vector3Collection(),
                Indices = new IntCollection(),
                Normals = new Vector3Collection()
            };
        }

        IndexedMeshValidator.Validate(positions, triangleIndices);

        int indexCount = triangleIndices.Count;
        Vector3Collection renderPositions = new Vector3Collection(indexCount);
        Vector3Collection renderNormals = new Vector3Collection(indexCount);
        IntCollection renderIndices = new IntCollection(indexCount);

        for (int indexPosition = 0; indexPosition < indexCount; indexPosition += 3)
        {
            Vector3 first = positions[triangleIndices[indexPosition]];
            Vector3 second = positions[triangleIndices[indexPosition + 1]];
            Vector3 third = positions[triangleIndices[indexPosition + 2]];
            Vector3 faceNormal = CalculateFaceNormal(first, second, third);
            int firstRenderIndex = renderPositions.Count;

            renderPositions.Add(first);
            renderPositions.Add(second);
            renderPositions.Add(third);
            renderNormals.Add(faceNormal);
            renderNormals.Add(faceNormal);
            renderNormals.Add(faceNormal);
            renderIndices.Add(firstRenderIndex);
            renderIndices.Add(firstRenderIndex + 1);
            renderIndices.Add(firstRenderIndex + 2);
        }

        return new MeshGeometry3D
        {
            Positions = renderPositions,
            Indices = renderIndices,
            Normals = renderNormals
        };
    }

    /// <summary>
    /// Calculates a winding-derived face normal with a stable fallback for preserved degenerate triangles.
    /// </summary>
    private static Vector3 CalculateFaceNormal(Vector3 first, Vector3 second, Vector3 third)
    {
        Vector3 faceNormal = Vector3.Cross(second - first, third - first);
        return faceNormal.LengthSquared() > DegenerateNormalToleranceSquared
            ? Vector3.Normalize(faceNormal)
            : Vector3.UnitZ;
    }
}
