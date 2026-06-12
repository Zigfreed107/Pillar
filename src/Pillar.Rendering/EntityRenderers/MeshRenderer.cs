// MeshRenderer.cs
// Creates and updates render-layer visuals for imported mesh entities, including the shared default mesh material definition.
using Pillar.Core.Entities;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Geometry.Analysis;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;

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
    private const int StandardMeshChildIndex = 0;
    private const int ClippedMeshChildIndex = 1;
    private const int FaceHighlightChildIndex = 2;
    private const int FaceSelectionChildIndex = 3;

    /// <summary>
    /// Creates one mesh visual that keeps geometry in local space and applies placement through a visual transform.
    /// </summary>
    public static GroupModel3D Create(MeshEntity mesh)
    {
        return Create(mesh, CreateDefaultMaterial());
    }

    /// <summary>
    /// Creates one mesh visual with the supplied material so application-level appearance settings stay outside domain entities.
    /// </summary>
    public static GroupModel3D Create(MeshEntity mesh, PhongMaterial material)
    {
        MeshGeometry3D geometry = new MeshGeometry3D
        {
            Positions = new Vector3Collection(mesh.Vertices),
            Indices = new IntCollection(mesh.TriangleIndices),
            Normals = mesh.Normals.Count == mesh.Vertices.Count
                ? new Vector3Collection(mesh.Normals)
                : null
        };

        GroupModel3D group = CreateSelectableMeshGroup(geometry, material);

        ApplyTransform(group, mesh);
        return group;
    }

    /// <summary>
    /// Creates one grouped mesh with a normal SSAO-capable child and a cross-section child for active clipping.
    /// </summary>
    public static GroupModel3D CreateSelectableMeshGroup(MeshGeometry3D geometry, PhongMaterial material)
    {
        if (geometry == null)
        {
            throw new ArgumentNullException(nameof(geometry));
        }

        if (material == null)
        {
            throw new ArgumentNullException(nameof(material));
        }

        MeshGeometryModel3D standardModel = new MeshGeometryModel3D
        {
            Geometry = geometry,
            Material = material,
            CullMode = SharpDX.Direct3D11.CullMode.Back,
            RenderWireframe = false,
            WireframeColor = System.Windows.Media.Color.FromRgb(65, 245, 135)
        };

        CrossSectionMeshGeometryModel3D clippedModel = new CrossSectionMeshGeometryModel3D
        {
            Geometry = geometry,
            Material = material,
            CullMode = SharpDX.Direct3D11.CullMode.Back,
            RenderWireframe = false,
            WireframeColor = System.Windows.Media.Color.FromRgb(65, 245, 135),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        GroupModel3D group = new GroupModel3D
        {
            Children = { standardModel, clippedModel }
        };

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
    /// Applies the visible Z interval and switches between the SSAO-capable full mesh and clipping mesh.
    /// </summary>
    public static void ApplyClipRange(GroupModel3D visual, float lowerZ, float upperZ, bool isClippingActive)
    {
        if (visual == null)
        {
            throw new ArgumentNullException(nameof(visual));
        }

        float normalizedLowerZ = global::System.Math.Min(lowerZ, upperZ);
        float normalizedUpperZ = global::System.Math.Max(lowerZ, upperZ);
        MeshGeometryModel3D? standardModel = GetStandardMeshModel(visual);
        CrossSectionMeshGeometryModel3D? clippedModel = GetClippedMeshModel(visual);

        SetSelectableMeshVisibility(standardModel, !isClippingActive);
        SetSelectableMeshVisibility(clippedModel, isClippingActive);

        for (int i = 0; i < visual.Children.Count; i++)
        {
            if (visual.Children[i] is CrossSectionMeshGeometryModel3D crossSectionMesh)
            {
                ApplyClipRange(crossSectionMesh, normalizedLowerZ, normalizedUpperZ, isClippingActive);
            }
        }
    }

    /// <summary>
    /// Gets the base imported mesh child that owns selection and full-model post effects.
    /// </summary>
    public static MeshGeometryModel3D? GetSelectableMeshModel(GroupModel3D visual)
    {
        CrossSectionMeshGeometryModel3D? clippedModel = GetClippedMeshModel(visual);

        if (clippedModel != null && clippedModel.Visibility == Visibility.Visible)
        {
            return clippedModel;
        }

        return GetStandardMeshModel(visual);
    }

    /// <summary>
    /// Applies one operation to every selectable mesh child so hidden and visible render paths stay in sync.
    /// </summary>
    public static void ApplyToSelectableMeshModels(GroupModel3D visual, Action<MeshGeometryModel3D> action)
    {
        if (visual == null)
        {
            throw new ArgumentNullException(nameof(visual));
        }

        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        MeshGeometryModel3D? standardModel = GetStandardMeshModel(visual);

        if (standardModel != null)
        {
            action(standardModel);
        }

        CrossSectionMeshGeometryModel3D? clippedModel = GetClippedMeshModel(visual);

        if (clippedModel != null)
        {
            action(clippedModel);
        }
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
    /// Gets the optional visual-only selected-face overlay from a grouped mesh visual.
    /// </summary>
    public static MeshGeometryModel3D? GetFaceSelectionModel(GroupModel3D visual)
    {
        if (visual.Children.Count <= FaceSelectionChildIndex)
        {
            return null;
        }

        return visual.Children[FaceSelectionChildIndex] as MeshGeometryModel3D;
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
            throw new ArgumentNullException(nameof(visual));
        }

        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
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
    /// Creates or updates the visual overlay used to highlight the temporary face selection set.
    /// </summary>
    public static void ApplyFaceSelection(
        GroupModel3D visual,
        MeshEntity mesh,
        IReadOnlyCollection<int> selectedTriangleIndices,
        Color4 selectionColor)
    {
        if (visual == null)
        {
            throw new ArgumentNullException(nameof(visual));
        }

        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (selectedTriangleIndices == null)
        {
            throw new ArgumentNullException(nameof(selectedTriangleIndices));
        }

        MeshGeometryModel3D? selectionModel = GetFaceSelectionModel(visual);

        if (selectedTriangleIndices.Count == 0)
        {
            if (selectionModel != null)
            {
                ClearFaceHighlightSelectionState(selectionModel);
                selectionModel.Visibility = System.Windows.Visibility.Collapsed;
            }

            return;
        }

        if (selectionModel == null)
        {
            EnsureFaceHighlightSlot(visual, selectionColor);
            selectionModel = CreateFaceHighlightModel(selectionColor);
            visual.Children.Add(selectionModel);
        }

        List<int> selectedMeshIndices = new List<int>(selectedTriangleIndices.Count * 3);
        int triangleCount = mesh.TriangleIndices.Count / 3;

        foreach (int triangleIndex in selectedTriangleIndices)
        {
            if (triangleIndex < 0 || triangleIndex >= triangleCount)
            {
                continue;
            }

            int baseIndex = triangleIndex * 3;
            selectedMeshIndices.Add(mesh.TriangleIndices[baseIndex]);
            selectedMeshIndices.Add(mesh.TriangleIndices[baseIndex + 1]);
            selectedMeshIndices.Add(mesh.TriangleIndices[baseIndex + 2]);
        }

        ClearFaceHighlightSelectionState(selectionModel);
        selectionModel.Material = CreateFaceHighlightMaterial(selectionColor);
        selectionModel.Geometry = new MeshGeometry3D
        {
            Positions = new Vector3Collection(mesh.Vertices),
            Indices = new IntCollection(selectedMeshIndices),
            Normals = mesh.Normals.Count == mesh.Vertices.Count
                ? new Vector3Collection(mesh.Normals)
                : null
        };
        selectionModel.Visibility = selectedMeshIndices.Count == 0
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
        return CreateMaterial(DefaultDiffuseColor);
    }

    /// <summary>
    /// Creates a material for imported model meshes from an application-configured diffuse color.
    /// </summary>
    public static PhongMaterial CreateMaterial(Color4 diffuseColor)
    {
        return new PhongMaterial
        {
            AmbientColor = diffuseColor,
            DiffuseColor = diffuseColor,
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
            AmbientColor = highlightColor,
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
        CrossSectionMeshGeometryModel3D highlightModel = new CrossSectionMeshGeometryModel3D
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
    /// Reserves the lower optional face-overlay slot so selected-face overlays keep a stable child index.
    /// </summary>
    private static void EnsureFaceHighlightSlot(GroupModel3D visual, Color4 fallbackColor)
    {
        if (visual.Children.Count > FaceHighlightChildIndex)
        {
            return;
        }

        MeshGeometryModel3D placeholder = CreateFaceHighlightModel(fallbackColor);
        placeholder.Visibility = Visibility.Collapsed;
        visual.Children.Add(placeholder);
    }

    /// <summary>
    /// Updates one cross-section mesh so the shader keeps fragments between two horizontal planes.
    /// </summary>
    private static void ApplyClipRange(CrossSectionMeshGeometryModel3D crossSectionMesh, float lowerZ, float upperZ, bool isClippingActive)
    {
        if (!isClippingActive)
        {
            crossSectionMesh.EnablePlane1 = false;
            crossSectionMesh.EnablePlane2 = false;
            return;
        }

        crossSectionMesh.CuttingOperation = CuttingOperation.Intersect;
        crossSectionMesh.EnablePlane1 = true;
        crossSectionMesh.EnablePlane2 = true;
        crossSectionMesh.Plane1 = new System.Numerics.Plane(new Vector3(0.0f, 0.0f, 1.0f), lowerZ);
        crossSectionMesh.Plane2 = new System.Numerics.Plane(new Vector3(0.0f, 0.0f, -1.0f), -upperZ);
    }

    /// <summary>
    /// Gets the normal mesh child that uses Helix's standard mesh technique and contributes to SSAO.
    /// </summary>
    private static MeshGeometryModel3D? GetStandardMeshModel(GroupModel3D visual)
    {
        if (visual.Children.Count <= StandardMeshChildIndex)
        {
            return null;
        }

        return visual.Children[StandardMeshChildIndex] as MeshGeometryModel3D;
    }

    /// <summary>
    /// Gets the cross-section mesh child used only while the clipping slider narrows the visible range.
    /// </summary>
    private static CrossSectionMeshGeometryModel3D? GetClippedMeshModel(GroupModel3D visual)
    {
        if (visual.Children.Count <= ClippedMeshChildIndex)
        {
            return null;
        }

        return visual.Children[ClippedMeshChildIndex] as CrossSectionMeshGeometryModel3D;
    }

    /// <summary>
    /// Updates visibility and hit testing together so hidden alternate render paths do not participate in selection.
    /// </summary>
    private static void SetSelectableMeshVisibility(MeshGeometryModel3D? meshModel, bool isVisible)
    {
        if (meshModel == null)
        {
            return;
        }

        meshModel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        meshModel.IsHitTestVisible = isVisible;
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
