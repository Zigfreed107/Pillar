// AreaSupportPattern.cs
// Converts selected mesh face areas into top-down support guide points and boundary preview segments.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Selection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Geometry.Supports;

/// <summary>
/// Describes one support guide point generated inside an Area Support face island.
/// </summary>
public readonly struct AreaSupportSample
{
    /// <summary>
    /// Creates one area support sample with a world position and source face normal.
    /// </summary>
    public AreaSupportSample(Vector3 position, Vector3 normal)
    {
        Position = position;
        Normal = normal;
    }

    /// <summary>
    /// Gets the generated support tip location on the selected model faces.
    /// </summary>
    public Vector3 Position { get; }

    /// <summary>
    /// Gets the representative selected face normal at the generated support tip.
    /// </summary>
    public Vector3 Normal { get; }
}

/// <summary>
/// Describes one top-down boundary segment around a contiguous selected face island.
/// </summary>
public readonly struct AreaSupportBoundarySegment
{
    /// <summary>
    /// Creates one visible yellow boundary segment.
    /// </summary>
    public AreaSupportBoundarySegment(Vector3 start, Vector3 end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets the segment start point in world space.
    /// </summary>
    public Vector3 Start { get; }

    /// <summary>
    /// Gets the segment end point in world space.
    /// </summary>
    public Vector3 End { get; }
}

/// <summary>
/// Carries all preview and generation data for one Area Support calculation.
/// </summary>
public sealed class AreaSupportResult
{
    /// <summary>
    /// Creates an immutable Area Support result.
    /// </summary>
    public AreaSupportResult(
        IReadOnlyList<AreaSupportBoundarySegment> boundarySegments,
        IReadOnlyList<AreaSupportBoundarySegment> offsetBoundarySegments,
        IReadOnlyList<AreaSupportSample> supportSamples,
        AreaSupportDiagnostics diagnostics)
    {
        BoundarySegments = new ReadOnlyCollection<AreaSupportBoundarySegment>(new List<AreaSupportBoundarySegment>(boundarySegments));
        OffsetBoundarySegments = new ReadOnlyCollection<AreaSupportBoundarySegment>(new List<AreaSupportBoundarySegment>(offsetBoundarySegments));
        SupportSamples = new ReadOnlyCollection<AreaSupportSample>(new List<AreaSupportSample>(supportSamples));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    /// <summary>
    /// Gets topmost yellow boundary segments around selected face islands.
    /// </summary>
    public IReadOnlyList<AreaSupportBoundarySegment> BoundarySegments { get; }

    /// <summary>
    /// Gets topmost dotted preview segments for the inward half-spacing boundary offset.
    /// </summary>
    public IReadOnlyList<AreaSupportBoundarySegment> OffsetBoundarySegments { get; }

    /// <summary>
    /// Gets support samples generated inside the selected face areas.
    /// </summary>
    public IReadOnlyList<AreaSupportSample> SupportSamples { get; }

    /// <summary>
    /// Gets non-rendering diagnostic counts for status reporting.
    /// </summary>
    public AreaSupportDiagnostics Diagnostics { get; }
}

/// <summary>
/// Captures non-rendering diagnostics from one Area Support calculation.
/// </summary>
public sealed class AreaSupportDiagnostics
{
    /// <summary>
    /// Creates immutable diagnostics for status reporting and smoke tests.
    /// </summary>
    public AreaSupportDiagnostics(int selectedTriangleCount, int islandCount, int rejectedCandidateCount, int duplicateCandidateCount)
    {
        SelectedTriangleCount = selectedTriangleCount;
        IslandCount = islandCount;
        RejectedCandidateCount = rejectedCandidateCount;
        DuplicateCandidateCount = duplicateCandidateCount;
    }

    /// <summary>
    /// Gets how many selected triangles were usable for the target mesh.
    /// </summary>
    public int SelectedTriangleCount { get; }

    /// <summary>
    /// Gets how many contiguous projected face islands were processed.
    /// </summary>
    public int IslandCount { get; }

    /// <summary>
    /// Gets how many generated XY candidates could not project back onto selected faces.
    /// </summary>
    public int RejectedCandidateCount { get; }

    /// <summary>
    /// Gets how many generated XY candidates were skipped because another sample was already nearby.
    /// </summary>
    public int DuplicateCandidateCount { get; }
}

/// <summary>
/// Provides renderer-agnostic Area Support boundary extraction and support distribution helpers.
/// </summary>
public static class AreaSupportPattern
{
    public const int MaximumSupportCount = 4096;
    public const int MaximumBoundarySegmentCount = 4096;
    public const float DefaultSpacing = AreaSupportSettings.DefaultSpacing;

    private const float DegenerateTolerance = 0.000001f;
    private const float DuplicateSpacingFactor = 0.45f;
    private const float PointInsideTolerance = 0.0001f;
    private const float MeshEdgePointKeyScale = 10000.0f;
    private const float OffsetBoundarySampleSpacing = 0.5f;

    /// <summary>
    /// Creates Area Support preview and support samples using the mesh's current world transform.
    /// </summary>
    public static bool TryCreate(MeshEntity mesh, AreaSupportSettings settings, out AreaSupportResult result)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return TryCreate(mesh, mesh.WorldTransform, settings, out result);
    }

    /// <summary>
    /// Creates Area Support preview and support samples using an explicit world transform.
    /// </summary>
    public static bool TryCreate(MeshEntity mesh, Matrix4x4 worldTransform, AreaSupportSettings settings, out AreaSupportResult result)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        List<int> selectedTriangleIndices = CreateSelectedTriangleIndexList(mesh, settings);

        if (selectedTriangleIndices.Count == 0)
        {
            result = null!;
            return false;
        }

        List<Vector3> worldVertices = CreateWorldVertices(mesh, worldTransform);
        Vector3[] triangleNormals = CreateTriangleNormals(mesh, worldVertices);
        List<int>[] adjacency = CreateSelectedTriangleAdjacency(mesh, selectedTriangleIndices);
        List<AreaSupportBoundarySegment> boundarySegments = new List<AreaSupportBoundarySegment>();
        List<AreaSupportBoundarySegment> offsetBoundarySegments = new List<AreaSupportBoundarySegment>();
        List<AreaSupportSample> supportSamples = new List<AreaSupportSample>();
        List<Vector2> acceptedSamplePoints = new List<Vector2>();
        int rejectedCandidateCount = 0;
        int duplicateCandidateCount = 0;
        int islandCount = 0;

        foreach (List<int> island in CreateTriangleIslands(selectedTriangleIndices, adjacency))
        {
            if (supportSamples.Count >= MaximumSupportCount)
            {
                break;
            }

            islandCount++;
            List<ProjectedTriangle> projectedTriangles = CreateProjectedTriangles(mesh, worldVertices, triangleNormals, island);
            List<BoundaryEdge> islandBoundaryEdges = CreateBoundaryEdges(mesh, worldVertices, island);

            List<BoundaryLoop> boundaryLoops = CreateBoundaryLoops(islandBoundaryEdges);

            AppendBoundarySegments(islandBoundaryEdges, boundarySegments);
            AppendOffsetBoundarySegments(settings, projectedTriangles, islandBoundaryEdges, boundaryLoops, offsetBoundarySegments);
            FillBoundaryCandidates(mesh, worldVertices, triangleNormals, settings, projectedTriangles, islandBoundaryEdges, boundaryLoops, supportSamples, acceptedSamplePoints, ref rejectedCandidateCount, ref duplicateCandidateCount);
            FillHexGridCandidates(mesh, worldVertices, triangleNormals, settings, projectedTriangles, islandBoundaryEdges, supportSamples, acceptedSamplePoints, ref rejectedCandidateCount, ref duplicateCandidateCount);
        }

        if (supportSamples.Count == 0)
        {
            result = null!;
            return false;
        }

        AreaSupportDiagnostics diagnostics = new AreaSupportDiagnostics(
            selectedTriangleIndices.Count,
            islandCount,
            rejectedCandidateCount,
            duplicateCandidateCount);
        result = new AreaSupportResult(boundarySegments, offsetBoundarySegments, supportSamples, diagnostics);
        return true;
    }

    /// <summary>
    /// Filters settings to valid triangle indices owned by the supplied mesh.
    /// </summary>
    private static List<int> CreateSelectedTriangleIndexList(MeshEntity mesh, AreaSupportSettings settings)
    {
        int triangleCount = mesh.TriangleIndices.Count / 3;
        HashSet<int> selectedTriangleSet = new HashSet<int>();

        for (int i = 0; i < settings.SelectedFaces.Count; i++)
        {
            FaceSelectionKey face = settings.SelectedFaces[i];

            if (face.MeshEntityId == mesh.Id && face.TriangleIndex >= 0 && face.TriangleIndex < triangleCount)
            {
                selectedTriangleSet.Add(face.TriangleIndex);
            }
        }

        List<int> selectedTriangleIndices = new List<int>(selectedTriangleSet);
        selectedTriangleIndices.Sort();
        return selectedTriangleIndices;
    }

    /// <summary>
    /// Creates transformed vertex positions once per Area Support calculation.
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
    /// Creates world-space triangle normals for support head planning.
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
    /// Builds selected-triangle adjacency from shared geometric mesh edges.
    /// </summary>
    private static List<int>[] CreateSelectedTriangleAdjacency(MeshEntity mesh, IReadOnlyList<int> selectedTriangleIndices)
    {
        int triangleCount = mesh.TriangleIndices.Count / 3;
        List<int>[] adjacency = new List<int>[triangleCount];
        Dictionary<MeshEdgeKey, List<int>> edgeOwnersByEdge = new Dictionary<MeshEdgeKey, List<int>>();
        HashSet<int> selectedTriangleSet = new HashSet<int>(selectedTriangleIndices);

        for (int i = 0; i < triangleCount; i++)
        {
            adjacency[i] = new List<int>(3);
        }

        for (int i = 0; i < selectedTriangleIndices.Count; i++)
        {
            int triangleIndex = selectedTriangleIndices[i];
            int baseIndex = triangleIndex * 3;
            AddTriangleEdge(mesh.Vertices[mesh.TriangleIndices[baseIndex]], mesh.Vertices[mesh.TriangleIndices[baseIndex + 1]], triangleIndex, selectedTriangleSet, edgeOwnersByEdge, adjacency);
            AddTriangleEdge(mesh.Vertices[mesh.TriangleIndices[baseIndex + 1]], mesh.Vertices[mesh.TriangleIndices[baseIndex + 2]], triangleIndex, selectedTriangleSet, edgeOwnersByEdge, adjacency);
            AddTriangleEdge(mesh.Vertices[mesh.TriangleIndices[baseIndex + 2]], mesh.Vertices[mesh.TriangleIndices[baseIndex]], triangleIndex, selectedTriangleSet, edgeOwnersByEdge, adjacency);
        }

        return adjacency;
    }

    /// <summary>
    /// Adds one geometric edge and links selected triangles when the edge has already been seen.
    /// </summary>
    private static void AddTriangleEdge(
        Vector3 firstVertex,
        Vector3 secondVertex,
        int triangleIndex,
        HashSet<int> selectedTriangleSet,
        Dictionary<MeshEdgeKey, List<int>> edgeOwnersByEdge,
        List<int>[] adjacency)
    {
        _ = selectedTriangleSet;
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
    /// Splits selected triangles into contiguous islands.
    /// </summary>
    private static List<List<int>> CreateTriangleIslands(IReadOnlyList<int> selectedTriangleIndices, IReadOnlyList<int>[] adjacency)
    {
        List<List<int>> islands = new List<List<int>>();
        HashSet<int> unvisited = new HashSet<int>(selectedTriangleIndices);
        Queue<int> openTriangles = new Queue<int>();

        while (unvisited.Count > 0)
        {
            int seedTriangleIndex = GetFirst(unvisited);
            List<int> island = new List<int>();
            unvisited.Remove(seedTriangleIndex);
            openTriangles.Enqueue(seedTriangleIndex);

            while (openTriangles.Count > 0)
            {
                int currentTriangleIndex = openTriangles.Dequeue();
                island.Add(currentTriangleIndex);
                IReadOnlyList<int> neighbors = adjacency[currentTriangleIndex];

                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighborTriangleIndex = neighbors[i];

                    if (!unvisited.Remove(neighborTriangleIndex))
                    {
                        continue;
                    }

                    openTriangles.Enqueue(neighborTriangleIndex);
                }
            }

            island.Sort();
            islands.Add(island);
        }

        return islands;
    }

    /// <summary>
    /// Creates XY-projected triangles used for inside tests and vertical projection.
    /// </summary>
    private static List<ProjectedTriangle> CreateProjectedTriangles(
        MeshEntity mesh,
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<Vector3> triangleNormals,
        IReadOnlyList<int> triangleIndices)
    {
        List<ProjectedTriangle> triangles = new List<ProjectedTriangle>(triangleIndices.Count);

        for (int i = 0; i < triangleIndices.Count; i++)
        {
            int triangleIndex = triangleIndices[i];
            int baseIndex = triangleIndex * 3;
            Vector3 a = worldVertices[mesh.TriangleIndices[baseIndex]];
            Vector3 b = worldVertices[mesh.TriangleIndices[baseIndex + 1]];
            Vector3 c = worldVertices[mesh.TriangleIndices[baseIndex + 2]];

            if (MathF.Abs(Cross(ToVector2(b) - ToVector2(a), ToVector2(c) - ToVector2(a))) <= DegenerateTolerance)
            {
                continue;
            }

            triangles.Add(new ProjectedTriangle(a, b, c, triangleNormals[triangleIndex]));
        }

        return triangles;
    }

    /// <summary>
    /// Creates boundary edges by keeping selected triangle edges with no neighbour in the same island.
    /// </summary>
    private static List<BoundaryEdge> CreateBoundaryEdges(MeshEntity mesh, IReadOnlyList<Vector3> worldVertices, IReadOnlyList<int> island)
    {
        Dictionary<MeshEdgeKey, BoundaryEdgeAccumulator> edgesByKey = new Dictionary<MeshEdgeKey, BoundaryEdgeAccumulator>();

        for (int i = 0; i < island.Count; i++)
        {
            int triangleIndex = island[i];
            int baseIndex = triangleIndex * 3;
            AddBoundaryCandidate(mesh.Vertices[mesh.TriangleIndices[baseIndex]], mesh.Vertices[mesh.TriangleIndices[baseIndex + 1]], worldVertices[mesh.TriangleIndices[baseIndex]], worldVertices[mesh.TriangleIndices[baseIndex + 1]], edgesByKey);
            AddBoundaryCandidate(mesh.Vertices[mesh.TriangleIndices[baseIndex + 1]], mesh.Vertices[mesh.TriangleIndices[baseIndex + 2]], worldVertices[mesh.TriangleIndices[baseIndex + 1]], worldVertices[mesh.TriangleIndices[baseIndex + 2]], edgesByKey);
            AddBoundaryCandidate(mesh.Vertices[mesh.TriangleIndices[baseIndex + 2]], mesh.Vertices[mesh.TriangleIndices[baseIndex]], worldVertices[mesh.TriangleIndices[baseIndex + 2]], worldVertices[mesh.TriangleIndices[baseIndex]], edgesByKey);
        }

        List<BoundaryEdge> boundaryEdges = new List<BoundaryEdge>();

        foreach (BoundaryEdgeAccumulator accumulator in edgesByKey.Values)
        {
            if (accumulator.Count == 1)
            {
                boundaryEdges.Add(accumulator.Edge);
            }
        }

        return boundaryEdges;
    }

    /// <summary>
    /// Adds one potential island boundary edge.
    /// </summary>
    private static void AddBoundaryCandidate(
        Vector3 localStart,
        Vector3 localEnd,
        Vector3 worldStart,
        Vector3 worldEnd,
        Dictionary<MeshEdgeKey, BoundaryEdgeAccumulator> edgesByKey)
    {
        MeshEdgeKey edgeKey = new MeshEdgeKey(new PointKey(localStart), new PointKey(localEnd));

        if (edgesByKey.TryGetValue(edgeKey, out BoundaryEdgeAccumulator accumulator))
        {
            accumulator.Count++;
            edgesByKey[edgeKey] = accumulator;
            return;
        }

        edgesByKey.Add(edgeKey, new BoundaryEdgeAccumulator(new BoundaryEdge(worldStart, worldEnd), 1));
    }

    /// <summary>
    /// Copies boundary edges to result preview segments up to the fixed renderer capacity.
    /// </summary>
    private static void AppendBoundarySegments(IReadOnlyList<BoundaryEdge> boundaryEdges, List<AreaSupportBoundarySegment> boundarySegments)
    {
        for (int i = 0; i < boundaryEdges.Count && boundarySegments.Count < MaximumBoundarySegmentCount; i++)
        {
            boundarySegments.Add(new AreaSupportBoundarySegment(boundaryEdges[i].Start, boundaryEdges[i].End));
        }
    }

    /// <summary>
    /// Creates preview segments for the inward half-spacing boundary offset used by the support filters.
    /// </summary>
    private static void AppendOffsetBoundarySegments(
        AreaSupportSettings settings,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        IReadOnlyList<BoundaryLoop> boundaryLoops,
        List<AreaSupportBoundarySegment> offsetBoundarySegments)
    {
        float boundaryOffset = settings.BoundaryOffset;

        for (int loopIndex = 0; loopIndex < boundaryLoops.Count && offsetBoundarySegments.Count < MaximumBoundarySegmentCount; loopIndex++)
        {
            BoundaryLoop boundaryLoop = boundaryLoops[loopIndex];
            bool isCounterClockwise = boundaryLoop.SignedArea >= 0.0f;
            List<List<Vector2>> offsetBoundaryPaths = CreateValidatedOffsetBoundaryPaths(settings, projectedTriangles, boundaryEdges, boundaryLoop, boundaryOffset, isCounterClockwise);

            for (int pathIndex = 0; pathIndex < offsetBoundaryPaths.Count && offsetBoundarySegments.Count < MaximumBoundarySegmentCount; pathIndex++)
            {
                IReadOnlyList<Vector2> offsetBoundaryPath = offsetBoundaryPaths[pathIndex];

                for (int pointIndex = 0; pointIndex + 1 < offsetBoundaryPath.Count && offsetBoundarySegments.Count < MaximumBoundarySegmentCount; pointIndex++)
                {
                    Vector2 offsetStart = offsetBoundaryPath[pointIndex];
                    Vector2 offsetEnd = offsetBoundaryPath[pointIndex + 1];

                    if (Vector2.DistanceSquared(offsetStart, offsetEnd) <= DegenerateTolerance)
                    {
                        continue;
                    }

                    if (TryProjectToSelectedArea(offsetStart, projectedTriangles, out AreaSupportSample startSample)
                        && TryProjectToSelectedArea(offsetEnd, projectedTriangles, out AreaSupportSample endSample))
                    {
                        offsetBoundarySegments.Add(new AreaSupportBoundarySegment(startSample.Position, endSample.Position));
                    }
                }
            }

            AppendThinRegionFallbackSegments(settings, projectedTriangles, boundaryEdges, boundaryLoop, isCounterClockwise, offsetBoundarySegments);
        }
    }

    /// <summary>
    /// Adds preview segments for centreline fallback paths in collapsed thin regions.
    /// </summary>
    private static void AppendThinRegionFallbackSegments(
        AreaSupportSettings settings,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        BoundaryLoop boundaryLoop,
        bool isCounterClockwise,
        List<AreaSupportBoundarySegment> offsetBoundarySegments)
    {
        List<Vector2> fallbackPoints = CreateThinRegionFallbackPoints(settings, projectedTriangles, boundaryEdges, boundaryLoop, isCounterClockwise);

        for (int pointIndex = 0; pointIndex + 1 < fallbackPoints.Count && offsetBoundarySegments.Count < MaximumBoundarySegmentCount; pointIndex++)
        {
            Vector2 start = fallbackPoints[pointIndex];
            Vector2 end = fallbackPoints[pointIndex + 1];

            if (Vector2.Distance(start, end) > settings.BoundarySpacing * 1.5f)
            {
                continue;
            }

            if (TryProjectToSelectedArea(start, projectedTriangles, out AreaSupportSample startSample)
                && TryProjectToSelectedArea(end, projectedTriangles, out AreaSupportSample endSample))
            {
                offsetBoundarySegments.Add(new AreaSupportBoundarySegment(startSample.Position, endSample.Position));
            }
        }
    }

    /// <summary>
    /// Adds support candidates half a spacing inward from the boundary segments.
    /// </summary>
    private static void FillBoundaryCandidates(
        MeshEntity mesh,
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<Vector3> triangleNormals,
        AreaSupportSettings settings,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        IReadOnlyList<BoundaryLoop> boundaryLoops,
        List<AreaSupportSample> supportSamples,
        List<Vector2> acceptedSamplePoints,
        ref int rejectedCandidateCount,
        ref int duplicateCandidateCount)
    {
        Vector2 centroid = CalculateCentroid(projectedTriangles);
        List<Vector2> acceptedBoundaryPoints = new List<Vector2>();
        float boundaryOffset = settings.BoundaryOffset;

        for (int loopIndex = 0; loopIndex < boundaryLoops.Count && supportSamples.Count < MaximumSupportCount; loopIndex++)
        {
            BoundaryLoop boundaryLoop = boundaryLoops[loopIndex];

            AddConcaveCornerCandidates(mesh, worldVertices, triangleNormals, settings, projectedTriangles, boundaryEdges, boundaryLoop, centroid, supportSamples, acceptedSamplePoints, acceptedBoundaryPoints, ref rejectedCandidateCount, ref duplicateCandidateCount);
            AddBoundaryLoopSpacingCandidates(mesh, worldVertices, triangleNormals, settings, projectedTriangles, boundaryEdges, boundaryLoop, centroid, supportSamples, acceptedSamplePoints, acceptedBoundaryPoints, boundaryOffset, ref rejectedCandidateCount, ref duplicateCandidateCount);
            AddThinRegionFallbackCandidates(mesh, worldVertices, triangleNormals, settings, projectedTriangles, boundaryEdges, boundaryLoop, supportSamples, acceptedSamplePoints, ref rejectedCandidateCount, ref duplicateCandidateCount);
        }
    }

    /// <summary>
    /// Adds supports at concave offset-boundary corners whose signed turn is above the configured threshold.
    /// </summary>
    private static void AddConcaveCornerCandidates(
        MeshEntity mesh,
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<Vector3> triangleNormals,
        AreaSupportSettings settings,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        BoundaryLoop boundaryLoop,
        Vector2 centroid,
        List<AreaSupportSample> supportSamples,
        List<Vector2> acceptedSamplePoints,
        List<Vector2> acceptedBoundaryPoints,
        ref int rejectedCandidateCount,
        ref int duplicateCandidateCount)
    {
        if (boundaryLoop.Points.Count < 3 || settings.ConcaveCornerAngleDegrees <= 0.0f)
        {
            return;
        }

        float boundaryOffset = settings.BoundaryOffset;
        bool isCounterClockwise = boundaryLoop.SignedArea >= 0.0f;

        for (int i = 0; i < boundaryLoop.Points.Count && supportSamples.Count < MaximumSupportCount; i++)
        {
            Vector2 previous = boundaryLoop.Points[(i + boundaryLoop.Points.Count - 1) % boundaryLoop.Points.Count];
            Vector2 current = boundaryLoop.Points[i];
            Vector2 next = boundaryLoop.Points[(i + 1) % boundaryLoop.Points.Count];
            Vector2 incoming = current - previous;
            Vector2 outgoing = next - current;

            if (incoming.LengthSquared() <= DegenerateTolerance || outgoing.LengthSquared() <= DegenerateTolerance)
            {
                continue;
            }

            float turnCross = Cross(incoming, outgoing);
            bool isConcave = isCounterClockwise ? turnCross < -DegenerateTolerance : turnCross > DegenerateTolerance;

            if (!isConcave)
            {
                continue;
            }

            float turnAngleDegrees = CalculateAngleDegrees(incoming, outgoing);

            if (turnAngleDegrees <= settings.ConcaveCornerAngleDegrees)
            {
                continue;
            }

            Vector2 previousInwardNormal = CalculateAreaInteriorNormal(previous, current, projectedTriangles, isCounterClockwise);
            Vector2 nextInwardNormal = CalculateAreaInteriorNormal(current, next, projectedTriangles, isCounterClockwise);
            Vector2 offsetDirection = previousInwardNormal + nextInwardNormal;

            if (offsetDirection.LengthSquared() <= DegenerateTolerance)
            {
                offsetDirection = centroid - current;
            }

            if (offsetDirection.LengthSquared() <= DegenerateTolerance)
            {
                continue;
            }

            Vector2 candidatePoint = current + (Vector2.Normalize(offsetDirection) * boundaryOffset);

            if (!IsInsideBoundaryOffset(candidatePoint, projectedTriangles, boundaryEdges, boundaryOffset))
            {
                candidatePoint = MoveTowardOffsetInterior(current, centroid, boundaryOffset, projectedTriangles, boundaryEdges);
            }

            TryAddProjectedBoundarySample(mesh, worldVertices, triangleNormals, candidatePoint, settings, boundaryOffset, projectedTriangles, boundaryEdges, supportSamples, acceptedSamplePoints, acceptedBoundaryPoints, ref rejectedCandidateCount, ref duplicateCandidateCount);
        }
    }

    /// <summary>
    /// Adds supports along validated offset-boundary path pieces without exceeding the configured spacing cap.
    /// </summary>
    private static void AddBoundaryLoopSpacingCandidates(
        MeshEntity mesh,
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<Vector3> triangleNormals,
        AreaSupportSettings settings,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        BoundaryLoop boundaryLoop,
        Vector2 centroid,
        List<AreaSupportSample> supportSamples,
        List<Vector2> acceptedSamplePoints,
        List<Vector2> acceptedBoundaryPoints,
        float boundaryOffset,
        ref int rejectedCandidateCount,
        ref int duplicateCandidateCount)
    {
        if (boundaryLoop.Points.Count < 2)
        {
            return;
        }

        bool isCounterClockwise = boundaryLoop.SignedArea >= 0.0f;
        List<List<Vector2>> offsetBoundaryPaths = CreateValidatedOffsetBoundaryPaths(settings, projectedTriangles, boundaryEdges, boundaryLoop, boundaryOffset, isCounterClockwise);

        for (int pathIndex = 0; pathIndex < offsetBoundaryPaths.Count && supportSamples.Count < MaximumSupportCount; pathIndex++)
        {
            IReadOnlyList<Vector2> offsetBoundaryPath = offsetBoundaryPaths[pathIndex];
            float pathLength = CalculateOpenPathLength(offsetBoundaryPath);

            if (pathLength <= DegenerateTolerance)
            {
                continue;
            }

            int supportCount = Math.Max(1, (int)MathF.Ceiling(pathLength / settings.BoundarySpacing));
            float actualSpacing = pathLength / supportCount;

            for (int supportIndex = 0; supportIndex < supportCount && supportSamples.Count < MaximumSupportCount; supportIndex++)
            {
                float distanceAlongPath = (supportIndex + 0.5f) * actualSpacing;

                if (!TryGetOpenPathPointAtDistance(offsetBoundaryPath, distanceAlongPath, out Vector2 candidatePoint))
                {
                    continue;
                }

                if (!IsInsideBoundaryOffset(candidatePoint, projectedTriangles, boundaryEdges, boundaryOffset))
                {
                    candidatePoint = MoveTowardOffsetInterior(candidatePoint, centroid, boundaryOffset, projectedTriangles, boundaryEdges);
                }

                TryAddProjectedBoundarySample(mesh, worldVertices, triangleNormals, candidatePoint, settings, boundaryOffset, projectedTriangles, boundaryEdges, supportSamples, acceptedSamplePoints, acceptedBoundaryPoints, ref rejectedCandidateCount, ref duplicateCandidateCount);
            }
        }
    }

    /// <summary>
    /// Projects one boundary candidate onto the selected faces after enforcing boundary-specific spacing.
    /// </summary>
    private static void TryAddProjectedBoundarySample(
        MeshEntity mesh,
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<Vector3> triangleNormals,
        Vector2 candidatePoint,
        AreaSupportSettings settings,
        float boundaryOffset,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        List<AreaSupportSample> supportSamples,
        List<Vector2> acceptedSamplePoints,
        List<Vector2> acceptedBoundaryPoints,
        ref int rejectedCandidateCount,
        ref int duplicateCandidateCount)
    {
        float boundaryDuplicateDistance = Math.Min(settings.BoundarySpacing, settings.Spacing) * DuplicateSpacingFactor;
        float boundarySpacingDistanceSquared = Math.Max(0.0f, boundaryDuplicateDistance - PointInsideTolerance);
        boundarySpacingDistanceSquared *= boundarySpacingDistanceSquared;

        for (int i = 0; i < acceptedBoundaryPoints.Count; i++)
        {
            if (Vector2.DistanceSquared(candidatePoint, acceptedBoundaryPoints[i]) < boundarySpacingDistanceSquared)
            {
                duplicateCandidateCount++;
                return;
            }
        }

        int supportCountBeforeAdd = supportSamples.Count;
        TryAddProjectedSample(
            mesh,
            worldVertices,
            triangleNormals,
            candidatePoint,
            settings.Spacing,
            boundaryOffset,
            projectedTriangles,
            boundaryEdges,
            supportSamples,
            acceptedSamplePoints,
            ref rejectedCandidateCount,
            ref duplicateCandidateCount);

        if (supportSamples.Count > supportCountBeforeAdd)
        {
            acceptedBoundaryPoints.Add(candidatePoint);
        }
    }

    /// <summary>
    /// Adds centreline fallback supports where the normal half-spacing offset collapses in a thin region.
    /// </summary>
    private static void AddThinRegionFallbackCandidates(
        MeshEntity mesh,
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<Vector3> triangleNormals,
        AreaSupportSettings settings,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        BoundaryLoop boundaryLoop,
        List<AreaSupportSample> supportSamples,
        List<Vector2> acceptedSamplePoints,
        ref int rejectedCandidateCount,
        ref int duplicateCandidateCount)
    {
        if (!settings.SupportThinRegions)
        {
            return;
        }

        bool isCounterClockwise = boundaryLoop.SignedArea >= 0.0f;
        List<Vector2> fallbackPoints = CreateThinRegionFallbackPoints(settings, projectedTriangles, boundaryEdges, boundaryLoop, isCounterClockwise);

        for (int i = 0; i < fallbackPoints.Count && supportSamples.Count < MaximumSupportCount; i++)
        {
            TryAddProjectedThinRegionSample(mesh, worldVertices, triangleNormals, fallbackPoints[i], settings, projectedTriangles, supportSamples, acceptedSamplePoints, ref rejectedCandidateCount, ref duplicateCandidateCount);
        }
    }

    /// <summary>
    /// Projects one thin-region fallback candidate without requiring the impossible half-spacing boundary clearance.
    /// </summary>
    private static void TryAddProjectedThinRegionSample(
        MeshEntity mesh,
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<Vector3> triangleNormals,
        Vector2 candidatePoint,
        AreaSupportSettings settings,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        List<AreaSupportSample> supportSamples,
        List<Vector2> acceptedSamplePoints,
        ref int rejectedCandidateCount,
        ref int duplicateCandidateCount)
    {
        if (!IsPointInsideArea(candidatePoint, projectedTriangles))
        {
            rejectedCandidateCount++;
            return;
        }

        float duplicateDistance = Math.Min(settings.BoundarySpacing, settings.Spacing) * DuplicateSpacingFactor;
        float duplicateDistanceSquared = duplicateDistance * duplicateDistance;

        for (int i = 0; i < acceptedSamplePoints.Count; i++)
        {
            if (Vector2.DistanceSquared(candidatePoint, acceptedSamplePoints[i]) <= duplicateDistanceSquared)
            {
                duplicateCandidateCount++;
                return;
            }
        }

        AreaSupportSample sample;

        if (!TryProjectToSelectedArea(candidatePoint, projectedTriangles, out sample))
        {
            rejectedCandidateCount++;
            return;
        }

        _ = mesh;
        _ = worldVertices;
        _ = triangleNormals;
        acceptedSamplePoints.Add(candidatePoint);
        supportSamples.Add(sample);
    }

    /// <summary>
    /// Calculates the closed perimeter length of an ordered XY boundary loop.
    /// </summary>
    private static float CalculateLoopPerimeter(IReadOnlyList<Vector2> points)
    {
        float perimeter = 0.0f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Count];
            perimeter += Vector2.Distance(start, end);
        }

        return perimeter;
    }

    /// <summary>
    /// Finds a point at a loop distance measured around a closed XY polyline.
    /// </summary>
    private static bool TryGetLoopPointAtDistance(
        IReadOnlyList<Vector2> points,
        float distanceAlongLoop,
        out Vector2 point)
    {
        float remainingDistance = distanceAlongLoop;

        for (int edgeIndex = 0; edgeIndex < points.Count; edgeIndex++)
        {
            Vector2 start = points[edgeIndex];
            Vector2 end = points[(edgeIndex + 1) % points.Count];
            float edgeLength = Vector2.Distance(start, end);

            if (edgeLength <= DegenerateTolerance)
            {
                continue;
            }

            if (remainingDistance <= edgeLength || edgeIndex == points.Count - 1)
            {
                float edgeFraction = Math.Clamp(remainingDistance / edgeLength, 0.0f, 1.0f);
                point = Vector2.Lerp(start, end, edgeFraction);
                return true;
            }

            remainingDistance -= edgeLength;
        }

        point = Vector2.Zero;
        return false;
    }

    /// <summary>
    /// Creates clipped offset-boundary path pieces by sampling source boundary edges and validating each offset sample.
    /// </summary>
    private static List<List<Vector2>> CreateValidatedOffsetBoundaryPaths(
        AreaSupportSettings settings,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        BoundaryLoop boundaryLoop,
        float boundaryOffset,
        bool isCounterClockwise)
    {
        List<List<Vector2>> paths = new List<List<Vector2>>();
        List<Vector2> currentPath = new List<Vector2>();

        if (boundaryLoop.Points.Count < 2)
        {
            return paths;
        }

        float sampleSpacing = CalculateValidatedBoundarySampleSpacing(settings);
        float splitDistance = Math.Max(settings.Spacing, settings.BoundarySpacing) * 1.25f;
        List<Vector2> cornerOffsetPoints = CreateOffsetBoundaryPoints(boundaryLoop, boundaryOffset, isCounterClockwise, projectedTriangles);

        for (int edgeIndex = 0; edgeIndex < boundaryLoop.Points.Count; edgeIndex++)
        {
            Vector2 start = boundaryLoop.Points[edgeIndex];
            Vector2 end = boundaryLoop.Points[(edgeIndex + 1) % boundaryLoop.Points.Count];
            float length = Vector2.Distance(start, end);

            if (length <= DegenerateTolerance)
            {
                FlushValidatedOffsetPath(paths, currentPath);
                continue;
            }

            Vector2 inwardDirection = CalculateAreaInteriorNormal(start, end, projectedTriangles, isCounterClockwise);

            if (inwardDirection.LengthSquared() <= DegenerateTolerance)
            {
                FlushValidatedOffsetPath(paths, currentPath);
                continue;
            }

            TryAppendValidatedOffsetPoint(cornerOffsetPoints[edgeIndex], projectedTriangles, boundaryEdges, boundaryOffset, sampleSpacing, splitDistance, paths, currentPath);

            int sampleCount = Math.Max(1, (int)MathF.Ceiling(length / sampleSpacing));

            for (int sampleIndex = 1; sampleIndex < sampleCount; sampleIndex++)
            {
                float edgeFraction = sampleIndex / (float)sampleCount;
                Vector2 boundaryPoint = Vector2.Lerp(start, end, edgeFraction);
                Vector2 candidatePoint = boundaryPoint + (inwardDirection * boundaryOffset);
                TryAppendValidatedOffsetPoint(candidatePoint, projectedTriangles, boundaryEdges, boundaryOffset, sampleSpacing, splitDistance, paths, currentPath);
            }

            int nextCornerIndex = (edgeIndex + 1) % boundaryLoop.Points.Count;
            TryAppendValidatedOffsetPoint(cornerOffsetPoints[nextCornerIndex], projectedTriangles, boundaryEdges, boundaryOffset, sampleSpacing, splitDistance, paths, currentPath);
        }

        FlushValidatedOffsetPath(paths, currentPath);
        return paths;
    }

    /// <summary>
    /// Chooses a short validation interval for long source mesh edges without tying support density to mesh density.
    /// </summary>
    private static float CalculateValidatedBoundarySampleSpacing(AreaSupportSettings settings)
    {
        float spacingBasedSample = Math.Max(0.1f, Math.Min(settings.Spacing, settings.BoundarySpacing) * 0.25f);
        return Math.Min(OffsetBoundarySampleSpacing, spacingBasedSample);
    }

    /// <summary>
    /// Adds a valid offset point to the current path, splitting if the local path crosses an invalid region.
    /// </summary>
    private static void TryAppendValidatedOffsetPoint(
        Vector2 candidatePoint,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        float boundaryOffset,
        float sampleSpacing,
        float splitDistance,
        List<List<Vector2>> paths,
        List<Vector2> currentPath)
    {
        if (!IsInsideBoundaryOffset(candidatePoint, projectedTriangles, boundaryEdges, boundaryOffset))
        {
            FlushValidatedOffsetPath(paths, currentPath);
            return;
        }

        if (currentPath.Count > 0)
        {
            Vector2 previousPoint = currentPath[currentPath.Count - 1];

            if (!CanConnectOffsetBoundarySamples(previousPoint, candidatePoint, projectedTriangles, boundaryEdges, boundaryOffset, sampleSpacing, splitDistance))
            {
                FlushValidatedOffsetPath(paths, currentPath);
            }
        }

        if (currentPath.Count == 0 || Vector2.DistanceSquared(currentPath[currentPath.Count - 1], candidatePoint) > DegenerateTolerance)
        {
            currentPath.Add(candidatePoint);
        }
    }

    /// <summary>
    /// Stores a completed offset path if it has enough points to draw or place supports along.
    /// </summary>
    private static void FlushValidatedOffsetPath(List<List<Vector2>> paths, List<Vector2> currentPath)
    {
        if (currentPath.Count >= 2)
        {
            paths.Add(new List<Vector2>(currentPath));
        }

        currentPath.Clear();
    }

    /// <summary>
    /// Checks that a path segment between two validated offset samples does not jump across an invalid region.
    /// </summary>
    private static bool CanConnectOffsetBoundarySamples(
        Vector2 start,
        Vector2 end,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        float boundaryOffset,
        float sampleSpacing,
        float splitDistance)
    {
        float segmentLength = Vector2.Distance(start, end);

        if (segmentLength <= DegenerateTolerance)
        {
            return true;
        }

        if (segmentLength > splitDistance)
        {
            return false;
        }

        int checkCount = Math.Max(1, (int)MathF.Ceiling(segmentLength / sampleSpacing));

        for (int checkIndex = 1; checkIndex < checkCount; checkIndex++)
        {
            float fraction = checkIndex / (float)checkCount;
            Vector2 checkPoint = Vector2.Lerp(start, end, fraction);

            if (!IsInsideBoundaryOffset(checkPoint, projectedTriangles, boundaryEdges, boundaryOffset))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Calculates the length of an open or explicitly closed XY polyline.
    /// </summary>
    private static float CalculateOpenPathLength(IReadOnlyList<Vector2> points)
    {
        float length = 0.0f;

        for (int pointIndex = 0; pointIndex + 1 < points.Count; pointIndex++)
        {
            length += Vector2.Distance(points[pointIndex], points[pointIndex + 1]);
        }

        return length;
    }

    /// <summary>
    /// Finds a point at a distance measured along an open or explicitly closed XY polyline.
    /// </summary>
    private static bool TryGetOpenPathPointAtDistance(IReadOnlyList<Vector2> points, float distanceAlongPath, out Vector2 point)
    {
        float remainingDistance = distanceAlongPath;

        for (int pointIndex = 0; pointIndex + 1 < points.Count; pointIndex++)
        {
            Vector2 start = points[pointIndex];
            Vector2 end = points[pointIndex + 1];
            float segmentLength = Vector2.Distance(start, end);

            if (segmentLength <= DegenerateTolerance)
            {
                continue;
            }

            if (remainingDistance <= segmentLength || pointIndex == points.Count - 2)
            {
                float segmentFraction = Math.Clamp(remainingDistance / segmentLength, 0.0f, 1.0f);
                point = Vector2.Lerp(start, end, segmentFraction);
                return true;
            }

            remainingDistance -= segmentLength;
        }

        point = Vector2.Zero;
        return false;
    }

    /// <summary>
    /// Creates centreline fallback points by measuring from sampled boundary points to the opposing boundary.
    /// </summary>
    private static List<Vector2> CreateThinRegionFallbackPoints(
        AreaSupportSettings settings,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        BoundaryLoop boundaryLoop,
        bool isCounterClockwise)
    {
        List<Vector2> fallbackPoints = new List<Vector2>();

        if (!settings.SupportThinRegions || boundaryLoop.Points.Count < 2)
        {
            return fallbackPoints;
        }

        for (int edgeIndex = 0; edgeIndex < boundaryLoop.Points.Count; edgeIndex++)
        {
            Vector2 start = boundaryLoop.Points[edgeIndex];
            Vector2 end = boundaryLoop.Points[(edgeIndex + 1) % boundaryLoop.Points.Count];
            float length = Vector2.Distance(start, end);

            if (length <= DegenerateTolerance)
            {
                continue;
            }

            Vector2 inwardDirection = CalculateAreaInteriorNormal(start, end, projectedTriangles, isCounterClockwise);
            int cellCount = Math.Max(1, (int)MathF.Ceiling(length / settings.BoundarySpacing));
            float actualSpacing = length / cellCount;

            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                float distance = (cellIndex + 0.5f) * actualSpacing;
                Vector2 boundaryPoint = Vector2.Lerp(start, end, Math.Clamp(distance / length, 0.0f, 1.0f));

                if (TryCreateThinRegionFallbackPoint(boundaryPoint, start, end, inwardDirection, settings, projectedTriangles, boundaryEdges, out Vector2 fallbackPoint))
                {
                    fallbackPoints.Add(fallbackPoint);
                }
            }
        }

        return fallbackPoints;
    }

    /// <summary>
    /// Finds the midpoint between one boundary sample and the opposing boundary when the local area is thin enough.
    /// </summary>
    private static bool TryCreateThinRegionFallbackPoint(
        Vector2 boundaryPoint,
        Vector2 sourceStart,
        Vector2 sourceEnd,
        Vector2 inwardDirection,
        AreaSupportSettings settings,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        out Vector2 fallbackPoint)
    {
        fallbackPoint = Vector2.Zero;

        if (inwardDirection.LengthSquared() <= DegenerateTolerance)
        {
            return false;
        }

        float closestDistance = float.MaxValue;

        for (int i = 0; i < boundaryEdges.Count; i++)
        {
            Vector2 segmentStart = ToVector2(boundaryEdges[i].Start);
            Vector2 segmentEnd = ToVector2(boundaryEdges[i].End);

            if (IsSameSegment(sourceStart, sourceEnd, segmentStart, segmentEnd))
            {
                continue;
            }

            if (TryIntersectRayWithSegment(boundaryPoint, inwardDirection, segmentStart, segmentEnd, out float distance)
                && distance < closestDistance)
            {
                closestDistance = distance;
            }
        }

        if (closestDistance == float.MaxValue)
        {
            return false;
        }

        if (closestDistance >= settings.Spacing - PointInsideTolerance)
        {
            return false;
        }

        if (closestDistance < settings.MinimumThinRegionThickness - PointInsideTolerance)
        {
            return false;
        }

        Vector2 candidatePoint = boundaryPoint + (Vector2.Normalize(inwardDirection) * closestDistance * 0.5f);

        if (!IsPointInsideArea(candidatePoint, projectedTriangles))
        {
            return false;
        }

        fallbackPoint = candidatePoint;
        return true;
    }

    /// <summary>
    /// Intersects a forward ray with a finite boundary segment.
    /// </summary>
    private static bool TryIntersectRayWithSegment(Vector2 rayOrigin, Vector2 rayDirection, Vector2 segmentStart, Vector2 segmentEnd, out float distance)
    {
        Vector2 segmentDirection = segmentEnd - segmentStart;
        float denominator = Cross(rayDirection, segmentDirection);
        distance = 0.0f;

        if (MathF.Abs(denominator) <= DegenerateTolerance)
        {
            return false;
        }

        Vector2 delta = segmentStart - rayOrigin;
        float rayDistance = Cross(delta, segmentDirection) / denominator;
        float segmentFraction = Cross(delta, rayDirection) / denominator;

        if (rayDistance <= PointInsideTolerance || segmentFraction < -PointInsideTolerance || segmentFraction > 1.0f + PointInsideTolerance)
        {
            return false;
        }

        distance = rayDistance;
        return true;
    }

    /// <summary>
    /// Checks whether two XY segments represent the same boundary edge regardless of direction.
    /// </summary>
    private static bool IsSameSegment(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        return (AreSamePoint(firstStart, secondStart) && AreSamePoint(firstEnd, secondEnd))
            || (AreSamePoint(firstStart, secondEnd) && AreSamePoint(firstEnd, secondStart));
    }

    /// <summary>
    /// Builds the true inward offset loop by intersecting adjacent offset boundary-edge lines.
    /// </summary>
    private static List<Vector2> CreateOffsetBoundaryPoints(
        BoundaryLoop boundaryLoop,
        float boundaryOffset,
        bool isCounterClockwise,
        IReadOnlyList<ProjectedTriangle> projectedTriangles)
    {
        List<Vector2> offsetPoints = new List<Vector2>(boundaryLoop.Points.Count);

        for (int i = 0; i < boundaryLoop.Points.Count; i++)
        {
            Vector2 previous = boundaryLoop.Points[(i + boundaryLoop.Points.Count - 1) % boundaryLoop.Points.Count];
            Vector2 current = boundaryLoop.Points[i];
            Vector2 next = boundaryLoop.Points[(i + 1) % boundaryLoop.Points.Count];
            Vector2 previousInwardNormal = CalculateAreaInteriorNormal(previous, current, projectedTriangles, isCounterClockwise);
            Vector2 nextInwardNormal = CalculateAreaInteriorNormal(current, next, projectedTriangles, isCounterClockwise);
            Vector2 previousOffsetStart = previous + (previousInwardNormal * boundaryOffset);
            Vector2 previousOffsetEnd = current + (previousInwardNormal * boundaryOffset);
            Vector2 nextOffsetStart = current + (nextInwardNormal * boundaryOffset);
            Vector2 nextOffsetEnd = next + (nextInwardNormal * boundaryOffset);
            Vector2 offsetPoint;

            if (!TryIntersectLines(previousOffsetStart, previousOffsetEnd, nextOffsetStart, nextOffsetEnd, out offsetPoint))
            {
                Vector2 averageInwardNormal = previousInwardNormal + nextInwardNormal;

                if (averageInwardNormal.LengthSquared() <= DegenerateTolerance)
                {
                    averageInwardNormal = nextInwardNormal;
                }

                offsetPoint = current + (Vector2.Normalize(averageInwardNormal) * boundaryOffset);
            }

            offsetPoints.Add(offsetPoint);
        }

        return offsetPoints;
    }

    /// <summary>
    /// Intersects two infinite XY lines expressed by two points on each line.
    /// </summary>
    private static bool TryIntersectLines(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd, out Vector2 intersection)
    {
        Vector2 firstDirection = firstEnd - firstStart;
        Vector2 secondDirection = secondEnd - secondStart;
        float denominator = Cross(firstDirection, secondDirection);

        if (MathF.Abs(denominator) <= DegenerateTolerance)
        {
            intersection = Vector2.Zero;
            return false;
        }

        float t = Cross(secondStart - firstStart, secondDirection) / denominator;
        intersection = firstStart + (firstDirection * t);
        return true;
    }

    /// <summary>
    /// Orders unordered island boundary edges into closed XY loops.
    /// </summary>
    private static List<BoundaryLoop> CreateBoundaryLoops(IReadOnlyList<BoundaryEdge> boundaryEdges)
    {
        Dictionary<PointKey2D, List<BoundaryEdgeLink>> linksByPoint = new Dictionary<PointKey2D, List<BoundaryEdgeLink>>();
        bool[] usedEdges = new bool[boundaryEdges.Count];

        for (int i = 0; i < boundaryEdges.Count; i++)
        {
            Vector2 start = ToVector2(boundaryEdges[i].Start);
            Vector2 end = ToVector2(boundaryEdges[i].End);
            AddBoundaryEdgeLink(linksByPoint, new PointKey2D(start), new BoundaryEdgeLink(i, end));
            AddBoundaryEdgeLink(linksByPoint, new PointKey2D(end), new BoundaryEdgeLink(i, start));
        }

        List<BoundaryLoop> boundaryLoops = new List<BoundaryLoop>();

        for (int edgeIndex = 0; edgeIndex < boundaryEdges.Count; edgeIndex++)
        {
            if (usedEdges[edgeIndex])
            {
                continue;
            }

            List<Vector2> points = new List<Vector2>();
            Vector2 firstPoint = ToVector2(boundaryEdges[edgeIndex].Start);
            Vector2 currentPoint = firstPoint;
            usedEdges[edgeIndex] = true;
            points.Add(firstPoint);
            currentPoint = ToVector2(boundaryEdges[edgeIndex].End);

            while (points.Count <= boundaryEdges.Count + 1)
            {
                if (AreSamePoint(currentPoint, firstPoint))
                {
                    break;
                }

                points.Add(currentPoint);
                PointKey2D currentKey = new PointKey2D(currentPoint);

                if (!linksByPoint.TryGetValue(currentKey, out List<BoundaryEdgeLink>? links))
                {
                    break;
                }

                bool didAdvance = false;

                for (int linkIndex = 0; linkIndex < links.Count; linkIndex++)
                {
                    BoundaryEdgeLink link = links[linkIndex];

                    if (usedEdges[link.EdgeIndex])
                    {
                        continue;
                    }

                    usedEdges[link.EdgeIndex] = true;
                    currentPoint = link.OtherPoint;
                    didAdvance = true;
                    break;
                }

                if (!didAdvance)
                {
                    break;
                }
            }

            if (points.Count >= 3)
            {
                boundaryLoops.Add(new BoundaryLoop(points));
            }
        }

        return boundaryLoops;
    }

    /// <summary>
    /// Adds one endpoint link used during boundary-loop assembly.
    /// </summary>
    private static void AddBoundaryEdgeLink(
        Dictionary<PointKey2D, List<BoundaryEdgeLink>> linksByPoint,
        PointKey2D pointKey,
        BoundaryEdgeLink link)
    {
        if (!linksByPoint.TryGetValue(pointKey, out List<BoundaryEdgeLink>? links))
        {
            links = new List<BoundaryEdgeLink>();
            linksByPoint.Add(pointKey, links);
        }

        links.Add(link);
    }

    /// <summary>
    /// Calculates the inward normal for one ordered boundary-loop segment.
    /// </summary>
    private static Vector2 CalculateInwardNormal(Vector2 start, Vector2 end, bool isCounterClockwise)
    {
        Vector2 edgeDirection = end - start;

        if (edgeDirection.LengthSquared() <= DegenerateTolerance)
        {
            return Vector2.Zero;
        }

        Vector2 normal = isCounterClockwise
            ? new Vector2(-edgeDirection.Y, edgeDirection.X)
            : new Vector2(edgeDirection.Y, -edgeDirection.X);

        return Vector2.Normalize(normal);
    }

    /// <summary>
    /// Calculates the boundary-side normal that points into the selected projected face area.
    /// </summary>
    private static Vector2 CalculateAreaInteriorNormal(
        Vector2 start,
        Vector2 end,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        bool isCounterClockwiseFallback)
    {
        Vector2 edgeDirection = end - start;

        if (edgeDirection.LengthSquared() <= DegenerateTolerance)
        {
            return Vector2.Zero;
        }

        Vector2 normalizedEdge = Vector2.Normalize(edgeDirection);
        Vector2 leftNormal = new Vector2(-normalizedEdge.Y, normalizedEdge.X);
        Vector2 rightNormal = -leftNormal;
        Vector2 midpoint = (start + end) * 0.5f;
        float edgeLength = edgeDirection.Length();
        float probeDistance = MathF.Max(PointInsideTolerance * 10.0f, MathF.Min(edgeLength * 0.1f, 0.01f));

        for (int attempt = 0; attempt < 4; attempt++)
        {
            Vector2 leftProbe = midpoint + (leftNormal * probeDistance);
            Vector2 rightProbe = midpoint + (rightNormal * probeDistance);
            bool isLeftInside = IsPointInsideArea(leftProbe, projectedTriangles);
            bool isRightInside = IsPointInsideArea(rightProbe, projectedTriangles);

            if (isLeftInside && !isRightInside)
            {
                return leftNormal;
            }

            if (isRightInside && !isLeftInside)
            {
                return rightNormal;
            }

            probeDistance *= 2.0f;
        }

        return CalculateInwardNormal(start, end, isCounterClockwiseFallback);
    }

    /// <summary>
    /// Calculates the unsigned angle between two vectors in degrees.
    /// </summary>
    private static float CalculateAngleDegrees(Vector2 first, Vector2 second)
    {
        if (first.LengthSquared() <= DegenerateTolerance || second.LengthSquared() <= DegenerateTolerance)
        {
            return 0.0f;
        }

        Vector2 normalizedFirst = Vector2.Normalize(first);
        Vector2 normalizedSecond = Vector2.Normalize(second);
        float dot = Math.Clamp(Vector2.Dot(normalizedFirst, normalizedSecond), -1.0f, 1.0f);
        float cross = Math.Abs(Cross(normalizedFirst, normalizedSecond));
        return MathF.Atan2(cross, dot) * (180.0f / MathF.PI);
    }

    /// <summary>
    /// Adds internal supports on a simple hexagonal grid across the projected island bounds.
    /// </summary>
    private static void FillHexGridCandidates(
        MeshEntity mesh,
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<Vector3> triangleNormals,
        AreaSupportSettings settings,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        List<AreaSupportSample> supportSamples,
        List<Vector2> acceptedSamplePoints,
        ref int rejectedCandidateCount,
        ref int duplicateCandidateCount)
    {
        if (projectedTriangles.Count == 0)
        {
            return;
        }

        Bounds2D bounds = CalculateBounds(projectedTriangles);
        float spacing = settings.Spacing;
        float boundaryOffset = settings.BoundaryOffset;
        float rowSpacing = spacing * 0.8660254f;
        int rowIndex = 0;

        for (float y = bounds.MinY; y <= bounds.MaxY && supportSamples.Count < MaximumSupportCount; y += rowSpacing)
        {
            float xOffset = (rowIndex % 2 == 0) ? 0.0f : spacing * 0.5f;

            for (float x = bounds.MinX + xOffset; x <= bounds.MaxX && supportSamples.Count < MaximumSupportCount; x += spacing)
            {
                Vector2 candidatePoint = new Vector2(x, y);

                if (!IsInsideBoundaryOffset(candidatePoint, projectedTriangles, boundaryEdges, boundaryOffset))
                {
                    continue;
                }

                TryAddProjectedSample(mesh, worldVertices, triangleNormals, candidatePoint, spacing, boundaryOffset, projectedTriangles, boundaryEdges, supportSamples, acceptedSamplePoints, ref rejectedCandidateCount, ref duplicateCandidateCount);
            }

            rowIndex++;
        }
    }

    /// <summary>
    /// Projects one XY candidate onto the selected faces and adds it if it is not a near-duplicate.
    /// </summary>
    private static void TryAddProjectedSample(
        MeshEntity mesh,
        IReadOnlyList<Vector3> worldVertices,
        IReadOnlyList<Vector3> triangleNormals,
        Vector2 candidatePoint,
        float spacing,
        float boundaryOffset,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        List<AreaSupportSample> supportSamples,
        List<Vector2> acceptedSamplePoints,
        ref int rejectedCandidateCount,
        ref int duplicateCandidateCount)
    {
        if (!IsInsideBoundaryOffset(candidatePoint, projectedTriangles, boundaryEdges, boundaryOffset))
        {
            rejectedCandidateCount++;
            return;
        }

        float duplicateDistanceSquared = spacing * DuplicateSpacingFactor * spacing * DuplicateSpacingFactor;

        for (int i = 0; i < acceptedSamplePoints.Count; i++)
        {
            if (Vector2.DistanceSquared(candidatePoint, acceptedSamplePoints[i]) <= duplicateDistanceSquared)
            {
                duplicateCandidateCount++;
                return;
            }
        }

        AreaSupportSample sample;

        if (!TryProjectToSelectedArea(candidatePoint, projectedTriangles, out sample))
        {
            rejectedCandidateCount++;
            return;
        }

        _ = mesh;
        _ = worldVertices;
        _ = triangleNormals;
        acceptedSamplePoints.Add(candidatePoint);
        supportSamples.Add(sample);
    }

    /// <summary>
    /// Vertically projects an XY point onto the uppermost selected projected triangle.
    /// </summary>
    private static bool TryProjectToSelectedArea(Vector2 point, IReadOnlyList<ProjectedTriangle> projectedTriangles, out AreaSupportSample sample)
    {
        float bestZ = -float.MaxValue;
        Vector3 bestPosition = Vector3.Zero;
        Vector3 bestNormal = Vector3.UnitZ;
        bool hasHit = false;

        for (int i = 0; i < projectedTriangles.Count; i++)
        {
            ProjectedTriangle triangle = projectedTriangles[i];
            BarycentricCoordinates coordinates;

            if (!TryGetBarycentric(point, triangle.A2, triangle.B2, triangle.C2, out coordinates))
            {
                continue;
            }

            Vector3 position = (triangle.A * coordinates.U) + (triangle.B * coordinates.V) + (triangle.C * coordinates.W);

            if (!hasHit || position.Z > bestZ)
            {
                hasHit = true;
                bestZ = position.Z;
                bestPosition = position;
                bestNormal = triangle.Normal;
            }
        }

        sample = new AreaSupportSample(bestPosition, bestNormal);
        return hasHit;
    }

    /// <summary>
    /// Checks whether an XY candidate is covered by any projected selected triangle.
    /// </summary>
    private static bool IsPointInsideArea(Vector2 point, IReadOnlyList<ProjectedTriangle> projectedTriangles)
    {
        for (int i = 0; i < projectedTriangles.Count; i++)
        {
            BarycentricCoordinates coordinates;

            if (TryGetBarycentric(point, projectedTriangles[i].A2, projectedTriangles[i].B2, projectedTriangles[i].C2, out coordinates))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether an XY candidate is inside the selected area after the boundary exclusion band is removed.
    /// </summary>
    private static bool IsInsideBoundaryOffset(
        Vector2 point,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges,
        float boundaryOffset)
    {
        if (!IsPointInsideArea(point, projectedTriangles))
        {
            return false;
        }

        float minimumDistance = Math.Max(0.0f, boundaryOffset - PointInsideTolerance);
        float minimumDistanceSquared = minimumDistance * minimumDistance;

        for (int i = 0; i < boundaryEdges.Count; i++)
        {
            Vector2 start = ToVector2(boundaryEdges[i].Start);
            Vector2 end = ToVector2(boundaryEdges[i].End);

            if (DistanceSquaredToSegment(point, start, end) < minimumDistanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Finds a simple interior fallback on the offset boundary when the local edge normal points outside a concave region.
    /// </summary>
    private static Vector2 MoveTowardOffsetInterior(
        Vector2 boundaryPoint,
        Vector2 centroid,
        float boundaryOffset,
        IReadOnlyList<ProjectedTriangle> projectedTriangles,
        IReadOnlyList<BoundaryEdge> boundaryEdges)
    {
        Vector2 direction = centroid - boundaryPoint;

        if (direction.LengthSquared() <= DegenerateTolerance)
        {
            return boundaryPoint;
        }

        direction = Vector2.Normalize(direction);

        for (int step = 1; step <= 8; step++)
        {
            Vector2 candidatePoint = boundaryPoint + (direction * boundaryOffset * step);

            if (IsInsideBoundaryOffset(candidatePoint, projectedTriangles, boundaryEdges, boundaryOffset))
            {
                return candidatePoint;
            }
        }

        return boundaryPoint + (direction * boundaryOffset);
    }

    /// <summary>
    /// Measures the squared distance from one point to a finite XY segment.
    /// </summary>
    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();

        if (lengthSquared <= DegenerateTolerance)
        {
            return Vector2.DistanceSquared(point, start);
        }

        float t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0.0f, 1.0f);
        Vector2 closestPoint = start + (segment * t);
        return Vector2.DistanceSquared(point, closestPoint);
    }

    /// <summary>
    /// Calculates barycentric coordinates in the XY plane.
    /// </summary>
    private static bool TryGetBarycentric(Vector2 point, Vector2 a, Vector2 b, Vector2 c, out BarycentricCoordinates coordinates)
    {
        Vector2 v0 = b - a;
        Vector2 v1 = c - a;
        Vector2 v2 = point - a;
        float d00 = Vector2.Dot(v0, v0);
        float d01 = Vector2.Dot(v0, v1);
        float d11 = Vector2.Dot(v1, v1);
        float d20 = Vector2.Dot(v2, v0);
        float d21 = Vector2.Dot(v2, v1);
        float denominator = (d00 * d11) - (d01 * d01);

        if (MathF.Abs(denominator) <= DegenerateTolerance)
        {
            coordinates = default;
            return false;
        }

        float v = ((d11 * d20) - (d01 * d21)) / denominator;
        float w = ((d00 * d21) - (d01 * d20)) / denominator;
        float u = 1.0f - v - w;
        coordinates = new BarycentricCoordinates(u, v, w);
        return u >= -PointInsideTolerance && v >= -PointInsideTolerance && w >= -PointInsideTolerance;
    }

    /// <summary>
    /// Calculates the average projected triangle center for simple inward boundary offsets.
    /// </summary>
    private static Vector2 CalculateCentroid(IReadOnlyList<ProjectedTriangle> projectedTriangles)
    {
        Vector2 sum = Vector2.Zero;
        int count = 0;

        for (int i = 0; i < projectedTriangles.Count; i++)
        {
            sum += projectedTriangles[i].A2;
            sum += projectedTriangles[i].B2;
            sum += projectedTriangles[i].C2;
            count += 3;
        }

        if (count == 0)
        {
            return Vector2.Zero;
        }

        return sum / count;
    }

    /// <summary>
    /// Calculates XY bounds for a selected island.
    /// </summary>
    private static Bounds2D CalculateBounds(IReadOnlyList<ProjectedTriangle> projectedTriangles)
    {
        Bounds2D bounds = new Bounds2D(float.MaxValue, float.MaxValue, -float.MaxValue, -float.MaxValue);

        for (int i = 0; i < projectedTriangles.Count; i++)
        {
            bounds.Include(projectedTriangles[i].A2);
            bounds.Include(projectedTriangles[i].B2);
            bounds.Include(projectedTriangles[i].C2);
        }

        return bounds;
    }

    /// <summary>
    /// Calculates a stable triangle normal with a vertical fallback for degenerate triangles.
    /// </summary>
    private static Vector3 CalculateNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);

        if (normal.LengthSquared() <= DegenerateTolerance)
        {
            return Vector3.UnitZ;
        }

        return Vector3.Normalize(normal);
    }

    /// <summary>
    /// Converts a 3D world point to its top-down XY coordinate.
    /// </summary>
    private static Vector2 ToVector2(Vector3 point)
    {
        return new Vector2(point.X, point.Y);
    }

    /// <summary>
    /// Calculates a 2D cross product.
    /// </summary>
    private static float Cross(Vector2 first, Vector2 second)
    {
        return (first.X * second.Y) - (first.Y * second.X);
    }

    /// <summary>
    /// Checks whether two XY points are equivalent for boundary-loop assembly.
    /// </summary>
    private static bool AreSamePoint(Vector2 first, Vector2 second)
    {
        return Vector2.DistanceSquared(first, second) <= PointInsideTolerance * PointInsideTolerance;
    }

    /// <summary>
    /// Returns one deterministic item from a set.
    /// </summary>
    private static int GetFirst(HashSet<int> values)
    {
        foreach (int value in values)
        {
            return value;
        }

        return -1;
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
    /// Stores one projected triangle with cached XY coordinates.
    /// </summary>
    private readonly struct ProjectedTriangle
    {
        public ProjectedTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            A = a;
            B = b;
            C = c;
            A2 = ToVector2(a);
            B2 = ToVector2(b);
            C2 = ToVector2(c);
            Normal = normal;
        }

        public Vector3 A { get; }
        public Vector3 B { get; }
        public Vector3 C { get; }
        public Vector2 A2 { get; }
        public Vector2 B2 { get; }
        public Vector2 C2 { get; }
        public Vector3 Normal { get; }
    }

    /// <summary>
    /// Stores one boundary edge in world space.
    /// </summary>
    private readonly struct BoundaryEdge
    {
        public BoundaryEdge(Vector3 start, Vector3 end)
        {
            Start = start;
            End = end;
        }

        public Vector3 Start { get; }
        public Vector3 End { get; }
    }

    /// <summary>
    /// Stores one ordered boundary loop and its signed XY area.
    /// </summary>
    private sealed class BoundaryLoop
    {
        public BoundaryLoop(IReadOnlyList<Vector2> points)
        {
            Points = new ReadOnlyCollection<Vector2>(new List<Vector2>(points));
            SignedArea = CalculateSignedArea(points);
        }

        public IReadOnlyList<Vector2> Points { get; }

        public float SignedArea { get; }

        private static float CalculateSignedArea(IReadOnlyList<Vector2> points)
        {
            float area = 0.0f;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Count];
                area += (current.X * next.Y) - (next.X * current.Y);
            }

            return area * 0.5f;
        }
    }

    /// <summary>
    /// Stores one endpoint-to-edge link used during boundary-loop assembly.
    /// </summary>
    private readonly struct BoundaryEdgeLink
    {
        public BoundaryEdgeLink(int edgeIndex, Vector2 otherPoint)
        {
            EdgeIndex = edgeIndex;
            OtherPoint = otherPoint;
        }

        public int EdgeIndex { get; }

        public Vector2 OtherPoint { get; }
    }

    /// <summary>
    /// Counts how many selected triangles own an island edge.
    /// </summary>
    private struct BoundaryEdgeAccumulator
    {
        public BoundaryEdgeAccumulator(BoundaryEdge edge, int count)
        {
            Edge = edge;
            Count = count;
        }

        public BoundaryEdge Edge { get; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Stores one barycentric point in a projected triangle.
    /// </summary>
    private readonly struct BarycentricCoordinates
    {
        public BarycentricCoordinates(float u, float v, float w)
        {
            U = u;
            V = v;
            W = w;
        }

        public float U { get; }
        public float V { get; }
        public float W { get; }
    }

    /// <summary>
    /// Stores mutable projected bounds for candidate generation.
    /// </summary>
    private struct Bounds2D
    {
        public Bounds2D(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public float MinX { get; private set; }
        public float MinY { get; private set; }
        public float MaxX { get; private set; }
        public float MaxY { get; private set; }

        public void Include(Vector2 point)
        {
            MinX = MathF.Min(MinX, point.X);
            MinY = MathF.Min(MinY, point.Y);
            MaxX = MathF.Max(MaxX, point.X);
            MaxY = MathF.Max(MaxY, point.Y);
        }
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
            return (long)MathF.Round(value * MeshEdgePointKeyScale);
        }
    }

    /// <summary>
    /// Stores one quantized XY point for boundary-loop endpoint matching.
    /// </summary>
    private readonly struct PointKey2D : IEquatable<PointKey2D>
    {
        public PointKey2D(Vector2 point)
        {
            X = Quantize(point.X);
            Y = Quantize(point.Y);
        }

        public long X { get; }

        public long Y { get; }

        public bool Equals(PointKey2D other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object? obj)
        {
            return obj is PointKey2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        private static long Quantize(float value)
        {
            return (long)MathF.Round(value * MeshEdgePointKeyScale);
        }
    }
}
