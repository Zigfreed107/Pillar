// HorizontalFaceAngleAnalyzer.cs
// Provides renderer-agnostic triangle angle classification used by viewport highlighting and future support-generation filters.
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Analysis;

/// <summary>
/// Classifies downward-facing mesh triangles by their plane angle relative to the XY build plate.
/// </summary>
public static class HorizontalFaceAngleAnalyzer
{
    private const double MaximumThresholdDegrees = 90.0;

    /// <summary>
    /// Returns the original mesh triangle indices for downward-facing faces within the supplied angle from horizontal.
    /// </summary>
    public static IReadOnlyList<int> CreateMatchingTriangleIndices(MeshEntity mesh, double thresholdDegrees)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        double clampedThresholdDegrees = ClampThresholdDegrees(thresholdDegrees);
        List<int> matchingTriangleIndices = new List<int>();
        Matrix4x4 worldTransform = mesh.WorldTransform;

        for (int triangleStart = 0; triangleStart < mesh.TriangleIndices.Count; triangleStart += 3)
        {
            if (!IsTriangleWithinThreshold(mesh, triangleStart, worldTransform, clampedThresholdDegrees))
            {
                continue;
            }

            matchingTriangleIndices.Add(mesh.TriangleIndices[triangleStart]);
            matchingTriangleIndices.Add(mesh.TriangleIndices[triangleStart + 1]);
            matchingTriangleIndices.Add(mesh.TriangleIndices[triangleStart + 2]);
        }

        return matchingTriangleIndices;
    }

    /// <summary>
    /// Tests one triangle against the angle threshold after applying the mesh's current world transform.
    /// </summary>
    private static bool IsTriangleWithinThreshold(
        MeshEntity mesh,
        int triangleStart,
        Matrix4x4 worldTransform,
        double thresholdDegrees)
    {
        Vector3 first = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[triangleStart]], worldTransform);
        Vector3 second = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[triangleStart + 1]], worldTransform);
        Vector3 third = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[triangleStart + 2]], worldTransform);

        Vector3 edgeA = second - first;
        Vector3 edgeB = third - first;
        Vector3 normal = Vector3.Cross(edgeA, edgeB);

        if (normal.LengthSquared() <= float.Epsilon)
        {
            return false;
        }

        normal = Vector3.Normalize(normal);

        if (normal.Z >= 0.0f)
        {
            return false;
        }

        // Downward resin-support candidates have normals near world -Z. A perfect underside is 0 degrees.
        double downwardZ = Math.Min(1.0, Math.Max(0.0, -normal.Z));
        double angleFromHorizontalDegrees = Math.Acos(downwardZ) * 180.0 / Math.PI;
        return angleFromHorizontalDegrees <= thresholdDegrees;
    }

    /// <summary>
    /// Keeps user-provided thresholds inside the physically meaningful range for a plane angle.
    /// </summary>
    private static double ClampThresholdDegrees(double thresholdDegrees)
    {
        if (double.IsNaN(thresholdDegrees) || double.IsInfinity(thresholdDegrees))
        {
            return 45.0;
        }

        return Math.Min(MaximumThresholdDegrees, Math.Max(0.0, thresholdDegrees));
    }
}
