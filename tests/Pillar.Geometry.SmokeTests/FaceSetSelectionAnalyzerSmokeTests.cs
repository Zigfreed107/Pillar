// FaceSetSelectionAnalyzerSmokeTests.cs
// Verifies reusable polygon containment and multi-seed coplanar growth used by the face-selection helper.
using Pillar.Core.Entities;
using Pillar.Geometry.Analysis;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.SmokeTests;

/// <summary>
/// Runs focused checks for camera-view polygon and shared angle-selection geometry.
/// </summary>
public static class FaceSetSelectionAnalyzerSmokeTests
{
    /// <summary>
    /// Adds all face-selection analyzer failures to the shared smoke-test result.
    /// </summary>
    public static void Run(List<string> failures)
    {
        RunTest(failures, "Face polygon contains convex interior and boundary points", ValidateConvexPolygonContainment);
        RunTest(failures, "Face polygon excludes a concave cutout", ValidateConcavePolygonContainment);
        RunTest(failures, "Face polygon requires the complete projected triangle", ValidateCompleteTriangleContainment);
        RunTest(failures, "Face polygon rejects triangle edges crossing a concave cutout", ValidateConcaveTriangleContainment);
        RunTest(failures, "Coplanar growth unions multiple disconnected seeds", ValidateMultipleCoplanarSeeds);
    }

    /// <summary>
    /// Verifies convex polygon containment includes its boundary but excludes exterior points.
    /// </summary>
    private static void ValidateConvexPolygonContainment()
    {
        Vector2[] polygon =
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(4.0f, 0.0f),
            new Vector2(4.0f, 4.0f),
            new Vector2(0.0f, 4.0f)
        };

        if (!FaceSetSelectionAnalyzer.IsPointInsideScreenPolygon(new Vector2(2.0f, 2.0f), polygon)
            || !FaceSetSelectionAnalyzer.IsPointInsideScreenPolygon(new Vector2(2.0f, 0.0f), polygon)
            || FaceSetSelectionAnalyzer.IsPointInsideScreenPolygon(new Vector2(5.0f, 2.0f), polygon))
        {
            throw new InvalidOperationException("Expected convex polygon containment to include the interior and boundary only.");
        }
    }

    /// <summary>
    /// Verifies the odd-even containment rule supports concave camera-view polygons.
    /// </summary>
    private static void ValidateConcavePolygonContainment()
    {
        Vector2[] polygon =
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(4.0f, 0.0f),
            new Vector2(4.0f, 4.0f),
            new Vector2(2.0f, 2.0f),
            new Vector2(0.0f, 4.0f)
        };

        if (!FaceSetSelectionAnalyzer.IsPointInsideScreenPolygon(new Vector2(2.0f, 1.0f), polygon)
            || FaceSetSelectionAnalyzer.IsPointInsideScreenPolygon(new Vector2(2.0f, 3.0f), polygon))
        {
            throw new InvalidOperationException("Expected the polygon's inward notch to remain outside the selected area.");
        }
    }

    /// <summary>
    /// Verifies a projected face is accepted only when all of its area is within a convex polygon.
    /// </summary>
    private static void ValidateCompleteTriangleContainment()
    {
        Vector2[] polygon =
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(4.0f, 0.0f),
            new Vector2(4.0f, 4.0f),
            new Vector2(0.0f, 4.0f)
        };

        bool containsCompleteTriangle = FaceSetSelectionAnalyzer.IsScreenTriangleEntirelyInsidePolygon(
            new Vector2(1.0f, 1.0f),
            new Vector2(3.0f, 1.0f),
            new Vector2(2.0f, 3.0f),
            polygon);
        bool containsPartialTriangle = FaceSetSelectionAnalyzer.IsScreenTriangleEntirelyInsidePolygon(
            new Vector2(1.0f, 1.0f),
            new Vector2(5.0f, 1.0f),
            new Vector2(2.0f, 3.0f),
            polygon);

        if (!containsCompleteTriangle || containsPartialTriangle)
        {
            throw new InvalidOperationException("Expected only the completely contained projected triangle.");
        }
    }

    /// <summary>
    /// Verifies contained vertices are insufficient when an edge crosses outside a concave polygon.
    /// </summary>
    private static void ValidateConcaveTriangleContainment()
    {
        Vector2[] polygon =
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(4.0f, 0.0f),
            new Vector2(4.0f, 4.0f),
            new Vector2(2.0f, 2.0f),
            new Vector2(0.0f, 4.0f)
        };
        bool containsCrossingTriangle = FaceSetSelectionAnalyzer.IsScreenTriangleEntirelyInsidePolygon(
            new Vector2(0.25f, 3.5f),
            new Vector2(3.75f, 3.5f),
            new Vector2(2.0f, 1.0f),
            polygon);
        bool containsBoundarySpanningTriangle = FaceSetSelectionAnalyzer.IsScreenTriangleEntirelyInsidePolygon(
            new Vector2(0.0f, 4.0f),
            new Vector2(4.0f, 4.0f),
            new Vector2(2.0f, 1.0f),
            polygon);

        if (containsCrossingTriangle || containsBoundarySpanningTriangle)
        {
            throw new InvalidOperationException("Expected the concave cutout to reject an intersecting projected face edge.");
        }
    }

    /// <summary>
    /// Verifies a shared angle toggle can expand all base candidates without rebuilding per seed.
    /// </summary>
    private static void ValidateMultipleCoplanarSeeds()
    {
        Vector3[] vertices =
        {
            Vector3.Zero,
            Vector3.UnitX,
            Vector3.UnitY,
            new Vector3(10.0f, 0.0f, 0.0f),
            new Vector3(11.0f, 0.0f, 0.0f),
            new Vector3(10.0f, 1.0f, 0.0f)
        };
        MeshEntity mesh = new MeshEntity("Disconnected coplanar faces", vertices, new[] { 0, 1, 2, 3, 4, 5 });
        List<int> selectedTriangles = new List<int>();

        FaceSetSelectionAnalyzer.FillConnectedCoplanarTriangles(
            mesh,
            new[] { 0, 1 },
            0.0,
            selectedTriangles);

        if (selectedTriangles.Count != 2
            || !selectedTriangles.Contains(0)
            || !selectedTriangles.Contains(1))
        {
            throw new InvalidOperationException("Expected both disconnected seed regions in the coplanar union.");
        }
    }

    /// <summary>
    /// Runs one named validation while preserving the smoke-test harness's aggregate failure reporting.
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
}
