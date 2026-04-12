using CadApp.Core.Entities;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;

namespace CadApp.Rendering.EntityRenderers;

/// <summary>
/// Rendering-layer factory for imported triangle mesh visuals.
/// </summary>
public static class MeshRenderer
{
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

        return new GroupModel3D
        {
            Children = { model }
        };
    }

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

    public static PhongMaterial CreateDefaultMaterial()
    {
        return new PhongMaterial
        {
            DiffuseColor = new Color4(0.7f, 0.7f, 0.7f, 1.0f),
            SpecularColor = new Color4(0.18f, 0.18f, 0.18f, 1.0f),
            SpecularShininess = 24f
        };
    }
}
