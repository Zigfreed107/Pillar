// RaftTextRenderer.cs
// Creates render-layer visuals and materials for durable and transient raft text.
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
/// Converts renderer-neutral raft text buffers into flat-shaded Helix meshes.
/// </summary>
public static class RaftTextRenderer
{
    /// <summary>
    /// Creates one selectable durable raft text visual.
    /// </summary>
    public static GroupModel3D Create(RaftTextEntity raftText)
    {
        if (raftText == null)
        {
            throw new ArgumentNullException(nameof(raftText));
        }

        MeshGeometry3D geometry = CreateFlatShadedGeometry(raftText.Vertices, raftText.TriangleIndices);
        return MeshRenderer.CreateSelectableMeshGroup(geometry, CreateMaterial(raftText.Color, 1.0f));
    }

    /// <summary>
    /// Creates the shared printable text material.
    /// </summary>
    public static PhongMaterial CreateMaterial(SupportLayerColor color, float opacity)
    {
        return TagRenderer.CreateMaterial(color, opacity);
    }

    /// <summary>
    /// Creates crisp per-face normals for extruded glyph caps and walls.
    /// </summary>
    public static MeshGeometry3D CreateFlatShadedGeometry(
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices)
    {
        return FlatShadedMeshGeometryBuilder.Create(vertices, triangleIndices);
    }
}
