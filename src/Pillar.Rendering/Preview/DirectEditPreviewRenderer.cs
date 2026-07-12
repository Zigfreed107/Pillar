// DirectEditPreviewRenderer.cs
// Draws reusable solid Direct Edit gizmos and disposable support previews without owning document state.
using HelixToolkit;
using HelixToolkit.Geometry;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Rendering.EntityRenderers;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Media.Media3D;
using SharpDxMeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Identifies the interactive portion of the Direct Edit gizmo under the pointer.
/// </summary>
public enum DirectEditGizmoHandleKind
{
    None,
    XAxis,
    YAxis,
    XYPlane,
    ZAxis
}

/// <summary>
/// Renders solid Direct Edit handles and rebuilt support previews in the scene preview root.
/// </summary>
public sealed class DirectEditPreviewRenderer
{
    private const float ArrowShaftLengthFactor = 0.72f;
    private const float ArrowShaftDiameterFactor = 0.10f;
    private const float ArrowHeadRadiusFactor = 0.15f;
    private readonly GroupModel3D _supportPreviewRoot = new GroupModel3D();
    private readonly MeshGeometryModel3D _xArrow;
    private readonly MeshGeometryModel3D _yArrow;
    private readonly MeshGeometryModel3D _zArrow;
    private readonly MeshGeometryModel3D _xyPlane;
    private readonly ScaleTransform3D _xArrowScale;
    private readonly ScaleTransform3D _yArrowScale;
    private readonly ScaleTransform3D _zArrowScale;
    private readonly TranslateTransform3D _xArrowTranslation;
    private readonly TranslateTransform3D _yArrowTranslation;
    private readonly TranslateTransform3D _zArrowTranslation;
    private readonly Vector3Collection _planePositions;
    private readonly int _supportSides;

    /// <summary>
    /// Creates reusable solid gizmo visuals and adds them to the scene preview root.
    /// </summary>
    public DirectEditPreviewRenderer(GroupModel3D sceneRoot, int supportSides)
    {
        _supportSides = global::System.Math.Max(6, supportSides);
        (_xArrow, _xArrowScale, _xArrowTranslation) = CreateArrow(
            new Color4(0.86f, 0.18f, 0.18f, 1.0f),
            Vector3.UnitX);
        (_yArrow, _yArrowScale, _yArrowTranslation) = CreateArrow(
            new Color4(0.18f, 0.75f, 0.25f, 1.0f),
            Vector3.UnitY);
        (_zArrow, _zArrowScale, _zArrowTranslation) = CreateArrow(
            new Color4(0.18f, 0.39f, 0.90f, 1.0f),
            Vector3.UnitZ);
        (_xyPlane, _planePositions) = CreatePlane();

        sceneRoot.Children.Add(_supportPreviewRoot);
        sceneRoot.Children.Add(_xyPlane);
        sceneRoot.Children.Add(_xArrow);
        sceneRoot.Children.Add(_yArrow);
        sceneRoot.Children.Add(_zArrow);
    }

    /// <summary>
    /// Positions and shows all solid handles for one selected shared stem.
    /// </summary>
    public void ShowGizmo(Vector3 basePosition, Vector3 stemTop, float xyLength, float zLength)
    {
        float safeXyLength = IsPositiveFinite(xyLength) ? xyLength : 1.0f;
        float safeZLength = IsPositiveFinite(zLength) ? zLength : 1.0f;
        ApplyArrowTransform(_xArrowScale, _xArrowTranslation, basePosition, safeXyLength);
        ApplyArrowTransform(_yArrowScale, _yArrowTranslation, basePosition, safeXyLength);
        ApplyArrowTransform(_zArrowScale, _zArrowTranslation, stemTop, safeZLength);
        UpdatePlane(basePosition, safeXyLength);
        _xyPlane.Geometry?.UpdateVertices();
        _xyPlane.Geometry?.UpdateBounds();
        SetGizmoVisibility(Visibility.Visible);
    }

    /// <summary>
    /// Replaces the translucent rebuilt-support preview shown during a drag.
    /// </summary>
    public void ShowSupportPreview(IReadOnlyList<SupportEntity> supports, SupportLayerColor color)
    {
        _supportPreviewRoot.Children.Clear();
        PhongMaterial material = SupportRenderer.CreateMaterial(color, 0.55f);

        for (int i = 0; i < supports.Count; i++)
        {
            GroupModel3D visual = SupportRenderer.Create(supports[i], material, _supportSides);
            SetHitTesting(visual, false);
            _supportPreviewRoot.Children.Add(visual);
        }

        _supportPreviewRoot.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Tests whether a hit model belongs to one interactive gizmo handle.
    /// </summary>
    public bool TryGetHandleKind(Element3D element, out DirectEditGizmoHandleKind kind)
    {
        if (ReferenceEquals(element, _xArrow))
        {
            kind = DirectEditGizmoHandleKind.XAxis;
            return true;
        }

        if (ReferenceEquals(element, _yArrow))
        {
            kind = DirectEditGizmoHandleKind.YAxis;
            return true;
        }

        if (ReferenceEquals(element, _zArrow))
        {
            kind = DirectEditGizmoHandleKind.ZAxis;
            return true;
        }

        if (ReferenceEquals(element, _xyPlane))
        {
            kind = DirectEditGizmoHandleKind.XYPlane;
            return true;
        }

        kind = DirectEditGizmoHandleKind.None;
        return false;
    }

    /// <summary>
    /// Hides every Direct Edit visual and releases disposable preview meshes.
    /// </summary>
    public void Hide()
    {
        SetGizmoVisibility(Visibility.Collapsed);
        _supportPreviewRoot.Visibility = Visibility.Collapsed;
        _supportPreviewRoot.Children.Clear();
    }

    /// <summary>
    /// Creates a unit-length solid shaft and conical arrowhead along one world axis.
    /// </summary>
    private (MeshGeometryModel3D Model, ScaleTransform3D Scale, TranslateTransform3D Translation) CreateArrow(
        Color4 color,
        Vector3 direction)
    {
        MeshBuilder builder = new MeshBuilder();
        Vector3 shaftEnd = direction * ArrowShaftLengthFactor;
        builder.AddCylinder(Vector3.Zero, shaftEnd, ArrowShaftDiameterFactor, _supportSides);
        builder.AddCone(
            shaftEnd,
            direction,
            ArrowHeadRadiusFactor,
            0.0f,
            1.0f - ArrowShaftLengthFactor,
            true,
            false,
            _supportSides);

        ScaleTransform3D scale = new ScaleTransform3D(1.0, 1.0, 1.0);
        TranslateTransform3D translation = new TranslateTransform3D();
        Transform3DGroup transform = new Transform3DGroup();
        transform.Children.Add(scale);
        transform.Children.Add(translation);
        PhongMaterial material = new PhongMaterial
        {
            AmbientColor = color,
            DiffuseColor = color,
            SpecularColor = new Color4(0.2f, 0.2f, 0.2f, 1.0f),
            SpecularShininess = 18.0f
        };
        MeshGeometryModel3D model = new MeshGeometryModel3D
        {
            Geometry = builder.ToMeshGeometry3D(),
            Material = material,
            Transform = transform,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = true
        };
        return (model, scale, translation);
    }

    /// <summary>
    /// Creates the translucent yellow XY drag plane.
    /// </summary>
    private static (MeshGeometryModel3D Model, Vector3Collection Positions) CreatePlane()
    {
        Vector3Collection positions = new Vector3Collection(4)
        {
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero
        };
        SharpDxMeshGeometry3D geometry = new SharpDxMeshGeometry3D
        {
            Positions = positions,
            Normals = new Vector3Collection { Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ },
            Indices = new IntCollection { 0, 1, 2, 0, 2, 3 }
        };
        Color4 color = new Color4(1.0f, 0.82f, 0.0f, 0.35f);
        MeshGeometryModel3D model = new MeshGeometryModel3D
        {
            Geometry = geometry,
            Material = new PhongMaterial { AmbientColor = color, DiffuseColor = color },
            CullMode = SharpDX.Direct3D11.CullMode.None,
            IsTransparent = true,
            IsHitTestVisible = true,
            Visibility = Visibility.Collapsed
        };
        return (model, positions);
    }

    /// <summary>
    /// Scales one unit arrow and moves its origin without rebuilding mesh buffers.
    /// </summary>
    private static void ApplyArrowTransform(
        ScaleTransform3D scale,
        TranslateTransform3D translation,
        Vector3 origin,
        float length)
    {
        scale.ScaleX = length;
        scale.ScaleY = length;
        scale.ScaleZ = length;
        translation.OffsetX = origin.X;
        translation.OffsetY = origin.Y;
        translation.OffsetZ = origin.Z;
    }

    /// <summary>
    /// Writes the square XY handle with one corner at the stem base.
    /// </summary>
    private void UpdatePlane(Vector3 origin, float length)
    {
        _planePositions[0] = origin;
        _planePositions[1] = origin + new Vector3(length, 0.0f, 0.0f);
        _planePositions[2] = origin + new Vector3(length, length, 0.0f);
        _planePositions[3] = origin + new Vector3(0.0f, length, 0.0f);
    }

    /// <summary>
    /// Changes visibility for all four interactive handles.
    /// </summary>
    private void SetGizmoVisibility(Visibility visibility)
    {
        _xArrow.Visibility = visibility;
        _yArrow.Visibility = visibility;
        _zArrow.Visibility = visibility;
        _xyPlane.Visibility = visibility;
    }

    /// <summary>
    /// Recursively disables hit testing on disposable support preview visuals.
    /// </summary>
    private static void SetHitTesting(Element3D element, bool isHitTestVisible)
    {
        element.IsHitTestVisible = isHitTestVisible;

        if (element is GroupModel3D group)
        {
            for (int i = 0; i < group.Children.Count; i++)
            {
                SetHitTesting(group.Children[i], isHitTestVisible);
            }
        }
    }

    /// <summary>
    /// Checks a configured world-space handle length.
    /// </summary>
    private static bool IsPositiveFinite(float value)
    {
        return float.IsFinite(value) && value > 0.0f;
    }
}
