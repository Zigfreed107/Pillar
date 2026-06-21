// RotationOriginPreviewRenderer.cs
// Draws reusable, non-interactive filled axis discs for the Transform Rotate tool without owning document state.
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Numerics;
using System.Windows;
using Color4 = HelixToolkit.Maths.Color4;
using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Renders translucent X, Y, and Z rotation guides at the active model rotation origin.
/// </summary>
public sealed class RotationOriginPreviewRenderer
{
    private const int CircleSegmentCount = 96;
    private const float GuideOpacity = 0.25f;

    private readonly MeshGeometryModel3D _xCircleModel;
    private readonly MeshGeometryModel3D _yCircleModel;
    private readonly MeshGeometryModel3D _zCircleModel;
    private readonly MeshGeometry3D _xCircleGeometry;
    private readonly MeshGeometry3D _yCircleGeometry;
    private readonly MeshGeometry3D _zCircleGeometry;
    private readonly Vector3Collection _xCirclePositions;
    private readonly Vector3Collection _yCirclePositions;
    private readonly Vector3Collection _zCirclePositions;
    private readonly Vector3Collection _xCircleNormals;
    private readonly Vector3Collection _yCircleNormals;
    private readonly Vector3Collection _zCircleNormals;

    /// <summary>
    /// Creates the three reusable filled axis guides and adds them to the preview scene root.
    /// </summary>
    public RotationOriginPreviewRenderer(GroupModel3D sceneRoot)
    {
        (_xCircleModel, _xCircleGeometry, _xCirclePositions, _xCircleNormals) = CreateCircleModel(MediaColors.Red, Vector3.UnitX);
        (_yCircleModel, _yCircleGeometry, _yCirclePositions, _yCircleNormals) = CreateCircleModel(MediaColors.Green, Vector3.UnitY);
        (_zCircleModel, _zCircleGeometry, _zCirclePositions, _zCircleNormals) = CreateCircleModel(MediaColors.Blue, Vector3.UnitZ);

        sceneRoot.Children.Add(_xCircleModel);
        sceneRoot.Children.Add(_yCircleModel);
        sceneRoot.Children.Add(_zCircleModel);
    }

    /// <summary>
    /// Updates all disc vertices and shows the guides around the supplied world-space origin.
    /// </summary>
    public void Show(Vector3 origin, float radius, Quaternion orientation)
    {
        float safeRadius = radius > 0.0f && !float.IsNaN(radius) && !float.IsInfinity(radius) ? radius : 1.0f;
        Quaternion safeOrientation = orientation.LengthSquared() > float.Epsilon ? Quaternion.Normalize(orientation) : Quaternion.Identity;
        UpdateCircle(_xCirclePositions, _xCircleNormals, origin, safeRadius, 0, safeOrientation);
        UpdateCircle(_yCirclePositions, _yCircleNormals, origin, safeRadius, 1, safeOrientation);
        UpdateCircle(_zCirclePositions, _zCircleNormals, origin, safeRadius, 2, safeOrientation);
        UpdateGeometry(_xCircleGeometry);
        UpdateGeometry(_yCircleGeometry);
        UpdateGeometry(_zCircleGeometry);
        SetVisibility(Visibility.Visible);
    }

    /// <summary>
    /// Hides all rotation guides when the tool closes or loses its target.
    /// </summary>
    public void Hide()
    {
        SetVisibility(Visibility.Collapsed);
    }

    /// <summary>
    /// Creates one preallocated, double-sided triangle-fan disc using a coordinate-axis color.
    /// </summary>
    private static (MeshGeometryModel3D Model, MeshGeometry3D Geometry, Vector3Collection Positions, Vector3Collection Normals) CreateCircleModel(
        MediaColor color,
        Vector3 normal)
    {
        Vector3Collection positions = new Vector3Collection(CircleSegmentCount + 1);
        Vector3Collection normals = new Vector3Collection(CircleSegmentCount + 1);
        IntCollection indices = new IntCollection(CircleSegmentCount * 3);

        for (int vertexIndex = 0; vertexIndex <= CircleSegmentCount; vertexIndex++)
        {
            positions.Add(Vector3.Zero);
            normals.Add(normal);
        }

        for (int segmentIndex = 0; segmentIndex < CircleSegmentCount; segmentIndex++)
        {
            indices.Add(0);
            indices.Add(segmentIndex + 1);
            indices.Add(((segmentIndex + 1) % CircleSegmentCount) + 1);
        }

        MeshGeometry3D geometry = new MeshGeometry3D
        {
            Positions = positions,
            Normals = normals,
            Indices = indices
        };
        MeshGeometryModel3D model = new MeshGeometryModel3D
        {
            Geometry = geometry,
            Material = CreateCircleMaterial(color),
            CullMode = SharpDX.Direct3D11.CullMode.None,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            IsTransparent = true
        };

        return (model, geometry, positions, normals);
    }

    /// <summary>
    /// Creates a flat, 75%-translucent material from one standard coordinate-axis color.
    /// </summary>
    private static PhongMaterial CreateCircleMaterial(MediaColor color)
    {
        Color4 guideColor = new Color4(
            color.R / 255.0f,
            color.G / 255.0f,
            color.B / 255.0f,
            GuideOpacity);

        return new PhongMaterial
        {
            AmbientColor = guideColor,
            DiffuseColor = guideColor,
            SpecularColor = new Color4(0.0f, 0.0f, 0.0f, GuideOpacity),
            SpecularShininess = 1.0f
        };
    }

    /// <summary>
    /// Writes one triangle-fan disc on YZ (X), XZ (Y), or XY (Z).
    /// </summary>
    private static void UpdateCircle(
        Vector3Collection positions,
        Vector3Collection normals,
        Vector3 origin,
        float radius,
        int axisIndex,
        Quaternion orientation)
    {
        float angleStep = (MathF.PI * 2.0f) / CircleSegmentCount;
        Vector3 localNormal = axisIndex switch
        {
            0 => Vector3.UnitX,
            1 => Vector3.UnitY,
            _ => Vector3.UnitZ
        };
        Vector3 worldNormal = Vector3.Transform(localNormal, orientation);
        positions[0] = origin;
        normals[0] = worldNormal;

        for (int segmentIndex = 0; segmentIndex < CircleSegmentCount; segmentIndex++)
        {
            float angle = segmentIndex * angleStep;
            positions[segmentIndex + 1] = CreateCirclePoint(origin, radius, angle, axisIndex, orientation);
            normals[segmentIndex + 1] = worldNormal;
        }
    }

    /// <summary>
    /// Creates one perimeter point on the requested disc plane and rotates it into the displayed axes.
    /// </summary>
    private static Vector3 CreateCirclePoint(
        Vector3 origin,
        float radius,
        float angle,
        int axisIndex,
        Quaternion orientation)
    {
        float first = MathF.Cos(angle) * radius;
        float second = MathF.Sin(angle) * radius;
        Vector3 localOffset = axisIndex switch
        {
            0 => new Vector3(0.0f, first, second),
            1 => new Vector3(first, 0.0f, second),
            _ => new Vector3(first, second, 0.0f)
        };

        return origin + Vector3.Transform(localOffset, orientation);
    }

    /// <summary>
    /// Notifies Helix that one preallocated disc's vertex buffer and bounds changed.
    /// </summary>
    private static void UpdateGeometry(MeshGeometry3D geometry)
    {
        geometry.UpdateVertices();
        geometry.UpdateBounds();
    }

    /// <summary>
    /// Changes all guide visibility as one logical preview.
    /// </summary>
    private void SetVisibility(Visibility visibility)
    {
        _xCircleModel.Visibility = visibility;
        _yCircleModel.Visibility = visibility;
        _zCircleModel.Visibility = visibility;
    }
}