// BackgroundGrid.cs
// Builds the viewport background grid visuals from a reusable grid definition so camera framing and rendering stay aligned.
using HelixToolkit.Geometry;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Numerics;
using System.Windows.Media.Media3D;
using MediaColor = System.Windows.Media.Color;

namespace Pillar.Rendering.BackgroundGrid;

/// <summary>
/// Builds the background grid visuals shown behind CAD entities.
/// </summary>
public class BackgroundGridRenderer
{
    private readonly BackgroundGridDefinition _definition;
    private readonly LineGeometryModel3D _grid;
    private readonly MeshGeometryModel3D _border;
    private readonly LineGeometryModel3D _doubleBorder;
    private readonly LineGeometryModel3D _origin;

    /// <summary>
    /// Initializes the grid renderer and adds its visuals to the supplied scene root.
    /// </summary>
    public BackgroundGridRenderer(GroupModel3D sceneRoot, BackgroundGridDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));

        LineBuilder doubleBorderBuilder = new LineBuilder();
        doubleBorderBuilder.AddBox(
            new Vector3(0.0f, 0.0f, 0.0f),
            _definition.Width + _definition.OutlineOffset,
            _definition.Height + _definition.OutlineOffset,
            0.0f);

        _doubleBorder = new LineGeometryModel3D
        {
            Geometry = doubleBorderBuilder.ToLineGeometry3D(),
            Color = _definition.DoubleBorderColor,
            Thickness = _definition.DoubleBorderThickness
        };

        sceneRoot.Children.Add(_doubleBorder);

        MeshBuilder borderBuilder = BuildBorder();
        _border = new MeshGeometryModel3D
        {
            Geometry = borderBuilder.ToMeshGeometry3D(),
            Material = CreateFlatMaterial(_definition.BorderColor),

            CullMode = SharpDX.Direct3D11.CullMode.Back
        };

        sceneRoot.Children.Add(_border);

        LineBuilder gridBuilder = BuildGridLineGeometry();
        _grid = new LineGeometryModel3D
        {
            Geometry = gridBuilder.ToLineGeometry3D(),
            Color = _definition.GridColor,
            Thickness = _definition.GridThickness
        };

        sceneRoot.Children.Add(_grid);

        LineBuilder originBuilder = BuildOriginGeometry();
        _origin = new LineGeometryModel3D
        {
            Geometry = originBuilder.ToLineGeometry3D(),
            Color = _definition.OriginColor,
            Thickness = _definition.OriginThickness
        };

        sceneRoot.Children.Add(_origin);
    }

    /// <summary>
    /// Gets the full world-space bounds occupied by the rendered grid.
    /// </summary>
    public Rect3D RenderBounds
    {
        get { return _definition.GetRenderBounds(); }
    }

    /// <summary>
    /// Builds the regularly spaced interior grid lines from the current definition.
    /// </summary>
    private LineBuilder BuildGridLineGeometry()
    {
        LineBuilder gridBuilder = new LineBuilder();
        float halfWidth = _definition.Width / 2.0f;
        float halfHeight = _definition.Height / 2.0f;

        float startY = (float)global::System.Math.Round((-halfHeight) / _definition.Spacing) * _definition.Spacing;
        float startX = (float)global::System.Math.Round((-halfWidth) / _definition.Spacing) * _definition.Spacing;

        int horizontalLineCount = (int)(_definition.Height / _definition.Spacing);
        int verticalLineCount = (int)(_definition.Width / _definition.Spacing);

        for (int horizontalLineIndex = 0; horizontalLineIndex <= horizontalLineCount; horizontalLineIndex += 1)
        {
            float currentY = startY + (horizontalLineIndex * _definition.Spacing);
            gridBuilder.AddLine(new Vector3(-halfWidth, currentY, 0.0f), new Vector3(halfWidth, currentY, 0.0f));
        }

        for (int verticalLineIndex = 0; verticalLineIndex <= verticalLineCount; verticalLineIndex += 1)
        {
            float currentX = startX + (verticalLineIndex * _definition.Spacing);
            gridBuilder.AddLine(new Vector3(currentX, -halfHeight, 0.0f), new Vector3(currentX, halfHeight, 0.0f));
        }

        return gridBuilder;
    }


    /// <summary>
    /// Builds the primary border as four flat mesh strips so the border has stable screen-independent thickness.
    /// </summary>
    private MeshBuilder BuildBorder()
    {
        float halfWidth = _definition.Width / 2.0f;
        float halfHeight = _definition.Height / 2.0f;
        float thickness = _definition.BorderThickness;
        float outerLeft = -halfWidth - thickness;
        float outerRight = halfWidth + thickness;
        float outerBottom = -halfHeight - thickness;
        float outerTop = halfHeight + thickness;

        MeshBuilder borderBuilder = new MeshBuilder();

        AddFlatQuad(borderBuilder, outerLeft, halfHeight, outerRight, outerTop);
        AddFlatQuad(borderBuilder, halfWidth, -halfHeight, outerRight, halfHeight);
        AddFlatQuad(borderBuilder, outerLeft, outerBottom, outerRight, -halfHeight);
        AddFlatQuad(borderBuilder, outerLeft, -halfHeight, -halfWidth, halfHeight);

        return borderBuilder;
    }

    /// <summary>
    /// Creates an unlit-looking material for flat background mesh elements.
    /// </summary>
    private static PhongMaterial CreateFlatMaterial(MediaColor color)
    {
        Color4 color4 = new Color4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);

        return new PhongMaterial
        {
            AmbientColor = color4,
            DiffuseColor = color4,
            SpecularColor = new Color4(0.0f, 0.0f, 0.0f, color4.Alpha)
        };
    }

    /// <summary>
    /// Adds a two-triangle rectangle in the grid plane.
    /// </summary>
    private static void AddFlatQuad(MeshBuilder builder, float left, float bottom, float right, float top)
    {
        Vector3 southWest = new Vector3(left, bottom, 0.0f);
        Vector3 southEast = new Vector3(right, bottom, 0.0f);
        Vector3 northWest = new Vector3(left, top, 0.0f);
        Vector3 northEast = new Vector3(right, top, 0.0f);

        builder.AddTriangle(southWest, southEast, northEast);
        builder.AddTriangle(southWest, northEast, northWest);
    }

    /// <summary>
    /// Builds the origin triad marker shown at the center of the build plate.
    /// </summary>
    private LineBuilder BuildOriginGeometry()
    {
        LineBuilder originBuilder = new LineBuilder();
        float originAxisLength = _definition.Spacing / 2.0f;

        originBuilder.AddLine(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(originAxisLength, 0.0f, 0.0f));
        originBuilder.AddLine(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, originAxisLength, 0.0f));
        originBuilder.AddLine(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, originAxisLength));

        return originBuilder;
    }
}
