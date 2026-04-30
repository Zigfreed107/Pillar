// CircleSupportPreviewRenderer.cs
// Draws transient circle-support guide geometry in the viewport without mutating the CAD document.
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Media;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Renders the Circle Support tool's temporary circle outline and projected support markers.
/// </summary>
public sealed class CircleSupportPreviewRenderer
{
    private const int CircleSegmentCount = 96;
    private const int MaximumMarkerCount = 512;
    private const float MarkerHalfSize = 0.18f;

    private readonly LineGeometry3D _circleGeometry;
    private readonly LineGeometryModel3D _circleModel;
    private readonly LineGeometry3D _markerGeometry;
    private readonly LineGeometryModel3D _markerModel;

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

        _markerGeometry = CreateMarkerGeometry();
        _markerModel = new LineGeometryModel3D
        {
            Geometry = _markerGeometry,
            Color = Colors.DeepSkyBlue,
            Thickness = 2.0f,
            Visibility = Visibility.Collapsed
        };

        sceneRoot.Children.Add(_circleModel);
        sceneRoot.Children.Add(_markerModel);
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
    /// Hides all Circle Support preview geometry.
    /// </summary>
    public void Hide()
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
}
