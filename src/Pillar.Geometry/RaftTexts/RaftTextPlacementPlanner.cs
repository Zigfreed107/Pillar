// RaftTextPlacementPlanner.cs
// Resolves pointer positions to interior raft locations that retain a fixed text boundary margin.
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.RaftTexts;

/// <summary>
/// Finds stable plan-view text placements on horizontal raft surfaces.
/// </summary>
public sealed class RaftTextPlacementPlanner
{
    private const float PositionTolerance = 0.0001f;
    private const int SearchIterationCount = 32;
    private const float SurfaceCellSize = 10.0f;
    private readonly List<SurfaceTriangle> _surfaces = new List<SurfaceTriangle>();
    private readonly List<Vector2> _validDestinations = new List<Vector2>();
    private readonly Dictionary<(int X, int Y), List<int>> _surfaceIndicesByCell = new Dictionary<(int X, int Y), List<int>>();
    private readonly float _minimumLocalX;
    private readonly float _maximumLocalX;
    private readonly float _minimumLocalY;
    private readonly float _maximumLocalY;
    private readonly float _projectionPlaneZ;

    /// <summary>
    /// Extracts horizontal raft surfaces and bordered local text bounds once per placement session.
    /// </summary>
    public RaftTextPlacementPlanner(
        RaftEntity raft,
        RaftTextMeshData localMesh,
        float borderOffset)
    {
        if (raft == null)
        {
            throw new ArgumentNullException(nameof(raft));
        }

        if (localMesh == null)
        {
            throw new ArgumentNullException(nameof(localMesh));
        }

        if (!float.IsFinite(borderOffset) || borderOffset < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(borderOffset));
        }

        (_minimumLocalX, _maximumLocalX, _minimumLocalY, _maximumLocalY) =
            CalculateBorderedBounds(localMesh, borderOffset);
        _projectionPlaneZ = ExtractHorizontalSurfaces(raft);
        BuildValidDestinations();
    }

    /// <summary>
    /// Gets the horizontal plane used to project the pointer ray.
    /// </summary>
    public float ProjectionPlaneZ
    {
        get { return _projectionPlaneZ; }
    }

    /// <summary>
    /// Finds the nearest valid placement to a pointer over the raft.
    /// </summary>
    public bool TryFindPlacement(Vector2 pointer, out Vector3 placement)
    {
        placement = default;

        if (_surfaces.Count == 0
            || !float.IsFinite(pointer.X)
            || !float.IsFinite(pointer.Y)
            || !TryGetSurfaceHeight(pointer, out _))
        {
            return false;
        }

        if (TryCreatePlacement(pointer, out placement))
        {
            return true;
        }

        float bestDistanceSquared = float.PositiveInfinity;
        Vector2 nearestDestination = default;

        for (int i = 0; i < _validDestinations.Count; i++)
        {
            float distanceSquared = Vector2.DistanceSquared(pointer, _validDestinations[i]);

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                nearestDestination = _validDestinations[i];
            }
        }

        if (!float.IsFinite(bestDistanceSquared))
        {
            return false;
        }

        Vector2 validPoint = FindNearestValidPoint(pointer, nearestDestination);
        return TryCreatePlacement(validPoint, out placement)
            || TryCreatePlacement(nearestDestination, out placement);
    }

    /// <summary>
    /// Precomputes valid fallback centers so pointer movement does not perform a quadratic search.
    /// </summary>
    private void BuildValidDestinations()
    {
        for (int i = 0; i < _surfaces.Count; i++)
        {
            SurfaceTriangle surface = _surfaces[i];
            AddValidDestination(surface.Centroid);
            AddValidDestination(surface.First);
            AddValidDestination(surface.Second);
            AddValidDestination(surface.Third);
            AddValidDestination((surface.First + surface.Second) * 0.5f);
            AddValidDestination((surface.Second + surface.Third) * 0.5f);
            AddValidDestination((surface.Third + surface.First) * 0.5f);
        }
    }

    /// <summary>
    /// Retains one feasible fallback placement candidate.
    /// </summary>
    private void AddValidDestination(Vector2 destination)
    {
        if (CanFitAt(destination))
        {
            _validDestinations.Add(destination);
        }
    }

    /// <summary>
    /// Searches from a known valid interior point toward the pointer without crossing the fit boundary.
    /// </summary>
    private Vector2 FindNearestValidPoint(Vector2 pointer, Vector2 validDestination)
    {
        float invalidParameter = 0.0f;
        float validParameter = 1.0f;

        for (int i = 0; i < SearchIterationCount; i++)
        {
            float midpoint = (invalidParameter + validParameter) * 0.5f;
            Vector2 candidate = Vector2.Lerp(pointer, validDestination, midpoint);

            if (CanFitAt(candidate))
            {
                validParameter = midpoint;
            }
            else
            {
                invalidParameter = midpoint;
            }
        }

        return Vector2.Lerp(pointer, validDestination, validParameter);
    }

    /// <summary>
    /// Creates a surface-height placement when the entire bordered text rectangle fits.
    /// </summary>
    private bool TryCreatePlacement(Vector2 center, out Vector3 placement)
    {
        placement = default;

        if (!CanFitAt(center) || !TryGetSurfaceHeight(center, out float surfaceZ))
        {
            return false;
        }

        placement = new Vector3(center, surfaceZ);
        return true;
    }

    /// <summary>
    /// Conservatively checks the four corners, edge midpoints, and center of the bordered text bounds.
    /// </summary>
    private bool CanFitAt(Vector2 center)
    {
        float minimumX = center.X + _minimumLocalX;
        float maximumX = center.X + _maximumLocalX;
        float minimumY = center.Y + _minimumLocalY;
        float maximumY = center.Y + _maximumLocalY;
        float middleX = (minimumX + maximumX) * 0.5f;
        float middleY = (minimumY + maximumY) * 0.5f;

        return IsOnRaft(minimumX, minimumY)
            && IsOnRaft(maximumX, minimumY)
            && IsOnRaft(maximumX, maximumY)
            && IsOnRaft(minimumX, maximumY)
            && IsOnRaft(middleX, minimumY)
            && IsOnRaft(maximumX, middleY)
            && IsOnRaft(middleX, maximumY)
            && IsOnRaft(minimumX, middleY)
            && IsOnRaft(middleX, middleY);
    }

    /// <summary>
    /// Tests one XY sample against any horizontal raft surface.
    /// </summary>
    private bool IsOnRaft(float x, float y)
    {
        return TryGetSurfaceHeight(new Vector2(x, y), out _);
    }

    /// <summary>
    /// Returns the uppermost horizontal surface beneath one XY point.
    /// </summary>
    private bool TryGetSurfaceHeight(Vector2 point, out float surfaceZ)
    {
        surfaceZ = float.NegativeInfinity;
        (int X, int Y) cell = GetSurfaceCell(point);

        if (!_surfaceIndicesByCell.TryGetValue(cell, out List<int>? surfaceIndices))
        {
            return false;
        }

        for (int i = 0; i < surfaceIndices.Count; i++)
        {
            SurfaceTriangle surface = _surfaces[surfaceIndices[i]];

            if (ContainsPoint(surface.First, surface.Second, surface.Third, point))
            {
                surfaceZ = MathF.Max(surfaceZ, surface.Z);
            }
        }

        return float.IsFinite(surfaceZ);
    }

    /// <summary>
    /// Captures non-degenerate horizontal triangles and returns their maximum Z for pointer projection.
    /// </summary>
    private float ExtractHorizontalSurfaces(RaftEntity raft)
    {
        float maximumZ = 0.0f;
        bool hasSurface = false;

        for (int i = 0; i + 2 < raft.TriangleIndices.Count; i += 3)
        {
            Vector3 first = raft.Vertices[raft.TriangleIndices[i]];
            Vector3 second = raft.Vertices[raft.TriangleIndices[i + 1]];
            Vector3 third = raft.Vertices[raft.TriangleIndices[i + 2]];

            if (MathF.Abs(first.Z - second.Z) > PositionTolerance
                || MathF.Abs(first.Z - third.Z) > PositionTolerance)
            {
                continue;
            }

            Vector2 first2D = new Vector2(first.X, first.Y);
            Vector2 second2D = new Vector2(second.X, second.Y);
            Vector2 third2D = new Vector2(third.X, third.Y);

            if (MathF.Abs(Cross(second2D - first2D, third2D - first2D)) <= PositionTolerance)
            {
                continue;
            }

            float z = (first.Z + second.Z + third.Z) / 3.0f;
            AddSurface(new SurfaceTriangle(first2D, second2D, third2D, z));
            maximumZ = hasSurface ? MathF.Max(maximumZ, z) : z;
            hasSurface = true;
        }

        return hasSurface ? maximumZ : 0.0f;
    }

    /// <summary>
    /// Adds one surface to the coarse XY lookup used by repeated fit samples.
    /// </summary>
    private void AddSurface(SurfaceTriangle surface)
    {
        int surfaceIndex = _surfaces.Count;
        _surfaces.Add(surface);
        Vector2 minimum = Vector2.Min(surface.First, Vector2.Min(surface.Second, surface.Third));
        Vector2 maximum = Vector2.Max(surface.First, Vector2.Max(surface.Second, surface.Third));
        (int X, int Y) minimumCell = GetSurfaceCell(minimum);
        (int X, int Y) maximumCell = GetSurfaceCell(maximum);

        for (int x = minimumCell.X; x <= maximumCell.X; x++)
        {
            for (int y = minimumCell.Y; y <= maximumCell.Y; y++)
            {
                (int X, int Y) cell = (x, y);

                if (!_surfaceIndicesByCell.TryGetValue(cell, out List<int>? surfaceIndices))
                {
                    surfaceIndices = new List<int>();
                    _surfaceIndicesByCell.Add(cell, surfaceIndices);
                }

                surfaceIndices.Add(surfaceIndex);
            }
        }
    }

    /// <summary>
    /// Quantizes one XY sample for the horizontal-surface lookup.
    /// </summary>
    private static (int X, int Y) GetSurfaceCell(Vector2 point)
    {
        return (
            (int)MathF.Floor(point.X / SurfaceCellSize),
            (int)MathF.Floor(point.Y / SurfaceCellSize));
    }

    /// <summary>
    /// Computes the visible-glyph bounds expanded by the required raft boundary margin.
    /// </summary>
    private static (float MinimumX, float MaximumX, float MinimumY, float MaximumY) CalculateBorderedBounds(
        RaftTextMeshData localMesh,
        float borderOffset)
    {
        if (localMesh.Positions.Count == 0)
        {
            return (-borderOffset, borderOffset, -borderOffset, borderOffset);
        }

        float minimumX = localMesh.Positions[0].X;
        float maximumX = minimumX;
        float minimumY = localMesh.Positions[0].Y;
        float maximumY = minimumY;

        for (int i = 1; i < localMesh.Positions.Count; i++)
        {
            Vector3 position = localMesh.Positions[i];
            minimumX = MathF.Min(minimumX, position.X);
            maximumX = MathF.Max(maximumX, position.X);
            minimumY = MathF.Min(minimumY, position.Y);
            maximumY = MathF.Max(maximumY, position.Y);
        }

        return (
            minimumX - borderOffset,
            maximumX + borderOffset,
            minimumY - borderOffset,
            maximumY + borderOffset);
    }

    /// <summary>
    /// Tests a point against one triangle using sign-consistent cross products.
    /// </summary>
    private static bool ContainsPoint(Vector2 first, Vector2 second, Vector2 third, Vector2 point)
    {
        float firstCross = Cross(second - first, point - first);
        float secondCross = Cross(third - second, point - second);
        float thirdCross = Cross(first - third, point - third);
        bool hasNegative = firstCross < -PositionTolerance
            || secondCross < -PositionTolerance
            || thirdCross < -PositionTolerance;
        bool hasPositive = firstCross > PositionTolerance
            || secondCross > PositionTolerance
            || thirdCross > PositionTolerance;
        return !(hasNegative && hasPositive);
    }

    /// <summary>
    /// Returns the scalar two-dimensional cross product.
    /// </summary>
    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.X * second.Y - first.Y * second.X;
    }

    /// <summary>
    /// Stores one reusable horizontal raft surface.
    /// </summary>
    private readonly record struct SurfaceTriangle(Vector2 First, Vector2 Second, Vector2 Third, float Z)
    {
        public Vector2 Centroid
        {
            get { return (First + Second + Third) / 3.0f; }
        }
    }
}
