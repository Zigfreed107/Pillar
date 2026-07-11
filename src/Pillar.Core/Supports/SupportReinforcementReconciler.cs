// SupportReinforcementReconciler.cs
// Removes reinforcement targets that become clustered while preserving durable modifier identities and settings.
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;

namespace Pillar.Core.Supports;

/// <summary>
/// Reconciles Brace and Buttress modifiers after topology-changing cluster operations.
/// </summary>
public static class SupportReinforcementReconciler
{
    /// <summary>
    /// Removes selected targets from existing Buttress modifiers and appends their replacement with current settings.
    /// </summary>
    public static IReadOnlyList<SupportModifierDefinition> ReapplyButtressTargets(
        IReadOnlyList<SupportModifierDefinition> modifiers,
        IReadOnlyCollection<Guid> selectedTargetIds,
        SupportButtressModifierSettings settings,
        int sourceGeneratorRevision,
        Guid toolSessionId)
    {
        if (modifiers == null)
        {
            throw new ArgumentNullException(nameof(modifiers));
        }

        if (selectedTargetIds == null)
        {
            throw new ArgumentNullException(nameof(selectedTargetIds));
        }

        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (toolSessionId == Guid.Empty)
        {
            throw new ArgumentException("A tool session id is required to reapply buttressing.", nameof(toolSessionId));
        }

        if (selectedTargetIds.Count == 0)
        {
            throw new ArgumentException("At least one selected support is required to reapply buttressing.", nameof(selectedTargetIds));
        }

        HashSet<Guid> selectedIdSet = new HashSet<Guid>(selectedTargetIds);
        List<SupportModifierDefinition> reconciledModifiers = new List<SupportModifierDefinition>(modifiers.Count + 1);

        for (int i = 0; i < modifiers.Count; i++)
        {
            SupportModifierDefinition modifier = modifiers[i];

            if (modifier.Kind != SupportModifierKind.Buttress)
            {
                reconciledModifiers.Add(modifier.CloneWithOrder(reconciledModifiers.Count));
                continue;
            }

            List<Guid> remainingTargetIds = CreateRemainingTargetIds(modifier.TargetSupportIds, selectedIdSet);

            if (remainingTargetIds.Count == 0)
            {
                continue;
            }

            reconciledModifiers.Add(new SupportModifierDefinition(
                modifier.Id,
                modifier.Kind,
                modifier.IsEnabled,
                reconciledModifiers.Count,
                null,
                null,
                modifier.ButtressSettings,
                remainingTargetIds,
                null,
                sourceGeneratorRevision,
                null,
                null,
                modifier.ToolSessionId));
        }

        List<Guid> replacementTargetIds = new List<Guid>(selectedIdSet);
        replacementTargetIds.Sort();
        reconciledModifiers.Add(new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.Buttress,
            true,
            reconciledModifiers.Count,
            null,
            null,
            settings,
            replacementTargetIds,
            null,
            sourceGeneratorRevision,
            null,
            null,
            toolSessionId));
        return reconciledModifiers;
    }
    /// <summary>
    /// Removes clustered support identities from reinforcement modifiers and drops entries that no longer have enough targets.
    /// </summary>
    public static SupportReinforcementReconciliationResult RemoveClusteredTargets(
        IReadOnlyList<SupportModifierDefinition> modifiers,
        IReadOnlyCollection<Guid> clusteredSupportIds)
    {
        if (modifiers == null)
        {
            throw new ArgumentNullException(nameof(modifiers));
        }

        if (clusteredSupportIds == null)
        {
            throw new ArgumentNullException(nameof(clusteredSupportIds));
        }

        HashSet<Guid> clusteredIdSet = new HashSet<Guid>(clusteredSupportIds);
        List<SupportModifierDefinition> reconciledModifiers = new List<SupportModifierDefinition>(modifiers.Count);
        int removedTargetCount = 0;
        int removedModifierCount = 0;
        int affectedModifierCount = 0;

        for (int i = 0; i < modifiers.Count; i++)
        {
            SupportModifierDefinition modifier = modifiers[i];

            if (modifier.Kind != SupportModifierKind.Brace && modifier.Kind != SupportModifierKind.Buttress)
            {
                reconciledModifiers.Add(modifier.CloneWithOrder(reconciledModifiers.Count));
                continue;
            }

            List<Guid> remainingTargetIds = CreateRemainingTargetIds(modifier.TargetSupportIds, clusteredIdSet);
            int removedFromModifier = modifier.TargetSupportIds.Count - remainingTargetIds.Count;

            if (removedFromModifier == 0)
            {
                reconciledModifiers.Add(modifier.CloneWithOrder(reconciledModifiers.Count));
                continue;
            }

            affectedModifierCount++;
            removedTargetCount += removedFromModifier;
            int minimumTargetCount = modifier.Kind == SupportModifierKind.Brace ? 2 : 1;

            if (remainingTargetIds.Count < minimumTargetCount)
            {
                removedModifierCount++;
                continue;
            }

            reconciledModifiers.Add(new SupportModifierDefinition(
                modifier.Id,
                modifier.Kind,
                modifier.IsEnabled,
                reconciledModifiers.Count,
                null,
                modifier.BraceSettings,
                modifier.ButtressSettings,
                remainingTargetIds,
                null,
                modifier.SourceGeneratorRevision,
                CreateRemainingBracePairExclusions(modifier.ExcludedBracePairs, remainingTargetIds),
                CreateRemainingBraceExclusionBatches(modifier.ExcludedBraceTargetBatches, remainingTargetIds),
                modifier.ToolSessionId));
        }

        return new SupportReinforcementReconciliationResult(
            reconciledModifiers,
            removedTargetCount,
            removedModifierCount,
            affectedModifierCount);
    }

    /// <summary>
    /// Builds the stable remaining target list after excluding clustered identities.
    /// </summary>
    private static List<Guid> CreateRemainingTargetIds(
        IReadOnlyList<Guid> targetSupportIds,
        HashSet<Guid> clusteredSupportIds)
    {
        List<Guid> remainingTargetIds = new List<Guid>(targetSupportIds.Count);

        for (int i = 0; i < targetSupportIds.Count; i++)
        {
            Guid targetSupportId = targetSupportIds[i];

            if (!clusteredSupportIds.Contains(targetSupportId))
            {
                remainingTargetIds.Add(targetSupportId);
            }
        }

        return remainingTargetIds;
    }

    /// <summary>
    /// Removes pair exclusions whose endpoints no longer belong to the reduced target set.
    /// </summary>
    private static List<SupportBracePair> CreateRemainingBracePairExclusions(
        IReadOnlyList<SupportBracePair> exclusions,
        IReadOnlyList<Guid> remainingTargetIds)
    {
        HashSet<Guid> remainingIds = new HashSet<Guid>(remainingTargetIds);
        List<SupportBracePair> result = new List<SupportBracePair>();

        for (int i = 0; i < exclusions.Count; i++)
        {
            SupportBracePair pair = exclusions[i];

            if (remainingIds.Contains(pair.FirstSupportId) && remainingIds.Contains(pair.SecondSupportId))
            {
                result.Add(pair.Clone());
            }
        }

        return result;
    }

    /// <summary>
    /// Removes target ids from compact exclusion batches and drops batches with fewer than two survivors.
    /// </summary>
    private static List<SupportModifierTargetBatch> CreateRemainingBraceExclusionBatches(
        IReadOnlyList<SupportModifierTargetBatch> exclusionBatches,
        IReadOnlyList<Guid> remainingTargetIds)
    {
        HashSet<Guid> remainingIds = new HashSet<Guid>(remainingTargetIds);
        List<SupportModifierTargetBatch> result = new List<SupportModifierTargetBatch>();

        for (int batchIndex = 0; batchIndex < exclusionBatches.Count; batchIndex++)
        {
            IReadOnlyList<Guid> excludedIds = exclusionBatches[batchIndex].TargetSupportIds;
            List<Guid> survivingExcludedIds = new List<Guid>(excludedIds.Count);

            for (int targetIndex = 0; targetIndex < excludedIds.Count; targetIndex++)
            {
                if (remainingIds.Contains(excludedIds[targetIndex]))
                {
                    survivingExcludedIds.Add(excludedIds[targetIndex]);
                }
            }

            if (survivingExcludedIds.Count >= 2)
            {
                result.Add(new SupportModifierTargetBatch(survivingExcludedIds));
            }
        }

        return result;
    }
}

/// <summary>
/// Describes a reinforcement-target reconciliation without depending on UI or command types.
/// </summary>
public sealed class SupportReinforcementReconciliationResult
{
    /// <summary>
    /// Creates one immutable reconciliation summary.
    /// </summary>
    public SupportReinforcementReconciliationResult(
        IReadOnlyList<SupportModifierDefinition> modifiers,
        int removedTargetCount,
        int removedModifierCount,
        int affectedModifierCount)
    {
        Modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
        RemovedTargetCount = removedTargetCount;
        RemovedModifierCount = removedModifierCount;
        AffectedModifierCount = affectedModifierCount;
    }

    /// <summary>
    /// Gets the complete surviving modifier stack in normalized order.
    /// </summary>
    public IReadOnlyList<SupportModifierDefinition> Modifiers { get; }

    /// <summary>
    /// Gets the number of clustered target identities removed from reinforcement modifiers.
    /// </summary>
    public int RemovedTargetCount { get; }

    /// <summary>
    /// Gets the number of complete reinforcement modifiers removed after target pruning.
    /// </summary>
    public int RemovedModifierCount { get; }

    /// <summary>
    /// Gets the number of reinforcement modifiers whose target lists changed.
    /// </summary>
    public int AffectedModifierCount { get; }

    /// <summary>
    /// Gets whether reconciliation changed any reinforcement target list.
    /// </summary>
    public bool HasChanges
    {
        get { return RemovedTargetCount > 0; }
    }
}