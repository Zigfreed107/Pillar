// MeshRenderer.cs
// Creates and updates render-layer visuals for imported mesh entities, including the shared default mesh material definition.
using Pillar.Core.Entities;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Geometry.Analysis;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Rendering.EntityRenderers;

/// <summary>
/// Rendering-layer factory for imported triangle mesh visuals.
/// </summary>
public static class MeshRenderer
{
    private static readonly Color4 DefaultDiffuseColor = new Color4(0.7f, 0.7f, 0.7f, 1.0f);
    private static readonly Color4 DefaultSpecularColor = new Color4(0.18f, 0.18f, 0.18f, 1.0f);
    private const float DefaultSpecularShininess = 24f;
    private const int FaceHighlightDepthBias = -4;
    private const int SelectableMeshChildIndex = 0;
    private const int FaceHighlightChildIndex = 1;

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
        return GetSelectableMeshModel(visual);
    }

    /// <summary>
    /// Gets the base imported mesh child that owns selection and full-model post effects.
    /// </summary>
    public static MeshGeometryModel3D? GetSelectableMeshModel(GroupModel3D visual)
    {
        if (visual.Children.Count <= SelectableMeshChildIndex)
        {
            return null;
        }

        return visual.Children[SelectableMeshChildIndex] as MeshGeometryModel3D;
    }

    /// <summary>
    /// Gets the optional visual-only face-highlight overlay from a grouped mesh visual.
    /// </summary>
    public static MeshGeometryModel3D? GetFaceHighlightModel(GroupModel3D visual)
    {
        if (visual.Children.Count <= FaceHighlightChildIndex)
        {
            return null;
        }

        return visual.Children[FaceHighlightChildIndex] as MeshGeometryModel3D;
    }

    /// <summary>
    /// Creates or updates the visual overlay used to highlight faces near the horizontal build plate angle.
    /// </summary>
    public static void ApplyFaceAngleHighlight(
        GroupModel3D visual,
        MeshEntity mesh,
        bool isEnabled,
        double thresholdDegrees,
        Color4 highlightColor)
    {
        if (visual == null)
        {
            throw new System.ArgumentNullException(nameof(visual));
        }

        if (mesh == null)
        {
            throw new System.ArgumentNullException(nameof(mesh));
        }

        MeshGeometryModel3D? highlightModel = GetFaceHighlightModel(visual);

        if (!isEnabled)
        {
            if (highlightModel != null)
            {
                ClearFaceHighlightSelectionState(highlightModel);
                highlightModel.Visibility = System.Windows.Visibility.Collapsed;
            }

            return;
        }

        if (highlightModel == null)
        {
            highlightModel = CreateFaceHighlightModel(highlightColor);
            visual.Children.Add(highlightModel);
        }

        ClearFaceHighlightSelectionState(highlightModel);
        IReadOnlyList<int> matchingTriangleIndices = HorizontalFaceAngleAnalyzer.CreateMatchingTriangleIndices(
            mesh,
            thresholdDegrees);

        highlightModel.Material = CreateFaceHighlightMaterial(highlightColor);
        highlightModel.Geometry = new MeshGeometry3D
        {
            Positions = new Vector3Collection(mesh.Vertices),
            Indices = new IntCollection(matchingTriangleIndices),
            Normals = mesh.Normals.Count == mesh.Vertices.Count
                ? new Vector3Collection(mesh.Normals)
                : null
        };
        highlightModel.Visibility = matchingTriangleIndices.Count == 0
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;
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
            DiffuseColor = DefaultDiffuseColor,
            SpecularColor = DefaultSpecularColor,
            SpecularShininess = DefaultSpecularShininess
        };
    }

    /// <summary>
    /// Creates the transparent red material used by the horizontal-face overlay.
    /// </summary>
    private static PhongMaterial CreateFaceHighlightMaterial(Color4 highlightColor)
    {
        return new PhongMaterial
        {
            DiffuseColor = highlightColor,
            SpecularColor = new Color4(0.05f, 0.05f, 0.05f, highlightColor.Alpha),
            SpecularShininess = DefaultSpecularShininess
        };
    }

    /// <summary>
    /// Creates the secondary mesh model that is rendered over selected source triangles.
    /// </summary>
    private static MeshGeometryModel3D CreateFaceHighlightModel(Color4 highlightColor)
    {
        MeshGeometryModel3D highlightModel = new MeshGeometryModel3D
        {
            Material = CreateFaceHighlightMaterial(highlightColor),
            CullMode = SharpDX.Direct3D11.CullMode.Back,
            DepthBias = FaceHighlightDepthBias,
            IsHitTestVisible = false,
            IsTransparent = highlightColor.Alpha < 1.0f
        };

        ClearFaceHighlightSelectionState(highlightModel);
        return highlightModel;
    }

    /// <summary>
    /// Keeps the visual-only face overlay out of the selection outline post-effect path.
    /// </summary>
    private static void ClearFaceHighlightSelectionState(MeshGeometryModel3D highlightModel)
    {
        highlightModel.IsSelected = false;
        highlightModel.PostEffects = string.Empty;
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
