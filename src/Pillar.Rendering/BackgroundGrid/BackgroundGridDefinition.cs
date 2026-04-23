// BackgroundGridDefinition.cs
// Defines the configurable background-grid dimensions and styling so rendering and camera framing share one source of truth.
using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Pillar.Rendering.BackgroundGrid;

/// <summary>
/// Describes the size and appearance of the background build grid.
/// </summary>
public sealed class BackgroundGridDefinition
{
    /// <summary>
    /// Gets the default grid definition used by the current workspace.
    /// </summary>
    public static BackgroundGridDefinition Default { get; } = new BackgroundGridDefinition(
        width: 126.0f,
        height: 223.0f,
        spacing: 10.0f,
        outlineOffset: 5.0f,
        gridColor: Color.FromRgb(225, 225, 225),
        gridThickness: 0.5f,
        borderColor: Color.FromRgb(200, 200, 200),
        borderThickness: 1.0f,
        doubleBorderColor: Color.FromRgb(200, 200, 200),
        doubleBorderThickness: 2.5f,
        originColor: Color.FromRgb(100, 0, 0),
        originThickness: 1.5f);

    /// <summary>
    /// Initializes one background-grid definition.
    /// </summary>
    public BackgroundGridDefinition(
        float width,
        float height,
        float spacing,
        float outlineOffset,
        Color gridColor,
        float gridThickness,
        Color borderColor,
        float borderThickness,
        Color doubleBorderColor,
        float doubleBorderThickness,
        Color originColor,
        float originThickness)
    {
        if (width <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Grid width must be greater than zero.");
        }

        if (height <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Grid height must be greater than zero.");
        }

        if (spacing <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), "Grid spacing must be greater than zero.");
        }

        if (outlineOffset < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(outlineOffset), "Grid outline offset cannot be negative.");
        }

        if (gridThickness <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(gridThickness), "Grid thickness must be greater than zero.");
        }

        if (borderThickness <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(borderThickness), "Border thickness must be greater than zero.");
        }

        if (doubleBorderThickness <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(doubleBorderThickness), "Double-border thickness must be greater than zero.");
        }

        if (originThickness <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(originThickness), "Origin thickness must be greater than zero.");
        }

        Width = width;
        Height = height;
        Spacing = spacing;
        OutlineOffset = outlineOffset;
        GridColor = gridColor;
        GridThickness = gridThickness;
        BorderColor = borderColor;
        BorderThickness = borderThickness;
        DoubleBorderColor = doubleBorderColor;
        DoubleBorderThickness = doubleBorderThickness;
        OriginColor = originColor;
        OriginThickness = originThickness;
    }

    /// <summary>
    /// Gets the grid width along the X axis.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Gets the grid height along the Y axis.
    /// </summary>
    public float Height { get; }

    /// <summary>
    /// Gets the spacing between adjacent interior grid lines.
    /// </summary>
    public float Spacing { get; }

    /// <summary>
    /// Gets the amount added to the outer decorative border.
    /// </summary>
    public float OutlineOffset { get; }

    /// <summary>
    /// Gets the color used for interior grid lines.
    /// </summary>
    public Color GridColor { get; }

    /// <summary>
    /// Gets the thickness used for interior grid lines.
    /// </summary>
    public float GridThickness { get; }

    /// <summary>
    /// Gets the color used for the primary grid border.
    /// </summary>
    public Color BorderColor { get; }

    /// <summary>
    /// Gets the thickness used for the primary grid border.
    /// </summary>
    public float BorderThickness { get; }

    /// <summary>
    /// Gets the color used for the outer decorative border.
    /// </summary>
    public Color DoubleBorderColor { get; }

    /// <summary>
    /// Gets the thickness used for the outer decorative border.
    /// </summary>
    public float DoubleBorderThickness { get; }

    /// <summary>
    /// Gets the color used for the origin marker.
    /// </summary>
    public Color OriginColor { get; }

    /// <summary>
    /// Gets the thickness used for the origin marker.
    /// </summary>
    public float OriginThickness { get; }

    /// <summary>
    /// Gets the world-space bounds that contain every rendered grid element.
    /// </summary>
    public Rect3D GetRenderBounds()
    {
        double outerWidth = Width + OutlineOffset;
        double outerHeight = Height + OutlineOffset;
        double halfOuterWidth = outerWidth / 2.0;
        double halfOuterHeight = outerHeight / 2.0;
        double originExtent = Spacing / 2.0;
        double maxX = global::System.Math.Max(halfOuterWidth, originExtent);
        double maxY = global::System.Math.Max(halfOuterHeight, originExtent);
        double maxZ = originExtent;

        return new Rect3D(-maxX, -maxY, 0.0, maxX * 2.0, maxY * 2.0, maxZ);
    }
}
