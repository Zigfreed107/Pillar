// ScaleOriginPreviewRenderer.cs
// Draws the visual-only viewport marker for the Transform Scale tool without owning document state.
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System.Numerics;
using System.Windows;

namespace Pillar.Rendering.Preview;

/// <summary>
/// Renders a small non-interactive crosshair at the active model scaling origin.
/// </summary>
public sealed class ScaleOriginPreviewRenderer
{
    private const int AxisLineCount = 3;
    private const float LineThickness = 2.0f;

    private readonly LineGeometryModel3D _originModel;
    private readonly LineGeometry3D _geometry;
    private readonly Vector3Collection _positions;

    /// <summary>
    /// Creates the reusable scale-origin preview geometry and adds it to the preview scene root.
    /// </summary>
    public ScaleOriginPreviewRenderer(GroupModel3D sceneRoot)
    {
        _positions = new Vector3Collection(AxisLineCount * 2);
        _geometry = new LineGeometry3D
        {
            Positions = _positions,
            Indices = new IntCollection()
        };

        for (int i = 0; i < AxisLineCount * 2; i++)
        {
            _positions.Add(Vector3.Zero);
            _geometry.Indices.Add(i);
        }

        _originModel = new LineGeometryModel3D
        {
            Geometry = _geometry,
            Color = System.Windows.Media.Color.FromRgb(0, 122, 204),
            Thickness = LineThickness,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        sceneRoot.Children.Add(_originModel);
    }

    /// <summary>
    /// Updates and shows the origin marker around the supplied world-space point.
    /// </summary>
    public void Show(Vector3 origin, float radius)
    {
        float safeRadius = radius > 0.0f && !float.IsNaN(radius) && !float.IsInfinity(radius)
            ? radius
            : 1.0f;

        _positions[0] = new Vector3(origin.X - safeRadius, origin.Y, origin.Z);
        _positions[1] = new Vector3(origin.X + safeRadius, origin.Y, origin.Z);
        _positions[2] = new Vector3(origin.X, origin.Y - safeRadius, origin.Z);
        _positions[3] = new Vector3(origin.X, origin.Y + safeRadius, origin.Z);
        _positions[4] = new Vector3(origin.X, origin.Y, origin.Z - safeRadius);
        _positions[5] = new Vector3(origin.X, origin.Y, origin.Z + safeRadius);
        _geometry.UpdateVertices();
        _geometry.UpdateBounds();
        _originModel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides the marker when the Scale tool closes or loses a valid target.
    /// </summary>
    public void Hide()
    {
        _originModel.Visibility = Visibility.Collapsed;
    }
}
