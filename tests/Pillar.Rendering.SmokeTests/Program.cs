// Program.cs
// Runs focused smoke tests for rendering-layer screen-space selection geometry.
using HelixToolkit.Wpf.SharpDX;
using Pillar.Rendering.Preview;
using Pillar.Rendering.Tools;
using System;
using System.Collections.Generic;
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
