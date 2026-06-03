// BackgroundGridDefinition.cs
// Defines the printable-volume grid dimensions and styling so rendering and camera framing share one source of truth.
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
        printableVolume: new PrintableVolumeDefinition(126.0f, 223.0f, 250.0f),
        spacing: 10.0f,
        outlineOffset: 5.0f,
        gridColor: Color.FromRgb(225, 225, 225),
        gridThickness: 0.5f,
        borderColor: Color.FromRgb(200, 200, 200),
        borderThickness: 1.0f,
        doubleBorderColor: Color.FromRgb(200, 200, 200),
        doubleBorderThickness: 2.5f,
        originColor: Color.FromRgb(100, 0, 0),
        originThickness: 1.5f,
        topCornerGuideLength: 10.0f);

    /// <summary>
    /// Initializes one background-grid definition.
    /// </summary>
    public BackgroundGridDefinition(
        PrintableVolumeDefinition printableVolume,
        float spacing,
        float outlineOffset,
        Color gridColor,
        float gridThickness,
        Color borderColor,
        float borderThickness,
        Color doubleBorderColor,
        float doubleBorderThickness,
        Color originColor,
        float originThickness,
        float topCornerGuideLength)
    {
        if (printableVolume == null)
        {
            throw new ArgumentNullException(nameof(printableVolume));
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

        if (topCornerGuideLength <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(topCornerGuideLength), "Top-corner guide length must be greater than zero.");
        }

        PrintableVolume = printableVolume;
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
        TopCornerGuideLength = topCornerGuideLength;
    }

    /// <summary>
    /// Gets a copy of this grid definition with different printable-volume dimensions.
    /// </summary>
    public BackgroundGridDefinition WithPrintableVolume(PrintableVolumeDefinition printableVolume)
    {
        return new BackgroundGridDefinition(
            printableVolume,
            Spacing,
            OutlineOffset,
            GridColor,
            GridThickness,
            BorderColor,
            BorderThickness,
            DoubleBorderColor,
            DoubleBorderThickness,
            OriginColor,
            OriginThickness,
            TopCornerGuideLength);
    }

    /// <summary>
    /// Gets the printable build volume represented by the grid and top-corner guides.
    /// </summary>
    public PrintableVolumeDefinition PrintableVolume { get; }

    /// <summary>
    /// Gets the grid width along the X axis.
    /// </summary>
    public float Width
    {
        get { return PrintableVolume.XDistance; }
    }

    /// <summary>
    /// Gets the grid height along the Y axis.
    /// </summary>
    public float Height
    {
        get { return PrintableVolume.YDistance; }
    }

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
    /// Gets the fixed length of each top-corner printable-volume guide segment.
    /// </summary>
    public float TopCornerGuideLength { get; }

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
        double maxZ = global::System.Math.Max(originExtent, PrintableVolume.ZDistance);

        return new Rect3D(-maxX, -maxY, 0.0, maxX * 2.0, maxY * 2.0, maxZ);
    }
}
