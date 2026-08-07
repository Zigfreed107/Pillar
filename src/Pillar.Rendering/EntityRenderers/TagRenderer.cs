// TagRenderer.cs
// Creates render-layer visuals and materials for durable and transient raft tags.
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Rendering.Geometry;
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
        return FlatShadedMeshGeometryBuilder.Create(vertices, triangleIndices);
    }
}
