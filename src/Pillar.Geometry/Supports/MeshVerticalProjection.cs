// MeshVerticalProjection.cs
// Projects guide points onto mesh triangles along the build Z axis without depending on rendering objects.
using Pillar.Core.Entities;
using Pillar.Core.Supports;
using System;
using System.Numerics;

namespace Pillar.Geometry.Supports;

/// <summary>
/// Describes one vertical projection hit on a transformed mesh.
/// </summary>
public readonly struct MeshProjectionHit
{
    /// <summary>
    /// Creates one projection hit with a world-space point and triangle normal.
    /// </summary>
    public MeshProjectionHit(Vector3 point, Vector3 normal)
    {
        Point = point;
        Normal = normal;
    }

    /// <summary>
    /// Gets the world-space point on the mesh triangle.
    /// </summary>
    public Vector3 Point { get; }

    /// <summary>
    /// Gets the world-space triangle normal at the projected point.
    /// </summary>
    public Vector3 Normal { get; }
}

/// <summary>
/// Projects world-space guide points vertically onto transformed mesh triangles.
/// </summary>
public static class MeshVerticalProjection
{
    private const float ProjectionTolerance = 0.001f;

    /// <summary>
    /// Calculates the bounded nearest-surface fallback radius used by generated support tools.
    /// </summary>
    public static float CalculateSupportFallbackRadius(float spacing, SupportProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0.0f)
        {
            return ProjectionTolerance;
        }

        float radius = MathF.Min(spacing * 0.5f, profile.ModelClearance);
        return MathF.Max(ProjectionTolerance, radius);
    }

    /// <summary>
    /// Finds the nearest intersection between a vertical Z line through a guide point and the supplied mesh.
    /// </summary>
    public static bool TryProjectToMesh(MeshEntity mesh, Vector3 guidePoint, out Vector3 projectedPoint)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return TryProjectToMesh(mesh, mesh.WorldTransform, guidePoint, out projectedPoint);
    }

    /// <summary>
    /// Finds the nearest vertical projection using an explicit mesh transform, which lets callers preview or regenerate against a pending transform.
    /// </summary>
    public static bool TryProjectToMesh(MeshEntity mesh, Matrix4x4 worldTransform, Vector3 guidePoint, out Vector3 projectedPoint)
    {
        MeshProjectionHit hit;

        if (TryProjectToMesh(mesh, worldTransform, guidePoint, out hit))
        {
            projectedPoint = hit.Point;
            return true;
        }

        projectedPoint = Vector3.Zero;
        return false;
    }

    /// <summary>
    /// Finds the nearest vertical projection hit and returns its world-space triangle normal.
    /// </summary>
    public static bool TryProjectToMesh(MeshEntity mesh, Vector3 guidePoint, out MeshProjectionHit hit)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return TryProjectToMesh(mesh, mesh.WorldTransform, guidePoint, out hit);
    }

    /// <summary>
    /// Finds the nearest vertical projection hit with an explicit mesh transform and triangle normal.
    /// </summary>
    public static bool TryProjectToMesh(MeshEntity mesh, Matrix4x4 worldTransform, Vector3 guidePoint, out MeshProjectionHit hit)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        float bestDistance = float.MaxValue;
        Vector3 bestPoint = Vector3.Zero;
        Vector3 bestNormal = Vector3.UnitZ;
        bool hasHit = false;

        for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            Vector3 a = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i]], worldTransform);
            Vector3 b = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i + 1]], worldTransform);
            Vector3 c = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i + 2]], worldTransform);

            if (!TryIntersectVerticalLineWithTriangle(guidePoint, a, b, c, out float z))
            {
                continue;
            }

            float distance = MathF.Abs(z - guidePoint.Z);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = new Vector3(guidePoint.X, guidePoint.Y, z);
                bestNormal = CalculateTriangleNormal(a, b, c);
                hasHit = true;
            }
        }

        hit = new MeshProjectionHit(bestPoint, bestNormal);
        return hasHit;
    }

    /// <summary>
    /// Finds the first exterior vertical projection that can accept a build-plate support without crossing the mesh.
    /// </summary>
    public static bool TryProjectSupportToMesh(
        MeshEntity mesh,
        Vector3 guidePoint,
        SupportProfile profile,
        out MeshProjectionHit hit,
        out SupportPlacementPlan placementPlan)
    {
        return TryProjectSupportToMesh(mesh, guidePoint, profile, 0.0f, out hit, out placementPlan);
    }

    /// <summary>
    /// Finds a supportable projection, falling back to a nearby surface point when vertical projection misses.
    /// </summary>
    public static bool TryProjectSupportToMesh(
        MeshEntity mesh,
        Vector3 guidePoint,
        SupportProfile profile,
        float fallbackRadius,
        out MeshProjectionHit hit,
        out SupportPlacementPlan placementPlan)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return TryProjectSupportToMesh(mesh, mesh.WorldTransform, guidePoint, profile, fallbackRadius, out hit, out placementPlan);
    }

    /// <summary>
    /// Finds the first supportable vertical projection using an explicit mesh transform.
    /// </summary>
    public static bool TryProjectSupportToMesh(
        MeshEntity mesh,
        Matrix4x4 worldTransform,
        Vector3 guidePoint,
        SupportProfile profile,
        out MeshProjectionHit hit,
        out SupportPlacementPlan placementPlan)
    {
        return TryProjectSupportToMesh(mesh, worldTransform, guidePoint, profile, 0.0f, out hit, out placementPlan);
    }

    /// <summary>
    /// Finds a supportable projection with an explicit transform and bounded nearest-surface fallback.
    /// </summary>
    public static bool TryProjectSupportToMesh(
        MeshEntity mesh,
        Matrix4x4 worldTransform,
        Vector3 guidePoint,
        SupportProfile profile,
        float fallbackRadius,
        out MeshProjectionHit hit,
        out SupportPlacementPlan placementPlan)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (TryProjectSupportVertically(mesh, worldTransform, guidePoint, profile, out hit, out placementPlan))
        {
            return true;
        }

        if (fallbackRadius <= ProjectionTolerance)
        {
            hit = default;
            placementPlan = default;
            return false;
        }

        return TryProjectSupportToNearestSurface(mesh, worldTransform, guidePoint, profile, fallbackRadius, out hit, out placementPlan);
    }

    /// <summary>
    /// Finds the nearest supportable vertical projection before any nearest-surface fallback is considered.
    /// </summary>
    private static bool TryProjectSupportVertically(
        MeshEntity mesh,
        Matrix4x4 worldTransform,
        Vector3 guidePoint,
        SupportProfile profile,
        out MeshProjectionHit hit,
        out SupportPlacementPlan placementPlan)
    {
        float bestDistance = float.MaxValue;
        MeshProjectionHit bestHit = default;
        SupportPlacementPlan bestPlacementPlan = default;
        bool hasHit = false;

        for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            Vector3 a = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i]], worldTransform);
            Vector3 b = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i + 1]], worldTransform);
            Vector3 c = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i + 2]], worldTransform);

            if (!TryIntersectVerticalLineWithTriangle(guidePoint, a, b, c, out float z))
            {
                continue;
            }

            if (z < 0.0f)
            {
                continue;
            }

            Vector3 point = new Vector3(guidePoint.X, guidePoint.Y, z);
            Vector3 normal = CalculateTriangleNormal(a, b, c);
            SupportPlacementPlan candidatePlacementPlan;

            if (!SupportPlacementPlanner.TryCreatePlacement(mesh, worldTransform, point, normal, profile, out candidatePlacementPlan))
            {
                continue;
            }

            float distance = MathF.Abs(z - guidePoint.Z);

            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestHit = new MeshProjectionHit(point, normal);
            bestPlacementPlan = candidatePlacementPlan;
            hasHit = true;
        }

        hit = bestHit;
        placementPlan = bestPlacementPlan;
        return hasHit;
    }

    /// <summary>
    /// Finds the closest supportable mesh surface point inside a bounded fallback radius.
    /// </summary>
    private static bool TryProjectSupportToNearestSurface(
        MeshEntity mesh,
        Matrix4x4 worldTransform,
        Vector3 guidePoint,
        SupportProfile profile,
        float fallbackRadius,
        out MeshProjectionHit hit,
        out SupportPlacementPlan placementPlan)
    {
        float fallbackRadiusSquared = fallbackRadius * fallbackRadius;
        float bestDistanceSquared = float.MaxValue;
        MeshProjectionHit bestHit = default;
        SupportPlacementPlan bestPlacementPlan = default;
        bool hasHit = false;

        for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            Vector3 a = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i]], worldTransform);
            Vector3 b = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i + 1]], worldTransform);
            Vector3 c = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[i + 2]], worldTransform);

            if (CanSkipTriangleByNearestBounds(guidePoint, fallbackRadius, a, b, c))
            {
                continue;
            }

            Vector3 closestPoint = GetClosestPointOnTriangle(guidePoint, a, b, c);
            float distanceSquared = Vector3.DistanceSquared(guidePoint, closestPoint);

            if (distanceSquared > fallbackRadiusSquared || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            Vector3 normal = CalculateTriangleNormal(a, b, c);
            SupportPlacementPlan candidatePlacementPlan;

            if (!SupportPlacementPlanner.TryCreatePlacement(mesh, worldTransform, closestPoint, normal, profile, out candidatePlacementPlan))
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestHit = new MeshProjectionHit(closestPoint, normal);
            bestPlacementPlan = candidatePlacementPlan;
            hasHit = true;
        }

        hit = bestHit;
        placementPlan = bestPlacementPlan;
        return hasHit;
    }

    /// <summary>
    /// Calculates a stable world-space triangle normal for a projected support contact.
    /// </summary>
    private static Vector3 CalculateTriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);

        if (normal.LengthSquared() <= 0.00000001f)
        {
            return Vector3.UnitZ;
        }

        return Vector3.Normalize(normal);
    }

    /// <summary>
    /// Quickly rejects triangles whose expanded bounds cannot contain a fallback nearest point.
    /// </summary>
    private static bool CanSkipTriangleByNearestBounds(Vector3 guidePoint, float fallbackRadius, Vector3 a, Vector3 b, Vector3 c)
    {
        float minX = MathF.Min(a.X, MathF.Min(b.X, c.X));
        float maxX = MathF.Max(a.X, MathF.Max(b.X, c.X));
        float minY = MathF.Min(a.Y, MathF.Min(b.Y, c.Y));
        float maxY = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
        float minZ = MathF.Min(a.Z, MathF.Min(b.Z, c.Z));
        float maxZ = MathF.Max(a.Z, MathF.Max(b.Z, c.Z));
        float dx = DistanceFromRange(guidePoint.X, minX, maxX);
        float dy = DistanceFromRange(guidePoint.Y, minY, maxY);
        float dz = DistanceFromRange(guidePoint.Z, minZ, maxZ);

        return (dx * dx) + (dy * dy) + (dz * dz) > fallbackRadius * fallbackRadius;
    }

    /// <summary>
    /// Gets the distance between one coordinate and a closed range.
    /// </summary>
    private static float DistanceFromRange(float value, float min, float max)
    {
        if (value < min)
        {
            return min - value;
        }

        if (value > max)
        {
            return value - max;
        }

        return 0.0f;
    }

    /// <summary>
    /// Calculates the closest point on one triangle to a guide point.
    /// </summary>
    private static Vector3 GetClosestPointOnTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = point - a;
        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);

        if (d1 <= 0.0f && d2 <= 0.0f)
        {
            return a;
        }

        Vector3 bp = point - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);

        if (d3 >= 0.0f && d4 <= d3)
        {
            return b;
        }

        float vc = (d1 * d4) - (d3 * d2);

        if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
        {
            float v = d1 / (d1 - d3);
            return a + (ab * v);
        }

        Vector3 cp = point - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);

        if (d6 >= 0.0f && d5 <= d6)
        {
            return c;
        }

        float vb = (d5 * d2) - (d1 * d6);

        if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
        {
            float w = d2 / (d2 - d6);
            return a + (ac * w);
        }

        float va = (d3 * d6) - (d5 * d4);

        if (va <= 0.0f && d4 - d3 >= 0.0f && d5 - d6 >= 0.0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + ((c - b) * w);
        }

        float denominator = 1.0f / (va + vb + vc);
        float vInside = vb * denominator;
        float wInside = vc * denominator;
        return a + (ab * vInside) + (ac * wInside);
    }

    /// <summary>
    /// Intersects one vertical line with one triangle by solving barycentric coordinates in the XY projection.
    /// </summary>
    private static bool TryIntersectVerticalLineWithTriangle(Vector3 guidePoint, Vector3 a, Vector3 b, Vector3 c, out float z)
    {
        const float Epsilon = 0.000001f;

        Vector2 p = new Vector2(guidePoint.X, guidePoint.Y);
        Vector2 a2 = new Vector2(a.X, a.Y);
        Vector2 b2 = new Vector2(b.X, b.Y);
        Vector2 c2 = new Vector2(c.X, c.Y);
        Vector2 v0 = b2 - a2;
        Vector2 v1 = c2 - a2;
        Vector2 v2 = p - a2;
        float denominator = (v0.X * v1.Y) - (v1.X * v0.Y);

        if (MathF.Abs(denominator) <= Epsilon)
        {
            return TryIntersectVerticalLineWithDegenerateTriangle(guidePoint, a, b, c, out z);
        }

        float u = ((v2.X * v1.Y) - (v1.X * v2.Y)) / denominator;
        float v = ((v0.X * v2.Y) - (v2.X * v0.Y)) / denominator;
        float w = 1.0f - u - v;

        if (u < -Epsilon || v < -Epsilon || w < -Epsilon)
        {
            z = 0.0f;
            return false;
        }

        z = (w * a.Z) + (u * b.Z) + (v * c.Z);
        return true;
    }

    /// <summary>
    /// Resolves vertical or near-vertical triangles whose XY projection collapses to a line or point.
    /// </summary>
    private static bool TryIntersectVerticalLineWithDegenerateTriangle(Vector3 guidePoint, Vector3 a, Vector3 b, Vector3 c, out float z)
    {
        float minimumZ = float.MaxValue;
        float maximumZ = float.MinValue;
        bool hasHit = false;

        TryAddDegenerateEdgeHit(guidePoint, a, b, ref minimumZ, ref maximumZ, ref hasHit);
        TryAddDegenerateEdgeHit(guidePoint, b, c, ref minimumZ, ref maximumZ, ref hasHit);
        TryAddDegenerateEdgeHit(guidePoint, c, a, ref minimumZ, ref maximumZ, ref hasHit);

        z = hasHit
            ? Math.Clamp(guidePoint.Z, minimumZ, maximumZ)
            : 0.0f;
        return hasHit;
    }

    /// <summary>
    /// Adds the Z value where a vertical guide line lies on one projected triangle edge.
    /// </summary>
    private static void TryAddDegenerateEdgeHit(
        Vector3 guidePoint,
        Vector3 edgeStart,
        Vector3 edgeEnd,
        ref float minimumZ,
        ref float maximumZ,
        ref bool hasHit)
    {
        Vector2 guide = new Vector2(guidePoint.X, guidePoint.Y);
        Vector2 start = new Vector2(edgeStart.X, edgeStart.Y);
        Vector2 end = new Vector2(edgeEnd.X, edgeEnd.Y);
        Vector2 edge = end - start;
        float edgeLengthSquared = edge.LengthSquared();

        if (edgeLengthSquared <= ProjectionTolerance * ProjectionTolerance)
        {
            if (Vector2.DistanceSquared(guide, start) > ProjectionTolerance * ProjectionTolerance)
            {
                return;
            }

            AddDegenerateCandidateZ(edgeStart.Z, ref minimumZ, ref maximumZ, ref hasHit);
            AddDegenerateCandidateZ(edgeEnd.Z, ref minimumZ, ref maximumZ, ref hasHit);
            return;
        }

        float t = Vector2.Dot(guide - start, edge) / edgeLengthSquared;

        if (t < -ProjectionTolerance || t > 1.0f + ProjectionTolerance)
        {
            return;
        }

        float clampedT = Math.Clamp(t, 0.0f, 1.0f);
        Vector2 closestPoint = start + (edge * clampedT);

        if (Vector2.DistanceSquared(guide, closestPoint) > ProjectionTolerance * ProjectionTolerance)
        {
            return;
        }

        float candidateZ = edgeStart.Z + ((edgeEnd.Z - edgeStart.Z) * clampedT);
        AddDegenerateCandidateZ(candidateZ, ref minimumZ, ref maximumZ, ref hasHit);
    }

    /// <summary>
    /// Expands the overlapping Z interval for a degenerate triangle projection.
    /// </summary>
    private static void AddDegenerateCandidateZ(float candidateZ, ref float minimumZ, ref float maximumZ, ref bool hasHit)
    {
        minimumZ = MathF.Min(minimumZ, candidateZ);
        maximumZ = MathF.Max(maximumZ, candidateZ);
        hasHit = true;
    }
}
