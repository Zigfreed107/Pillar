// CircleSupportPreviewRenderer.cs
// Draws transient circle-support guide geometry in the viewport without mutating the CAD document.
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using MeshBuilder = HelixToolkit.Geometry.MeshBuilder;
using ScaleTransform3D = System.Windows.Media.Media3D.ScaleTransform3D;
using SharpDxMeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;
using Transform3DGroup = System.Windows.Media.Media3D.Transform3DGroup;
using TranslateTransform3D = System.Windows.Media.Media3D.TranslateTransform3D;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Renders the Circle Support tool's temporary circle outline and projected support markers.
/// </summary>
public sealed class CircleSupportPreviewRenderer
{
    private const int CircleSegmentCount = 96;
    private const int MaximumMarkerCount = 512;
    private const float MarkerHalfSize = 0.18f;
    private const float MinimumHandleDiameter = 0.001f;

    private readonly LineGeometry3D _circleGeometry;
    private readonly LineGeometryModel3D _circleModel;
    private readonly TopMostGroup3D _circleTopMostRoot;
    private readonly LineGeometry3D _markerGeometry;
    private readonly LineGeometryModel3D _markerModel;
    private readonly TopMostGroup3D _markerTopMostRoot;
    private readonly MeshGeometryModel3D _firstHandleModel;
    private readonly MeshGeometryModel3D _secondHandleModel;
    private readonly ScaleTransform3D _firstHandleScale;
    private readonly ScaleTransform3D _secondHandleScale;
    private readonly TranslateTransform3D _firstHandleTranslation;
    private readonly TranslateTransform3D _secondHandleTranslation;
    private readonly PhongMaterial _handleMaterial;
    private readonly PhongMaterial _activeHandleMaterial;

    /// <summary>
    /// Creates the reusable preview geometry and attaches it to the supplied preview scene root.
    /// </summary>
    public CircleSupportPreviewRenderer(GroupModel3D sceneRoot)
    {
        _circleGeometry = CreateCircleGeometry();
        _circleModel = new LineGeometryModel3D
        {
            Geometry = _circleGeometry,
            Color = Colors.Goldenrod,
            Thickness = 2.0f,
            Visibility = Visibility.Collapsed
        };
        _circleTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _circleTopMostRoot.Children.Add(_circleModel);

        _markerGeometry = CreateMarkerGeometry();
        _markerModel = new LineGeometryModel3D
        {
            Geometry = _markerGeometry,
            Color = Colors.DeepSkyBlue,
            Thickness = 2.0f,
            Visibility = Visibility.Collapsed
        };
        _markerTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _markerTopMostRoot.Children.Add(_markerModel);

        SharpDxMeshGeometry3D handleGeometry = CreateHandleGeometry();
        _handleMaterial = CreateHandleMaterial(new Color4(0.1f, 0.55f, 1.0f, 1.0f));
        _activeHandleMaterial = CreateHandleMaterial(new Color4(1.0f, 0.55f, 0.08f, 1.0f));
        _firstHandleScale = new ScaleTransform3D(1.0, 1.0, 1.0);
        _secondHandleScale = new ScaleTransform3D(1.0, 1.0, 1.0);
        _firstHandleTranslation = new TranslateTransform3D();
        _secondHandleTranslation = new TranslateTransform3D();
        _firstHandleModel = CreateHandleModel(handleGeometry, _firstHandleScale, _firstHandleTranslation);
        _secondHandleModel = CreateHandleModel(handleGeometry, _secondHandleScale, _secondHandleTranslation);

        sceneRoot.Children.Add(_circleTopMostRoot);
        sceneRoot.Children.Add(_markerTopMostRoot);
        sceneRoot.Children.Add(_firstHandleModel);
        sceneRoot.Children.Add(_secondHandleModel);
    }

    /// <summary>
    /// Updates the circle outline preview from a stable 3D circle definition.
    /// </summary>
    public void ShowCircle(Circle3D circle)
    {
        Vector3Collection positions = _circleGeometry.Positions!;

        for (int i = 0; i < CircleSegmentCount; i++)
        {
            float angle = (float)(i * System.Math.PI * 2.0 / CircleSegmentCount);
            positions[i] = circle.GetPoint(angle);
        }

        _circleGeometry.UpdateVertices();
        _circleGeometry.UpdateBounds();
        _circleModel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Updates the projected support marker preview.
    /// </summary>
    public void ShowMarkers(IReadOnlyList<Vector3> markerPositions)
    {
        int markerCount = System.Math.Min(markerPositions.Count, MaximumMarkerCount);
        Vector3Collection positions = _markerGeometry.Positions!;

        for (int i = 0; i < MaximumMarkerCount; i++)
        {
            Vector3 position = i < markerCount ? markerPositions[i] : Vector3.Zero;
            int baseIndex = i * 4;

            if (i < markerCount)
            {
                positions[baseIndex] = new Vector3(position.X - MarkerHalfSize, position.Y, position.Z);
                positions[baseIndex + 1] = new Vector3(position.X + MarkerHalfSize, position.Y, position.Z);
                positions[baseIndex + 2] = new Vector3(position.X, position.Y - MarkerHalfSize, position.Z);
                positions[baseIndex + 3] = new Vector3(position.X, position.Y + MarkerHalfSize, position.Z);
            }
            else
            {
                positions[baseIndex] = position;
                positions[baseIndex + 1] = position;
                positions[baseIndex + 2] = position;
                positions[baseIndex + 3] = position;
            }
        }

        _markerGeometry.UpdateVertices();
        _markerGeometry.UpdateBounds();
        _markerModel.Visibility = markerCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Hides the projected support marker preview without clearing or rebuilding its reusable geometry.
    /// </summary>
    public void HideMarkers()
    {
        _markerModel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the transient diameter editing handles and sizes them from the active spacing setting.
    /// </summary>
    public void ShowDiameterHandles(
        Vector3 firstPoint,
        Vector3? secondPoint,
        float handleDiameter,
        CircleSupportDiameterHandleKind activeHandle)
    {
        double diameter = System.Math.Max(handleDiameter, MinimumHandleDiameter);
        ApplyHandleTransform(_firstHandleScale, _firstHandleTranslation, firstPoint, diameter);
        _firstHandleModel.Material = activeHandle == CircleSupportDiameterHandleKind.FirstPoint
            ? _activeHandleMaterial
            : _handleMaterial;
        _firstHandleModel.Visibility = Visibility.Visible;

        if (secondPoint.HasValue)
        {
            ApplyHandleTransform(_secondHandleScale, _secondHandleTranslation, secondPoint.Value, diameter);
            _secondHandleModel.Material = activeHandle == CircleSupportDiameterHandleKind.SecondPoint
                ? _activeHandleMaterial
                : _handleMaterial;
            _secondHandleModel.Visibility = Visibility.Visible;
        }
        else
        {
            _secondHandleModel.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Returns the diameter handle represented by one hit-tested preview model.
    /// </summary>
    public bool TryGetDiameterHandleKind(Element3D hitModel, out CircleSupportDiameterHandleKind handleKind)
    {
        if (ReferenceEquals(hitModel, _firstHandleModel))
        {
            handleKind = CircleSupportDiameterHandleKind.FirstPoint;
            return true;
        }

        if (ReferenceEquals(hitModel, _secondHandleModel))
        {
            handleKind = CircleSupportDiameterHandleKind.SecondPoint;
            return true;
        }

        handleKind = CircleSupportDiameterHandleKind.None;
        return false;
    }

    /// <summary>
    /// Hides all Circle Support preview geometry.
    /// </summary>
    public void Hide()
    {
        _circleModel.Visibility = Visibility.Collapsed;
        _markerModel.Visibility = Visibility.Collapsed;
        _firstHandleModel.Visibility = Visibility.Collapsed;
        _secondHandleModel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Hides only the generated circle and marker previews while leaving editable handles visible.
    /// </summary>
    public void HideCircleAndMarkers()
    {
        _circleModel.Visibility = Visibility.Collapsed;
        _markerModel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Creates fixed circle line topology so only vertex positions change during preview.
    /// </summary>
    private static LineGeometry3D CreateCircleGeometry()
    {
        LineGeometry3D geometry = new LineGeometry3D
        {
            Positions = new Vector3Collection(CircleSegmentCount),
            Indices = new IntCollection(CircleSegmentCount * 2)
        };

        for (int i = 0; i < CircleSegmentCount; i++)
        {
            geometry.Positions.Add(Vector3.Zero);
            geometry.Indices.Add(i);
            geometry.Indices.Add((i + 1) % CircleSegmentCount);
        }

        return geometry;
    }

    /// <summary>
    /// Creates fixed marker line topology so marker previews do not allocate new Helix visuals.
    /// </summary>
    private static LineGeometry3D CreateMarkerGeometry()
    {
        LineGeometry3D geometry = new LineGeometry3D
        {
            Positions = new Vector3Collection(MaximumMarkerCount * 4),
            Indices = new IntCollection(MaximumMarkerCount * 4)
        };

        for (int i = 0; i < MaximumMarkerCount; i++)
        {
            int baseIndex = i * 4;
            geometry.Positions.Add(Vector3.Zero);
            geometry.Positions.Add(Vector3.Zero);
            geometry.Positions.Add(Vector3.Zero);
            geometry.Positions.Add(Vector3.Zero);
            geometry.Indices.Add(baseIndex);
            geometry.Indices.Add(baseIndex + 1);
            geometry.Indices.Add(baseIndex + 2);
            geometry.Indices.Add(baseIndex + 3);
        }

        return geometry;
    }

    /// <summary>
    /// Creates one unit-diameter sphere mesh reused by both diameter handles.
    /// </summary>
    private static SharpDxMeshGeometry3D CreateHandleGeometry()
    {
        MeshBuilder builder = new MeshBuilder();
        builder.AddSphere(Vector3.Zero, 0.5f);
        return builder.ToMeshGeometry3D();
    }

    /// <summary>
    /// Creates one handle visual with reusable scale and translation transforms.
    /// </summary>
    private MeshGeometryModel3D CreateHandleModel(
        SharpDxMeshGeometry3D geometry,
        ScaleTransform3D scale,
        TranslateTransform3D translation)
    {
        Transform3DGroup transform = new Transform3DGroup();
        transform.Children.Add(scale);
        transform.Children.Add(translation);

        return new MeshGeometryModel3D
        {
            Geometry = geometry,
            Material = _handleMaterial,
            Name = "CircleSupportDiameterHandle",
            Transform = transform,
            CullMode = SharpDX.Direct3D11.CullMode.None,
            Visibility = Visibility.Collapsed
        };
    }

    /// <summary>
    /// Creates a translucent material for the interactive diameter handles.
    /// </summary>
    private static PhongMaterial CreateHandleMaterial(Color4 color)
    {
        return new PhongMaterial
        {
            DiffuseColor = color,
            AmbientColor = color,
            SpecularColor = new Color4(0.9f, 0.9f, 0.9f, color.Alpha),
            SpecularShininess = 24.0f
        };
    }

    /// <summary>
    /// Updates one handle transform without rebuilding its mesh, keeping the sphere center on the circle plane.
    /// </summary>
    private static void ApplyHandleTransform(
        ScaleTransform3D scale,
        TranslateTransform3D translation,
        Vector3 position,
        double diameter)
    {
        scale.ScaleX = diameter;
        scale.ScaleY = diameter;
        scale.ScaleZ = diameter;
        translation.OffsetX = position.X;
        translation.OffsetY = position.Y;
        translation.OffsetZ = position.Z;
    }
}
