// SupportDirectEditPlanner.cs
// Rebuilds edited support stems and branches from durable direct-edit intent.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Core.Supports;

/// <summary>
/// Applies one direct-edit modifier while preserving model contact points and support identities.
/// </summary>
public static class SupportDirectEditPlanner
{
    private const float GeometryTolerance = 0.0001f;

    /// <summary>
    /// Rebuilds every targeted support around the edited shared stem.
    /// </summary>
    public static IReadOnlyList<SupportEntity> Evaluate(IReadOnlyList<SupportEntity> supports, SupportModifierDefinition modifier)
    {
        if (supports == null)
        {
            throw new ArgumentNullException(nameof(supports));
        }

        if (modifier == null || modifier.Kind != SupportModifierKind.DirectEdit || modifier.DirectEditSettings == null)
        {
            throw new ArgumentException("Direct edit evaluation requires Direct Edit settings.", nameof(modifier));
        }

        HashSet<Guid> targetIds = new HashSet<Guid>(modifier.TargetSupportIds);
        List<SupportEntity> result = new List<SupportEntity>(supports.Count);

        for (int i = 0; i < supports.Count; i++)
        {
            SupportEntity support = supports[i];
            result.Add(targetIds.Contains(support.Id) ? RebuildSupport(support, modifier.DirectEditSettings) : support);
        }

        return result;
    }

    /// <summary>
    /// Recreates one support with a vertical edited stem and a branch ending at the original head joint.
    /// </summary>
    public static SupportEntity RebuildSupport(SupportEntity support, SupportDirectEditSettings settings)
    {
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
        Vector3 headJoint = support.TipPosition - (headDirection * support.Profile.HeadHeight);
        Vector3 stemTop = new Vector3(settings.BasePosition.X, settings.BasePosition.Y, settings.StemTopZ);
        Vector3 branchVector = headJoint - stemTop;
        float branchLength = branchVector.Length();
        Vector3 branchDirection = branchLength > GeometryTolerance ? branchVector / branchLength : Vector3.UnitZ;

        return SupportEntity.CreateLoaded(
            support.Id,
            support.Name,
            support.SupportLayerGroupId,
            support.TipPosition,
            settings.BasePosition,
            headDirection,
            branchLength > GeometryTolerance ? branchLength : 0.0f,
            branchDirection,
            support.Profile,
            support.Style);
    }

    /// <summary>
    /// Calculates the current stem joint used to position the Z gizmo.
    /// </summary>
    public static Vector3 CalculateStemTop(SupportEntity support)
    {
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
        Vector3 headJoint = support.TipPosition - (headDirection * support.Profile.HeadHeight);
        return support.BranchLength > GeometryTolerance
            ? headJoint - (Vector3.Normalize(support.BranchDirection) * support.BranchLength)
            : headJoint;
    }
}
