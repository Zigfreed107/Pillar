// RaftRenderer.cs
// Creates the render-layer visual for model-owned procedural raft entities.
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Numerics;

namespace Pillar.Rendering.EntityRenderers;

/// <summary>
/// Converts renderer-neutral raft buffers into a selectable Helix mesh.
/// </summary>
public static class RaftRenderer
{
    private const float AmbientColorScale = 0.25f;
    private static readonly Color4 RaftSpecularColor = new Color4(0.28f, 0.28f, 0.28f, 1.0f);
    private const float RaftSpecularShininess = 32.0f;

    /// <summary>
    /// Creates one selectable raft visual.
    /// </summary>
    public static GroupModel3D Create(RaftEntity raft)
    {
        if (raft == null)
        {
            throw new ArgumentNullException(nameof(raft));
        }

        MeshGeometry3D geometry = CreateFlatShadedGeometry(raft);
        return MeshRenderer.CreateSelectableMeshGroup(geometry, CreateMaterial(raft.Color));
    }

    /// <summary>
    /// Creates a shaded raft material from its renderer-neutral layer color.
    /// </summary>
    public static PhongMaterial CreateMaterial(SupportLayerColor color)
    {
        float red = color.Red / 255.0f;
        float green = color.Green / 255.0f;
        float blue = color.Blue / 255.0f;
        Color4 diffuseColor = new Color4(red, green, blue, 1.0f);
        Color4 ambientColor = new Color4(red * AmbientColorScale, green * AmbientColorScale, blue * AmbientColorScale, 1.0f);

        return new PhongMaterial
        {
            AmbientColor = ambientColor,
            DiffuseColor = diffuseColor,
            SpecularColor = RaftSpecularColor,
            SpecularShininess = RaftSpecularShininess
        };
    }

    /// <summary>
    /// Expands indexed raft triangles so each face has one crisp lighting normal instead of sharing smoothed normals across hard edges.
    /// </summary>
    private static MeshGeometry3D CreateFlatShadedGeometry(RaftEntity raft)
    {
        int indexCount = raft.TriangleIndices.Count;
        Vector3Collection positions = new Vector3Collection(indexCount);
        Vector3Collection normals = new Vector3Collection(indexCount);
        IntCollection indices = new IntCollection(indexCount);

        for (int i = 0; i + 2 < indexCount; i += 3)
        {
            Vector3 first = raft.Vertices[raft.TriangleIndices[i]];
            Vector3 second = raft.Vertices[raft.TriangleIndices[i + 1]];
            Vector3 third = raft.Vertices[raft.TriangleIndices[i + 2]];
            Vector3 normal = Vector3.Cross(second - first, third - first);

            if (normal.LengthSquared() > 0.00000001f)
            {
                normal = Vector3.Normalize(normal);
            }
            else
            {
                normal = Vector3.UnitZ;
            }

            int firstExpandedIndex = positions.Count;
            positions.Add(first);
            positions.Add(second);
            positions.Add(third);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            indices.Add(firstExpandedIndex);
            indices.Add(firstExpandedIndex + 1);
            indices.Add(firstExpandedIndex + 2);
        }

        return new MeshGeometry3D
        {
            Positions = positions,
            Indices = indices,
            Normals = normals
        };
    }
}