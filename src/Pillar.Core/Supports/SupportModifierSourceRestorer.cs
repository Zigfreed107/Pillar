// SupportModifierSourceRestorer.cs
// Recovers generator output from evaluated supports so modifier stacks can be replayed, edited, or removed.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Core.Supports;

/// <summary>
/// Reverses durable Direct Edit geometry and clustered output before modifier pipeline replay.
/// </summary>
public static class SupportModifierSourceRestorer
{
    /// <summary>
    /// Restores source supports from current evaluated output and the modifier actions that produced it.
    /// </summary>
    public static IReadOnlyList<SupportEntity> Restore(
        IReadOnlyList<SupportEntity> supportEntities,
        IReadOnlyList<SupportModifierDefinition> modifiers)
    {
        if (supportEntities == null)
        {
            throw new ArgumentNullException(nameof(supportEntities));
        }

        if (modifiers == null)
        {
            throw new ArgumentNullException(nameof(modifiers));
        }

        List<SupportEntity> restoredSupports = new List<SupportEntity>(supportEntities.Count);

        for (int supportIndex = 0; supportIndex < supportEntities.Count; supportIndex++)
        {
            SupportEntity support = supportEntities[supportIndex];

            if (support.Style.Kind == SupportStyleKind.BraceMember || support.Style.Kind == SupportStyleKind.Buttress)
            {
                continue;
            }

            SupportEntity restored = ReverseDirectEdits(support, modifiers);
            restoredSupports.Add(restored.Style.Kind == SupportStyleKind.Clustered
                ? RestoreIndividualSupport(restored)
                : restored);
        }

        return restoredSupports;
    }

    /// <summary>
    /// Reverses matching Direct Edit actions from newest to oldest.
    /// </summary>
    private static SupportEntity ReverseDirectEdits(
        SupportEntity support,
        IReadOnlyList<SupportModifierDefinition> modifiers)
    {
        SupportEntity restored = support;

        for (int modifierIndex = modifiers.Count - 1; modifierIndex >= 0; modifierIndex--)
        {
            SupportModifierDefinition modifier = modifiers[modifierIndex];

            if (modifier.Kind != SupportModifierKind.DirectEdit
                || modifier.DirectEditSettings == null
                || !ContainsTarget(modifier.TargetSupportIds, support.Id))
            {
                continue;
            }

            SupportDirectEditSettings reverseSettings = new SupportDirectEditSettings(
                modifier.DirectEditSettings.OriginalBasePosition,
                modifier.DirectEditSettings.OriginalStemTopZ);
            restored = SupportDirectEditPlanner.RebuildSupport(restored, reverseSettings);
        }

        return restored;
    }

    /// <summary>
    /// Converts one clustered branch back to its original vertical individual support.
    /// </summary>
    private static SupportEntity RestoreIndividualSupport(SupportEntity support)
    {
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
        Vector3 headJointPosition = support.TipPosition - (headDirection * support.Profile.HeadHeight);
        Vector3 basePosition = new Vector3(headJointPosition.X, headJointPosition.Y, support.BasePosition.Z);
        return SupportEntity.CreateLoaded(
            support.Id,
            support.Name,
            support.SupportLayerGroupId,
            support.TipPosition,
            basePosition,
            support.HeadDirection,
            0.0f,
            Vector3.UnitZ,
            support.Profile);
    }

    /// <summary>
    /// Tests a compact modifier target list without allocating a lookup per support.
    /// </summary>
    private static bool ContainsTarget(IReadOnlyList<Guid> targetIds, Guid supportId)
    {
        for (int i = 0; i < targetIds.Count; i++)
        {
            if (targetIds[i] == supportId)
            {
                return true;
            }
        }

        return false;
    }
}
