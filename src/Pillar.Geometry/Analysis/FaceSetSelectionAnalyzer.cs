// FaceSetSelectionAnalyzer.cs
// Provides renderer-agnostic mesh face queries for reusable face-set selection workflows.
using Pillar.Core.Entities;
using Pillar.Geometry.Topology;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Analysis;

/// <summary>
/// Finds mesh triangles selected by click, line, or normal-angle grow operations.
/// </summary>
public static class FaceSetSelectionAnalyzer
{
    private const float DegenerateNormalTolerance = 0.00000001f;
    private const double LineIntersectionTolerance = 0.0001;

    /// <summary>
    /// Returns the triangle under a world-space hit point.
    /// </summary>
    public static bool TryFindContainingTriangleIndex(MeshEntity mesh, Vector3 worldPoint, out int triangleIndex)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        Matrix4x4 worldTransform = mesh.WorldTransform;
        float bestDistanceSquared = float.MaxValue;
        int bestTriangleIndex = -1;
        int triangleCount = mesh.TriangleIndices.Count / 3;

        for (int i = 0; i < triangleCount; i++)
        {
            int baseIndex = i * 3;
            Vector3 a = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[baseIndex]], worldTransform);
            Vector3 b = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[baseIndex + 1]], worldTransform);
            Vector3 c = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[baseIndex + 2]], worldTransform);

            if (!TryGetClosestPointOnTriangle(worldPoint, a, b, c, out Vector3 closestPoint))
            {
                continue;
            }

            float distanceSquared = Vector3.DistanceSquared(worldPoint, closestPoint);

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestTriangleIndex = i;
            }
        }

        triangleIndex = bestTriangleIndex;
        return bestTriangleIndex >= 0;
    }

    /// <summary>
    /// Fills triangle indices whose projected faces intersect a screen-space line segment.
    /// </summary>
    public static void FillTrianglesCrossedByScreenLine(
        MeshEntity mesh,
        Func<Vector3, Vector2?> projectWorldPoint,
        Vector2 screenStart,
        Vector2 screenEnd,
        ICollection<int> selectedTriangleIndices)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (projectWorldPoint == null)
        {
            throw new ArgumentNullException(nameof(projectWorldPoint));
        }

        if (selectedTriangleIndices == null)
        {
            throw new ArgumentNullException(nameof(selectedTriangleIndices));
        }

        Matrix4x4 worldTransform = mesh.WorldTransform;
        int triangleCount = mesh.TriangleIndices.Count / 3;

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            int baseIndex = triangleIndex * 3;
            Vector2? a = ProjectTriangleVertex(mesh, worldTransform, baseIndex, projectWorldPoint);
            Vector2? b = ProjectTriangleVertex(mesh, worldTransform, baseIndex + 1, projectWorldPoint);
            Vector2? c = ProjectTriangleVertex(mesh, worldTransform, baseIndex + 2, projectWorldPoint);

            if (!a.HasValue || !b.HasValue || !c.HasValue)
            {
                continue;
            }

            if (LineIntersectsProjectedTriangle(screenStart, screenEnd, a.Value, b.Value, c.Value))
            {
                selectedTriangleIndices.Add(triangleIndex);
            }
        }
    }

    /// <summary>
    /// Fills connected neighbouring triangles whose normals differ by no more than the threshold.
    /// </summary>
    public static void FillConnectedCoplanarTriangles(
        MeshEntity mesh,
        int seedTriangleIndex,
        double thresholdDegrees,
        ICollection<int> selectedTriangleIndices)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (selectedTriangleIndices == null)
        {
            throw new ArgumentNullException(nameof(selectedTriangleIndices));
        }

        int triangleCount = mesh.TriangleIndices.Count / 3;

        if (seedTriangleIndex < 0 || seedTriangleIndex >= triangleCount)
        {
            return;
        }

        Vector3[] triangleNormals = CreateTriangleNormals(mesh);
        IndexedMeshTopology topology = IndexedMeshTopology.Create(mesh.Vertices, mesh.TriangleIndices);
        bool[] includedTriangles = new bool[triangleCount];
        Queue<int> openTriangles = new Queue<int>();
        double clampedThresholdDegrees = Math.Min(180.0, Math.Max(0.0, thresholdDegrees));
        float minimumDot = MathF.Cos((float)(clampedThresholdDegrees * Math.PI / 180.0));

        includedTriangles[seedTriangleIndex] = true;
        openTriangles.Enqueue(seedTriangleIndex);

        while (openTriangles.Count > 0)
        {
            int currentTriangleIndex = openTriangles.Dequeue();
            selectedTriangleIndices.Add(currentTriangleIndex);
            IReadOnlyList<int> neighbors = topology.GetAdjacentTriangles(currentTriangleIndex);

            for (int i = 0; i < neighbors.Count; i++)
            {
                int nextTriangleIndex = neighbors[i];

                if (includedTriangles[nextTriangleIndex])
                {
                    continue;
                }

                float dot = Math.Clamp(Vector3.Dot(triangleNormals[currentTriangleIndex], triangleNormals[nextTriangleIndex]), -1.0f, 1.0f);

                if (dot < minimumDot)
                {
                    continue;
                }

                includedTriangles[nextTriangleIndex] = true;
                openTriangles.Enqueue(nextTriangleIndex);
            }
        }
    }

    /// <summary>
    /// Projects one indexed triangle vertex into screen coordinates.
    /// </summary>
    private static Vector2? ProjectTriangleVertex(
        MeshEntity mesh,
        Matrix4x4 worldTransform,
        int triangleBufferIndex,
        Func<Vector3, Vector2?> projectWorldPoint)
    {
        int vertexIndex = mesh.TriangleIndices[triangleBufferIndex];
        Vector3 worldPoint = Vector3.Transform(mesh.Vertices[vertexIndex], worldTransform);
        return projectWorldPoint(worldPoint);
    }

    /// <summary>
    /// Tests a 2D line segment against a projected triangle.
    /// </summary>
    private static bool LineIntersectsProjectedTriangle(Vector2 lineStart, Vector2 lineEnd, Vector2 a, Vector2 b, Vector2 c)
    {
        if (IsPointInTriangle(lineStart, a, b, c) || IsPointInTriangle(lineEnd, a, b, c))
        {
            return true;
        }

        return SegmentsIntersect(lineStart, lineEnd, a, b)
            || SegmentsIntersect(lineStart, lineEnd, b, c)
            || SegmentsIntersect(lineStart, lineEnd, c, a);
    }

    /// <summary>
    /// Tests a Vector2 against a 2D triangle using signed areas.
    /// </summary>
    private static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        double first = Cross(a, b, point);
        double second = Cross(b, c, point);
        double third = Cross(c, a, point);
        bool hasNegative = first < -LineIntersectionTolerance || second < -LineIntersectionTolerance || third < -LineIntersectionTolerance;
        bool hasPositive = first > LineIntersectionTolerance || second > LineIntersectionTolerance || third > LineIntersectionTolerance;
        return !(hasNegative && hasPositive);
    }

    /// <summary>
    /// Tests two screen-space segments, including collinear overlap.
    /// </summary>
    private static bool SegmentsIntersect(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        double firstDirection = Cross(secondStart, secondEnd, firstStart);
        double secondDirection = Cross(secondStart, secondEnd, firstEnd);
        double thirdDirection = Cross(firstStart, firstEnd, secondStart);
        double fourthDirection = Cross(firstStart, firstEnd, secondEnd);

        if (((firstDirection > 0.0 && secondDirection < 0.0) || (firstDirection < 0.0 && secondDirection > 0.0))
            && ((thirdDirection > 0.0 && fourthDirection < 0.0) || (thirdDirection < 0.0 && fourthDirection > 0.0)))
        {
            return true;
        }

        return IsPointOnSegment(secondStart, secondEnd, firstStart, firstDirection)
            || IsPointOnSegment(secondStart, secondEnd, firstEnd, secondDirection)
            || IsPointOnSegment(firstStart, firstEnd, secondStart, thirdDirection)
            || IsPointOnSegment(firstStart, firstEnd, secondEnd, fourthDirection);
    }

    /// <summary>
    /// Calculates a signed 2D cross product.
    /// </summary>
    private static double Cross(Vector2 lineStart, Vector2 lineEnd, Vector2 point)
    {
        return ((point.X - lineStart.X) * (lineEnd.Y - lineStart.Y))
            - ((point.Y - lineStart.Y) * (lineEnd.X - lineStart.X));
    }

    /// <summary>
    /// Tests whether one point lies on a segment when collinearity is already known.
    /// </summary>
    private static bool IsPointOnSegment(Vector2 segmentStart, Vector2 segmentEnd, Vector2 point, double cross)
    {
        if (Math.Abs(cross) > LineIntersectionTolerance)
        {
            return false;
        }

        return point.X >= Math.Min(segmentStart.X, segmentEnd.X) - LineIntersectionTolerance
            && point.X <= Math.Max(segmentStart.X, segmentEnd.X) + LineIntersectionTolerance
            && point.Y >= Math.Min(segmentStart.Y, segmentEnd.Y) - LineIntersectionTolerance
            && point.Y <= Math.Max(segmentStart.Y, segmentEnd.Y) + LineIntersectionTolerance;
    }

    /// <summary>
    /// Creates world-space triangle normals for coplanar growth.
    /// </summary>
    private static Vector3[] CreateTriangleNormals(MeshEntity mesh)
    {
        Matrix4x4 worldTransform = mesh.WorldTransform;
        int triangleCount = mesh.TriangleIndices.Count / 3;
        Vector3[] triangleNormals = new Vector3[triangleCount];

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            int baseIndex = triangleIndex * 3;
            Vector3 a = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[baseIndex]], worldTransform);
            Vector3 b = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[baseIndex + 1]], worldTransform);
            Vector3 c = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[baseIndex + 2]], worldTransform);
            triangleNormals[triangleIndex] = CalculateNormal(a, b, c);
        }

        return triangleNormals;
    }

    /// <summary>
    /// Calculates a normalized triangle normal with a stable fallback for degenerate faces.
    /// </summary>
    private static Vector3 CalculateNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);

        if (normal.LengthSquared() <= DegenerateNormalTolerance)
        {
            return Vector3.UnitZ;
        }

        return Vector3.Normalize(normal);
    }

    /// <summary>
    /// Finds the closest point on a triangle to a candidate hit point.
    /// </summary>
    private static bool TryGetClosestPointOnTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c, out Vector3 closestPoint)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 normal = Vector3.Cross(ab, ac);

        if (normal.LengthSquared() <= DegenerateNormalTolerance)
        {
            closestPoint = Vector3.Zero;
            return false;
        }

        Vector3 normalizedNormal = Vector3.Normalize(normal);
        Vector3 projectedPoint = point - (Vector3.Dot(point - a, normalizedNormal) * normalizedNormal);

        if (IsPointInsideTriangle(projectedPoint, a, b, c))
        {
            closestPoint = projectedPoint;
            return true;
        }

        closestPoint = ClosestPointOnSegment(projectedPoint, a, b);
        Vector3 bcPoint = ClosestPointOnSegment(projectedPoint, b, c);
        Vector3 caPoint = ClosestPointOnSegment(projectedPoint, c, a);

        if (Vector3.DistanceSquared(projectedPoint, bcPoint) < Vector3.DistanceSquared(projectedPoint, closestPoint))
        {
            closestPoint = bcPoint;
        }

        if (Vector3.DistanceSquared(projectedPoint, caPoint) < Vector3.DistanceSquared(projectedPoint, closestPoint))
        {
            closestPoint = caPoint;
        }

        return true;
    }

    /// <summary>
    /// Checks whether a projected point is inside one 3D triangle.
    /// </summary>
    private static bool IsPointInsideTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 v0 = b - a;
        Vector3 v1 = c - a;
        Vector3 v2 = point - a;
        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);
        float denominator = (d00 * d11) - (d01 * d01);

        if (MathF.Abs(denominator) <= DegenerateNormalTolerance)
        {
            return false;
        }

        float v = ((d11 * d20) - (d01 * d21)) / denominator;
        float w = ((d00 * d21) - (d01 * d20)) / denominator;
        float u = 1.0f - v - w;

        return u >= -0.0001f && v >= -0.0001f && w >= -0.0001f;
    }

    /// <summary>
    /// Finds the nearest point on a finite 3D segment.
    /// </summary>
    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.LengthSquared();

        if (lengthSquared <= DegenerateNormalTolerance)
        {
            return start;
        }

        float t = Math.Clamp(Vector3.Dot(point - start, segment) / lengthSquared, 0.0f, 1.0f);
        return start + (segment * t);
    }
}

