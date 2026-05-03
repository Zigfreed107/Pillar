// RingSupportPreviewRenderer.cs
// Draws transient Ring Support guide geometry in the viewport without mutating the CAD document.
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
/// Renders the Ring Support tool's temporary circle outline, projected support markers, and editable point handles.
/// </summary>
public sealed class RingSupportPreviewRenderer
{
    private const int CircleSegmentCount = 96;
    private const int PointHandleCount = 3;
    private const int MaximumMarkerCount = 512;
    private const float MarkerHalfSize = 0.18f;
    private const float MinimumHandleDiameter = 0.001f;
    private const float PointHandleOpacity = 0.5f;

    private readonly LineGeometry3D _circleGeometry;
    private readonly LineGeometryModel3D _circleModel;
    private readonly TopMostGroup3D _circleTopMostRoot;
    private readonly LineGeometry3D _markerGeometry;
    private readonly LineGeometryModel3D _markerModel;
    private readonly TopMostGroup3D _markerTopMostRoot;
    private readonly MeshGeometryModel3D _firstHandleModel;
    private readonly MeshGeometryModel3D _secondHandleModel;
    private readonly MeshGeometryModel3D _thirdHandleModel;
    private readonly LineGeometry3D _handleRingGeometry;
    private readonly LineGeometryModel3D _handleRingModel;
    private readonly TopMostGroup3D _handleTopMostRoot;
    private readonly ScaleTransform3D _firstHandleScale;
    private readonly ScaleTransform3D _secondHandleScale;
    private readonly ScaleTransform3D _thirdHandleScale;
    private readonly TranslateTransform3D _firstHandleTranslation;
    private readonly TranslateTransform3D _secondHandleTranslation;
    private readonly TranslateTransform3D _thirdHandleTranslation;
    private readonly PhongMaterial _handleMaterial;

    /// <summary>
    /// Creates the reusable preview geometry and attaches it to the supplied preview scene root.
    /// </summary>
    public RingSupportPreviewRenderer(GroupModel3D sceneRoot)
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
        _handleMaterial = CreateHandleMaterial(new Color4(0.0f, 0.75f, 1.0f, PointHandleOpacity));
        _firstHandleScale = new ScaleTransform3D(1.0, 1.0, 1.0);
        _secondHandleScale = new ScaleTransform3D(1.0, 1.0, 1.0);
        _thirdHandleScale = new ScaleTransform3D(1.0, 1.0, 1.0);
        _firstHandleTranslation = new TranslateTransform3D();
        _secondHandleTranslation = new TranslateTransform3D();
        _thirdHandleTranslation = new TranslateTransform3D();
        _firstHandleModel = CreateHandleModel(handleGeometry, _firstHandleScale, _firstHandleTranslation);
        _secondHandleModel = CreateHandleModel(handleGeometry, _secondHandleScale, _secondHandleTranslation);
        _thirdHandleModel = CreateHandleModel(handleGeometry, _thirdHandleScale, _thirdHandleTranslation);
        _handleRingGeometry = CreateHandleRingGeometry();
        _handleRingModel = new LineGeometryModel3D
        {
            Geometry = _handleRingGeometry,
            Color = Colors.DeepSkyBlue,
            Thickness = 2.0f,
            Visibility = Visibility.Collapsed
        };
        _handleTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _handleTopMostRoot.Children.Add(_firstHandleModel);
        _handleTopMostRoot.Children.Add(_secondHandleModel);
        _handleTopMostRoot.Children.Add(_thirdHandleModel);
        _handleTopMostRoot.Children.Add(_handleRingModel);

        sceneRoot.Children.Add(_circleTopMostRoot);
        sceneRoot.Children.Add(_markerTopMostRoot);
        sceneRoot.Children.Add(_handleTopMostRoot);
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
    /// Updates the transient circumference point handles and sizes them from the active spacing setting.
    /// </summary>
    public void ShowPointHandles(
        Vector3 firstPoint,
        Vector3? secondPoint,
        Vector3? thirdPoint,
        float handleDiameter)
    {
        double diameter = System.Math.Max(handleDiameter, MinimumHandleDiameter);
        ApplyHandleTransform(_firstHandleScale, _firstHandleTranslation, firstPoint, diameter);
        _firstHandleModel.Material = _handleMaterial;
        _firstHandleModel.Visibility = Visibility.Visible;
        UpdateHandleRing(0, firstPoint, diameter, true);

        ShowOptionalPointHandle(
            1,
            _secondHandleModel,
            _secondHandleScale,
            _secondHandleTranslation,
            secondPoint,
            diameter);

        ShowOptionalPointHandle(
            2,
            _thirdHandleModel,
            _thirdHandleScale,
            _thirdHandleTranslation,
            thirdPoint,
            diameter);

        _handleRingGeometry.UpdateVertices();
        _handleRingGeometry.UpdateBounds();
        _handleRingModel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Returns the point handle represented by one hit-tested preview model.
    /// </summary>
    public bool TryGetPointHandleKind(Element3D hitModel, out RingSupportPointHandleKind handleKind)
    {
        if (ReferenceEquals(hitModel, _firstHandleModel))
        {
            handleKind = RingSupportPointHandleKind.FirstPoint;
            return true;
        }

        if (ReferenceEquals(hitModel, _secondHandleModel))
        {
            handleKind = RingSupportPointHandleKind.SecondPoint;
            return true;
        }

        if (ReferenceEquals(hitModel, _thirdHandleModel))
        {
            handleKind = RingSupportPointHandleKind.ThirdPoint;
            return true;
        }

        handleKind = RingSupportPointHandleKind.None;
        return false;
    }

    /// <summary>
    /// Hides all Ring Support preview geometry.
    /// </summary>
    public void Hide()
    {
        _circleModel.Visibility = Visibility.Collapsed;
        _markerModel.Visibility = Visibility.Collapsed;
        _firstHandleModel.Visibility = Visibility.Collapsed;
        _secondHandleModel.Visibility = Visibility.Collapsed;
        _thirdHandleModel.Visibility = Visibility.Collapsed;
        _handleRingModel.Visibility = Visibility.Collapsed;
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
    /// Creates fixed point-handle outline topology so handle rings can stay topmost without allocating during drags.
    /// </summary>
    private static LineGeometry3D CreateHandleRingGeometry()
    {
        LineGeometry3D geometry = new LineGeometry3D
        {
            Positions = new Vector3Collection(PointHandleCount * CircleSegmentCount),
            Indices = new IntCollection(PointHandleCount * CircleSegmentCount * 2)
        };

        for (int handleIndex = 0; handleIndex < PointHandleCount; handleIndex++)
        {
            int baseIndex = handleIndex * CircleSegmentCount;

            for (int segmentIndex = 0; segmentIndex < CircleSegmentCount; segmentIndex++)
            {
                geometry.Positions.Add(Vector3.Zero);
                geometry.Indices.Add(baseIndex + segmentIndex);
                geometry.Indices.Add(baseIndex + ((segmentIndex + 1) % CircleSegmentCount));
            }
        }

        return geometry;
    }

    /// <summary>
    /// Creates one unit-diameter sphere mesh reused by all point handles.
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
            Name = "RingSupportPointHandle",
            Transform = transform,
            CullMode = SharpDX.Direct3D11.CullMode.None,
            IsTransparent = true,
            Visibility = Visibility.Collapsed
        };
    }

    /// <summary>
    /// Creates a translucent material for the interactive point handles.
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
    /// Shows or hides one optional point handle without rebuilding its mesh.
    /// </summary>
    private void ShowOptionalPointHandle(
        int handleIndex,
        MeshGeometryModel3D handleModel,
        ScaleTransform3D handleScale,
        TranslateTransform3D handleTranslation,
        Vector3? point,
        double diameter)
    {
        if (!point.HasValue)
        {
            handleModel.Visibility = Visibility.Collapsed;
            UpdateHandleRing(handleIndex, Vector3.Zero, diameter, false);
            return;
        }

        ApplyHandleTransform(handleScale, handleTranslation, point.Value, diameter);
        handleModel.Material = _handleMaterial;
        handleModel.Visibility = Visibility.Visible;
        UpdateHandleRing(handleIndex, point.Value, diameter, true);
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

    /// <summary>
    /// Updates one topmost blue circle around a sphere handle while preserving the shared line buffer.
    /// </summary>
    private void UpdateHandleRing(int handleIndex, Vector3 center, double diameter, bool isVisible)
    {
        Vector3Collection positions = _handleRingGeometry.Positions!;
        int baseIndex = handleIndex * CircleSegmentCount;
        float radius = (float)(diameter * 0.5);

        for (int segmentIndex = 0; segmentIndex < CircleSegmentCount; segmentIndex++)
        {
            if (!isVisible)
            {
                positions[baseIndex + segmentIndex] = Vector3.Zero;
                continue;
            }

            float angle = (float)(segmentIndex * System.Math.PI * 2.0 / CircleSegmentCount);
            float x = center.X + (float)System.Math.Cos(angle) * radius;
            float y = center.Y + (float)System.Math.Sin(angle) * radius;
            positions[baseIndex + segmentIndex] = new Vector3(x, y, center.Z);
        }
    }
}
