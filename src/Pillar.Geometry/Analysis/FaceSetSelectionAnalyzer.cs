// FaceSetSelectionAnalyzer.cs
// Provides renderer-agnostic mesh face queries for reusable face-set selection workflows.
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Analysis;

/// <summary>
/// Finds mesh triangles selected by click, line, or normal-angle grow operations.
/// </summary>
public static class FaceSetSelectionAnalyzer
{
    private const float EdgePointKeyScale = 10000.0f;
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
        List<int>[] adjacency = CreateTriangleAdjacency(mesh, triangleCount);
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
            IReadOnlyList<int> neighbors = adjacency[currentTriangleIndex];

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
    /// Builds local-space triangle adjacency from shared geometric edges.
    /// </summary>
    private static List<int>[] CreateTriangleAdjacency(MeshEntity mesh, int triangleCount)
    {
        List<int>[] adjacency = new List<int>[triangleCount];
        Dictionary<MeshEdgeKey, List<int>> edgeOwnersByEdge = new Dictionary<MeshEdgeKey, List<int>>();

        for (int i = 0; i < triangleCount; i++)
        {
            adjacency[i] = new List<int>(3);
        }

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            int baseIndex = triangleIndex * 3;
            AddTriangleEdge(mesh.Vertices[mesh.TriangleIndices[baseIndex]], mesh.Vertices[mesh.TriangleIndices[baseIndex + 1]], triangleIndex, edgeOwnersByEdge, adjacency);
            AddTriangleEdge(mesh.Vertices[mesh.TriangleIndices[baseIndex + 1]], mesh.Vertices[mesh.TriangleIndices[baseIndex + 2]], triangleIndex, edgeOwnersByEdge, adjacency);
            AddTriangleEdge(mesh.Vertices[mesh.TriangleIndices[baseIndex + 2]], mesh.Vertices[mesh.TriangleIndices[baseIndex]], triangleIndex, edgeOwnersByEdge, adjacency);
        }

        return adjacency;
    }

    /// <summary>
    /// Adds one mesh edge and links this triangle to previous owners of the same geometric edge.
    /// </summary>
    private static void AddTriangleEdge(
        Vector3 firstVertex,
        Vector3 secondVertex,
        int triangleIndex,
        Dictionary<MeshEdgeKey, List<int>> edgeOwnersByEdge,
        List<int>[] adjacency)
    {
        MeshEdgeKey edgeKey = new MeshEdgeKey(new PointKey(firstVertex), new PointKey(secondVertex));

        if (edgeOwnersByEdge.TryGetValue(edgeKey, out List<int>? ownerTriangleIndices))
        {
            for (int i = 0; i < ownerTriangleIndices.Count; i++)
            {
                int ownerTriangleIndex = ownerTriangleIndices[i];
                adjacency[triangleIndex].Add(ownerTriangleIndex);
                adjacency[ownerTriangleIndex].Add(triangleIndex);
            }

            ownerTriangleIndices.Add(triangleIndex);
            return;
        }

        edgeOwnersByEdge.Add(edgeKey, new List<int> { triangleIndex });
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

    /// <summary>
    /// Compares two quantized point keys in deterministic XYZ order.
    /// </summary>
    private static int ComparePointKeys(PointKey first, PointKey second)
    {
        int xComparison = first.X.CompareTo(second.X);

        if (xComparison != 0)
        {
            return xComparison;
        }

        int yComparison = first.Y.CompareTo(second.Y);

        if (yComparison != 0)
        {
            return yComparison;
        }

        return first.Z.CompareTo(second.Z);
    }

    /// <summary>
    /// Stores one mesh edge using deterministic quantized endpoint ordering.
    /// </summary>
    private readonly struct MeshEdgeKey : IEquatable<MeshEdgeKey>
    {
        public MeshEdgeKey(PointKey first, PointKey second)
        {
            if (ComparePointKeys(first, second) <= 0)
            {
                First = first;
                Second = second;
            }
            else
            {
                First = second;
                Second = first;
            }
        }

        public PointKey First { get; }

        public PointKey Second { get; }

        public bool Equals(MeshEdgeKey other)
        {
            return First.Equals(other.First) && Second.Equals(other.Second);
        }

        public override bool Equals(object? obj)
        {
            return obj is MeshEdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(First, Second);
        }
    }

    /// <summary>
    /// Stores one quantized mesh point for geometric edge matching.
    /// </summary>
    private readonly struct PointKey : IEquatable<PointKey>
    {
        public PointKey(Vector3 point)
        {
            X = Quantize(point.X);
            Y = Quantize(point.Y);
            Z = Quantize(point.Z);
        }

        public long X { get; }

        public long Y { get; }

        public long Z { get; }

        public bool Equals(PointKey other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object? obj)
        {
            return obj is PointKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        private static long Quantize(float value)
        {
            return (long)MathF.Round(value * EdgePointKeyScale);
        }
    }
}

