// LineRenderer.cs
// Rendering-layer factory for CAD line visuals with a base pass and a selectable highlight overlay.
using Pillar.Core.Entities;
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System.Numerics;
using System.Windows;
using System.Windows.Media;

namespace Pillar.Rendering.EntityRenderers;

public static class LineRenderer
{
    private const string SelectionOverlayName = "SelectionOverlay";
    private const double DefaultSelectionOutlineThickness = 4.0;

    /// <summary>
    /// Creates a composite visual for a line entity.
    /// Child 0: Base line (always visible).
    /// Child 1: Selection overlay (toggled by SceneManager).
    /// </summary>
    public static GroupModel3D Create(LineEntity line)
    {
        return Create(line, Color.FromRgb(255, 215, 0));
    }

    /// <summary>
    /// Creates a composite visual for a line entity with an explicit selection overlay color.
    /// </summary>
    public static GroupModel3D Create(LineEntity line, Color selectionOutlineColor)
    {
        return Create(line, selectionOutlineColor, DefaultSelectionOutlineThickness);
    }

    /// <summary>
    /// Creates a composite visual for a line entity with explicit selection overlay styling.
    /// </summary>
    public static GroupModel3D Create(LineEntity line, Color selectionOutlineColor, double selectionOutlineThickness)
    {
        LineBuilder builder = new LineBuilder();
        builder.AddLine(
            new Vector3(line.Start.X, line.Start.Y, line.Start.Z),
            new Vector3(line.End.X, line.End.Y, line.End.Z));

        LineGeometry3D geometry = builder.ToLineGeometry3D();

        LineGeometryModel3D baseLine = new LineGeometryModel3D
        {
            Geometry = geometry,
            Color = Color.FromRgb(0, 0, 255),
            Thickness = 1.0,
            Smoothness = 1,
            DepthBias = -100,
            SlopeScaledDepthBias = -1.0f
        };

        LineGeometryModel3D selectionOverlay = new LineGeometryModel3D
        {
            Name = SelectionOverlayName,
            Geometry = geometry,
            Color = selectionOutlineColor,
            Thickness = selectionOutlineThickness > 0.0 ? selectionOutlineThickness : DefaultSelectionOutlineThickness,
            Smoothness = 1,
            DepthBias = 1000,
            SlopeScaledDepthBias = -1.5f,
            Visibility = Visibility.Hidden
        };

        return

            new GroupModel3D()
            {
                Children = { baseLine, selectionOverlay }
            };

    }

    public static Element3D? GetSelectionOverlay(GroupModel3D line)
    {
        if (line.Children.Count <= 1)
        {
            return null;
        }

        if (line.Children[1] is LineGeometryModel3D selectionOverlay
            && string.Equals(selectionOverlay.Name, SelectionOverlayName, System.StringComparison.Ordinal))
        {
            return selectionOverlay;
        }

        return null;
    }
}
