// RaftMeshBuilder.cs
// Generates support-hull footprint, Delaunay-wireframe, and per-support feet raft triangle meshes.
using Pillar.Core.Entities;
using Pillar.Core.Rafts;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Rafts;

/// <summary>
/// Builds procedural resin-print raft meshes without rendering dependencies.
/// </summary>
public static class RaftMeshBuilder
{
    private const int CircularSegmentCount = 32;
    private const float ContourTolerance = 0.0001f;
    private const float MinimumSegmentLength = 0.001f;

    /// <summary>
    /// Generates one raft from the model's support bases.
    /// </summary>
    public static RaftMeshData Build(IReadOnlyList<SupportEntity> supports, RaftSettings settings)
    {
        if (supports == null) throw new ArgumentNullException(nameof(supports));
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        List<Vector3> positions = new List<Vector3>();
        List<int> indices = new List<int>();

        switch (settings.Type)
        {
            case RaftType.Mesh:
                BuildMeshRaft(supports, settings, positions, indices);
                break;
            case RaftType.Feet:
                BuildFeetRaft(supports, settings, positions, indices);
                break;
            default:
                BuildFootprintRaft(supports, settings, positions, indices);
                break;
        }

        return new RaftMeshData(positions, indices);
    }

    /// <summary>
    /// Builds a convex, hole-free envelope around the physical footprints of all support bases.
    /// </summary>
    private static void BuildFootprintRaft(IReadOnlyList<SupportEntity> supports, RaftSettings settings, List<Vector3> positions, List<int> indices)
    {
        List<SupportBaseDisc> supportBases = GetUniqueSupportBaseDiscs(supports);
        if (supportBases.Count == 0) return;

        List<Vector2> baseEnvelope = CreateSupportBaseEnvelope(supportBases, settings.FootprintOffset);
        if (!IsValidConvexContour(baseEnvelope)) return;

        float bodyOutset = CalculateChamferInset(settings.RaftHeight, settings.EdgeAngleDegrees);
        float extraPadding = settings.LipHeight > 0.0f
            ? FindRequiredLipPadding(baseEnvelope, bodyOutset, settings.LipWidth)
            : 0.0f;

        if (!float.IsFinite(extraPadding)) return;
        if (extraPadding > 0.0f
            && !TryOffsetConvexContour(baseEnvelope, extraPadding, out baseEnvelope))
        {
            return;
        }

        if (!TryOffsetConvexContour(baseEnvelope, bodyOutset, out List<Vector2> raftTop)) return;

        AddExtrudedConvexPolygon(baseEnvelope, raftTop, 0.0f, settings.RaftHeight, positions, indices);

        if (settings.LipHeight > 0.0f)
        {
            AddLip(raftTop, settings, positions, indices);
        }
    }

    /// <summary>
    /// Adds the upper annular lip with its width measured inward from the outer top edge.
    /// </summary>
    private static void AddLip(List<Vector2> raftTop, RaftSettings settings, List<Vector3> positions, List<int> indices)
    {
        if (!TryOffsetConvexContour(raftTop, -settings.LipWidth, out List<Vector2> lowerInner)) return;

        float lipOutset = CalculateChamferInset(settings.LipHeight, settings.EdgeAngleDegrees);
        if (!TryOffsetConvexContour(raftTop, lipOutset, out List<Vector2> upperOuter)) return;
        if (!TryOffsetConvexContour(lowerInner, lipOutset, out List<Vector2> upperInner)) return;

        float lowerZ = settings.RaftHeight;
        float upperZ = settings.RaftHeight + settings.LipHeight;

        AddContourWall(raftTop, upperOuter, lowerZ, upperZ, false, positions, indices);
        AddContourWall(lowerInner, upperInner, lowerZ, upperZ, true, positions, indices);
        AddRingSurface(upperOuter, upperInner, upperZ, positions, indices);
    }

    /// <summary>
    /// Coalesces coincident support bases while retaining the largest physical base radius.
    /// </summary>
    private static List<SupportBaseDisc> GetUniqueSupportBaseDiscs(IReadOnlyList<SupportEntity> supports)
    {
        List<SupportBaseDisc> supportBases = new List<SupportBaseDisc>();
        Dictionary<(int X, int Y), int> indicesByPosition = new Dictionary<(int X, int Y), int>();

        for (int i = 0; i < supports.Count; i++)
        {
            Vector3 basePosition = supports[i].BasePosition;
            Vector2 center = new Vector2(basePosition.X, basePosition.Y);
            float radius = supports[i].Profile.BaseBottomRadius;
            (int X, int Y) key = (
                (int)MathF.Round(basePosition.X * 1000.0f),
                (int)MathF.Round(basePosition.Y * 1000.0f));

            if (indicesByPosition.TryGetValue(key, out int existingIndex))
            {
                SupportBaseDisc existing = supportBases[existingIndex];
                if (radius > existing.Radius)
                {
                    supportBases[existingIndex] = new SupportBaseDisc(existing.Center, radius);
                }

                continue;
            }

            indicesByPosition.Add(key, supportBases.Count);
            supportBases.Add(new SupportBaseDisc(center, radius));
        }

        return supportBases;
    }

    /// <summary>
    /// Samples each physical support base disc and returns their counter-clockwise convex envelope.
    /// </summary>
    private static List<Vector2> CreateSupportBaseEnvelope(IReadOnlyList<SupportBaseDisc> supportBases, float footprintOffset)
    {
        List<Vector2> samples = new List<Vector2>(supportBases.Count * CircularSegmentCount);
        float angularStep = MathF.Tau / CircularSegmentCount;
        float circumscribedScale = 1.0f / MathF.Cos(MathF.PI / CircularSegmentCount);

        for (int baseIndex = 0; baseIndex < supportBases.Count; baseIndex++)
        {
            SupportBaseDisc supportBase = supportBases[baseIndex];
            float sampleRadius = (supportBase.Radius + footprintOffset) * circumscribedScale;

            for (int segmentIndex = 0; segmentIndex < CircularSegmentCount; segmentIndex++)
            {
                float angle = segmentIndex * angularStep;
                samples.Add(supportBase.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * sampleRadius);
            }
        }

        return CreateConvexHull(samples);
    }

    /// <summary>
    /// Computes a counter-clockwise monotone-chain convex hull without retaining collinear points.
    /// </summary>
    private static List<Vector2> CreateConvexHull(List<Vector2> points)
    {
        points.Sort((Vector2 first, Vector2 second) =>
        {
            int xComparison = first.X.CompareTo(second.X);
            return xComparison != 0 ? xComparison : first.Y.CompareTo(second.Y);
        });

        List<Vector2> uniquePoints = new List<Vector2>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            if (uniquePoints.Count == 0
                || Vector2.DistanceSquared(uniquePoints[uniquePoints.Count - 1], points[i]) > ContourTolerance * ContourTolerance)
            {
                uniquePoints.Add(points[i]);
            }
        }

        if (uniquePoints.Count < 3) return uniquePoints;

        List<Vector2> hull = new List<Vector2>(uniquePoints.Count * 2);
        for (int i = 0; i < uniquePoints.Count; i++)
        {
            while (hull.Count >= 2
                   && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], uniquePoints[i] - hull[hull.Count - 1]) <= ContourTolerance)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(uniquePoints[i]);
        }

        int lowerCount = hull.Count;
        for (int i = uniquePoints.Count - 2; i >= 0; i--)
        {
            while (hull.Count > lowerCount
                   && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], uniquePoints[i] - hull[hull.Count - 1]) <= ContourTolerance)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(uniquePoints[i]);
        }

        hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    /// <summary>
    /// Builds one tapered square foot beneath every unique support base.
    /// </summary>
    private static void BuildFeetRaft(IReadOnlyList<SupportEntity> supports, RaftSettings settings, List<Vector3> positions, List<int> indices)
    {
        HashSet<(int X, int Y)> usedBases = new HashSet<(int X, int Y)>();
        float bottomHalfSize = settings.FootSize * 0.5f;
        float topHalfSize = bottomHalfSize + CalculateChamferInset(settings.RaftHeight, settings.EdgeAngleDegrees);

        for (int i = 0; i < supports.Count; i++)
        {
            Vector3 basePosition = supports[i].BasePosition;
            (int X, int Y) key = ((int)MathF.Round(basePosition.X * 1000.0f), (int)MathF.Round(basePosition.Y * 1000.0f));

            if (usedBases.Add(key))
            {
                AddBoxFrustum(new Vector2(basePosition.X, basePosition.Y), bottomHalfSize, topHalfSize, settings.RaftHeight, positions, indices);
            }
        }
    }

    /// <summary>
    /// Builds rectangular-prism edges from the unique edges of a Delaunay triangulation.
    /// </summary>
    private static void BuildMeshRaft(IReadOnlyList<SupportEntity> supports, RaftSettings settings, List<Vector3> positions, List<int> indices)
    {
        List<SupportBaseDisc> supportBases = GetUniqueSupportBaseDiscs(supports);
        if (supportBases.Count == 1)
        {
            float bottomRadius = supportBases[0].Radius;
            float topRadius = bottomRadius + CalculateChamferInset(settings.RaftThickness, settings.EdgeAngleDegrees);
            AddDiscFrustum(
                supportBases[0].Center,
                bottomRadius,
                topRadius,
                settings.RaftThickness,
                positions,
                indices);
            return;
        }

        List<Vector2> points = GetUniqueSupportBasePoints(supportBases);
        HashSet<Edge> edges = CreateDelaunayEdges(points);

        if (points.Count == 2)
        {
            edges.Add(new Edge(0, 1));
        }

        float bottomHalfWidth = settings.LineThickness * 0.5f;
        float topHalfWidth = bottomHalfWidth + CalculateChamferInset(settings.RaftThickness, settings.EdgeAngleDegrees);
        float maximumSideLengthSquared = settings.MaxSideLength * settings.MaxSideLength;

        foreach (Edge edge in edges)
        {
            Vector2 start = points[edge.A];
            Vector2 end = points[edge.B];
            if (Vector2.DistanceSquared(start, end) <= maximumSideLengthSquared)
            {
                AddSegmentFrustum(start, end, bottomHalfWidth, topHalfWidth, settings.RaftThickness, positions, indices);
            }
        }
    }

    /// <summary>
    /// Extracts centers from already-coalesced support base discs.
    /// </summary>
    private static List<Vector2> GetUniqueSupportBasePoints(IReadOnlyList<SupportBaseDisc> supportBases)
    {
        List<Vector2> points = new List<Vector2>(supportBases.Count);

        for (int i = 0; i < supportBases.Count; i++)
        {
            points.Add(supportBases[i].Center);
        }

        return points;
    }

    /// <summary>
    /// Computes Delaunay edges with incremental Bowyer-Watson triangulation.
    /// </summary>
    private static HashSet<Edge> CreateDelaunayEdges(IReadOnlyList<Vector2> points)
    {
        HashSet<Edge> edges = new HashSet<Edge>();

        if (points.Count < 2)
        {
            return edges;
        }

        List<Vector2> workingPoints = new List<Vector2>(points);
        Vector2 minimum = points[0];
        Vector2 maximum = points[0];

        for (int i = 1; i < points.Count; i++)
        {
            minimum = Vector2.Min(minimum, points[i]);
            maximum = Vector2.Max(maximum, points[i]);
        }

        Vector2 center = (minimum + maximum) * 0.5f;
        float span = MathF.Max(maximum.X - minimum.X, maximum.Y - minimum.Y);
        span = MathF.Max(span, 1.0f) * 16.0f;
        int superA = workingPoints.Count;
        int superB = superA + 1;
        int superC = superA + 2;
        workingPoints.Add(center + new Vector2(-2.0f * span, -span));
        workingPoints.Add(center + new Vector2(0.0f, 2.0f * span));
        workingPoints.Add(center + new Vector2(2.0f * span, -span));

        List<DelaunayTriangle> triangles = new List<DelaunayTriangle>
        {
            new DelaunayTriangle(superA, superB, superC)
        };

        for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            Dictionary<Edge, int> boundaryEdgeCounts = new Dictionary<Edge, int>();
            List<int> removedTriangleIndices = new List<int>();

            for (int triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++)
            {
                DelaunayTriangle triangle = triangles[triangleIndex];
                if (!TryGetCircumcircle(
                        workingPoints[triangle.A],
                        workingPoints[triangle.B],
                        workingPoints[triangle.C],
                        out Vector2 circumcenter,
                        out float radiusSquared)
                    || Vector2.DistanceSquared(workingPoints[pointIndex], circumcenter) > radiusSquared + 0.0001f)
                {
                    continue;
                }

                removedTriangleIndices.Add(triangleIndex);
                IncrementEdgeCount(boundaryEdgeCounts, new Edge(triangle.A, triangle.B));
                IncrementEdgeCount(boundaryEdgeCounts, new Edge(triangle.B, triangle.C));
                IncrementEdgeCount(boundaryEdgeCounts, new Edge(triangle.C, triangle.A));
            }

            for (int i = removedTriangleIndices.Count - 1; i >= 0; i--)
            {
                triangles.RemoveAt(removedTriangleIndices[i]);
            }

            foreach (KeyValuePair<Edge, int> pair in boundaryEdgeCounts)
            {
                if (pair.Value != 1)
                {
                    continue;
                }

                Vector2 first = workingPoints[pair.Key.A];
                Vector2 second = workingPoints[pair.Key.B];
                if (MathF.Abs(Cross(second - first, workingPoints[pointIndex] - first)) > 0.00001f)
                {
                    triangles.Add(new DelaunayTriangle(pair.Key.A, pair.Key.B, pointIndex));
                }
            }
        }

        for (int i = 0; i < triangles.Count; i++)
        {
            DelaunayTriangle triangle = triangles[i];
            if (triangle.A >= points.Count || triangle.B >= points.Count || triangle.C >= points.Count)
            {
                continue;
            }

            edges.Add(new Edge(triangle.A, triangle.B));
            edges.Add(new Edge(triangle.B, triangle.C));
            edges.Add(new Edge(triangle.C, triangle.A));
        }

        if (edges.Count == 0)
        {
            AddCollinearEdges(points, edges);
        }

        return edges;
    }

    /// <summary>
    /// Counts how many removed triangles share one cavity edge.
    /// </summary>
    private static void IncrementEdgeCount(Dictionary<Edge, int> edgeCounts, Edge edge)
    {
        edgeCounts.TryGetValue(edge, out int count);
        edgeCounts[edge] = count + 1;
    }

    /// <summary>
    /// Connects ordered points when every support base is collinear.
    /// </summary>
    private static void AddCollinearEdges(IReadOnlyList<Vector2> points, HashSet<Edge> edges)
    {
        List<int> orderedIndices = new List<int>(points.Count);
        for (int i = 0; i < points.Count; i++) orderedIndices.Add(i);

        Vector2 minimum = points[0];
        Vector2 maximum = points[0];
        for (int i = 1; i < points.Count; i++)
        {
            minimum = Vector2.Min(minimum, points[i]);
            maximum = Vector2.Max(maximum, points[i]);
        }

        bool sortByX = maximum.X - minimum.X >= maximum.Y - minimum.Y;
        orderedIndices.Sort((int first, int second) =>
        {
            float firstValue = sortByX ? points[first].X : points[first].Y;
            float secondValue = sortByX ? points[second].X : points[second].Y;
            return firstValue.CompareTo(secondValue);
        });

        for (int i = 1; i < orderedIndices.Count; i++)
        {
            edges.Add(new Edge(orderedIndices[i - 1], orderedIndices[i]));
        }
    }

    /// <summary>
    /// Calculates a triangle circumcircle.
    /// </summary>
    private static bool TryGetCircumcircle(Vector2 a, Vector2 b, Vector2 c, out Vector2 center, out float radiusSquared)
    {
        float divisor = 2.0f * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        if (MathF.Abs(divisor) < 0.00001f)
        {
            center = default;
            radiusSquared = 0.0f;
            return false;
        }

        float aSquared = a.LengthSquared();
        float bSquared = b.LengthSquared();
        float cSquared = c.LengthSquared();
        center = new Vector2(
            (aSquared * (b.Y - c.Y) + bSquared * (c.Y - a.Y) + cSquared * (a.Y - b.Y)) / divisor,
            (aSquared * (c.X - b.X) + bSquared * (a.X - c.X) + cSquared * (b.X - a.X)) / divisor);
        radiusSquared = Vector2.DistanceSquared(center, a);
        return true;
    }

    /// <summary>
    /// Adds a closed trapezoidal prism along one XY segment.
    /// </summary>
    private static void AddSegmentFrustum(Vector2 start, Vector2 end, float bottomHalfWidth, float topHalfWidth, float height, List<Vector3> positions, List<int> indices)
    {
        Vector2 direction = end - start;
        if (direction.LengthSquared() < MinimumSegmentLength * MinimumSegmentLength) return;
        direction = Vector2.Normalize(direction);
        Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
        Vector2[] bottom = { start - perpendicular * bottomHalfWidth, end - perpendicular * bottomHalfWidth, end + perpendicular * bottomHalfWidth, start + perpendicular * bottomHalfWidth };
        Vector2[] top = { start - direction * (topHalfWidth - bottomHalfWidth) - perpendicular * topHalfWidth, end + direction * (topHalfWidth - bottomHalfWidth) - perpendicular * topHalfWidth, end + direction * (topHalfWidth - bottomHalfWidth) + perpendicular * topHalfWidth, start - direction * (topHalfWidth - bottomHalfWidth) + perpendicular * topHalfWidth };
        AddPrism(bottom, top, height, positions, indices);
    }

    /// <summary>
    /// Adds one closed square frustum.
    /// </summary>
    private static void AddBoxFrustum(Vector2 center, float bottomHalfSize, float topHalfSize, float height, List<Vector3> positions, List<int> indices)
    {
        Vector2[] bottom = CreateSquare(center, bottomHalfSize);
        Vector2[] top = CreateSquare(center, topHalfSize);
        AddPrism(bottom, top, height, positions, indices);
    }

    /// <summary>
    /// Adds one circular frustum for the single-base Mesh fallback.
    /// </summary>
    private static void AddDiscFrustum(Vector2 center, float bottomRadius, float topRadius, float height, List<Vector3> positions, List<int> indices)
    {
        Vector2[] bottom = CreateCircle(center, bottomRadius);
        Vector2[] top = CreateCircle(center, topRadius);
        AddPrism(bottom, top, height, positions, indices);
    }

    /// <summary>
    /// Creates one counter-clockwise circumscribed circular contour.
    /// </summary>
    private static Vector2[] CreateCircle(Vector2 center, float radius)
    {
        Vector2[] circle = new Vector2[CircularSegmentCount];
        float angularStep = MathF.Tau / CircularSegmentCount;
        float sampleRadius = radius / MathF.Cos(MathF.PI / CircularSegmentCount);

        for (int i = 0; i < circle.Length; i++)
        {
            float angle = i * angularStep;
            circle[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * sampleRadius;
        }

        return circle;
    }

    /// <summary>
    /// Creates four counter-clockwise square corners.
    /// </summary>
    private static Vector2[] CreateSquare(Vector2 center, float halfSize)
    {
        return new[]
        {
            center + new Vector2(-halfSize, -halfSize),
            center + new Vector2(halfSize, -halfSize),
            center + new Vector2(halfSize, halfSize),
            center + new Vector2(-halfSize, halfSize)
        };
    }

    /// <summary>
    /// Adds caps and walls for equal-count bottom and top contours.
    /// </summary>
    private static void AddPrism(IReadOnlyList<Vector2> bottom, IReadOnlyList<Vector2> top, float height, List<Vector3> positions, List<int> indices)
    {
        int baseIndex = positions.Count;
        for (int i = 0; i < bottom.Count; i++) positions.Add(new Vector3(bottom[i], 0.0f));
        for (int i = 0; i < top.Count; i++) positions.Add(new Vector3(top[i], height));
        AddFan(baseIndex, bottom.Count, true, indices);
        AddFan(baseIndex + bottom.Count, top.Count, false, indices);
        AddWalls(baseIndex, baseIndex + bottom.Count, bottom.Count, false, indices);
    }

    /// <summary>
    /// Extrudes a convex polygon with centered caps and corresponding tapered contours.
    /// </summary>
    private static void AddExtrudedConvexPolygon(IReadOnlyList<Vector2> bottom, IReadOnlyList<Vector2> top, float bottomZ, float topZ, List<Vector3> positions, List<int> indices)
    {
        if (bottom.Count != top.Count || top.Count < 3) return;

        int bottomStart = positions.Count;
        for (int i = 0; i < bottom.Count; i++) positions.Add(new Vector3(bottom[i], bottomZ));

        int topStart = positions.Count;
        for (int i = 0; i < top.Count; i++) positions.Add(new Vector3(top[i], topZ));

        Vector2 bottomCenter = Vector2.Zero;
        Vector2 topCenter = Vector2.Zero;
        for (int i = 0; i < bottom.Count; i++)
        {
            bottomCenter += bottom[i];
            topCenter += top[i];
        }

        bottomCenter /= bottom.Count;
        topCenter /= top.Count;
        int bottomCenterIndex = positions.Count;
        positions.Add(new Vector3(bottomCenter, bottomZ));
        int topCenterIndex = positions.Count;
        positions.Add(new Vector3(topCenter, topZ));

        AddCenteredFan(bottomStart, bottomCenterIndex, bottom.Count, true, indices);
        AddCenteredFan(topStart, topCenterIndex, top.Count, false, indices);
        AddWalls(bottomStart, topStart, top.Count, false, indices);
    }

    /// <summary>
    /// Adds quads joining two equal-count contours.
    /// </summary>
    private static void AddContourWall(IReadOnlyList<Vector2> lower, IReadOnlyList<Vector2> upper, float lowerZ, float upperZ, bool reverse, List<Vector3> positions, List<int> indices)
    {
        if (lower.Count != upper.Count) return;
        int lowerStart = positions.Count;
        for (int i = 0; i < lower.Count; i++) positions.Add(new Vector3(lower[i], lowerZ));
        int upperStart = positions.Count;
        for (int i = 0; i < upper.Count; i++) positions.Add(new Vector3(upper[i], upperZ));
        AddWalls(lowerStart, upperStart, lower.Count, reverse, indices);
    }

    /// <summary>
    /// Adds the top surface of an equal-count polygon ring.
    /// </summary>
    private static void AddRingSurface(IReadOnlyList<Vector2> outer, IReadOnlyList<Vector2> inner, float z, List<Vector3> positions, List<int> indices)
    {
        if (outer.Count != inner.Count) return;
        int outerStart = positions.Count;
        for (int i = 0; i < outer.Count; i++) positions.Add(new Vector3(outer[i], z));
        int innerStart = positions.Count;
        for (int i = 0; i < inner.Count; i++) positions.Add(new Vector3(inner[i], z));
        AddWalls(outerStart, innerStart, outer.Count, false, indices);
    }

    /// <summary>
    /// Adds triangle pairs between corresponding contour segments.
    /// </summary>
    private static void AddWalls(int lowerStart, int upperStart, int count, bool reverse, List<int> indices)
    {
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            if (!reverse)
            {
                AddTriangle(lowerStart + i, lowerStart + next, upperStart + next, indices);
                AddTriangle(lowerStart + i, upperStart + next, upperStart + i, indices);
            }
            else
            {
                AddTriangle(lowerStart + i, upperStart + next, lowerStart + next, indices);
                AddTriangle(lowerStart + i, upperStart + i, upperStart + next, indices);
            }
        }
    }

    /// <summary>
    /// Adds a convex cap as a triangle fan.
    /// </summary>
    private static void AddFan(int start, int count, bool reverse, List<int> indices)
    {
        for (int i = 1; i < count - 1; i++)
        {
            if (reverse) AddTriangle(start, start + i + 1, start + i, indices);
            else AddTriangle(start, start + i, start + i + 1, indices);
        }
    }

    /// <summary>
    /// Adds a convex face fan around an explicit center vertex.
    /// </summary>
    private static void AddCenteredFan(int boundaryStart, int centerIndex, int count, bool reverse, List<int> indices)
    {
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            if (reverse) AddTriangle(centerIndex, boundaryStart + next, boundaryStart + i, indices);
            else AddTriangle(centerIndex, boundaryStart + i, boundaryStart + next, indices);
        }
    }

    /// <summary>
    /// Finds the smallest additional outset that leaves room for the requested lip width.
    /// </summary>
    private static float FindRequiredLipPadding(IReadOnlyList<Vector2> baseEnvelope, float bodyOutset, float lipWidth)
    {
        if (CanFitLip(baseEnvelope, bodyOutset, lipWidth)) return 0.0f;

        float lowerBound = 0.0f;
        float upperBound = MathF.Max(lipWidth, 0.001f);

        for (int i = 0; i < 16 && !CanFitLipWithPadding(baseEnvelope, upperBound, bodyOutset, lipWidth); i++)
        {
            upperBound *= 2.0f;
        }

        if (!CanFitLipWithPadding(baseEnvelope, upperBound, bodyOutset, lipWidth))
        {
            return float.PositiveInfinity;
        }

        for (int i = 0; i < 24; i++)
        {
            float midpoint = (lowerBound + upperBound) * 0.5f;
            if (CanFitLipWithPadding(baseEnvelope, midpoint, bodyOutset, lipWidth))
            {
                upperBound = midpoint;
            }
            else
            {
                lowerBound = midpoint;
            }
        }

        return upperBound;
    }

    /// <summary>
    /// Tests lip fit after uniformly expanding the support envelope.
    /// </summary>
    private static bool CanFitLipWithPadding(IReadOnlyList<Vector2> baseEnvelope, float padding, float bodyOutset, float lipWidth)
    {
        return TryOffsetConvexContour(baseEnvelope, padding, out List<Vector2> paddedEnvelope)
            && CanFitLip(paddedEnvelope, bodyOutset, lipWidth);
    }

    /// <summary>
    /// Tests whether the raft top can contain a valid inner lip contour.
    /// </summary>
    private static bool CanFitLip(IReadOnlyList<Vector2> baseEnvelope, float bodyOutset, float lipWidth)
    {
        return TryOffsetConvexContour(baseEnvelope, bodyOutset, out List<Vector2> raftTop)
            && TryOffsetConvexContour(raftTop, -lipWidth, out _);
    }

    /// <summary>
    /// Offsets a convex contour while retaining one corresponding output vertex per input vertex.
    /// </summary>
    private static bool TryOffsetConvexContour(IReadOnlyList<Vector2> contour, float distance, out List<Vector2> result)
    {
        result = new List<Vector2>(contour.Count);
        if (!IsValidConvexContour(contour)) return false;

        if (MathF.Abs(distance) <= ContourTolerance)
        {
            result.AddRange(contour);
            return true;
        }

        for (int i = 0; i < contour.Count; i++)
        {
            Vector2 previous = contour[(i + contour.Count - 1) % contour.Count];
            Vector2 current = contour[i];
            Vector2 next = contour[(i + 1) % contour.Count];
            Vector2 previousDirection = Vector2.Normalize(current - previous);
            Vector2 nextDirection = Vector2.Normalize(next - current);
            Vector2 previousNormal = new Vector2(previousDirection.Y, -previousDirection.X);
            Vector2 nextNormal = new Vector2(nextDirection.Y, -nextDirection.X);
            Vector2 firstLinePoint = current + previousNormal * distance;
            Vector2 secondLinePoint = current + nextNormal * distance;
            float divisor = Cross(previousDirection, nextDirection);
            Vector2 intersection;

            if (MathF.Abs(divisor) <= ContourTolerance)
            {
                Vector2 averagedNormal = previousNormal + nextNormal;
                averagedNormal = averagedNormal.LengthSquared() > ContourTolerance * ContourTolerance
                    ? Vector2.Normalize(averagedNormal)
                    : previousNormal;
                intersection = current + averagedNormal * distance;
            }
            else
            {
                float firstParameter = Cross(secondLinePoint - firstLinePoint, nextDirection) / divisor;
                intersection = firstLinePoint + previousDirection * firstParameter;
            }

            if (!float.IsFinite(intersection.X) || !float.IsFinite(intersection.Y)) return false;
            result.Add(intersection);
        }

        if (!IsValidConvexContour(result)) return false;
        return distance >= 0.0f || HasRequiredInset(contour, result, -distance);
    }

    /// <summary>
    /// Confirms that an inward offset stayed at least the requested distance from every source edge.
    /// </summary>
    private static bool HasRequiredInset(IReadOnlyList<Vector2> source, IReadOnlyList<Vector2> inset, float distance)
    {
        for (int pointIndex = 0; pointIndex < inset.Count; pointIndex++)
        {
            for (int edgeIndex = 0; edgeIndex < source.Count; edgeIndex++)
            {
                Vector2 edgeStart = source[edgeIndex];
                Vector2 edge = source[(edgeIndex + 1) % source.Count] - edgeStart;
                float signedDistance = Cross(edge, inset[pointIndex] - edgeStart) / edge.Length();
                if (signedDistance < distance - ContourTolerance * 2.0f) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates finite, counter-clockwise, non-degenerate convex contour geometry.
    /// </summary>
    private static bool IsValidConvexContour(IReadOnlyList<Vector2> contour)
    {
        if (contour.Count < 3 || SignedArea(contour) <= ContourTolerance) return false;

        for (int i = 0; i < contour.Count; i++)
        {
            Vector2 previous = contour[(i + contour.Count - 1) % contour.Count];
            Vector2 current = contour[i];
            Vector2 next = contour[(i + 1) % contour.Count];

            if (!float.IsFinite(current.X)
                || !float.IsFinite(current.Y)
                || Vector2.DistanceSquared(current, next) <= MinimumSegmentLength * MinimumSegmentLength
                || Cross(current - previous, next - current) < -ContourTolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Converts wall height and angle to the horizontal chamfer distance.
    /// </summary>
    private static float CalculateChamferInset(float height, float angleDegrees)
    {
        float radians = angleDegrees * MathF.PI / 180.0f;
        return angleDegrees >= 89.999f ? 0.0f : height / MathF.Tan(radians);
    }

    /// <summary>
    /// Computes signed polygon area.
    /// </summary>
    private static float SignedArea(IReadOnlyList<Vector2> contour)
    {
        float area = 0.0f;
        for (int i = 0; i < contour.Count; i++) area += Cross(contour[i], contour[(i + 1) % contour.Count]);
        return area * 0.5f;
    }

    /// <summary>
    /// Computes the 2D scalar cross product.
    /// </summary>
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>
    /// Appends one triangle.
    /// </summary>
    private static void AddTriangle(int a, int b, int c, List<int> indices)
    {
        indices.Add(a);
        indices.Add(b);
        indices.Add(c);
    }

    /// <summary>
    /// Stores one triangle in the incremental Delaunay triangulation.
    /// </summary>
    private readonly record struct DelaunayTriangle(int A, int B, int C);

    /// <summary>
    /// Stores one unique support-base center and its largest physical radius.
    /// </summary>
    private readonly record struct SupportBaseDisc(Vector2 Center, float Radius);

    /// <summary>
    /// Stores a normalized undirected point-index edge.
    /// </summary>
    private readonly record struct Edge
    {
        public Edge(int a, int b)
        {
            A = Math.Min(a, b);
            B = Math.Max(a, b);
        }

        public int A { get; }
        public int B { get; }
    }
}
