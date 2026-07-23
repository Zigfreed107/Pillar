// Program.cs
// Runs focused smoke tests for rendering-layer screen-space selection geometry.
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Rafts;
using Pillar.Rendering.EntityRenderers;
using Pillar.Rendering.Preview;
using Pillar.Rendering.Tools;
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
    public static int Main()
    {
        List<string> failures = new List<string>();

        RunTest(failures, "Outside segment is rejected", ValidateOutsideSegmentIsRejected);
        RunTest(failures, "Crossing segment is accepted", ValidateCrossingSegmentIsAccepted);
        RunTest(failures, "All control points inside passes within", ValidateAllControlPointsInsidePassesWithin);
        RunTest(failures, "Outside control point fails within", ValidateOutsideControlPointFailsWithin);
        RunTest(failures, "Edge-touching segment is accepted", ValidateEdgeTouchingSegmentIsAccepted);
        RunTest(failures, "Direct Edit arrows use solid meshes", ValidateDirectEditArrowsUseSolidMeshes);
        RunTest(failures, "Raft geometry includes flat lighting normals", ValidateRaftGeometryIncludesFlatLightingNormals);

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
    /// Validates that Direct Edit arrows expose solid mesh hit targets instead of line geometry.
    /// </summary>
    private static void ValidateDirectEditArrowsUseSolidMeshes()
    {
        GroupModel3D root = new GroupModel3D();
        _ = new DirectEditPreviewRenderer(root, 16);

        if (root.Children.Count < 5)
        {
            throw new InvalidOperationException("Expected Direct Edit preview visuals to be created.");
        }

        for (int i = root.Children.Count - 3; i < root.Children.Count; i++)
        {
            if (root.Children[i] is not MeshGeometryModel3D)
            {
                throw new InvalidOperationException("Expected every Direct Edit arrow to use solid mesh geometry.");
            }
        }
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
