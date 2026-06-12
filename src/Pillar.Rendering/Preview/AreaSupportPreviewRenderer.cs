// AreaSupportPreviewRenderer.cs
// Draws transient Area Support guide geometry in the viewport without mutating the CAD document.
using HelixToolkit;
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
/// Renders the Area Support tool's temporary boundary outlines, support markers, and optional spacing circles.
/// </summary>
public sealed class AreaSupportPreviewRenderer
{
    private const int MaximumBoundarySegmentCount = AreaSupportPattern.MaximumBoundarySegmentCount;
    private const int MaximumMarkerCount = AreaSupportPattern.MaximumSupportCount;
    private const int CircleSegmentCount = 32;
    private const float MarkerHalfSize = 0.18f;

    private readonly LineGeometry3D _boundaryGeometry;
    private readonly LineGeometryModel3D _boundaryModel;
    private readonly TopMostGroup3D _boundaryTopMostRoot;
    private readonly LineGeometry3D _spacingCircleGeometry;
    private readonly LineGeometryModel3D _spacingCircleModel;
    private readonly TopMostGroup3D _spacingCircleTopMostRoot;
    private readonly LineGeometry3D _markerGeometry;
    private readonly LineGeometryModel3D _markerModel;
    private readonly TopMostGroup3D _markerTopMostRoot;

    /// <summary>
    /// Creates the reusable preview geometry and attaches it to the supplied preview scene root.
    /// </summary>
    public AreaSupportPreviewRenderer(GroupModel3D sceneRoot)
    {
        _boundaryGeometry = CreatePairedLineGeometry(MaximumBoundarySegmentCount * 2);
        _boundaryModel = new LineGeometryModel3D
        {
            Geometry = _boundaryGeometry,
            Color = Colors.Goldenrod,
            Thickness = 2.0f,
            Visibility = Visibility.Collapsed
        };
        _boundaryTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _boundaryTopMostRoot.Children.Add(_boundaryModel);

        _spacingCircleGeometry = CreateCircleLineGeometry();
        _spacingCircleModel = new LineGeometryModel3D
        {
            Geometry = _spacingCircleGeometry,
            Color = Color.FromArgb(96, 0, 191, 255),
            Thickness = 1.0f,
            Visibility = Visibility.Collapsed
        };
        _spacingCircleTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _spacingCircleTopMostRoot.Children.Add(_spacingCircleModel);

        _markerGeometry = CreatePairedLineGeometry(MaximumMarkerCount * 4);
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

        sceneRoot.Children.Add(_boundaryTopMostRoot);
        sceneRoot.Children.Add(_spacingCircleTopMostRoot);
        sceneRoot.Children.Add(_markerTopMostRoot);
    }

    /// <summary>
    /// Updates all Area Support preview geometry from an area support result.
    /// </summary>
    public void Show(AreaSupportResult areaSupportResult, float spacing, bool showSupportSpacing)
    {
        if (areaSupportResult == null)
        {
            throw new ArgumentNullException(nameof(areaSupportResult));
        }

        ShowBoundary(areaSupportResult.BoundarySegments);
        ShowMarkers(areaSupportResult.SupportSamples);
        ShowSpacingCircles(areaSupportResult.SupportSamples, spacing, showSupportSpacing);
    }

    /// <summary>
    /// Hides all Area Support preview geometry.
    /// </summary>
    public void Hide()
    {
        _boundaryModel.Visibility = Visibility.Collapsed;
        _spacingCircleModel.Visibility = Visibility.Collapsed;
        _markerModel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the yellow selected-area boundary line preview.
    /// </summary>
    private void ShowBoundary(IReadOnlyList<AreaSupportBoundarySegment> boundarySegments)
    {
        int segmentCount = global::System.Math.Min(boundarySegments.Count, MaximumBoundarySegmentCount);
        Vector3Collection positions = _boundaryGeometry.Positions!;

        for (int segmentIndex = 0; segmentIndex < MaximumBoundarySegmentCount; segmentIndex++)
        {
            int baseIndex = segmentIndex * 2;

            if (segmentIndex < segmentCount)
            {
                positions[baseIndex] = boundarySegments[segmentIndex].Start;
                positions[baseIndex + 1] = boundarySegments[segmentIndex].End;
            }
            else
            {
                positions[baseIndex] = Vector3.Zero;
                positions[baseIndex + 1] = Vector3.Zero;
            }
        }

        UpdateGeometry(_boundaryGeometry);
        _boundaryModel.Visibility = segmentCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the blue cross marker preview.
    /// </summary>
    private void ShowMarkers(IReadOnlyList<AreaSupportSample> markerSamples)
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

        UpdateGeometry(_markerGeometry);
        _markerModel.Visibility = markerCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Updates optional transparent spacing circles around each support preview marker.
    /// </summary>
    private void ShowSpacingCircles(IReadOnlyList<AreaSupportSample> markerSamples, float spacing, bool showSupportSpacing)
    {
        int markerCount = showSupportSpacing ? global::System.Math.Min(markerSamples.Count, MaximumMarkerCount) : 0;
        float radius = global::System.Math.Max(spacing, 0.0f);
        Vector3Collection positions = _spacingCircleGeometry.Positions!;

        for (int markerIndex = 0; markerIndex < MaximumMarkerCount; markerIndex++)
        {
            Vector3 center = markerIndex < markerCount ? markerSamples[markerIndex].Position : Vector3.Zero;
            int baseIndex = markerIndex * CircleSegmentCount;

            for (int segmentIndex = 0; segmentIndex < CircleSegmentCount; segmentIndex++)
            {
                if (markerIndex >= markerCount)
                {
                    positions[baseIndex + segmentIndex] = Vector3.Zero;
                    continue;
                }

                float angle = (float)(segmentIndex * global::System.Math.PI * 2.0 / CircleSegmentCount);
                positions[baseIndex + segmentIndex] = new Vector3(
                    center.X + (float)global::System.Math.Cos(angle) * radius,
                    center.Y + (float)global::System.Math.Sin(angle) * radius,
                    center.Z);
            }
        }

        UpdateGeometry(_spacingCircleGeometry);
        _spacingCircleModel.Visibility = markerCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Creates fixed line topology so only vertex positions change during preview.
    /// </summary>
    private static LineGeometry3D CreatePairedLineGeometry(int positionCount)
    {
        LineGeometry3D geometry = new LineGeometry3D
        {
            Positions = new Vector3Collection(positionCount),
            Indices = new IntCollection(positionCount)
        };

        for (int i = 0; i < positionCount; i++)
        {
            geometry.Positions.Add(Vector3.Zero);
            geometry.Indices.Add(i);
        }

        return geometry;
    }

    /// <summary>
    /// Creates fixed circle topology for every possible spacing marker.
    /// </summary>
    private static LineGeometry3D CreateCircleLineGeometry()
    {
        LineGeometry3D geometry = new LineGeometry3D
        {
            Positions = new Vector3Collection(MaximumMarkerCount * CircleSegmentCount),
            Indices = new IntCollection(MaximumMarkerCount * CircleSegmentCount * 2)
        };

        for (int markerIndex = 0; markerIndex < MaximumMarkerCount; markerIndex++)
        {
            int baseIndex = markerIndex * CircleSegmentCount;

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
    /// Pushes updated line vertices and bounds into Helix.
    /// </summary>
    private static void UpdateGeometry(LineGeometry3D geometry)
    {
        geometry.UpdateVertices();
        geometry.UpdateBounds();
    }
}
