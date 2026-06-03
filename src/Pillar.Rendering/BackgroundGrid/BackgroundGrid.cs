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
    private readonly MeshGeometryModel3D _grid;
    private readonly MeshGeometryModel3D _border;
    private readonly MeshGeometryModel3D _doubleBorder;
    private readonly LineGeometryModel3D _origin;
    private readonly LineGeometryModel3D _topCornerGuides;

    /// <summary>
    /// Initializes the grid renderer and adds its visuals to the supplied scene root.
    /// </summary>
    public BackgroundGridRenderer(GroupModel3D sceneRoot, BackgroundGridDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));

        MeshBuilder doubleBorderBuilder = BuildDoubleBorder();
        _doubleBorder = new MeshGeometryModel3D
        {
            Geometry = doubleBorderBuilder.ToMeshGeometry3D(),
            Material = CreateFlatMaterial(_definition.DoubleBorderColor),
            CullMode = SharpDX.Direct3D11.CullMode.Back
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

        MeshBuilder gridBuilder = BuildGridMeshGeometry();
        _grid = new MeshGeometryModel3D
        {
            Geometry = gridBuilder.ToMeshGeometry3D(),
            Material = CreateFlatMaterial(_definition.GridColor),
            CullMode = SharpDX.Direct3D11.CullMode.Back
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

        LineBuilder topCornerGuideBuilder = BuildTopCornerGuideGeometry();
        _topCornerGuides = new LineGeometryModel3D
        {
            Geometry = topCornerGuideBuilder.ToLineGeometry3D(),
            Color = _definition.GridColor,
            Thickness = _definition.GridThickness
        };

        sceneRoot.Children.Add(_topCornerGuides);
    }

    /// <summary>
    /// Gets the full world-space bounds occupied by the rendered grid.
    /// </summary>
    public Rect3D RenderBounds
    {
        get { return _definition.GetRenderBounds(); }
    }

    /// <summary>
    /// Builds the regularly spaced interior grid strips from the current definition.
    /// </summary>
    private MeshBuilder BuildGridMeshGeometry()
    {
        MeshBuilder gridBuilder = new MeshBuilder();
        float halfWidth = _definition.Width / 2.0f;
        float halfHeight = _definition.Height / 2.0f;
        float halfThickness = _definition.GridThickness / 2.0f;

        float startY = (float)global::System.Math.Round((-halfHeight) / _definition.Spacing) * _definition.Spacing;
        float startX = (float)global::System.Math.Round((-halfWidth) / _definition.Spacing) * _definition.Spacing;

        int horizontalLineCount = (int)(_definition.Height / _definition.Spacing);
        int verticalLineCount = (int)(_definition.Width / _definition.Spacing);

        for (int horizontalLineIndex = 0; horizontalLineIndex <= horizontalLineCount; horizontalLineIndex += 1)
        {
            float currentY = startY + (horizontalLineIndex * _definition.Spacing);
            AddFlatQuad(
                gridBuilder,
                -halfWidth,
                currentY - halfThickness,
                halfWidth,
                currentY + halfThickness);
        }

        for (int verticalLineIndex = 0; verticalLineIndex <= verticalLineCount; verticalLineIndex += 1)
        {
            float currentX = startX + (verticalLineIndex * _definition.Spacing);
            AddFlatQuad(
                gridBuilder,
                currentX - halfThickness,
                -halfHeight,
                currentX + halfThickness,
                halfHeight);
        }

        return gridBuilder;
    }

    /// <summary>
    /// Builds the primary border as four top-facing mesh strips so back-face culling hides it from below.
    /// </summary>
    private MeshBuilder BuildBorder()
    {
        return BuildRectangleFrame(_definition.Width, _definition.Height, _definition.BorderThickness);
    }

    /// <summary>
    /// Builds the outer decorative border as top-facing mesh strips around the build plate.
    /// </summary>
    private MeshBuilder BuildDoubleBorder()
    {
        return BuildRectangleFrame(
            _definition.Width + _definition.OutlineOffset,
            _definition.Height + _definition.OutlineOffset,
            _definition.DoubleBorderThickness);
    }

    /// <summary>
    /// Builds one rectangular frame from four flat quads in the grid plane.
    /// </summary>
    private MeshBuilder BuildRectangleFrame(float width, float height, float thickness)
    {
        MeshBuilder frameBuilder = new MeshBuilder();
        float halfWidth = width / 2.0f;
        float halfHeight = height / 2.0f;
        float halfThickness = thickness / 2.0f;
        float outerLeft = -halfWidth - halfThickness;
        float outerRight = halfWidth + halfThickness;
        float outerBottom = -halfHeight - halfThickness;
        float outerTop = halfHeight + halfThickness;

        AddFlatQuad(frameBuilder, outerLeft, halfHeight - halfThickness, outerRight, outerTop);
        AddFlatQuad(frameBuilder, halfWidth - halfThickness, -halfHeight, outerRight, halfHeight);
        AddFlatQuad(frameBuilder, outerLeft, outerBottom, outerRight, -halfHeight + halfThickness);
        AddFlatQuad(frameBuilder, outerLeft, -halfHeight, -halfWidth + halfThickness, halfHeight);

        return frameBuilder;
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

    /// <summary>
    /// Builds fixed-length X, Y, and Z guide segments at each top corner of the printable volume.
    /// </summary>
    private LineBuilder BuildTopCornerGuideGeometry()
    {
        LineBuilder guideBuilder = new LineBuilder();
        float halfWidth = _definition.Width / 2.0f;
        float halfHeight = _definition.Height / 2.0f;
        float topZ = _definition.PrintableVolume.ZDistance;
        float guideLength = _definition.TopCornerGuideLength;

        AddTopCornerGuides(guideBuilder, -halfWidth, -halfHeight, topZ, guideLength, 1.0f, 1.0f);
        AddTopCornerGuides(guideBuilder, halfWidth, -halfHeight, topZ, guideLength, -1.0f, 1.0f);
        AddTopCornerGuides(guideBuilder, -halfWidth, halfHeight, topZ, guideLength, 1.0f, -1.0f);
        AddTopCornerGuides(guideBuilder, halfWidth, halfHeight, topZ, guideLength, -1.0f, -1.0f);

        return guideBuilder;
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
    /// Adds one inward-facing corner marker made of short X, Y, and Z line segments.
    /// </summary>
    private static void AddTopCornerGuides(
        LineBuilder builder,
        float x,
        float y,
        float z,
        float guideLength,
        float xDirection,
        float yDirection)
    {
        Vector3 corner = new Vector3(x, y, z);

        builder.AddLine(corner, new Vector3(x + (xDirection * guideLength), y, z));
        builder.AddLine(corner, new Vector3(x, y + (yDirection * guideLength), z));
        builder.AddLine(corner, new Vector3(x, y, z - guideLength));
    }

}
