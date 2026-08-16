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
    public SupportPlacementPlan(
        Vector3 basePosition,
        Vector3 headDirection,
        float branchLength,
        Vector3 branchDirection,
        SupportBaseAttachmentKind baseAttachmentKind,
        Vector3 baseDirection)
    {
        BasePosition = basePosition;
        HeadDirection = headDirection;
        BranchLength = branchLength;
        BranchDirection = branchDirection;
        BaseAttachmentKind = baseAttachmentKind;
        BaseDirection = baseDirection;
    }

    /// <summary>
    /// Gets the contact position on the build plate or model.
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

    /// <summary>
    /// Gets whether the base starts on the build plate or the model.
    /// </summary>
    public SupportBaseAttachmentKind BaseAttachmentKind { get; }

    /// <summary>
    /// Gets the direction from a model base contact toward the vertical stem.
    /// </summary>
    public Vector3 BaseDirection { get; }
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

        return TryCreatePlacement(
            mesh,
            mesh.WorldTransform,
            contactPoint,
            surfaceNormal,
            profile,
            SupportBaseGenerationMode.BuildPlateOnly,
            out placementPlan);
    }

    /// <summary>
    /// Creates a support placement using the requested base-surface preference.
    /// </summary>
    public static bool TryCreatePlacement(
        MeshEntity mesh,
        Vector3 contactPoint,
        Vector3 surfaceNormal,
        SupportProfile profile,
        SupportBaseGenerationMode baseGenerationMode,
        out SupportPlacementPlan placementPlan)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        return TryCreatePlacement(
            mesh,
            mesh.WorldTransform,
            contactPoint,
            surfaceNormal,
            profile,
            baseGenerationMode,
            out placementPlan);
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
        return TryCreatePlacement(
            mesh,
            worldTransform,
            contactPoint,
            surfaceNormal,
            profile,
            SupportBaseGenerationMode.BuildPlateOnly,
            out placementPlan);
    }

    /// <summary>
    /// Creates a support placement against an explicit transform and base-surface preference.
    /// </summary>
    public static bool TryCreatePlacement(
        MeshEntity mesh,
        Matrix4x4 worldTransform,
        Vector3 contactPoint,
        Vector3 surfaceNormal,
        SupportProfile profile,
        SupportBaseGenerationMode baseGenerationMode,
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

        if (!SupportBranchPlanner.TryCreateBranchPlan(
            mesh,
            worldTransform,
            contactPoint,
            headDirection,
            profile,
            baseGenerationMode,
            out branchPlan))
        {
            placementPlan = default;
            return false;
        }

        placementPlan = new SupportPlacementPlan(
            branchPlan.BasePosition,
            headDirection,
            branchPlan.BranchLength,
            branchPlan.BranchDirection,
            branchPlan.BaseAttachmentKind,
            branchPlan.BaseDirection);
        return true;
    }
}
