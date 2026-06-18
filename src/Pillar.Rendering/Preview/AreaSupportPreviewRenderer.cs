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
using Color4 = HelixToolkit.Maths.Color4;

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
    private const float SpacingDiscRed = 0.0f;
    private const float SpacingDiscGreen = 0.75f;
    private const float SpacingDiscBlue = 1.0f;
    private const float SpacingDiscOpacity = 0.2f;

    private readonly LineGeometry3D _boundaryGeometry;
    private readonly LineGeometryModel3D _boundaryModel;
    private readonly TopMostGroup3D _boundaryTopMostRoot;
    private readonly LineGeometry3D _offsetBoundaryGeometry;
    private readonly LineGeometryModel3D _offsetBoundaryModel;
    private readonly TopMostGroup3D _offsetBoundaryTopMostRoot;
    private readonly LineGeometry3D _spacingCircleGeometry;
    private readonly LineGeometryModel3D _spacingCircleModel;
    private readonly MeshGeometry3D _spacingDiscGeometry;
    private readonly MeshGeometryModel3D _spacingDiscModel;
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

        _offsetBoundaryGeometry = CreatePairedLineGeometry(MaximumBoundarySegmentCount * 2);
        _offsetBoundaryModel = new LineGeometryModel3D
        {
            Geometry = _offsetBoundaryGeometry,
            Color = Colors.Yellow,
            Thickness = 1.5f,
            Visibility = Visibility.Collapsed
        };
        _offsetBoundaryTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _offsetBoundaryTopMostRoot.Children.Add(_offsetBoundaryModel);

        _spacingCircleGeometry = CreateCircleLineGeometry();
        _spacingCircleModel = new LineGeometryModel3D
        {
            Geometry = _spacingCircleGeometry,
            Color = Color.FromArgb(96, 0, 191, 255),
            Thickness = 1.0f,
            Visibility = Visibility.Collapsed
        };
        _spacingDiscGeometry = CreateSpacingDiscGeometry();
        _spacingDiscModel = new MeshGeometryModel3D
        {
            Geometry = _spacingDiscGeometry,
            Material = CreateSpacingDiscMaterial(),
            Name = "AreaSupportSpacingDisc",
            CullMode = SharpDX.Direct3D11.CullMode.None,
            IsTransparent = true,
            Visibility = Visibility.Collapsed
        };
        _spacingCircleTopMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        _spacingCircleTopMostRoot.Children.Add(_spacingDiscModel);
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
        sceneRoot.Children.Add(_offsetBoundaryTopMostRoot);
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
        ShowOffsetBoundary(areaSupportResult.OffsetBoundarySegments);
        ShowMarkers(areaSupportResult.SupportSamples);
        ShowSpacingCircles(areaSupportResult.SupportSamples, spacing, showSupportSpacing);
    }

    /// <summary>
    /// Hides all Area Support preview geometry.
    /// </summary>
    public void Hide()
    {
        _boundaryModel.Visibility = Visibility.Collapsed;
        _offsetBoundaryModel.Visibility = Visibility.Collapsed;
        _spacingDiscModel.Visibility = Visibility.Collapsed;
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
    /// Updates the yellow line that shows the inward offset boundary used by candidate filtering.
    /// </summary>
    private void ShowOffsetBoundary(IReadOnlyList<AreaSupportBoundarySegment> offsetBoundarySegments)
    {
        int segmentCount = global::System.Math.Min(offsetBoundarySegments.Count, MaximumBoundarySegmentCount);
        Vector3Collection positions = _offsetBoundaryGeometry.Positions!;

        for (int segmentIndex = 0; segmentIndex < MaximumBoundarySegmentCount; segmentIndex++)
        {
            int baseIndex = segmentIndex * 2;

            if (segmentIndex < segmentCount)
            {
                positions[baseIndex] = offsetBoundarySegments[segmentIndex].Start;
                positions[baseIndex + 1] = offsetBoundarySegments[segmentIndex].End;
            }
            else
            {
                positions[baseIndex] = Vector3.Zero;
                positions[baseIndex + 1] = Vector3.Zero;
            }
        }

        UpdateGeometry(_offsetBoundaryGeometry);
        _offsetBoundaryModel.Visibility = segmentCount > 0 ? Visibility.Visible : Visibility.Collapsed;
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
        Vector3Collection circlePositions = _spacingCircleGeometry.Positions!;
        Vector3Collection discPositions = _spacingDiscGeometry.Positions!;

        for (int markerIndex = 0; markerIndex < MaximumMarkerCount; markerIndex++)
        {
            Vector3 center = markerIndex < markerCount ? markerSamples[markerIndex].Position : Vector3.Zero;
            int circleBaseIndex = markerIndex * CircleSegmentCount;
            int discBaseIndex = markerIndex * (CircleSegmentCount + 1);
            discPositions[discBaseIndex] = center;

            for (int segmentIndex = 0; segmentIndex < CircleSegmentCount; segmentIndex++)
            {
                if (markerIndex >= markerCount)
                {
                    circlePositions[circleBaseIndex + segmentIndex] = Vector3.Zero;
                    discPositions[discBaseIndex + segmentIndex + 1] = Vector3.Zero;
                    continue;
                }

                float angle = (float)(segmentIndex * global::System.Math.PI * 2.0 / CircleSegmentCount);
                Vector3 perimeterPosition = new Vector3(
                    center.X + (float)global::System.Math.Cos(angle) * radius,
                    center.Y + (float)global::System.Math.Sin(angle) * radius,
                    center.Z);
                circlePositions[circleBaseIndex + segmentIndex] = perimeterPosition;
                discPositions[discBaseIndex + segmentIndex + 1] = perimeterPosition;
            }
        }

        UpdateGeometry(_spacingCircleGeometry);
        UpdateGeometry(_spacingDiscGeometry);
        _spacingDiscModel.Visibility = markerCount > 0 ? Visibility.Visible : Visibility.Collapsed;
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
    /// Creates fixed triangle-fan topology for the translucent interior of every spacing circle.
    /// </summary>
    private static MeshGeometry3D CreateSpacingDiscGeometry()
    {
        int verticesPerDisc = CircleSegmentCount + 1;
        MeshGeometry3D geometry = new MeshGeometry3D
        {
            Positions = new Vector3Collection(MaximumMarkerCount * verticesPerDisc),
            Indices = new IntCollection(MaximumMarkerCount * CircleSegmentCount * 3),
            Normals = new Vector3Collection(MaximumMarkerCount * verticesPerDisc)
        };

        for (int markerIndex = 0; markerIndex < MaximumMarkerCount; markerIndex++)
        {
            int baseIndex = markerIndex * verticesPerDisc;

            for (int vertexIndex = 0; vertexIndex < verticesPerDisc; vertexIndex++)
            {
                geometry.Positions.Add(Vector3.Zero);
                geometry.Normals.Add(Vector3.UnitZ);
            }

            for (int segmentIndex = 0; segmentIndex < CircleSegmentCount; segmentIndex++)
            {
                geometry.Indices.Add(baseIndex);
                geometry.Indices.Add(baseIndex + segmentIndex + 1);
                geometry.Indices.Add(baseIndex + ((segmentIndex + 1) % CircleSegmentCount) + 1);
            }
        }

        return geometry;
    }

    /// <summary>
    /// Creates the translucent cyan material used for support-spacing disc interiors.
    /// </summary>
    private static PhongMaterial CreateSpacingDiscMaterial()
    {
        Color4 color = new Color4(SpacingDiscRed, SpacingDiscGreen, SpacingDiscBlue, SpacingDiscOpacity);

        return new PhongMaterial
        {
            AmbientColor = color,
            DiffuseColor = color,
            SpecularColor = new Color4(0.0f, 0.0f, 0.0f, SpacingDiscOpacity),
            SpecularShininess = 1.0f
        };
    }

    /// <summary>
    /// Pushes updated line vertices and bounds into Helix.
    /// </summary>
    private static void UpdateGeometry(LineGeometry3D geometry)
    {
        geometry.UpdateVertices();
        geometry.UpdateBounds();
    }

    /// <summary>
    /// Pushes updated mesh vertices and bounds into Helix.
    /// </summary>
    private static void UpdateGeometry(MeshGeometry3D geometry)
    {
        geometry.UpdateVertices();
        geometry.UpdateBounds();
    }
}
