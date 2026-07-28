// TagPlacementPlanner.cs
// Extracts raft bottom perimeters and resolves the closest stable tag placement to a pointer point.
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Tags;

/// <summary>
/// Finds tag attachment points on generated raft boundaries without rendering dependencies.
/// </summary>
public sealed class TagPlacementPlanner
{
    private const float PositionTolerance = 0.0001f;
    private readonly List<BoundarySegment> _segments = new List<BoundarySegment>();

    /// <summary>
    /// Extracts the bottom-face boundary segments once for one placement session.
    /// </summary>
    public TagPlacementPlanner(RaftEntity raft)
    {
        if (raft == null)
        {
            throw new ArgumentNullException(nameof(raft));
        }

        BuildBoundarySegments(raft);
    }

    /// <summary>
    /// Gets the horizontal plane used to project pointer rays during placement.
    /// </summary>
    public float PlacementZ { get; private set; }

    /// <summary>
    /// Finds the closest point and local tangent on any disconnected raft perimeter.
    /// </summary>
    public bool TryFindClosestPlacement(Vector2 point, out TagPlacement placement)
    {
        placement = default;

        if (_segments.Count == 0 || !float.IsFinite(point.X) || !float.IsFinite(point.Y))
        {
            return false;
        }

        float bestDistanceSquared = float.PositiveInfinity;
        Vector2 bestPoint = Vector2.Zero;
        Vector2 bestTangent = Vector2.UnitX;

        for (int i = 0; i < _segments.Count; i++)
        {
            BoundarySegment segment = _segments[i];
            Vector2 direction = segment.End - segment.Start;
            float lengthSquared = direction.LengthSquared();

            if (lengthSquared <= PositionTolerance * PositionTolerance)
            {
                continue;
            }

            float parameter = Math.Clamp(
                Vector2.Dot(point - segment.Start, direction) / lengthSquared,
                0.0f,
                1.0f);
            Vector2 closestPoint = segment.Start + direction * parameter;
            float distanceSquared = Vector2.DistanceSquared(point, closestPoint);

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestPoint = closestPoint;
                bestTangent = Vector2.Normalize(direction);
            }
        }

        if (!float.IsFinite(bestDistanceSquared))
        {
            return false;
        }

        placement = new TagPlacement(
            new Vector3(bestPoint.X, bestPoint.Y, PlacementZ),
            bestTangent);
        return true;
    }

    /// <summary>
    /// Counts edges used once by coplanar bottom triangles; those edges form the printable raft perimeter.
    /// </summary>
    private void BuildBoundarySegments(RaftEntity raft)
    {
        if (raft.Vertices.Count == 0)
        {
            return;
        }

        float minimumZ = raft.Vertices[0].Z;

        for (int i = 1; i < raft.Vertices.Count; i++)
        {
            minimumZ = MathF.Min(minimumZ, raft.Vertices[i].Z);
        }

        PlacementZ = minimumZ;
        Dictionary<EdgeKey, EdgeUse> edgeUses = new Dictionary<EdgeKey, EdgeUse>();

        for (int i = 0; i + 2 < raft.TriangleIndices.Count; i += 3)
        {
            int firstIndex = raft.TriangleIndices[i];
            int secondIndex = raft.TriangleIndices[i + 1];
            int thirdIndex = raft.TriangleIndices[i + 2];
            Vector3 first = raft.Vertices[firstIndex];
            Vector3 second = raft.Vertices[secondIndex];
            Vector3 third = raft.Vertices[thirdIndex];

            if (!IsAtBottom(first.Z, minimumZ)
                || !IsAtBottom(second.Z, minimumZ)
                || !IsAtBottom(third.Z, minimumZ))
            {
                continue;
            }

            AddEdgeUse(firstIndex, secondIndex, first, second, edgeUses);
            AddEdgeUse(secondIndex, thirdIndex, second, third, edgeUses);
            AddEdgeUse(thirdIndex, firstIndex, third, first, edgeUses);
        }

        foreach (EdgeUse edgeUse in edgeUses.Values)
        {
            if (edgeUse.Count == 1
                && Vector2.DistanceSquared(edgeUse.Start, edgeUse.End) > PositionTolerance * PositionTolerance)
            {
                _segments.Add(new BoundarySegment(edgeUse.Start, edgeUse.End));
            }
        }
    }

    /// <summary>
    /// Adds one normalized indexed edge to the bottom-triangle usage map.
    /// </summary>
    private static void AddEdgeUse(
        int firstIndex,
        int secondIndex,
        Vector3 first,
        Vector3 second,
        Dictionary<EdgeKey, EdgeUse> edgeUses)
    {
        EdgeKey key = new EdgeKey(firstIndex, secondIndex);

        if (edgeUses.TryGetValue(key, out EdgeUse existing))
        {
            edgeUses[key] = new EdgeUse(existing.Start, existing.End, existing.Count + 1);
            return;
        }

        edgeUses.Add(
            key,
            new EdgeUse(
                new Vector2(first.X, first.Y),
                new Vector2(second.X, second.Y),
                1));
    }

    /// <summary>
    /// Compares a vertex height with the raft's bottom plane.
    /// </summary>
    private static bool IsAtBottom(float value, float minimumZ)
    {
        return MathF.Abs(value - minimumZ) <= PositionTolerance;
    }

    private readonly record struct BoundarySegment(Vector2 Start, Vector2 End);

    private readonly record struct EdgeUse(Vector2 Start, Vector2 End, int Count);

    /// <summary>
    /// Normalizes an indexed edge so adjacent triangles share one dictionary key.
    /// </summary>
    private readonly record struct EdgeKey
    {
        public EdgeKey(int first, int second)
        {
            First = Math.Min(first, second);
            Second = Math.Max(first, second);
        }

        public int First { get; }
        public int Second { get; }
    }
}
