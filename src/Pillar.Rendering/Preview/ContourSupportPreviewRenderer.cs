// ContourSupportPreviewRenderer.cs
// Draws transient Contour Support guide geometry in the viewport without mutating the CAD document.
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

namespace Pillar.Rendering.Preview;

/// <summary>
/// Renders the Contour Support tool's temporary contour polyline and projected support markers.
/// </summary>
public sealed class ContourSupportPreviewRenderer
{
    private const int MaximumContourSegmentCount = ContourSupportPattern.MaximumSupportCount;
    private const int MaximumMarkerCount = ContourSupportPattern.MaximumSupportCount;
    private const float MarkerHalfSize = 0.18f;

    private readonly LineGeometry3D _contourGeometry;
    private readonly LineGeometryModel3D _contourModel;
    private readonly TopMostGroup3D _contourTopMostRoot;
    private readonly LineGeometry3D _markerGeometry;
    private readonly LineGeometryModel3D _markerModel;
    private readonly TopMostGroup3D _markerTopMostRoot;

    /// <summary>
    /// Creates the reusable preview geometry and attaches it to the supplied preview scene root.
    /// </summary>
    public ContourSupportPreviewRenderer(GroupModel3D sceneRoot)
    {
        _contourGeometry = CreateContourGeometry();
        _contourModel = new LineGeometryModel3D
        {
            Geometry = _contourGeometry,
            Color = Colors.Goldenrod,
            Thickness = 2.0f,
            Visibility = Visibility.Collapsed
        };
        _contourTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _contourTopMostRoot.Children.Add(_contourModel);

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

        sceneRoot.Children.Add(_contourTopMostRoot);
        sceneRoot.Children.Add(_markerTopMostRoot);
    }

    /// <summary>
    /// Updates the contour and marker previews from a contour extraction result.
    /// </summary>
    public void Show(ContourSupportResult contourResult)
    {
        if (contourResult == null)
        {
            throw new ArgumentNullException(nameof(contourResult));
        }

        ShowContour(contourResult.ContourPoints, contourResult.IsClosed);
        ShowMarkers(contourResult.SupportSamples);
    }

    /// <summary>
    /// Hides all Contour Support preview geometry.
    /// </summary>
    public void Hide()
    {
        _contourModel.Visibility = Visibility.Collapsed;
        _markerModel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the yellow contour line preview.
    /// </summary>
    private void ShowContour(IReadOnlyList<Vector3> points, bool isClosed)
    {
        int segmentCount = CalculateSegmentCount(points, isClosed);
        Vector3Collection positions = _contourGeometry.Positions!;

        for (int segmentIndex = 0; segmentIndex < MaximumContourSegmentCount; segmentIndex++)
        {
            int baseIndex = segmentIndex * 2;

            if (segmentIndex < segmentCount)
            {
                Vector3 start = points[segmentIndex];
                Vector3 end = segmentIndex == points.Count - 1
                    ? points[0]
                    : points[segmentIndex + 1];
                positions[baseIndex] = start;
                positions[baseIndex + 1] = end;
            }
            else
            {
                positions[baseIndex] = Vector3.Zero;
                positions[baseIndex + 1] = Vector3.Zero;
            }
        }

        _contourGeometry.UpdateVertices();
        _contourGeometry.UpdateBounds();
        _contourModel.Visibility = segmentCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the blue cross marker preview.
    /// </summary>
    private void ShowMarkers(IReadOnlyList<ContourSupportSample> markerSamples)
    {
        int markerCount = global::System.Math.Min(markerSamples.Count, MaximumMarkerCount);
        Vector3Collection positions = _markerGeometry.Positions!;

        for (int i = 0; i < MaximumMarkerCount; i++)
        {
            Vector3 position = i < markerCount ? markerSamples[i].Position : Vector3.Zero;
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
    /// Creates fixed contour line topology so only vertex positions change during preview.
    /// </summary>
    private static LineGeometry3D CreateContourGeometry()
    {
        LineGeometry3D geometry = new LineGeometry3D
        {
            Positions = new Vector3Collection(MaximumContourSegmentCount * 2),
            Indices = new IntCollection(MaximumContourSegmentCount * 2)
        };

        for (int i = 0; i < MaximumContourSegmentCount; i++)
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
    /// Counts the visible contour segments.
    /// </summary>
    private static int CalculateSegmentCount(IReadOnlyList<Vector3> points, bool isClosed)
    {
        if (points.Count < 2)
        {
            return 0;
        }

        int segmentCount = isClosed ? points.Count : points.Count - 1;
        return global::System.Math.Min(segmentCount, MaximumContourSegmentCount);
    }
}
