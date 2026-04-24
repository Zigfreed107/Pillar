using Pillar.Core.Entities;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System.Numerics;

namespace Pillar.Rendering.EntityRenderers;

/// <summary>
/// Rendering-layer factory for imported triangle mesh visuals.
/// </summary>
public static class MeshRenderer
{
    /// <summary>
    /// Creates one mesh visual that keeps geometry in local space and applies placement through a visual transform.
    /// </summary>
    public static GroupModel3D Create(MeshEntity mesh)
    {
        MeshGeometry3D geometry = new MeshGeometry3D
        {
            Positions = new Vector3Collection(mesh.Vertices),
            Indices = new IntCollection(mesh.TriangleIndices),
            Normals = mesh.Normals.Count == mesh.Vertices.Count
                ? new Vector3Collection(mesh.Normals)
                : null
        };

        MeshGeometryModel3D model = new MeshGeometryModel3D
        {
            Geometry = geometry,
            Material = CreateDefaultMaterial(),
            CullMode = SharpDX.Direct3D11.CullMode.Back,
            RenderWireframe = false,
            WireframeColor = System.Windows.Media.Color.FromRgb(65, 245, 135)
        };

        GroupModel3D group = new GroupModel3D
        {
            Children = { model }
        };

        ApplyTransform(group, mesh);
        return group;
    }

    /// <summary>
    /// Gets the renderable mesh child from one grouped mesh visual.
    /// </summary>
    public static MeshGeometryModel3D? GetMeshModel(GroupModel3D visual)
    {
        foreach (Element3D child in visual.Children)
        {
            if (child is MeshGeometryModel3D meshModel)
            {
                return meshModel;
            }
        }

        return null;
    }

    /// <summary>
    /// Applies the mesh's composed world transform to the render visual.
    /// </summary>
    public static void ApplyTransform(GroupModel3D visual, MeshEntity mesh)
    {
        visual.Transform = new System.Windows.Media.Media3D.MatrixTransform3D(CreateMatrix3D(mesh.WorldTransform));
    }

    /// <summary>
    /// Creates the default material used by imported model meshes.
    /// </summary>
    public static PhongMaterial CreateDefaultMaterial()
    {
        return new PhongMaterial
        {
            DiffuseColor = new Color4(0.7f, 0.7f, 0.7f, 1.0f),
            SpecularColor = new Color4(0.18f, 0.18f, 0.18f, 1.0f),
            SpecularShininess = 24f
        };
    }

    /// <summary>
    /// Converts a numerics matrix into the WPF 3D matrix type used by Helix visuals.
    /// </summary>
    private static System.Windows.Media.Media3D.Matrix3D CreateMatrix3D(Matrix4x4 matrix)
    {
        return new System.Windows.Media.Media3D.Matrix3D(
            matrix.M11,
            matrix.M12,
            matrix.M13,
            matrix.M14,
            matrix.M21,
            matrix.M22,
            matrix.M23,
            matrix.M24,
            matrix.M31,
            matrix.M32,
            matrix.M33,
            matrix.M34,
            matrix.M41,
            matrix.M42,
            matrix.M43,
            matrix.M44);
    }
}
