// SupportRenderer.cs
// Converts procedural support mesh data into a Helix mesh visual without leaking Helix types into the domain layer.
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Geometry.Supports;

namespace Pillar.Rendering.EntityRenderers;

/// <summary>
/// Rendering-layer factory for procedural support visuals.
/// </summary>
public static class SupportRenderer
{
    /// <summary>
    /// Creates one renderable support visual from a support entity.
    /// </summary>
    public static GroupModel3D Create(SupportEntity support)
    {
        SupportMeshData meshData = SupportMeshBuilder.Build(support);
        MeshGeometry3D geometry = new MeshGeometry3D
        {
            Positions = new Vector3Collection(meshData.Positions),
            Indices = new IntCollection(meshData.TriangleIndices),
            Normals = new Vector3Collection(meshData.Normals)
        };

        MeshGeometryModel3D model = new MeshGeometryModel3D
        {
            Geometry = geometry,
            Material = MeshRenderer.CreateDefaultMaterial(),
            CullMode = SharpDX.Direct3D11.CullMode.Back
        };

        return new GroupModel3D
        {
            Children = { model }
        };
    }
}
