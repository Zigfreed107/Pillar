// ScaledCursorPreviewRenderer.cs
// Draws the visual-only scaled cursor guide circle without adding document entities or participating in selection.
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Renders a reusable horizontal circle that follows the mouse on the build plate.
/// </summary>
public sealed class ScaledCursorPreviewRenderer
{
    private const int CircleSegmentCount = 96;
    private const float DefaultDiameter = 10.0f;
    private const float MinimumDiameter = 0.001f;
    private const float LineThickness = 2.0f;

    private readonly LineGeometry3D _geometry;
    private readonly LineGeometryModel3D _circleModel;
    private readonly Vector3Collection _positions;

    /// <summary>
    /// Creates fixed circle topology and attaches it to the shared preview scene root.
    /// </summary>
    public ScaledCursorPreviewRenderer(GroupModel3D sceneRoot)
    {
        _positions = new Vector3Collection(CircleSegmentCount);
        _geometry = new LineGeometry3D
        {
            Positions = _positions,
            Indices = new IntCollection(CircleSegmentCount * 2)
        };

        for (int i = 0; i < CircleSegmentCount; i++)
        {
            _positions.Add(Vector3.Zero);
            _geometry.Indices.Add(i);
            _geometry.Indices.Add((i + 1) % CircleSegmentCount);
        }

        _circleModel = new LineGeometryModel3D
        {
            Geometry = _geometry,
            Color = Colors.DeepSkyBlue,
            Thickness = LineThickness,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        TopMostGroup3D topMostRoot = new TopMostGroup3D
        {
            EnableTopMost = true
        };
        topMostRoot.Children.Add(_circleModel);
        sceneRoot.Children.Add(topMostRoot);
    }

    /// <summary>
    /// Updates the guide circle around the supplied center using a horizontal XY-plane orientation.
    /// </summary>
    public void Show(Vector3 center, float diameter, Color color)
    {
        float safeDiameter = IsFinitePositive(diameter) ? diameter : DefaultDiameter;
        float radius = global::System.Math.Max(safeDiameter, MinimumDiameter) * 0.5f;

        for (int i = 0; i < CircleSegmentCount; i++)
        {
            float angle = (float)(i * global::System.Math.PI * 2.0 / CircleSegmentCount);
            float x = center.X + (float)global::System.Math.Cos(angle) * radius;
            float y = center.Y + (float)global::System.Math.Sin(angle) * radius;
            _positions[i] = new Vector3(x, y, center.Z);
        }

        _circleModel.Color = color;
        _geometry.UpdateVertices();
        _geometry.UpdateBounds();
        _circleModel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides the guide circle while keeping its reusable geometry available for the next mouse move.
    /// </summary>
    public void Hide()
    {
        _circleModel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Rejects invalid diameter values before they reach geometry updates.
    /// </summary>
    private static bool IsFinitePositive(float value)
    {
        return value > 0.0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
