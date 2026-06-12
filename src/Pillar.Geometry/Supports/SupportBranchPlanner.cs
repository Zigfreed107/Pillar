// SupportBranchPlanner.cs
// Calculates optional support branch geometry against mesh triangles before support entities reach rendering or export.
using Pillar.Core.Entities;
using Pillar.Core.Supports;
using System;
using System.Numerics;

namespace Pillar.Geometry.Supports;

/// <summary>
/// Describes the precomputed branch placement used by one support entity.
/// </summary>
public readonly struct SupportBranchPlan
{
    /// <summary>
    /// Creates one branch placement result.
    /// </summary>
    public SupportBranchPlan(Vector3 basePosition, float branchLength, Vector3 branchDirection)
    {
        BasePosition = basePosition;
        BranchLength = branchLength;
        BranchDirection = branchDirection;
    }

    /// <summary>
    /// Gets the build-plane base position below the chosen stem joint.
    /// </summary>
    public Vector3 BasePosition { get; }

    /// <summary>
    /// Gets the optional branch cylinder length.
    /// </summary>
    public float BranchLength { get; }

    /// <summary>
    /// Gets the branch direction from the stem joint toward the head joint.
    /// </summary>
    public Vector3 BranchDirection { get; }
}

/// <summary>
/// Finds a branch length that moves the vertical stem clear of the supported mesh.
/// </summary>
public static class SupportBranchPlanner
{
    private const float GeometryTolerance = 0.000001f;
    private const float HeadContactAllowance = 0.001f;
    private const int BranchSearchSteps = 24;

    /// <summary>
    /// Calculates branch data against the mesh's current world transform.
    /// </summary>
    public static bool TryCreateBranchPlan(
        MeshEntity mesh,
        Vector3 tipPosition,
        Vector3 headDirection,
        SupportProfile profile,
        out SupportBranchPlan plan)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return TryCreateBranchPlan(mesh, mesh.WorldTransform, tipPosition, headDirection, profile, out plan);
    }

    /// <summary>
    /// Calculates branch data against an explicit mesh transform for transform regeneration.
    /// </summary>
    public static bool TryCreateBranchPlan(
        MeshEntity mesh,
        Matrix4x4 worldTransform,
        Vector3 tipPosition,
        Vector3 headDirection,
        SupportProfile profile,
        out SupportBranchPlan plan)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        Vector3 clampedHeadDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(headDirection, profile);
        float usableHeadLength = CalculateUsableHeadLength(tipPosition, 0.0f, clampedHeadDirection, profile.HeadHeight);
        Vector3 headJointPosition = tipPosition - (clampedHeadDirection * usableHeadLength);
        Vector3 branchDirection = CreateBranchDirection(clampedHeadDirection, profile);
        float stemCollisionRadius = MathF.Max(profile.StemBottomDiameter, profile.StemTopDiameter) * 0.5f;
        float supportBodyRadius = profile.StemTopDiameter * 0.5f;

        if (profile.MaximumBranchLength <= GeometryTolerance)
        {
            return TryCreateCandidatePlan(
                mesh,
                worldTransform,
                tipPosition,
                headJointPosition,
                headJointPosition,
                clampedHeadDirection,
                0.0f,
                branchDirection,
                stemCollisionRadius,
                supportBodyRadius,
                out plan);
        }

        if (TryCreateCandidatePlan(
            mesh,
            worldTransform,
            tipPosition,
            headJointPosition,
            headJointPosition,
            clampedHeadDirection,
            0.0f,
            branchDirection,
            stemCollisionRadius,
            supportBodyRadius,
            out plan))
        {
            return true;
        }

        for (int stepIndex = 1; stepIndex <= BranchSearchSteps; stepIndex++)
        {
            float branchLength = profile.MaximumBranchLength * stepIndex / BranchSearchSteps;
            Vector3 stemJointPosition = headJointPosition - (branchDirection * branchLength);

            if (stemJointPosition.Z <= GeometryTolerance)
            {
                continue;
            }

            if (TryCreateCandidatePlan(
                mesh,
                worldTransform,
                tipPosition,
                stemJointPosition,
                headJointPosition,
                clampedHeadDirection,
                branchLength,
                branchDirection,
                stemCollisionRadius,
                supportBodyRadius,
                out plan))
            {
                return true;
            }
        }

        plan = default;
        return false;
    }

    /// <summary>
    /// Calculates the head length that can physically fit above the build plate.
    /// </summary>
    private static float CalculateUsableHeadLength(Vector3 tipPosition, float baseZ, Vector3 headDirection, float requestedHeadLength)
    {
        if (headDirection.Z <= GeometryTolerance)
        {
            return requestedHeadLength;
        }

        float maximumLengthByHeight = MathF.Max(0.0f, (tipPosition.Z - baseZ) / headDirection.Z);
        return MathF.Min(requestedHeadLength, maximumLengthByHeight);
    }

    /// <summary>
    /// Creates one branch plan only when every non-penetrating support segment clears the mesh.
    /// </summary>
    private static bool TryCreateCandidatePlan(
        MeshEntity mesh,
        Matrix4x4 worldTransform,
        Vector3 tipPosition,
        Vector3 stemJointPosition,
        Vector3 headJointPosition,
        Vector3 headDirection,
        float branchLength,
        Vector3 branchDirection,
        float stemCollisionRadius,
        float supportBodyRadius,
        out SupportBranchPlan plan)
    {
        if (!IsSupportPathClear(
            mesh,
            worldTransform,
            tipPosition,
            stemJointPosition,
            headJointPosition,
            headDirection,
            branchLength,
            stemCollisionRadius,
            supportBodyRadius))
        {
            plan = default;
            return false;
        }

        plan = CreatePlan(stemJointPosition, branchLength, branchDirection);
        return true;
    }

    /// <summary>
    /// Tests the stem, optional branch, and exterior head centerline against the transformed mesh.
    /// </summary>
    private static bool IsSupportPathClear(
        MeshEntity mesh,
        Matrix4x4 worldTransform,
        Vector3 tipPosition,
        Vector3 stemJointPosition,
        Vector3 headJointPosition,
        Vector3 headDirection,
        float branchLength,
        float stemCollisionRadius,
        float supportBodyRadius)
    {
        Vector3 stemStart = new Vector3(stemJointPosition.X, stemJointPosition.Y, 0.0f);

        if (!IsCapsuleClear(mesh, worldTransform, stemStart, stemJointPosition, stemCollisionRadius))
        {
            return false;
        }

        if (branchLength > GeometryTolerance
            && !IsCapsuleClear(mesh, worldTransform, stemJointPosition, headJointPosition, supportBodyRadius))
        {
            return false;
        }

        Vector3 headCheckEnd = tipPosition - (headDirection * HeadContactAllowance);

        if (Vector3.DistanceSquared(headJointPosition, headCheckEnd) <= GeometryTolerance)
        {
            return true;
        }

        return IsSegmentCenterlineClear(mesh, worldTransform, headJointPosition, headCheckEnd);
    }

    /// <summary>
    /// Creates a branch direction at the preset branch angle using the head direction's horizontal azimuth.
    /// </summary>
    public static Vector3 CreateBranchDirection(Vector3 headDirection, SupportProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        float angleRadians = profile.BranchAngleFromVerticalDegrees * (MathF.PI / 180.0f);
        Vector3 horizontal = new Vector3(headDirection.X, headDirection.Y, 0.0f);

        if (horizontal.LengthSquared() <= GeometryTolerance)
        {
            horizontal = Vector3.UnitX;
        }
        else
        {
            horizontal = Vector3.Normalize(horizontal);
        }

        return Vector3.Normalize((Vector3.UnitZ * MathF.Cos(angleRadians)) + (horizontal * MathF.Sin(angleRadians)));
    }

    /// <summary>
    /// Creates a branch plan with a base point directly below the supplied stem joint.
    /// </summary>
    private static SupportBranchPlan CreatePlan(Vector3 stemJointPosition, float branchLength, Vector3 branchDirection)
    {
        Vector3 basePosition = new Vector3(stemJointPosition.X, stemJointPosition.Y, 0.0f);
        return new SupportBranchPlan(basePosition, branchLength, branchDirection);
    }

    /// <summary>
    /// Tests whether a swept clearance radius around a support segment clears the transformed mesh.
    /// </summary>
    private static bool IsCapsuleClear(MeshEntity mesh, Matrix4x4 worldTransform, Vector3 segmentStart, Vector3 segmentEnd, float clearanceRadius)
    {
        if (clearanceRadius <= GeometryTolerance)
        {
            return true;
        }

        float clearanceRadiusSquared = clearanceRadius * clearanceRadius;

        for (int triangleStart = 0; triangleStart < mesh.TriangleIndices.Count; triangleStart += 3)
        {
            Vector3 a = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[triangleStart]], worldTransform);
            Vector3 b = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[triangleStart + 1]], worldTransform);
            Vector3 c = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[triangleStart + 2]], worldTransform);

            if (CanSkipTriangleByBounds(segmentStart, segmentEnd, clearanceRadius, a, b, c))
            {
                continue;
            }

            if (DistanceSquaredBetweenSegmentAndTriangle(segmentStart, segmentEnd, a, b, c) <= clearanceRadiusSquared)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tests whether a support segment centerline intersects any transformed mesh triangle.
    /// </summary>
    private static bool IsSegmentCenterlineClear(MeshEntity mesh, Matrix4x4 worldTransform, Vector3 segmentStart, Vector3 segmentEnd)
    {
        for (int triangleStart = 0; triangleStart < mesh.TriangleIndices.Count; triangleStart += 3)
        {
            Vector3 a = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[triangleStart]], worldTransform);
            Vector3 b = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[triangleStart + 1]], worldTransform);
            Vector3 c = Vector3.Transform(mesh.Vertices[mesh.TriangleIndices[triangleStart + 2]], worldTransform);

            if (DoesSegmentIntersectTriangle(segmentStart, segmentEnd, a, b, c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Quickly rejects triangles whose expanded bounding box cannot reach the proposed vertical stem.
    /// </summary>
    private static bool CanSkipTriangleByBounds(Vector3 segmentStart, Vector3 segmentEnd, float clearanceRadius, Vector3 a, Vector3 b, Vector3 c)
    {
        float minZ = MathF.Min(a.Z, MathF.Min(b.Z, c.Z));
        float maxZ = MathF.Max(a.Z, MathF.Max(b.Z, c.Z));
        float segmentMinZ = MathF.Min(segmentStart.Z, segmentEnd.Z);
        float segmentMaxZ = MathF.Max(segmentStart.Z, segmentEnd.Z);

        if (maxZ < segmentMinZ - clearanceRadius || minZ > segmentMaxZ + clearanceRadius)
        {
            return true;
        }

        float minX = MathF.Min(a.X, MathF.Min(b.X, c.X));
        float maxX = MathF.Max(a.X, MathF.Max(b.X, c.X));
        float minY = MathF.Min(a.Y, MathF.Min(b.Y, c.Y));
        float maxY = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
        float segmentMinX = MathF.Min(segmentStart.X, segmentEnd.X);
        float segmentMaxX = MathF.Max(segmentStart.X, segmentEnd.X);
        float segmentMinY = MathF.Min(segmentStart.Y, segmentEnd.Y);
        float segmentMaxY = MathF.Max(segmentStart.Y, segmentEnd.Y);
        float dx = DistanceBetweenRanges(segmentMinX, segmentMaxX, minX, maxX);
        float dy = DistanceBetweenRanges(segmentMinY, segmentMaxY, minY, maxY);

        return (dx * dx) + (dy * dy) > clearanceRadius * clearanceRadius;
    }

    /// <summary>
    /// Gets the distance between two closed coordinate ranges.
    /// </summary>
    private static float DistanceBetweenRanges(float firstMin, float firstMax, float secondMin, float secondMax)
    {
        if (firstMax < secondMin)
        {
            return secondMin - firstMax;
        }

        if (secondMax < firstMin)
        {
            return firstMin - secondMax;
        }

        return 0.0f;
    }

    /// <summary>
    /// Calculates the closest squared distance between a segment and a triangle.
    /// </summary>
    private static float DistanceSquaredBetweenSegmentAndTriangle(Vector3 segmentStart, Vector3 segmentEnd, Vector3 a, Vector3 b, Vector3 c)
    {
        if (DoesSegmentIntersectTriangle(segmentStart, segmentEnd, a, b, c))
        {
            return 0.0f;
        }

        float bestDistanceSquared = PointTriangleDistanceSquared(segmentStart, a, b, c);
        bestDistanceSquared = MathF.Min(bestDistanceSquared, PointTriangleDistanceSquared(segmentEnd, a, b, c));
        bestDistanceSquared = MathF.Min(bestDistanceSquared, SegmentSegmentDistanceSquared(segmentStart, segmentEnd, a, b));
        bestDistanceSquared = MathF.Min(bestDistanceSquared, SegmentSegmentDistanceSquared(segmentStart, segmentEnd, b, c));
        bestDistanceSquared = MathF.Min(bestDistanceSquared, SegmentSegmentDistanceSquared(segmentStart, segmentEnd, c, a));
        return bestDistanceSquared;
    }

    /// <summary>
    /// Tests a segment against a triangle using the Moller-Trumbore ray intersection method.
    /// </summary>
    private static bool DoesSegmentIntersectTriangle(Vector3 segmentStart, Vector3 segmentEnd, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 direction = segmentEnd - segmentStart;
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;
        Vector3 p = Vector3.Cross(direction, edge2);
        float determinant = Vector3.Dot(edge1, p);

        if (MathF.Abs(determinant) <= GeometryTolerance)
        {
            return false;
        }

        float inverseDeterminant = 1.0f / determinant;
        Vector3 t = segmentStart - a;
        float u = Vector3.Dot(t, p) * inverseDeterminant;

        if (u < 0.0f || u > 1.0f)
        {
            return false;
        }

        Vector3 q = Vector3.Cross(t, edge1);
        float v = Vector3.Dot(direction, q) * inverseDeterminant;

        if (v < 0.0f || u + v > 1.0f)
        {
            return false;
        }

        float distanceAlongSegment = Vector3.Dot(edge2, q) * inverseDeterminant;
        return distanceAlongSegment >= 0.0f && distanceAlongSegment <= 1.0f;
    }

    /// <summary>
    /// Calculates squared distance from a point to a triangle.
    /// </summary>
    private static float PointTriangleDistanceSquared(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = point - a;
        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);

        if (d1 <= 0.0f && d2 <= 0.0f)
        {
            return Vector3.DistanceSquared(point, a);
        }

        Vector3 bp = point - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);

        if (d3 >= 0.0f && d4 <= d3)
        {
            return Vector3.DistanceSquared(point, b);
        }

        float vc = (d1 * d4) - (d3 * d2);

        if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
        {
            float v = d1 / (d1 - d3);
            Vector3 projection = a + (ab * v);
            return Vector3.DistanceSquared(point, projection);
        }

        Vector3 cp = point - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);

        if (d6 >= 0.0f && d5 <= d6)
        {
            return Vector3.DistanceSquared(point, c);
        }

        float vb = (d5 * d2) - (d1 * d6);

        if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
        {
            float w = d2 / (d2 - d6);
            Vector3 projection = a + (ac * w);
            return Vector3.DistanceSquared(point, projection);
        }

        float va = (d3 * d6) - (d5 * d4);

        if (va <= 0.0f && d4 - d3 >= 0.0f && d5 - d6 >= 0.0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            Vector3 projection = b + ((c - b) * w);
            return Vector3.DistanceSquared(point, projection);
        }

        float denominator = 1.0f / (va + vb + vc);
        float vInside = vb * denominator;
        float wInside = vc * denominator;
        Vector3 closestPoint = a + (ab * vInside) + (ac * wInside);
        return Vector3.DistanceSquared(point, closestPoint);
    }

    /// <summary>
    /// Calculates squared distance between two finite line segments.
    /// </summary>
    private static float SegmentSegmentDistanceSquared(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        Vector3 d1 = q1 - p1;
        Vector3 d2 = q2 - p2;
        Vector3 r = p1 - p2;
        float a = Vector3.Dot(d1, d1);
        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);
        float s;
        float t;

        if (a <= GeometryTolerance && e <= GeometryTolerance)
        {
            return Vector3.DistanceSquared(p1, p2);
        }

        if (a <= GeometryTolerance)
        {
            s = 0.0f;
            t = Math.Clamp(f / e, 0.0f, 1.0f);
        }
        else
        {
            float c = Vector3.Dot(d1, r);

            if (e <= GeometryTolerance)
            {
                t = 0.0f;
                s = Math.Clamp(-c / a, 0.0f, 1.0f);
            }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denominator = (a * e) - (b * b);

                s = denominator <= GeometryTolerance
                    ? 0.0f
                    : Math.Clamp(((b * f) - (c * e)) / denominator, 0.0f, 1.0f);

                t = (b * s + f) / e;

                if (t < 0.0f)
                {
                    t = 0.0f;
                    s = Math.Clamp(-c / a, 0.0f, 1.0f);
                }
                else if (t > 1.0f)
                {
                    t = 1.0f;
                    s = Math.Clamp((b - c) / a, 0.0f, 1.0f);
                }
            }
        }

        Vector3 closestPoint1 = p1 + (d1 * s);
        Vector3 closestPoint2 = p2 + (d2 * t);
        return Vector3.DistanceSquared(closestPoint1, closestPoint2);
    }
}
