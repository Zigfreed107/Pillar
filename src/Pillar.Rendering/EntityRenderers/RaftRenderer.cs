// RaftRenderer.cs
// Creates the render-layer visual for model-owned procedural raft entities.
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Rendering.Geometry;
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

        MeshGeometry3D geometry = FlatShadedMeshGeometryBuilder.Create(raft.Vertices, raft.TriangleIndices);
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
}