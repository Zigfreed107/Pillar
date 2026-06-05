// LineSupportPreviewRenderer.cs
// Draws transient Line Support guide geometry in the viewport without mutating the CAD document.
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Geometry.Supports;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using MeshBuilder = HelixToolkit.Geometry.MeshBuilder;
using MediaColor = System.Windows.Media.Color;
using ScaleTransform3D = System.Windows.Media.Media3D.ScaleTransform3D;
using SharpDxMeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;
using Transform3DGroup = System.Windows.Media.Media3D.Transform3DGroup;
using TranslateTransform3D = System.Windows.Media.Media3D.TranslateTransform3D;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Renders the Line Support tool's temporary polyline, projected support markers, and spacing guide.
/// </summary>
public sealed class LineSupportPreviewRenderer
{
    private const int CircleSegmentCount = 96;
    private const int MaximumPolylineSegmentCount = LineSupportPattern.MaximumSupportCount;
    private const int MaximumMarkerCount = LineSupportPattern.MaximumSupportCount;
    private const float MarkerHalfSize = 0.18f;
    private const float MinimumGuideDiameter = 0.001f;
    private const float GuideOpacity = 0.25f;
    private const float PointHandleOpacity = 0.25f;

    private static readonly MediaColor PreviewLineColor = Colors.Goldenrod;
    private static readonly MediaColor MarkerColor = Colors.DeepSkyBlue;
    private static readonly MediaColor GuideCircleColor = Colors.DeepSkyBlue;

    private readonly LineGeometry3D _polylineGeometry;
    private readonly LineGeometryModel3D _polylineModel;
    private readonly TopMostGroup3D _polylineTopMostRoot;
    private readonly LineGeometry3D _markerGeometry;
    private readonly LineGeometryModel3D _markerModel;
    private readonly TopMostGroup3D _markerTopMostRoot;
    private readonly MeshGeometryModel3D _guideSphereModel;
    private readonly LineGeometry3D _guideCircleGeometry;
    private readonly LineGeometryModel3D _guideCircleModel;
    private readonly TopMostGroup3D _guideTopMostRoot;
    private readonly ScaleTransform3D _guideSphereScale;
    private readonly TranslateTransform3D _guideSphereTranslation;
    private readonly SharpDxMeshGeometry3D _pointHandleGeometry;
    private readonly PhongMaterial _pointHandleMaterial;
    private readonly TopMostGroup3D _pointHandleTopMostRoot;
    private readonly List<MeshGeometryModel3D> _pointHandleModels = new List<MeshGeometryModel3D>();
    private readonly List<ScaleTransform3D> _pointHandleScales = new List<ScaleTransform3D>();
    private readonly List<TranslateTransform3D> _pointHandleTranslations = new List<TranslateTransform3D>();

    /// <summary>
    /// Creates the reusable preview geometry and attaches it to the supplied preview scene root.
    /// </summary>
    public LineSupportPreviewRenderer(GroupModel3D sceneRoot)
    {
        _polylineGeometry = CreatePolylineGeometry();
        _polylineModel = new LineGeometryModel3D
        {
            Geometry = _polylineGeometry,
            Color = PreviewLineColor,
            Thickness = 2.0f,
            Visibility = Visibility.Collapsed
        };
        _polylineTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _polylineTopMostRoot.Children.Add(_polylineModel);

        _markerGeometry = CreateMarkerGeometry();
        _markerModel = new LineGeometryModel3D
        {
            Geometry = _markerGeometry,
            Color = MarkerColor,
            Thickness = 2.0f,
            Visibility = Visibility.Collapsed
        };
        _markerTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _markerTopMostRoot.Children.Add(_markerModel);

        _guideSphereScale = new ScaleTransform3D(1.0, 1.0, 1.0);
        _guideSphereTranslation = new TranslateTransform3D();
        _guideSphereModel = CreateGuideSphereModel(_guideSphereScale, _guideSphereTranslation);
        _guideCircleGeometry = CreateGuideCircleGeometry();
        _guideCircleModel = new LineGeometryModel3D
        {
            Geometry = _guideCircleGeometry,
            Color = GuideCircleColor,
            Thickness = 2.0f,
            Visibility = Visibility.Collapsed
        };
        _guideTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _guideTopMostRoot.Children.Add(_guideSphereModel);
        _guideTopMostRoot.Children.Add(_guideCircleModel);

        _pointHandleGeometry = CreateSphereGeometry();
        _pointHandleMaterial = CreateTransparentMaterial(new Color4(0.0f, 0.75f, 1.0f, PointHandleOpacity));
        _pointHandleTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };

        sceneRoot.Children.Add(_polylineTopMostRoot);
        sceneRoot.Children.Add(_markerTopMostRoot);
        sceneRoot.Children.Add(_guideTopMostRoot);
        sceneRoot.Children.Add(_pointHandleTopMostRoot);
    }

    /// <summary>
    /// Updates the transient polyline preview from committed points plus an optional live hover point.
    /// </summary>
    public void ShowPolyline(IReadOnlyList<Vector3> points, Vector3? hoverPoint)
    {
        int segmentCount = CalculateVisibleSegmentCount(points, hoverPoint);
        Vector3Collection positions = _polylineGeometry.Positions!;

        for (int segmentIndex = 0; segmentIndex < MaximumPolylineSegmentCount; segmentIndex++)
        {
            int baseIndex = segmentIndex * 2;

            if (segmentIndex < segmentCount)
            {
                Vector3 start = GetPreviewPoint(points, hoverPoint, segmentIndex);
                Vector3 end = GetPreviewPoint(points, hoverPoint, segmentIndex + 1);
                positions[baseIndex] = start;
                positions[baseIndex + 1] = end;
            }
            else
            {
                positions[baseIndex] = Vector3.Zero;
                positions[baseIndex + 1] = Vector3.Zero;
            }
        }

        _polylineGeometry.UpdateVertices();
        _polylineGeometry.UpdateBounds();
        _polylineModel.Visibility = segmentCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the projected support marker preview.
    /// </summary>
    public void ShowMarkers(IReadOnlyList<Vector3> markerPositions)
    {
        int markerCount = global::System.Math.Min(markerPositions.Count, MaximumMarkerCount);
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
    /// Updates the transparent spacing sphere and XY guide circle at the current cursor hit.
    /// </summary>
    public void ShowSpacingGuide(Vector3 center, float diameter)
    {
        double normalizedDiameter = global::System.Math.Max(diameter, MinimumGuideDiameter);
        _guideSphereScale.ScaleX = normalizedDiameter;
        _guideSphereScale.ScaleY = normalizedDiameter;
        _guideSphereScale.ScaleZ = normalizedDiameter;
        _guideSphereTranslation.OffsetX = center.X;
        _guideSphereTranslation.OffsetY = center.Y;
        _guideSphereTranslation.OffsetZ = center.Z;
        UpdateGuideCircle(center, (float)(normalizedDiameter * 0.5));
        _guideSphereModel.Visibility = Visibility.Visible;
        _guideCircleModel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides the cursor spacing guide.
    /// </summary>
    public void HideSpacingGuide()
    {
        _guideSphereModel.Visibility = Visibility.Collapsed;
        _guideCircleModel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the editable polyline point handles and sizes them from the active spacing setting.
    /// </summary>
    public void ShowPointHandles(IReadOnlyList<Vector3> points, float handleDiameter)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        int handleCount = global::System.Math.Min(points.Count, LineSupportPattern.MaximumSupportCount);
        double diameter = global::System.Math.Max(handleDiameter, MinimumGuideDiameter);
        EnsurePointHandleCapacity(handleCount);

        for (int i = 0; i < _pointHandleModels.Count; i++)
        {
            if (i < handleCount)
            {
                ApplySphereTransform(_pointHandleScales[i], _pointHandleTranslations[i], points[i], diameter);
                _pointHandleModels[i].Visibility = Visibility.Visible;
            }
            else
            {
                _pointHandleModels[i].Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// Returns the point index represented by one hit-tested preview model.
    /// </summary>
    public bool TryGetPointHandleIndex(Element3D hitModel, out int pointIndex)
    {
        for (int i = 0; i < _pointHandleModels.Count; i++)
        {
            if (ReferenceEquals(hitModel, _pointHandleModels[i]) && _pointHandleModels[i].Visibility == Visibility.Visible)
            {
                pointIndex = i;
                return true;
            }
        }

        pointIndex = -1;
        return false;
    }

    /// <summary>
    /// Hides all editable polyline point handles.
    /// </summary>
    public void HidePointHandles()
    {
        for (int i = 0; i < _pointHandleModels.Count; i++)
        {
            _pointHandleModels[i].Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Hides all Line Support preview geometry.
    /// </summary>
    public void Hide()
    {
        _polylineModel.Visibility = Visibility.Collapsed;
        _markerModel.Visibility = Visibility.Collapsed;
        _guideSphereModel.Visibility = Visibility.Collapsed;
        _guideCircleModel.Visibility = Visibility.Collapsed;
        HidePointHandles();
    }

    /// <summary>
    /// Creates fixed line topology so only vertex positions change during preview.
    /// </summary>
    private static LineGeometry3D CreatePolylineGeometry()
    {
        LineGeometry3D geometry = new LineGeometry3D
        {
            Positions = new Vector3Collection(MaximumPolylineSegmentCount * 2),
            Indices = new IntCollection(MaximumPolylineSegmentCount * 2)
        };

        for (int i = 0; i < MaximumPolylineSegmentCount; i++)
        {
            int baseIndex = i * 2;
            geometry.Positions.Add(Vector3.Zero);
            geometry.Positions.Add(Vector3.Zero);
            geometry.Indices.Add(baseIndex);
            geometry.Indices.Add(baseIndex + 1);
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
    /// Creates fixed circle topology for the XY spacing guide.
    /// </summary>
    private static LineGeometry3D CreateGuideCircleGeometry()
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
    /// Creates one reusable transparent sphere visual for cursor spacing feedback.
    /// </summary>
    private static MeshGeometryModel3D CreateGuideSphereModel(ScaleTransform3D scale, TranslateTransform3D translation)
    {
        Transform3DGroup transform = new Transform3DGroup();
        transform.Children.Add(scale);
        transform.Children.Add(translation);
        Color4 color = new Color4(0.0f, 0.75f, 1.0f, GuideOpacity);

        return new MeshGeometryModel3D
        {
            Geometry = CreateSphereGeometry(),
            Material = CreateTransparentMaterial(color),
            Name = "LineSupportSpacingGuide",
            Transform = transform,
            CullMode = SharpDX.Direct3D11.CullMode.None,
            IsTransparent = true,
            Visibility = Visibility.Collapsed
        };
    }

    /// <summary>
    /// Ensures the renderer has enough reusable point handle visuals for the current polyline.
    /// </summary>
    private void EnsurePointHandleCapacity(int handleCount)
    {
        while (_pointHandleModels.Count < handleCount)
        {
            ScaleTransform3D scale = new ScaleTransform3D(1.0, 1.0, 1.0);
            TranslateTransform3D translation = new TranslateTransform3D();
            MeshGeometryModel3D pointHandleModel = CreatePointHandleModel(scale, translation);
            _pointHandleScales.Add(scale);
            _pointHandleTranslations.Add(translation);
            _pointHandleModels.Add(pointHandleModel);
            _pointHandleTopMostRoot.Children.Add(pointHandleModel);
        }
    }

    /// <summary>
    /// Creates one reusable editable polyline point handle visual.
    /// </summary>
    private MeshGeometryModel3D CreatePointHandleModel(ScaleTransform3D scale, TranslateTransform3D translation)
    {
        Transform3DGroup transform = new Transform3DGroup();
        transform.Children.Add(scale);
        transform.Children.Add(translation);

        return new MeshGeometryModel3D
        {
            Geometry = _pointHandleGeometry,
            Material = _pointHandleMaterial,
            Name = "LineSupportPointHandle",
            Transform = transform,
            CullMode = SharpDX.Direct3D11.CullMode.None,
            IsTransparent = true,
            Visibility = Visibility.Collapsed
        };
    }

    /// <summary>
    /// Creates one unit-diameter sphere mesh reused by spacing and point handle visuals.
    /// </summary>
    private static SharpDxMeshGeometry3D CreateSphereGeometry()
    {
        MeshBuilder builder = new MeshBuilder();
        builder.AddSphere(Vector3.Zero, 0.5f);
        return builder.ToMeshGeometry3D();
    }

    /// <summary>
    /// Creates a translucent material for spacing and editable point handle previews.
    /// </summary>
    private static PhongMaterial CreateTransparentMaterial(Color4 color)
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
    /// Updates one reusable sphere transform without rebuilding its mesh.
    /// </summary>
    private static void ApplySphereTransform(
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
    /// Updates the XY guide circle while preserving its fixed line buffer.
    /// </summary>
    private void UpdateGuideCircle(Vector3 center, float radius)
    {
        Vector3Collection positions = _guideCircleGeometry.Positions!;

        for (int segmentIndex = 0; segmentIndex < CircleSegmentCount; segmentIndex++)
        {
            float angle = (float)(segmentIndex * global::System.Math.PI * 2.0 / CircleSegmentCount);
            float x = center.X + (float)global::System.Math.Cos(angle) * radius;
            float y = center.Y + (float)global::System.Math.Sin(angle) * radius;
            positions[segmentIndex] = new Vector3(x, y, center.Z);
        }

        _guideCircleGeometry.UpdateVertices();
        _guideCircleGeometry.UpdateBounds();
    }

    /// <summary>
    /// Counts visible preview segments from committed points plus an optional hover point.
    /// </summary>
    private static int CalculateVisibleSegmentCount(IReadOnlyList<Vector3> points, Vector3? hoverPoint)
    {
        int pointCount = points.Count + (hoverPoint.HasValue ? 1 : 0);

        if (pointCount < 2)
        {
            return 0;
        }

        return global::System.Math.Min(pointCount - 1, MaximumPolylineSegmentCount);
    }

    /// <summary>
    /// Reads one point from the combined committed-plus-hover preview sequence.
    /// </summary>
    private static Vector3 GetPreviewPoint(IReadOnlyList<Vector3> points, Vector3? hoverPoint, int index)
    {
        if (index < points.Count)
        {
            return points[index];
        }

        return hoverPoint ?? points[points.Count - 1];
    }
}
