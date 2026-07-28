// TagRenderer.cs
// Creates render-layer visuals and materials for durable and transient raft tags.
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Rendering.EntityRenderers;

/// <summary>
/// Converts renderer-neutral tag buffers into flat-shaded Helix meshes.
/// </summary>
public static class TagRenderer
{
    private const float AmbientColorScale = 0.25f;
    private static readonly Color4 TagSpecularColor = new Color4(0.28f, 0.28f, 0.28f, 1.0f);
    private const float TagSpecularShininess = 32.0f;

    /// <summary>
    /// Creates one selectable durable tag visual.
    /// </summary>
    public static GroupModel3D Create(TagEntity tag)
    {
        if (tag == null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        MeshGeometry3D geometry = CreateFlatShadedGeometry(tag.Vertices, tag.TriangleIndices);
        return MeshRenderer.CreateSelectableMeshGroup(geometry, CreateMaterial(tag.Color, 1.0f));
    }

    /// <summary>
    /// Creates a shaded tag material with an optional transient-preview opacity.
    /// </summary>
    public static PhongMaterial CreateMaterial(SupportLayerColor color, float opacity)
    {
        float normalizedOpacity = System.Math.Clamp(opacity, 0.0f, 1.0f);
        float red = color.Red / 255.0f;
        float green = color.Green / 255.0f;
        float blue = color.Blue / 255.0f;
        Color4 diffuseColor = new Color4(red, green, blue, normalizedOpacity);
        Color4 ambientColor = new Color4(
            red * AmbientColorScale,
            green * AmbientColorScale,
            blue * AmbientColorScale,
            normalizedOpacity);

        return new PhongMaterial
        {
            AmbientColor = ambientColor,
            DiffuseColor = diffuseColor,
            SpecularColor = new Color4(
                TagSpecularColor.Red,
                TagSpecularColor.Green,
                TagSpecularColor.Blue,
                normalizedOpacity),
            SpecularShininess = TagSpecularShininess
        };
    }

    /// <summary>
    /// Expands indexed triangles so each body face retains a crisp lighting normal.
    /// </summary>
    public static MeshGeometry3D CreateFlatShadedGeometry(
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices)
    {
        if (vertices == null)
        {
            throw new ArgumentNullException(nameof(vertices));
        }

        if (triangleIndices == null)
        {
            throw new ArgumentNullException(nameof(triangleIndices));
        }

        int indexCount = triangleIndices.Count;
        Vector3Collection positions = new Vector3Collection(indexCount);
        Vector3Collection normals = new Vector3Collection(indexCount);
        IntCollection indices = new IntCollection(indexCount);

        for (int i = 0; i + 2 < indexCount; i += 3)
        {
            Vector3 first = vertices[triangleIndices[i]];
            Vector3 second = vertices[triangleIndices[i + 1]];
            Vector3 third = vertices[triangleIndices[i + 2]];
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
