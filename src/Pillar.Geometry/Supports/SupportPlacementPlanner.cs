// SupportPlacementPlanner.cs
// Centralizes support contact orientation and clearance validation before support entities are created.
using Pillar.Core.Entities;
using Pillar.Core.Supports;
using System;
using System.Numerics;

namespace Pillar.Geometry.Supports;

/// <summary>
/// Describes a validated support placement ready to become a renderer-agnostic support entity.
/// </summary>
public readonly struct SupportPlacementPlan
{
    /// <summary>
    /// Creates one validated support placement result.
    /// </summary>
    public SupportPlacementPlan(Vector3 basePosition, Vector3 headDirection, float branchLength, Vector3 branchDirection)
    {
        BasePosition = basePosition;
        HeadDirection = headDirection;
        BranchLength = branchLength;
        BranchDirection = branchDirection;
    }

    /// <summary>
    /// Gets the build-plane base position below the chosen stem joint.
    /// </summary>
    public Vector3 BasePosition { get; }

    /// <summary>
    /// Gets the normalized direction from the head joint toward the model contact.
    /// </summary>
    public Vector3 HeadDirection { get; }

    /// <summary>
    /// Gets the optional branch cylinder length between the stem joint and head joint.
    /// </summary>
    public float BranchLength { get; }

    /// <summary>
    /// Gets the normalized direction from the stem joint toward the head joint.
    /// </summary>
    public Vector3 BranchDirection { get; }
}

/// <summary>
/// Validates support placement against model surface direction and mesh clearance.
/// </summary>
public static class SupportPlacementPlanner
{
    /// <summary>
    /// Creates a support placement against the mesh's current world transform.
    /// </summary>
    public static bool TryCreatePlacement(
        MeshEntity mesh,
        Vector3 contactPoint,
        Vector3 surfaceNormal,
        SupportProfile profile,
        out SupportPlacementPlan placementPlan)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return TryCreatePlacement(mesh, mesh.WorldTransform, contactPoint, surfaceNormal, profile, out placementPlan);
    }

    /// <summary>
    /// Creates a support placement against an explicit mesh transform for previews and transform regeneration.
    /// </summary>
    public static bool TryCreatePlacement(
        MeshEntity mesh,
        Matrix4x4 worldTransform,
        Vector3 contactPoint,
        Vector3 surfaceNormal,
        SupportProfile profile,
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

        Vector3 headDirection;

        if (!SupportHeadDirectionCalculator.TryCreateHeadDirectionFromSurfaceNormal(surfaceNormal, profile, out headDirection))
        {
            placementPlan = default;
            return false;
        }

        SupportBranchPlan branchPlan;

        if (!SupportBranchPlanner.TryCreateBranchPlan(mesh, worldTransform, contactPoint, headDirection, profile, out branchPlan))
        {
            placementPlan = default;
            return false;
        }

        placementPlan = new SupportPlacementPlan(
            branchPlan.BasePosition,
            headDirection,
            branchPlan.BranchLength,
            branchPlan.BranchDirection);
        return true;
    }
}
