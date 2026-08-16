// Program.cs
// Runs focused smoke tests for rendering-layer screen-space selection geometry.
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Rafts;
using Pillar.Core.Tags;
using Pillar.Geometry.Tags;
using Pillar.Rendering.EntityRenderers;
using Pillar.Rendering.Preview;
using Pillar.Rendering.Tools;
using Pillar.UI.Controls;
using Pillar.UI.Modes;
using Pillar.UI.Tags;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;

namespace Pillar.Rendering.SmokeTests;

/// <summary>
/// Provides a small executable validation harness for rendering selection helpers.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs all smoke tests and returns a process exit code.
    /// </summary>
    [STAThread]
    public static int Main()
    {
        List<string> failures = new List<string>();

        RunTest(failures, "Outside segment is rejected", ValidateOutsideSegmentIsRejected);
        RunTest(failures, "Crossing segment is accepted", ValidateCrossingSegmentIsAccepted);
        RunTest(failures, "All control points inside passes within", ValidateAllControlPointsInsidePassesWithin);
        RunTest(failures, "Outside control point fails within", ValidateOutsideControlPointFailsWithin);
        RunTest(failures, "Edge-touching segment is accepted", ValidateEdgeTouchingSegmentIsAccepted);
        RunTest(failures, "Direct Edit arrows are solid and top-most", ValidateDirectEditArrowsAreSolidAndTopMost);
        RunTest(failures, "Translate arrows are solid and top-most", ValidateTranslateArrowsAreSolidAndTopMost);
        RunTest(failures, "Raft geometry includes flat lighting normals", ValidateRaftGeometryIncludesFlatLightingNormals);
        RunTest(failures, "Locked tag preview is opaque and visible", ValidateLockedTagPreviewIsOpaqueAndVisible);
        RunTest(failures, "Installed font creates solid tag text", ValidateInstalledFontCreatesSolidTagText);
        RunTest(failures, "Missing tag font falls back", ValidateMissingTagFontFallsBack);
        RunTest(failures, "Tag options initialize safely", ValidateTagOptionsInitializeSafely);
        IndexedMeshRenderingSmokeTests.Run(failures);
        IslandDetectionRenderingSmokeTests.Run(failures);
        RaftTextRenderingSmokeTests.Run(failures);

        if (failures.Count > 0)
        {
            Console.Error.WriteLine("Rendering smoke tests failed:");

            for (int i = 0; i < failures.Count; i++)
            {
                Console.Error.WriteLine(failures[i]);
            }

            return 1;
        }

        Console.WriteLine("Rendering smoke tests passed.");
        return 0;
    }

    /// <summary>
    /// Records one failed validation while allowing the remaining cases to run.
    /// </summary>
    private static void RunTest(List<string> failures, string name, Action test)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            failures.Add($"{name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that raft rendering supplies one face normal for every expanded triangle vertex.
    /// </summary>
    private static void ValidateRaftGeometryIncludesFlatLightingNormals()
    {
        SupportLayerColor color = new SupportLayerColor(64, 128, 192);
        RaftEntity raft = new RaftEntity(
            Guid.NewGuid(),
            new RaftSettings(),
            new[]
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f)
            },
            new[] { 0, 1, 2 },
            color);
        GroupModel3D group = RaftRenderer.Create(raft);

        if (group.Children.Count == 0
            || group.Children[0] is not MeshGeometryModel3D meshModel
            || meshModel.Geometry is not HelixToolkit.SharpDX.MeshGeometry3D geometry)
        {
            throw new InvalidOperationException("Expected the raft renderer to create mesh geometry.");
        }

        if (geometry.Positions == null
            || geometry.Normals == null
            || geometry.Normals.Count != geometry.Positions.Count
            || geometry.Normals.Count != 3)
        {
            throw new InvalidOperationException("Expected every expanded raft vertex to carry a lighting normal.");
        }

        if (meshModel.Material is not PhongMaterial material
            || MathF.Abs(material.DiffuseColor.Red - (64.0f / 255.0f)) > 0.000001f
            || MathF.Abs(material.DiffuseColor.Green - (128.0f / 255.0f)) > 0.000001f
            || MathF.Abs(material.DiffuseColor.Blue - (192.0f / 255.0f)) > 0.000001f
            || MathF.Abs(material.AmbientColor.Red - (16.0f / 255.0f)) > 0.000001f)
        {
            throw new InvalidOperationException("Expected the raft material to use its assigned layer color with reduced ambient light.");
        }

        for (int i = 0; i < geometry.Normals.Count; i++)
        {
            if (Vector3.DistanceSquared(geometry.Normals[i], Vector3.UnitZ) > 0.000001f)
            {
                throw new InvalidOperationException("Expected a planar raft triangle to use a consistent face normal.");
            }
        }
    }

    /// <summary>
    /// Validates that locking a tag switches its reusable preview into the opaque render pass.
    /// </summary>
    private static void ValidateLockedTagPreviewIsOpaqueAndVisible()
    {
        GroupModel3D root = new GroupModel3D();
        TagPreviewRenderer renderer = new TagPreviewRenderer(root);
        TagMeshData mesh = new TagMeshData(
            new[]
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f)
            },
            new[] { 0, 1, 2 });
        renderer.Show(mesh, new SupportLayerColor(64, 128, 192), 0.45f);

        if (root.Children.Count != 1
            || root.Children[0] is not MeshGeometryModel3D previewModel
            || !previewModel.IsTransparent)
        {
            throw new InvalidOperationException("Expected the moving tag preview to use the transparent render pass.");
        }

        renderer.Show(mesh, new SupportLayerColor(64, 128, 192), 1.0f);

        if (previewModel.IsTransparent || previewModel.Visibility != Visibility.Visible)
        {
            throw new InvalidOperationException("Expected the locked tag preview to remain visible in the opaque render pass.");
        }
    }

    /// <summary>
    /// Validates the WPF installed-font adapter and hole-aware text extrusion together.
    /// </summary>
    private static void ValidateInstalledFontCreatesSolidTagText()
    {
        TagSettings settings = new TagSettings(
            text: "B8",
            fontFamilyName: TagSettings.DefaultFontFamilyName,
            fontSize: 5.0f,
            textHeight: 1.0f);
        TagTextOutlineData outline = WpfTagTextOutlineFactory.Create(settings);
        TagTextMeshData mesh = TagTextMeshBuilder.Build(settings, outline);

        if (outline.MeasuredWidth <= 0.0f || outline.Contours.Count < 2)
        {
            throw new InvalidOperationException("Expected the installed default font to produce measured glyph contours.");
        }

        if (mesh.Positions.Count == 0 || mesh.TriangleIndices.Count == 0)
        {
            throw new InvalidOperationException("Expected installed-font glyphs to produce extruded triangle geometry.");
        }

        TagSettings trailingSpaceSettings = new TagSettings(
            text: "B8   ",
            fontFamilyName: TagSettings.DefaultFontFamilyName,
            fontSize: 5.0f,
            textHeight: 1.0f);
        TagTextOutlineData trailingSpaceOutline = WpfTagTextOutlineFactory.Create(trailingSpaceSettings);

        if (MathF.Abs(trailingSpaceOutline.MeasuredWidth - outline.MeasuredWidth) > 0.0001f)
        {
            throw new InvalidOperationException("Expected trailing spaces to be excluded from visible glyph width.");
        }
    }

    /// <summary>
    /// Validates that an unavailable saved family still produces geometry through the Arial fallback.
    /// </summary>
    private static void ValidateMissingTagFontFallsBack()
    {
        TagSettings settings = new TagSettings(
            text: "Fallback",
            fontFamilyName: "Pillar Missing Font Family",
            fontSize: 5.0f,
            textHeight: 1.0f);
        TagTextOutlineData outline = WpfTagTextOutlineFactory.Create(settings);
        TagTextMeshData mesh = TagTextMeshBuilder.Build(settings, outline);

        if (outline.MeasuredWidth <= 0.0f || mesh.TriangleIndices.Count == 0)
        {
            throw new InvalidOperationException("Expected an unavailable tag font to fall back to printable glyph geometry.");
        }
    }

    /// <summary>
    /// Validates that early XAML value events cannot access controls created later in the options panel.
    /// </summary>
    private static void ValidateTagOptionsInitializeSafely()
    {
        if (Application.Current == null)
        {
            Pillar.UI.App application = new Pillar.UI.App();
            application.InitializeComponent();
        }

        TagToolOptionsControl control = new TagToolOptionsControl();
        TagSettings settings = control.GetSettings();

        if (MathF.Abs(settings.OuterWidth - 8.5f) > 0.0001f
            || MathF.Abs(settings.InnerWidth - 8.5f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the Tag options panel to initialize its default widths.");
        }

        NumericUpDown tagHeightInput = (NumericUpDown)control.FindName("TagHeightInput");
        NumericUpDown borderOffsetInput = (NumericUpDown)control.FindName("BorderOffsetInput");
        NumericUpDown fontSizeInput = (NumericUpDown)control.FindName("FontSizeInput");
        NumericUpDown outerWidthInput = (NumericUpDown)control.FindName("OuterWidthInput");
        tagHeightInput.Value = 3.0;

        if (borderOffsetInput.Minimum < 3.0
            || borderOffsetInput.Value < 3.0
            || outerWidthInput.Minimum < fontSizeInput.Value + borderOffsetInput.Value)
        {
            throw new InvalidOperationException("Expected Tag Height to clamp Border Offset and its dependent Outer Width minimum.");
        }
    }

    /// <summary>
    /// Validates that Direct Edit arrows expose solid mesh hit targets in an always-on-top render group.
    /// </summary>
    private static void ValidateDirectEditArrowsAreSolidAndTopMost()
    {
        GroupModel3D root = new GroupModel3D();
        DirectEditPreviewRenderer renderer = new DirectEditPreviewRenderer(root, 16);
        renderer.ShowGizmo(
            new Vector3(1.0f, 2.0f, 3.0f),
            new Vector3(1.0f, 2.0f, 8.0f),
            2.0f,
            3.0f,
            true);

        if (root.Children.Count != 3
            || root.Children[2] is not TopMostGroup3D topMostRoot
            || !topMostRoot.EnableTopMost
            || topMostRoot.Children.Count != 4)
        {
            throw new InvalidOperationException("Expected Direct Edit arrows beneath one enabled top-most render group.");
        }

        for (int i = 0; i < topMostRoot.Children.Count; i++)
        {
            if (topMostRoot.Children[i] is not MeshGeometryModel3D arrow
                || !arrow.IsHitTestVisible)
            {
                throw new InvalidOperationException("Expected every Direct Edit arrow to remain a solid interactive mesh.");
            }
        }

        if (!renderer.TryGetHandleKind(topMostRoot.Children[3], out DirectEditGizmoHandleKind handleKind)
            || handleKind != DirectEditGizmoHandleKind.BaseZAxis
            || topMostRoot.Children[3].Visibility != Visibility.Visible)
        {
            throw new InvalidOperationException("Expected a visible lower Z handle for a model-connected base.");
        }

        renderer.ShowGizmo(
            new Vector3(1.0f, 2.0f, 0.0f),
            new Vector3(1.0f, 2.0f, 8.0f),
            2.0f,
            3.0f,
            false);

        if (topMostRoot.Children[3].Visibility != Visibility.Collapsed)
        {
            throw new InvalidOperationException("Expected the lower Z handle to remain hidden for a build-plate base.");
        }
    }

    /// <summary>
    /// Validates that model translation arrows use solid mesh hit targets in an always-on-top render group.
    /// </summary>
    private static void ValidateTranslateArrowsAreSolidAndTopMost()
    {
        GroupModel3D root = new GroupModel3D();
        const float shaftDiameter = 0.25f;
        const float headLength = 1.0f;
        const float headDiameter = 0.75f;
        Vector3 origin = new Vector3(1.0f, 2.0f, 3.0f);
        float[] expectedLengths = { 3.0f, 6.0f, 9.0f };
        ModelTranslatePreviewRenderer renderer = new ModelTranslatePreviewRenderer(
            root,
            16,
            shaftDiameter,
            headLength,
            headDiameter);
        renderer.Show(origin, new Vector3(expectedLengths[0], expectedLengths[1], expectedLengths[2]));

        if (root.Children.Count != 1
            || root.Children[0] is not TopMostGroup3D topMostRoot
            || !topMostRoot.EnableTopMost
            || topMostRoot.Children.Count != 3)
        {
            throw new InvalidOperationException("Expected three translation arrows beneath one enabled top-most render group.");
        }

        for (int i = 0; i < topMostRoot.Children.Count; i++)
        {
            if (topMostRoot.Children[i] is not MeshGeometryModel3D arrow
                || arrow.Geometry is not HelixToolkit.SharpDX.MeshGeometry3D geometry
                || arrow.Visibility != Visibility.Visible
                || arrow.Transform is not System.Windows.Media.Media3D.TranslateTransform3D translation
                || System.Math.Abs(translation.OffsetX - origin.X) > 0.0001
                || System.Math.Abs(translation.OffsetY - origin.Y) > 0.0001
                || System.Math.Abs(translation.OffsetZ - origin.Z) > 0.0001
                || System.Math.Abs(CalculateArrowAxisMaximum(geometry, i) - expectedLengths[i]) > 0.0001f
                || System.Math.Abs(CalculateArrowDiameterAtAxisCoordinate(geometry, i, 0.0f) - shaftDiameter) > 0.0001f
                || System.Math.Abs(CalculateArrowMaximumDiameter(geometry, i) - headDiameter) > 0.0001f
                || System.Math.Abs(CalculateArrowHeadBaseCoordinate(geometry, i) - (expectedLengths[i] - headLength)) > 0.0001f)
            {
                throw new InvalidOperationException("Expected each translation arrow to use independent absolute shaft and arrowhead dimensions.");
            }
        }
    }

    /// <summary>
    /// Calculates the furthest geometry coordinate along one translation arrow axis.
    /// </summary>
    private static float CalculateArrowAxisMaximum(HelixToolkit.SharpDX.MeshGeometry3D geometry, int axisIndex)
    {
        IList<Vector3>? positions = geometry.Positions;

        if (positions == null || positions.Count == 0)
        {
            return 0.0f;
        }

        float maximum = GetArrowAxisCoordinate(positions[0], axisIndex);

        for (int i = 1; i < positions.Count; i++)
        {
            maximum = MathF.Max(maximum, GetArrowAxisCoordinate(positions[i], axisIndex));
        }

        return maximum;
    }

    /// <summary>
    /// Calculates the widest geometry diameter at one coordinate along the arrow axis.
    /// </summary>
    private static float CalculateArrowDiameterAtAxisCoordinate(
        HelixToolkit.SharpDX.MeshGeometry3D geometry,
        int axisIndex,
        float axisCoordinate)
    {
        IList<Vector3>? positions = geometry.Positions;

        if (positions == null)
        {
            return 0.0f;
        }

        float maximumRadius = 0.0f;

        for (int i = 0; i < positions.Count; i++)
        {
            if (MathF.Abs(GetArrowAxisCoordinate(positions[i], axisIndex) - axisCoordinate) <= 0.0001f)
            {
                maximumRadius = MathF.Max(maximumRadius, GetArrowRadialDistance(positions[i], axisIndex));
            }
        }

        return maximumRadius * 2.0f;
    }

    /// <summary>
    /// Calculates the largest untransformed diameter perpendicular to one translation arrow axis.
    /// </summary>
    private static float CalculateArrowMaximumDiameter(HelixToolkit.SharpDX.MeshGeometry3D geometry, int axisIndex)
    {
        IList<Vector3>? positions = geometry.Positions;

        if (positions == null)
        {
            return 0.0f;
        }

        float maximumRadius = 0.0f;

        for (int i = 0; i < positions.Count; i++)
        {
            maximumRadius = MathF.Max(maximumRadius, GetArrowRadialDistance(positions[i], axisIndex));
        }

        return maximumRadius * 2.0f;
    }

    /// <summary>
    /// Locates the arrowhead base from the axis coordinate carrying the maximum radial extent.
    /// </summary>
    private static float CalculateArrowHeadBaseCoordinate(HelixToolkit.SharpDX.MeshGeometry3D geometry, int axisIndex)
    {
        IList<Vector3>? positions = geometry.Positions;

        if (positions == null || positions.Count == 0)
        {
            return 0.0f;
        }

        float maximumRadius = CalculateArrowMaximumDiameter(geometry, axisIndex) * 0.5f;

        for (int i = 0; i < positions.Count; i++)
        {
            if (MathF.Abs(GetArrowRadialDistance(positions[i], axisIndex) - maximumRadius) <= 0.0001f)
            {
                return GetArrowAxisCoordinate(positions[i], axisIndex);
            }
        }

        return 0.0f;
    }

    /// <summary>
    /// Gets one point component along the selected world axis.
    /// </summary>
    private static float GetArrowAxisCoordinate(Vector3 position, int axisIndex)
    {
        return axisIndex switch
        {
            0 => position.X,
            1 => position.Y,
            _ => position.Z
        };
    }

    /// <summary>
    /// Calculates one point's distance from the selected arrow axis.
    /// </summary>
    private static float GetArrowRadialDistance(Vector3 position, int axisIndex)
    {
        return axisIndex switch
        {
            0 => MathF.Sqrt((position.Y * position.Y) + (position.Z * position.Z)),
            1 => MathF.Sqrt((position.X * position.X) + (position.Z * position.Z)),
            _ => MathF.Sqrt((position.X * position.X) + (position.Y * position.Y))
        };
    }

    /// <summary>
    /// Validates that an off-window segment does not count as crossing.
    /// </summary>
    private static void ValidateOutsideSegmentIsRejected()
    {
        Rect rectangle = new Rect(10.0, 10.0, 20.0, 20.0);

        if (ScreenSelectionGeometry.SegmentIntersectsRectangle(new Point(0.0, 0.0), new Point(5.0, 5.0), rectangle))
        {
            throw new InvalidOperationException("Expected a fully outside segment to be rejected.");
        }
    }

    /// <summary>
    /// Validates that a segment passing through the rectangle counts as crossing.
    /// </summary>
    private static void ValidateCrossingSegmentIsAccepted()
    {
        Rect rectangle = new Rect(10.0, 10.0, 20.0, 20.0);

        if (!ScreenSelectionGeometry.SegmentIntersectsRectangle(new Point(0.0, 20.0), new Point(40.0, 20.0), rectangle))
        {
            throw new InvalidOperationException("Expected a segment crossing the rectangle to be accepted.");
        }
    }

    /// <summary>
    /// Validates that a within-selection test accepts only fully enclosed control points.
    /// </summary>
    private static void ValidateAllControlPointsInsidePassesWithin()
    {
        Rect rectangle = new Rect(10.0, 10.0, 20.0, 20.0);
        Point[] points = new Point[]
        {
            new Point(12.0, 12.0),
            new Point(20.0, 20.0),
            new Point(28.0, 28.0)
        };

        if (!ScreenSelectionGeometry.ContainsAllPoints(rectangle, points))
        {
            throw new InvalidOperationException("Expected all enclosed points to pass within selection.");
        }
    }

    /// <summary>
    /// Validates that one outside control point prevents within selection.
    /// </summary>
    private static void ValidateOutsideControlPointFailsWithin()
    {
        Rect rectangle = new Rect(10.0, 10.0, 20.0, 20.0);
        Point[] points = new Point[]
        {
            new Point(12.0, 12.0),
            new Point(31.0, 20.0),
            new Point(20.0, 28.0)
        };

        if (ScreenSelectionGeometry.ContainsAllPoints(rectangle, points))
        {
            throw new InvalidOperationException("Expected an outside control point to fail within selection.");
        }
    }

    /// <summary>
    /// Validates that contact at a rectangle edge is treated as crossing.
    /// </summary>
    private static void ValidateEdgeTouchingSegmentIsAccepted()
    {
        Rect rectangle = new Rect(10.0, 10.0, 20.0, 20.0);
        Point[] points = new Point[]
        {
            new Point(0.0, 10.0),
            new Point(10.0, 10.0)
        };

        if (!ScreenSelectionGeometry.ContainsOrCrossesPolyline(rectangle, points))
        {
            throw new InvalidOperationException("Expected an edge-touching segment to be accepted.");
        }
    }
}
