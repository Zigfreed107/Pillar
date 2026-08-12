// ModelTranslatePreviewRenderer.cs
// Draws reusable always-on-top solid axis arrows with absolute shaft and arrowhead dimensions.
using HelixToolkit;
using HelixToolkit.Geometry;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Identifies one world-axis model translation handle.
/// </summary>
public enum ModelTranslateGizmoHandleKind
{
    None,
    XAxis,
    YAxis,
    ZAxis
}

/// <summary>
/// Renders solid world-axis arrows in a top-most scene pass without owning model transform state.
/// </summary>
public sealed class ModelTranslatePreviewRenderer
{
    public const float DefaultShaftDiameter = 1.5f;
    public const float DefaultHeadLength = 3.0f;
    public const float DefaultHeadDiameter = 4.5f;
    private const float DefaultArrowLength = 1.0f;
    private readonly TopMostGroup3D _topMostRoot;
    private readonly MeshGeometryModel3D _xArrow;
    private readonly MeshGeometryModel3D _yArrow;
    private readonly MeshGeometryModel3D _zArrow;
    private readonly TranslateTransform3D _xArrowTranslation;
    private readonly TranslateTransform3D _yArrowTranslation;
    private readonly TranslateTransform3D _zArrowTranslation;
    private readonly int _arrowSides;
    private readonly float _shaftDiameter;
    private readonly float _headLength;
    private readonly float _headRadius;
    private Vector3 _renderedArrowLengths;

    /// <summary>
    /// Creates reusable arrow models using absolute model-unit shaft and arrowhead dimensions.
    /// </summary>
    public ModelTranslatePreviewRenderer(
        GroupModel3D sceneRoot,
        int arrowSides,
        float shaftDiameter = DefaultShaftDiameter,
        float headLength = DefaultHeadLength,
        float headDiameter = DefaultHeadDiameter)
    {
        if (sceneRoot == null)
        {
            throw new ArgumentNullException(nameof(sceneRoot));
        }

        _arrowSides = global::System.Math.Max(6, arrowSides);
        _shaftDiameter = SanitizeDimension(shaftDiameter, DefaultShaftDiameter);
        _headLength = SanitizeDimension(headLength, DefaultHeadLength);
        _headRadius = SanitizeDimension(headDiameter, DefaultHeadDiameter) * 0.5f;
        (_xArrow, _xArrowTranslation) = CreateArrow(
            new Color4(0.86f, 0.18f, 0.18f, 1.0f),
            Vector3.UnitX);
        (_yArrow, _yArrowTranslation) = CreateArrow(
            new Color4(0.18f, 0.75f, 0.25f, 1.0f),
            Vector3.UnitY);
        (_zArrow, _zArrowTranslation) = CreateArrow(
            new Color4(0.18f, 0.39f, 0.90f, 1.0f),
            Vector3.UnitZ);
        _renderedArrowLengths = new Vector3(DefaultArrowLength);

        _topMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _topMostRoot.Children.Add(_xArrow);
        _topMostRoot.Children.Add(_yArrow);
        _topMostRoot.Children.Add(_zArrow);
        sceneRoot.Children.Add(_topMostRoot);
    }

    /// <summary>
    /// Positions all arrows and rebuilds geometry only when their model-derived lengths change.
    /// </summary>
    public void Show(Vector3 worldOrigin, Vector3 arrowLengths)
    {
        Vector3 safeLengths = new Vector3(
            SanitizeDimension(arrowLengths.X, DefaultArrowLength),
            SanitizeDimension(arrowLengths.Y, DefaultArrowLength),
            SanitizeDimension(arrowLengths.Z, DefaultArrowLength));

        if (_renderedArrowLengths != safeLengths)
        {
            _xArrow.Geometry = CreateArrowGeometry(Vector3.UnitX, safeLengths.X);
            _yArrow.Geometry = CreateArrowGeometry(Vector3.UnitY, safeLengths.Y);
            _zArrow.Geometry = CreateArrowGeometry(Vector3.UnitZ, safeLengths.Z);
            _renderedArrowLengths = safeLengths;
        }

        ApplyArrowTranslation(_xArrowTranslation, worldOrigin);
        ApplyArrowTranslation(_yArrowTranslation, worldOrigin);
        ApplyArrowTranslation(_zArrowTranslation, worldOrigin);
        SetVisibility(Visibility.Visible);
    }

    /// <summary>
    /// Maps one hit scene element back to its axis handle.
    /// </summary>
    public bool TryGetHandleKind(Element3D element, out ModelTranslateGizmoHandleKind kind)
    {
        if (ReferenceEquals(element, _xArrow))
        {
            kind = ModelTranslateGizmoHandleKind.XAxis;
            return true;
        }

        if (ReferenceEquals(element, _yArrow))
        {
            kind = ModelTranslateGizmoHandleKind.YAxis;
            return true;
        }

        if (ReferenceEquals(element, _zArrow))
        {
            kind = ModelTranslateGizmoHandleKind.ZAxis;
            return true;
        }

        kind = ModelTranslateGizmoHandleKind.None;
        return false;
    }

    /// <summary>
    /// Hides every model translation handle while retaining its scene models for reuse.
    /// </summary>
    public void Hide()
    {
        SetVisibility(Visibility.Collapsed);
    }

    /// <summary>
    /// Creates one reusable scene model with default-length geometry along the supplied world axis.
    /// </summary>
    private (MeshGeometryModel3D Model, TranslateTransform3D Translation) CreateArrow(
        Color4 color,
        Vector3 direction)
    {
        TranslateTransform3D translation = new TranslateTransform3D();
        PhongMaterial material = new PhongMaterial
        {
            AmbientColor = color,
            DiffuseColor = color,
            SpecularColor = new Color4(0.2f, 0.2f, 0.2f, 1.0f),
            SpecularShininess = 18.0f
        };
        MeshGeometryModel3D model = new MeshGeometryModel3D
        {
            Geometry = CreateArrowGeometry(direction, DefaultArrowLength),
            Material = material,
            Transform = translation,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = true
        };
        return (model, translation);
    }

    /// <summary>
    /// Builds one axis arrow with an absolute head length and diameters inside the requested total length.
    /// </summary>
    private HelixToolkit.SharpDX.MeshGeometry3D CreateArrowGeometry(Vector3 direction, float totalLength)
    {
        float effectiveHeadLength = MathF.Min(_headLength, totalLength);
        float shaftLength = MathF.Max(0.0f, totalLength - effectiveHeadLength);
        Vector3 shaftEnd = direction * shaftLength;
        MeshBuilder builder = new MeshBuilder();

        if (shaftLength > 0.0f)
        {
            builder.AddCylinder(Vector3.Zero, shaftEnd, _shaftDiameter, _arrowSides);
        }

        builder.AddCone(
            shaftEnd,
            direction,
            _headRadius,
            0.0f,
            effectiveHeadLength,
            true,
            false,
            _arrowSides);
        return builder.ToMeshGeometry3D();
    }

    /// <summary>
    /// Reuses one translation transform when the model origin moves during a drag.
    /// </summary>
    private static void ApplyArrowTranslation(TranslateTransform3D translation, Vector3 origin)
    {
        translation.OffsetX = origin.X;
        translation.OffsetY = origin.Y;
        translation.OffsetZ = origin.Z;
    }

    /// <summary>
    /// Changes all handle visibility together.
    /// </summary>
    private void SetVisibility(Visibility visibility)
    {
        _xArrow.Visibility = visibility;
        _yArrow.Visibility = visibility;
        _zArrow.Visibility = visibility;
    }

    /// <summary>
    /// Replaces an invalid absolute dimension with its safe application default.
    /// </summary>
    private static float SanitizeDimension(float value, float fallback)
    {
        return float.IsFinite(value) && value > 0.0f ? value : fallback;
    }
}
