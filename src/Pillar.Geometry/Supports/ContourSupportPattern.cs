// ContourSupportPattern.cs
// Extracts connected horizontal contour paths from mesh faces and distributes support guide points along them.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Geometry.Supports;

/// <summary>
/// Describes one support guide point generated along a Contour Support path.
/// </summary>
public readonly struct ContourSupportSample
{
    /// <summary>
    /// Creates one contour support sample with a world position and source face normal.
    /// </summary>
    public ContourSupportSample(Vector3 position, Vector3 normal)
    {
        Position = position;
        Normal = normal;
    }

    /// <summary>
    /// Gets the generated support tip location on the contour.
    /// </summary>
    public Vector3 Position { get; }

    /// <summary>
    /// Gets the representative face normal at the generated support tip.
    /// </summary>
    public Vector3 Normal { get; }
}

/// <summary>
/// Carries one extracted Contour Support path and its generated support samples.
/// </summary>
public sealed class ContourSupportResult
{
    /// <summary>
    /// Creates an immutable contour extraction result.
    /// </summary>
    public ContourSupportResult(
        IReadOnlyList<Vector3> contourPoints,
        IReadOnlyList<ContourSupportSample> supportSamples,
        bool isClosed,
        float length,
        ContourSupportDiagnostics diagnostics)
    {
        ContourPoints = new ReadOnlyCollection<Vector3>(new List<Vector3>(contourPoints));
        SupportSamples = new ReadOnlyCollection<ContourSupportSample>(new List<ContourSupportSample>(supportSamples));
        IsClosed = isClosed;
        Length = length;
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    /// <summary>
    /// Gets the ordered contour points. Closed paths do not repeat the first point at the end.
    /// </summary>
    public IReadOnlyList<Vector3> ContourPoints { get; }

    /// <summary>
    /// Gets the support samples distributed along the selected contour.
    /// </summary>
    public IReadOnlyList<ContourSupportSample> SupportSamples { get; }

    /// <summary>
    /// Gets whether the selected contour is a closed loop.
    /// </summary>
    public bool IsClosed { get; }

    /// <summary>
    /// Gets the measured contour length.
    /// </summary>
    public float Length { get; }

    /// <summary>
    /// Gets extraction diagnostics that explain contour selection and topology decisions.
    /// </summary>
    public ContourSupportDiagnostics Diagnostics { get; }
}

/// <summary>
/// Captures non-rendering diagnostics from one Contour Support extraction.
/// </summary>
public sealed class ContourSupportDiagnostics
{
    /// <summary>
    /// Creates immutable extraction diagnostics for status reporting and smoke tests.
    /// </summary>
    public ContourSupportDiagnostics(
        int includedTriangleCount,
        int slicedSegmentCount,
        int assembledPathCount,
        float selectedPathLength,
        float nearestRejectedLongerPathLength,
        int endpointDegreeIssueCount,
        int thresholdBlockedAdjacencyCount,
        bool usedNearestLongerPath)
    {
        IncludedTriangleCount = includedTriangleCount;
        SlicedSegmentCount = slicedSegmentCount;
        AssembledPathCount = assembledPathCount;
        SelectedPathLength = selectedPathLength;
        NearestRejectedLongerPathLength = nearestRejectedLongerPathLength;
        EndpointDegreeIssueCount = endpointDegreeIssueCount;
        ThresholdBlockedAdjacencyCount = thresholdBlockedAdjacencyCount;
        UsedNearestLongerPath = usedNearestLongerPath;
    }

    /// <summary>
    /// Gets how many triangles were accepted into the connected seed face patch.
    /// </summary>
    public int IncludedTriangleCount { get; }

    /// <summary>
    /// Gets how many triangle-plane intersection segments were produced.
    /// </summary>
    public int SlicedSegmentCount { get; }

    /// <summary>
    /// Gets how many ordered contour paths were assembled from the slice segments.
    /// </summary>
    public int AssembledPathCount { get; }

    /// <summary>
    /// Gets the selected contour path length.
    /// </summary>
    public float SelectedPathLength { get; }

    /// <summary>
    /// Gets the nearest longer path length that was rejected, or zero when none was found.
    /// </summary>
    public float NearestRejectedLongerPathLength { get; }

    /// <summary>
    /// Gets the count of contour endpoints with ambiguous branch degree.
    /// </summary>
    public int EndpointDegreeIssueCount { get; }

    /// <summary>
    /// Gets how many adjacent triangle crossings were blocked by the coplanar threshold.
    /// </summary>
    public int ThresholdBlockedAdjacencyCount { get; }

    /// <summary>
    /// Gets whether a nearby longer contour replaced a short seed-containing path.
    /// </summary>
    public bool UsedNearestLongerPath { get; }
}

/// <summary>
/// Provides renderer-agnostic contour extraction and support distribution helpers.
/// </summary>
public static class ContourSupportPattern
{
    public const int MaximumSupportCount = 4096;
    public const float DefaultSpacing = ContourSupportSettings.DefaultSpacing;

    private const float IntersectionTolerance = 0.0001f;
    private const float AssemblyPointTolerance = 0.001f;
    private const float MinimumSegmentLength = 0.0001f;
    private const float MeshEdgePointKeyScale = 10000.0f;
    private const float ContourAssemblyPointKeyScale = 1000.0f;
    private const float SeedPathReplacementLengthRatio = 1.25f;
    private const float MinimumAlternatePathDistance = 0.5f;
    private const float MaximumAlternatePathDistance = 2.0f;
    private const float AlternatePathSpacingFactor = 0.1f;

    /// <summary>
    /// Extracts the active contour using the mesh's current world transform.
    /// </summary>
    public static bool TryCreate(MeshEntity mesh, ContourSupportSettings settings, out ContourSupportResult result)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return TryCreate(mesh, mesh.WorldTransform, settings, out result);
    }

    /// <summary>
    /// Extracts the active contour using an explicit world transform.
    /// </summary>
    public static bool TryCreate(MeshEntity mesh, Matrix4x4 worldTransform, ContourSupportSettings settings, out ContourSupportResult result)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        int triangleCount = mesh.TriangleIndices.Count / 3;

        if (settings.SeedTriangleIndex < 0 || settings.SeedTriangleIndex >= triangleCount)
        {
            result = null!;
            return false;
        }

        List<Vector3> worldVertices = CreateWorldVertices(mesh, worldTransform);
        Vector3[] triangleNormals = CreateTriangleNormals(mesh, worldVertices);
        List<int>[] adjacency = CreateTriangleAdjacency(mesh, triangleCount);
        int thresholdBlockedAdjacencyCount;
        bool[] includedTriangles = CreateConnectedFacePatch(
            settings.SeedTriangleIndex,
            settings.CoplanarThresholdDegrees,
            triangleNormals,
            adjacency,
            out thresholdBlockedAdjacencyCount);
        List<ContourSegment> segments = CreateContourSegments(mesh, worldVertices, triangleNormals, includedTriangles, settings.ZHeight);

        if (segments.Count == 0)
        {
            result = null!;
            return false;
        }

        int endpointDegreeIssueCount;
        List<ContourPath> paths = AssemblePaths(segments, out endpointDegreeIssueCount);
        float nearestRejectedLongerPathLength;
        bool usedNearestLongerPath;
        ContourPath? selectedPath = SelectPath(
            paths,
            settings,
            out nearestRejectedLongerPathLength,
            out usedNearestLongerPath);

        if (selectedPath == null)
        {
            result = null!;
            return false;
        }

        selectedPath.OrientOpenPathFromSeed(settings.SeedPoint);
        List<ContourSupportSample> supportSamples = new List<ContourSupportSample>();
        selectedPath.FillSupportSamples(settings, supportSamples);

        if (supportSamples.Count == 0)
        {
            result = null!;
            return false;
        }

        ContourSupportDiagnostics diagnostics = new ContourSupportDiagnostics(
            CountIncludedTriangles(includedTriangles),
            segments.Count,
            paths.Count,
            selectedPath.Length,
            nearestRejectedLongerPathLength,
            endpointDegreeIssueCount,
            thresholdBlockedAdjacencyCount,
            usedNearestLongerPath);

        result = new ContourSupportResult(selectedPath.Points, supportSamples, selectedPath.IsClosed, selectedPath.Length, diagnostics);
        return true;
    }

    /// <summary>
    /// Finds the triangle index under a world-space mesh hit point.
    /// </summary>
    public static bool TryFindContainingTriangleIndex(MeshEntity mesh, Vector3 worldPoint, out int triangleIndex)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return TryFindContainingTriangleIndex(mesh, mesh.WorldTransform, worldPoint, out triangleIndex);
    }

    /// <summary>
    /// Finds the triangle index under a world-space mesh hit point using an explicit transform.
    /// </summary>
    public static bool TryFindContainingTriangleIndex(MeshEntity mesh, Matrix4x4 worldTransform, Vector3 worldPoint, out int triangleIndex)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

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
    /// Creates transformed vertex positions once per contour calculation.
    /// </summary>
    private static List<Vector3> CreateWorldVertices(MeshEntity mesh, Matrix4x4 worldTransform)
    {
        List<Vector3> worldVertices = new List<Vector3>(mesh.Vertices.Count);

        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            worldVertices.Add(Vector3.Transform(mesh.Vertices[i], worldTransform));
        }

        return worldVertices;
    }

    /// <summary>
    /// Creates world-space triangle normals for adjacency threshold checks and support heads.
    /// </summary>
    private static Vector3[] CreateTriangleNormals(MeshEntity mesh, IReadOnlyList<Vector3> worldVertices)
    {
        int triangleCount = mesh.TriangleIndices.Count / 3;
        Vector3[] triangleNormals = new Vector3[triangleCount];

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            int baseIndex = triangleIndex * 3;
            Vector3 a = worldVertices[mesh.TriangleIndices[baseIndex]];
            Vector3 b = worldVertices[mesh.TriangleIndices[baseIndex + 1]];
            Vector3 c = worldVertices[mesh.TriangleIndices[baseIndex + 2]];
            triangleNormals[triangleIndex] = CalculateNormal(a, b, c);
        }

        return triangleNormals;
    }

    /// <summary>
    /// Builds triangle adjacency from shared geometric mesh edges.
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
            Vector3 a = mesh.Vertices[mesh.TriangleIndices[baseIndex]];
            Vector3 b = mesh.Vertices[mesh.TriangleIndices[baseIndex + 1]];
            Vector3 c = mesh.Vertices[mesh.TriangleIndices[baseIndex + 2]];

            AddTriangleEdge(a, b, triangleIndex, edgeOwnersByEdge, adjacency);
            AddTriangleEdge(b, c, triangleIndex, edgeOwnersByEdge, adjacency);
            AddTriangleEdge(c, a, triangleIndex, edgeOwnersByEdge, adjacency);
        }

        return adjacency;
    }

    /// <summary>
    /// Adds one geometric edge and links triangles when the edge has already been seen.
    /// </summary>
    private static void AddTriangleEdge(
        Vector3 firstVertex,
        Vector3 secondVertex,
        int triangleIndex,
        Dictionary<MeshEdgeKey, List<int>> edgeOwnersByEdge,
        List<int>[] adjacency)
    {
        MeshEdgeKey edgeKey = new MeshEdgeKey(
            new PointKey(firstVertex, MeshEdgePointKeyScale),
            new PointKey(secondVertex, MeshEdgePointKeyScale));

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
    /// Flood-fills the connected face patch from the seed triangle using the normal angle threshold.
    /// </summary>
    private static bool[] CreateConnectedFacePatch(
        int seedTriangleIndex,
        float thresholdDegrees,
        IReadOnlyList<Vector3> triangleNormals,
        IReadOnlyList<int>[] adjacency,
        out int thresholdBlockedAdjacencyCount)
    {
        bool[] includedTriangles = new bool[triangleNormals.Count];
        Queue<int> openTriangles = new Queue<int>();
        float thresholdRadians = thresholdDegrees * (MathF.PI / 180.0f);
        float minimumDot = MathF.Cos(thresholdRadians);
        thresholdBlockedAdjacencyCount = 0;

        includedTriangles[seedTriangleIndex] = true;
        openTriangles.Enqueue(seedTriangleIndex);

        while (openTriangles.Count > 0)
        {
            int currentTriangleIndex = openTriangles.Dequeue();
            Vector3 currentNormal = triangleNormals[currentTriangleIndex];
            IReadOnlyList<int> neighbors = adjacency[currentTriangleIndex];

            for (int i = 0; i < neighbors.Count; i++)
            {
                int nextTriangleIndex = neighbors[i];

                if (includedTriangles[nextTriangleIndex])
                {
                    continue;
                }

                float dot = Math.Clamp(Vector3.Dot(currentNormal, triangleNormals[nextTriangleIndex]), -1.0f, 1.0f);

                if (dot < minimumDot)
                {
                    thresholdBlockedAdjacencyCount++;
                    continue;
                }

                includedTriangles[nextTriangleIndex] = true;
                openTriangles.Enqueue(nextTriangleIndex);
            }
        }

        return includedTriangles;
    }

    /// <summary>
    /// Counts the triangles that were accepted into the connected face patch.
    /// </summary>
    private static int CountIncludedTriangles(IReadOnlyList<bool> includedTriangles)
    {
        int includedTriangleCount = 0;

        for (int i = 0; i < includedTriangles.Count; i++)
        {
            if (includedTriangles[i])
            {
                includedTriangleCount++;
            }
        }

        return includedTriangleCount;
    }

    /// <summary>
    /// Slices every included triangle against the requested horizontal Z plane.
    /// </summary>
    private static List<ContourSegment> CreateContourSegments(
        MeshEntity mesh,
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<Vector3> triangleNormals,
        IReadOnlyList<bool> includedTriangles,
        float zHeight)
    {
        List<ContourSegment> segments = new List<ContourSegment>();
        int triangleCount = mesh.TriangleIndices.Count / 3;

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            if (!includedTriangles[triangleIndex])
            {
                continue;
            }

            int baseIndex = triangleIndex * 3;
            Vector3 a = worldVertices[mesh.TriangleIndices[baseIndex]];
            Vector3 b = worldVertices[mesh.TriangleIndices[baseIndex + 1]];
            Vector3 c = worldVertices[mesh.TriangleIndices[baseIndex + 2]];

            if (TryCreateIntersectionSegment(a, b, c, zHeight, out Vector3 start, out Vector3 end)
                && Vector3.Distance(start, end) > MinimumSegmentLength)
            {
                segments.Add(new ContourSegment(start, end, triangleNormals[triangleIndex], triangleIndex));
            }
        }

        return segments;
    }

    /// <summary>
    /// Creates one line segment from a triangle-plane intersection.
    /// </summary>
    private static bool TryCreateIntersectionSegment(Vector3 a, Vector3 b, Vector3 c, float zHeight, out Vector3 start, out Vector3 end)
    {
        List<Vector3> intersectionPoints = new List<Vector3>(4);
        AddEdgeIntersection(a, b, zHeight, intersectionPoints);
        AddEdgeIntersection(b, c, zHeight, intersectionPoints);
        AddEdgeIntersection(c, a, zHeight, intersectionPoints);
        RemoveDuplicatePoints(intersectionPoints);

        if (intersectionPoints.Count < 2)
        {
            start = Vector3.Zero;
            end = Vector3.Zero;
            return false;
        }

        start = intersectionPoints[0];
        end = FindFarthestPoint(start, intersectionPoints);
        return Vector3.Distance(start, end) > MinimumSegmentLength;
    }

    /// <summary>
    /// Adds all useful intersections for one triangle edge and the horizontal contour plane.
    /// </summary>
    private static void AddEdgeIntersection(Vector3 a, Vector3 b, float zHeight, List<Vector3> intersectionPoints)
    {
        float da = a.Z - zHeight;
        float db = b.Z - zHeight;
        bool aOnPlane = MathF.Abs(da) <= IntersectionTolerance;
        bool bOnPlane = MathF.Abs(db) <= IntersectionTolerance;

        if (aOnPlane)
        {
            intersectionPoints.Add(new Vector3(a.X, a.Y, zHeight));
        }

        if (bOnPlane)
        {
            intersectionPoints.Add(new Vector3(b.X, b.Y, zHeight));
        }

        if (aOnPlane || bOnPlane)
        {
            return;
        }

        if ((da < 0.0f && db > 0.0f) || (da > 0.0f && db < 0.0f))
        {
            float t = da / (da - db);
            intersectionPoints.Add(Vector3.Lerp(a, b, t));
        }
    }

    /// <summary>
    /// Removes repeated intersection points caused by vertices exactly on the contour plane.
    /// </summary>
    private static void RemoveDuplicatePoints(List<Vector3> points)
    {
        for (int i = points.Count - 1; i >= 0; i--)
        {
            for (int j = 0; j < i; j++)
            {
                if (Vector3.Distance(points[i], points[j]) <= IntersectionTolerance)
                {
                    points.RemoveAt(i);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Finds the point farthest from a starting point in a small intersection list.
    /// </summary>
    private static Vector3 FindFarthestPoint(Vector3 start, IReadOnlyList<Vector3> points)
    {
        Vector3 farthestPoint = points[0];
        float farthestDistanceSquared = -1.0f;

        for (int i = 0; i < points.Count; i++)
        {
            float distanceSquared = Vector3.DistanceSquared(start, points[i]);

            if (distanceSquared > farthestDistanceSquared)
            {
                farthestDistanceSquared = distanceSquared;
                farthestPoint = points[i];
            }
        }

        return farthestPoint;
    }

    /// <summary>
    /// Assembles unordered contour segments into ordered paths.
    /// </summary>
    private static List<ContourPath> AssemblePaths(IReadOnlyList<ContourSegment> segments, out int endpointDegreeIssueCount)
    {
        bool[] usedSegments = new bool[segments.Count];
        Dictionary<PointKey, List<int>> segmentIndicesByEndpoint = CreateSegmentEndpointMap(segments);
        endpointDegreeIssueCount = CountEndpointDegreeIssues(segmentIndicesByEndpoint);
        List<ContourPath> paths = new List<ContourPath>();

        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            if (usedSegments[segmentIndex])
            {
                continue;
            }

            ContourPath path = new ContourPath();
            path.Points.Add(segments[segmentIndex].Start);
            path.Points.Add(segments[segmentIndex].End);
            path.SegmentNormals.Add(segments[segmentIndex].Normal);
            path.SourceTriangleIndices.Add(segments[segmentIndex].TriangleIndex);
            usedSegments[segmentIndex] = true;

            ExtendPath(path, segments, segmentIndicesByEndpoint, usedSegments, extendAtEnd: true);
            ExtendPath(path, segments, segmentIndicesByEndpoint, usedSegments, extendAtEnd: false);
            path.NormalizeClosure();

            if (path.Points.Count >= 2)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    /// <summary>
    /// Counts endpoints where more than two slice segments meet and path assembly becomes ambiguous.
    /// </summary>
    private static int CountEndpointDegreeIssues(IReadOnlyDictionary<PointKey, List<int>> segmentIndicesByEndpoint)
    {
        int endpointDegreeIssueCount = 0;

        foreach (KeyValuePair<PointKey, List<int>> endpointSegments in segmentIndicesByEndpoint)
        {
            if (endpointSegments.Value.Count > 2)
            {
                endpointDegreeIssueCount++;
            }
        }

        return endpointDegreeIssueCount;
    }

    /// <summary>
    /// Creates a lookup from quantized segment endpoints to the segments touching those endpoints.
    /// </summary>
    private static Dictionary<PointKey, List<int>> CreateSegmentEndpointMap(IReadOnlyList<ContourSegment> segments)
    {
        Dictionary<PointKey, List<int>> segmentIndicesByEndpoint = new Dictionary<PointKey, List<int>>();

        for (int i = 0; i < segments.Count; i++)
        {
            AddSegmentEndpoint(segmentIndicesByEndpoint, segments[i].StartKey, i);
            AddSegmentEndpoint(segmentIndicesByEndpoint, segments[i].EndKey, i);
        }

        return segmentIndicesByEndpoint;
    }

    /// <summary>
    /// Adds one segment endpoint to the contour assembly lookup.
    /// </summary>
    private static void AddSegmentEndpoint(Dictionary<PointKey, List<int>> segmentIndicesByEndpoint, PointKey endpointKey, int segmentIndex)
    {
        if (segmentIndicesByEndpoint.TryGetValue(endpointKey, out List<int>? segmentIndices))
        {
            segmentIndices.Add(segmentIndex);
            return;
        }

        segmentIndicesByEndpoint.Add(endpointKey, new List<int> { segmentIndex });
    }

    /// <summary>
    /// Extends a path from either end by consuming any segment touching that quantized endpoint.
    /// </summary>
    private static void ExtendPath(
        ContourPath path,
        IReadOnlyList<ContourSegment> segments,
        IReadOnlyDictionary<PointKey, List<int>> segmentIndicesByEndpoint,
        bool[] usedSegments,
        bool extendAtEnd)
    {
        bool didExtend = true;

        while (didExtend)
        {
            didExtend = false;
            Vector3 endpoint = extendAtEnd ? path.Points[path.Points.Count - 1] : path.Points[0];
            PointKey endpointKey = new PointKey(endpoint, ContourAssemblyPointKeyScale);

            if (!segmentIndicesByEndpoint.TryGetValue(endpointKey, out List<int>? candidateSegmentIndices))
            {
                return;
            }

            for (int i = 0; i < candidateSegmentIndices.Count; i++)
            {
                int segmentIndex = candidateSegmentIndices[i];

                if (usedSegments[segmentIndex])
                {
                    continue;
                }

                ContourSegment segment = segments[segmentIndex];

                if (endpointKey.Equals(segment.StartKey))
                {
                    path.AddSegment(segment.End, segment.Normal, segment.TriangleIndex, extendAtEnd);
                    usedSegments[segmentIndex] = true;
                    didExtend = true;
                    break;
                }

                if (endpointKey.Equals(segment.EndKey))
                {
                    path.AddSegment(segment.Start, segment.Normal, segment.TriangleIndex, extendAtEnd);
                    usedSegments[segmentIndex] = true;
                    didExtend = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Selects the contour containing the seed face intersection, or the nearest contour after Z edits.
    /// </summary>
    private static ContourPath? SelectPath(
        IReadOnlyList<ContourPath> paths,
        ContourSupportSettings settings,
        out float nearestRejectedLongerPathLength,
        out bool usedNearestLongerPath)
    {
        ContourPath? seedPath = null;
        ContourPath? nearestPath = null;
        ContourPath? nearestLongerPath = null;
        float nearestDistanceSquared = float.MaxValue;
        float nearestLongerDistanceSquared = float.MaxValue;
        Vector3 projectedSeed = new Vector3(settings.SeedPoint.X, settings.SeedPoint.Y, settings.ZHeight);
        nearestRejectedLongerPathLength = 0.0f;
        usedNearestLongerPath = false;

        for (int i = 0; i < paths.Count; i++)
        {
            ContourPath path = paths[i];

            if (path.ContainsSourceTriangle(settings.SeedTriangleIndex))
            {
                if (seedPath == null || path.Length > seedPath.Length)
                {
                    seedPath = path;
                }
            }

            float distanceSquared = path.DistanceSquaredTo(projectedSeed);

            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestPath = path;
            }
        }

        if (seedPath == null)
        {
            return nearestPath;
        }

        for (int i = 0; i < paths.Count; i++)
        {
            ContourPath path = paths[i];

            if (ReferenceEquals(path, seedPath) || path.Length <= seedPath.Length * SeedPathReplacementLengthRatio)
            {
                continue;
            }

            float distanceSquared = path.DistanceSquaredTo(projectedSeed);

            if (distanceSquared < nearestLongerDistanceSquared)
            {
                nearestLongerDistanceSquared = distanceSquared;
                nearestLongerPath = path;
            }
        }

        if (nearestLongerPath == null)
        {
            return seedPath;
        }

        nearestRejectedLongerPathLength = nearestLongerPath.Length;
        float alternatePathDistance = MathF.Min(
            MaximumAlternatePathDistance,
            MathF.Max(MinimumAlternatePathDistance, settings.Spacing * AlternatePathSpacingFactor));

        if (nearestLongerDistanceSquared <= alternatePathDistance * alternatePathDistance)
        {
            usedNearestLongerPath = true;
            nearestRejectedLongerPathLength = 0.0f;
            return nearestLongerPath;
        }

        return seedPath;
    }

    /// <summary>
    /// Calculates a stable triangle normal with a vertical fallback for degenerate triangles.
    /// </summary>
    private static Vector3 CalculateNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);

        if (normal.LengthSquared() <= 0.00000001f)
        {
            return Vector3.UnitZ;
        }

        return Vector3.Normalize(normal);
    }

    /// <summary>
    /// Checks whether two contour points are equivalent for assembly purposes.
    /// </summary>
    private static bool AreSamePoint(Vector3 first, Vector3 second)
    {
        return Vector3.Distance(first, second) <= AssemblyPointTolerance;
    }

    /// <summary>
    /// Finds the closest point on a triangle to a candidate hit point.
    /// </summary>
    private static bool TryGetClosestPointOnTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c, out Vector3 closestPoint)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 normal = Vector3.Cross(ab, ac);

        if (normal.LengthSquared() <= 0.00000001f)
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
    /// Checks whether a projected point is inside a triangle using barycentric coordinates.
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

        if (MathF.Abs(denominator) <= 0.00000001f)
        {
            return false;
        }

        float v = ((d11 * d20) - (d01 * d21)) / denominator;
        float w = ((d00 * d21) - (d01 * d20)) / denominator;
        float u = 1.0f - v - w;

        return u >= -IntersectionTolerance && v >= -IntersectionTolerance && w >= -IntersectionTolerance;
    }

    /// <summary>
    /// Finds the nearest point on a finite segment.
    /// </summary>
    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.LengthSquared();

        if (lengthSquared <= 0.00000001f)
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
    /// Stores one quantized contour point for path assembly.
    /// </summary>
    private readonly struct PointKey : IEquatable<PointKey>
    {
        public PointKey(Vector3 point)
            : this(point, ContourAssemblyPointKeyScale)
        {
        }

        public PointKey(Vector3 point, float keyScale)
        {
            X = Quantize(point.X, keyScale);
            Y = Quantize(point.Y, keyScale);
            Z = Quantize(point.Z, keyScale);
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

        private static long Quantize(float value, float keyScale)
        {
            return (long)MathF.Round(value * keyScale);
        }
    }

    /// <summary>
    /// Stores one raw contour segment before assembly.
    /// </summary>
    private readonly struct ContourSegment
    {
        public ContourSegment(Vector3 start, Vector3 end, Vector3 normal, int triangleIndex)
        {
            Start = start;
            End = end;
            StartKey = new PointKey(start);
            EndKey = new PointKey(end);
            Normal = normal;
            TriangleIndex = triangleIndex;
        }

        public Vector3 Start { get; }

        public Vector3 End { get; }

        public PointKey StartKey { get; }

        public PointKey EndKey { get; }

        public Vector3 Normal { get; }

        public int TriangleIndex { get; }
    }

    /// <summary>
    /// Stores one ordered contour path and the segment normals used to sample it.
    /// </summary>
    private sealed class ContourPath
    {
        public List<Vector3> Points { get; } = new List<Vector3>();

        public List<Vector3> SegmentNormals { get; } = new List<Vector3>();

        public List<int> SourceTriangleIndices { get; } = new List<int>();

        public bool IsClosed { get; private set; }

        public float Length
        {
            get
            {
                float length = 0.0f;

                for (int i = 1; i < Points.Count; i++)
                {
                    length += Vector3.Distance(Points[i - 1], Points[i]);
                }

                if (IsClosed && Points.Count > 2)
                {
                    length += Vector3.Distance(Points[Points.Count - 1], Points[0]);
                }

                return length;
            }
        }

        /// <summary>
        /// Adds a connected segment to the start or end of the path.
        /// </summary>
        public void AddSegment(Vector3 point, Vector3 normal, int triangleIndex, bool addAtEnd)
        {
            if (addAtEnd)
            {
                Points.Add(point);
                SegmentNormals.Add(normal);
                SourceTriangleIndices.Add(triangleIndex);
                return;
            }

            Points.Insert(0, point);
            SegmentNormals.Insert(0, normal);
            SourceTriangleIndices.Insert(0, triangleIndex);
        }

        /// <summary>
        /// Marks closed paths and removes the repeated endpoint used during assembly.
        /// </summary>
        public void NormalizeClosure()
        {
            if (Points.Count < 3 || !AreSamePoint(Points[0], Points[Points.Count - 1]))
            {
                IsClosed = false;
                return;
            }

            Points.RemoveAt(Points.Count - 1);
            IsClosed = true;
        }

        /// <summary>
        /// Reverses open paths when needed so the start is nearest the user's original click.
        /// </summary>
        public void OrientOpenPathFromSeed(Vector3 seedPoint)
        {
            if (IsClosed || Points.Count < 2)
            {
                return;
            }

            float firstDistanceSquared = Vector3.DistanceSquared(Points[0], seedPoint);
            float lastDistanceSquared = Vector3.DistanceSquared(Points[Points.Count - 1], seedPoint);

            if (lastDistanceSquared >= firstDistanceSquared)
            {
                return;
            }

            Points.Reverse();
            SegmentNormals.Reverse();
            SourceTriangleIndices.Reverse();
        }

        /// <summary>
        /// Gets whether any segment in this path came from the seed triangle.
        /// </summary>
        public bool ContainsSourceTriangle(int triangleIndex)
        {
            for (int i = 0; i < SourceTriangleIndices.Count; i++)
            {
                if (SourceTriangleIndices[i] == triangleIndex)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Measures the closest distance from the path to a point.
        /// </summary>
        public float DistanceSquaredTo(Vector3 point)
        {
            float bestDistanceSquared = float.MaxValue;

            for (int i = 1; i < Points.Count; i++)
            {
                Vector3 closestPoint = ClosestPointOnSegment(point, Points[i - 1], Points[i]);
                bestDistanceSquared = Math.Min(bestDistanceSquared, Vector3.DistanceSquared(point, closestPoint));
            }

            if (IsClosed && Points.Count > 2)
            {
                Vector3 closestPoint = ClosestPointOnSegment(point, Points[Points.Count - 1], Points[0]);
                bestDistanceSquared = Math.Min(bestDistanceSquared, Vector3.DistanceSquared(point, closestPoint));
            }

            return bestDistanceSquared;
        }

        /// <summary>
        /// Appends support samples along the path using closed-loop or open-path distribution rules.
        /// </summary>
        public void FillSupportSamples(ContourSupportSettings settings, List<ContourSupportSample> supportSamples)
        {
            supportSamples.Clear();

            if (Points.Count < 2)
            {
                return;
            }

            float pathLength = Length;

            if (pathLength <= MinimumSegmentLength)
            {
                return;
            }

            if (IsClosed)
            {
                int supportCount = Math.Max(3, (int)MathF.Ceiling(pathLength / settings.Spacing));
                float closedStartDistance = NormalizeClosedLoopDistance(settings.StartOffset, pathLength);

                for (int i = 0; i < supportCount && supportSamples.Count < MaximumSupportCount; i++)
                {
                    float distance = closedStartDistance + (pathLength * (i / (float)supportCount));
                    supportSamples.Add(EvaluateAtDistance(distance));
                }

                return;
            }

            float startDistance = settings.StartOffset;
            float finalDistance = pathLength - settings.FinalOffset;

            if (startDistance > finalDistance || finalDistance < 0.0f)
            {
                return;
            }

            float usableLength = finalDistance - startDistance;

            if (usableLength <= MinimumSegmentLength)
            {
                supportSamples.Add(EvaluateAtDistance(Math.Clamp(startDistance, 0.0f, pathLength)));
                return;
            }

            int intervalCount = Math.Max(1, (int)MathF.Ceiling(usableLength / settings.Spacing));
            int openSupportCount = intervalCount + 1;

            for (int i = 0; i < openSupportCount && supportSamples.Count < MaximumSupportCount; i++)
            {
                float t = i / (float)(openSupportCount - 1);
                float distance = startDistance + (usableLength * t);
                supportSamples.Add(EvaluateAtDistance(distance));
            }
        }

        /// <summary>
        /// Evaluates a support sample at a measured distance along the path.
        /// </summary>
        private ContourSupportSample EvaluateAtDistance(float distance)
        {
            float pathLength = Length;
            float remainingDistance = IsClosed
                ? NormalizeClosedLoopDistance(distance, pathLength)
                : Math.Max(0.0f, distance);

            for (int i = 1; i < Points.Count; i++)
            {
                Vector3 start = Points[i - 1];
                Vector3 end = Points[i];
                float segmentLength = Vector3.Distance(start, end);

                if (segmentLength <= MinimumSegmentLength)
                {
                    continue;
                }

                if (remainingDistance <= segmentLength)
                {
                    float t = Math.Clamp(remainingDistance / segmentLength, 0.0f, 1.0f);
                    return new ContourSupportSample(Vector3.Lerp(start, end, t), SegmentNormals[Math.Min(i - 1, SegmentNormals.Count - 1)]);
                }

                remainingDistance -= segmentLength;
            }

            if (IsClosed && Points.Count > 2)
            {
                Vector3 start = Points[Points.Count - 1];
                Vector3 end = Points[0];
                float segmentLength = Vector3.Distance(start, end);

                if (segmentLength > MinimumSegmentLength)
                {
                    float t = Math.Clamp(remainingDistance / segmentLength, 0.0f, 1.0f);
                    return new ContourSupportSample(Vector3.Lerp(start, end, t), SegmentNormals[SegmentNormals.Count - 1]);
                }
            }

            return new ContourSupportSample(Points[Points.Count - 1], SegmentNormals[SegmentNormals.Count - 1]);
        }

        /// <summary>
        /// Wraps a distance around a closed contour so offsets can rotate support positions without changing spacing.
        /// </summary>
        private static float NormalizeClosedLoopDistance(float distance, float pathLength)
        {
            if (pathLength <= MinimumSegmentLength)
            {
                return 0.0f;
            }

            float wrappedDistance = distance % pathLength;

            if (wrappedDistance < 0.0f)
            {
                wrappedDistance += pathLength;
            }

            return wrappedDistance;
        }
    }
}
