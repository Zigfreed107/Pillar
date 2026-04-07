using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;

using System.Numerics;

/// <summary>
/// Handles rendering of the interactive preview line.
/// </summary>
public class PreviewLineRenderer
{
    private readonly LineGeometryModel3D _lineModel;
    private readonly LineGeometry3D _geometry;

    public PreviewLineRenderer(GroupModel3D sceneRoot)
    {
        _geometry = new LineGeometry3D
        {
            Positions = new Vector3Collection(2),
            Indices = new IntCollection { 0, 1 }
        };

        // Pre-allocate positions
        _geometry.Positions.Add(new Vector3());
        _geometry.Positions.Add(new Vector3());

        _lineModel = new LineGeometryModel3D
        {
            Geometry = _geometry,
            Color = System.Windows.Media.Colors.Yellow,
            Thickness = 2.0f,
            Visibility = System.Windows.Visibility.Collapsed
        };

        sceneRoot.Children.Add(_lineModel);
    }

    /// <summary>
    /// Updates the preview line positions.
    /// </summary>
    public void Show(Vector3 start, Vector3 end)
    {
        _geometry.Positions[0] = start;
        _geometry.Positions[1] = end;

        _lineModel.Visibility = System.Windows.Visibility.Visible;
    }

    /// <summary>
    /// Hides the preview line.
    /// </summary>
    public void Hide()
    {
        _lineModel.Visibility = System.Windows.Visibility.Collapsed;
    }
}