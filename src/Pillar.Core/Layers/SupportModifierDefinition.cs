// SupportModifierDefinition.cs
// Stores one renderer-independent support editing operation in a support group's modifier stack.
using System;
using System.Collections.Generic;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes one saved operation that transforms generated support-layer output.
/// </summary>
public sealed class SupportModifierDefinition
{
    private readonly List<Guid> _targetSupportIds;
    private readonly List<SupportModifierTargetBatch> _targetSupportIdBatches;

    /// <summary>
    /// Creates one support modifier definition with a stable document identity.
    /// </summary>
    public SupportModifierDefinition(
        Guid id,
        SupportModifierKind kind,
        SupportModifierScope scope,
        bool isEnabled,
        int order,
        SupportClusterModifierSettings? clusterSettings,
        IReadOnlyList<Guid>? targetSupportIds,
        int? sourceGeneratorRevision)
        : this(id, kind, scope, isEnabled, order, clusterSettings, targetSupportIds, null, sourceGeneratorRevision)
    {
    }

    /// <summary>
    /// Creates one support modifier definition with ordered target batches for cumulative edits.
    /// </summary>
    public SupportModifierDefinition(
        Guid id,
        SupportModifierKind kind,
        SupportModifierScope scope,
        bool isEnabled,
        int order,
        SupportClusterModifierSettings? clusterSettings,
        IReadOnlyList<Guid>? targetSupportIds,
        IReadOnlyList<SupportModifierTargetBatch>? targetSupportIdBatches,
        int? sourceGeneratorRevision)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A support modifier must have a stable identifier.", nameof(id));
        }

        if (order < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Support modifier order cannot be negative.");
        }

        Id = id;
        Kind = kind;
        Scope = scope;
        IsEnabled = isEnabled;
        Order = order;
        ClusterSettings = clusterSettings?.Clone();
        SourceGeneratorRevision = sourceGeneratorRevision;
        _targetSupportIdBatches = CreateTargetSupportIdBatchList(scope, targetSupportIds, targetSupportIdBatches);
        _targetSupportIds = CreateFlattenedTargetSupportIdList(targetSupportIds, _targetSupportIdBatches);

        ValidateScope();
        ValidateSettings();
    }

    /// <summary>
    /// Creates a new modifier definition with a generated identity.
    /// </summary>
    public static SupportModifierDefinition CreateNew(
        SupportModifierKind kind,
        SupportModifierScope scope,
        int order,
        SupportClusterModifierSettings? clusterSettings,
        IReadOnlyList<Guid>? targetSupportIds,
        int? sourceGeneratorRevision)
    {
        return CreateNew(kind, scope, order, clusterSettings, targetSupportIds, null, sourceGeneratorRevision);
    }

    /// <summary>
    /// Creates a new modifier definition with a generated identity and ordered target batches.
    /// </summary>
    public static SupportModifierDefinition CreateNew(
        SupportModifierKind kind,
        SupportModifierScope scope,
        int order,
        SupportClusterModifierSettings? clusterSettings,
        IReadOnlyList<Guid>? targetSupportIds,
        IReadOnlyList<SupportModifierTargetBatch>? targetSupportIdBatches,
        int? sourceGeneratorRevision)
    {
        return new SupportModifierDefinition(
            Guid.NewGuid(),
            kind,
            scope,
            true,
            order,
            clusterSettings,
            targetSupportIds,
            targetSupportIdBatches,
            sourceGeneratorRevision);
    }

    /// <summary>
    /// Gets the stable identity of this modifier stack entry.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the editing operation type stored by this modifier.
    /// </summary>
    public SupportModifierKind Kind { get; }

    /// <summary>
    /// Gets whether this modifier targets the full layer or stored support identities.
    /// </summary>
    public SupportModifierScope Scope { get; }

    /// <summary>
    /// Gets whether this modifier participates in support-layer evaluation.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets this modifier's ordered position in its owning support group.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Gets clustering settings when this modifier was created by the Cluster Supports tool.
    /// </summary>
    public SupportClusterModifierSettings? ClusterSettings { get; }

    /// <summary>
    /// Gets the support identities targeted by a selection-scoped modifier.
    /// </summary>
    public IReadOnlyList<Guid> TargetSupportIds
    {
        get { return _targetSupportIds; }
    }

    /// <summary>
    /// Gets the ordered target batches captured by cumulative selection-scoped edits.
    /// </summary>
    public IReadOnlyList<SupportModifierTargetBatch> TargetSupportIdBatches
    {
        get { return _targetSupportIdBatches; }
    }

    /// <summary>
    /// Gets the generator revision captured by a selection-scoped modifier.
    /// </summary>
    public int? SourceGeneratorRevision { get; }

    /// <summary>
    /// Gets the user-visible layer-panel label for this modifier.
    /// </summary>
    public string DisplayName
    {
        get
        {
            string kindText = GetKindDisplayName(Kind);

            if (Scope == SupportModifierScope.WholeLayer)
            {
                return $"{kindText} - Whole Layer";
            }

            return $"{kindText} - Selection ({_targetSupportIds.Count})";
        }
    }

    /// <summary>
    /// Creates a defensive copy for document ownership and undo snapshots.
    /// </summary>
    public SupportModifierDefinition Clone()
    {
        return new SupportModifierDefinition(
            Id,
            Kind,
            Scope,
            IsEnabled,
            Order,
            ClusterSettings,
            _targetSupportIds,
            _targetSupportIdBatches,
            SourceGeneratorRevision);
    }

    /// <summary>
    /// Creates a defensive copy at a new ordered stack position.
    /// </summary>
    public SupportModifierDefinition CloneWithOrder(int order)
    {
        return new SupportModifierDefinition(
            Id,
            Kind,
            Scope,
            IsEnabled,
            order,
            ClusterSettings,
            _targetSupportIds,
            _targetSupportIdBatches,
            SourceGeneratorRevision);
    }

    /// <summary>
    /// Converts one modifier kind into the label prefix shown in the Layer Panel.
    /// </summary>
    private static string GetKindDisplayName(SupportModifierKind kind)
    {
        switch (kind)
        {
            case SupportModifierKind.Cluster:
                return "Cluster";

            case SupportModifierKind.Brace:
                return "Brace";

            case SupportModifierKind.Delete:
                return "Delete";

            default:
                return kind.ToString();
        }
    }

    /// <summary>
    /// Copies ordered selection target batches, or creates one batch for older flat target data.
    /// </summary>
    private static List<SupportModifierTargetBatch> CreateTargetSupportIdBatchList(
        SupportModifierScope scope,
        IReadOnlyList<Guid>? targetSupportIds,
        IReadOnlyList<SupportModifierTargetBatch>? targetSupportIdBatches)
    {
        List<SupportModifierTargetBatch> result = new List<SupportModifierTargetBatch>();

        if (scope == SupportModifierScope.WholeLayer)
        {
            return result;
        }

        if (targetSupportIdBatches != null && targetSupportIdBatches.Count > 0)
        {
            for (int i = 0; i < targetSupportIdBatches.Count; i++)
            {
                result.Add(targetSupportIdBatches[i].Clone());
            }

            return result;
        }

        if (targetSupportIds != null && targetSupportIds.Count > 0)
        {
            result.Add(new SupportModifierTargetBatch(targetSupportIds));
        }

        return result;
    }

    /// <summary>
    /// Builds the unique target list used by labels, validation, and older persistence fields.
    /// </summary>
    private static List<Guid> CreateFlattenedTargetSupportIdList(
        IReadOnlyList<Guid>? targetSupportIds,
        IReadOnlyList<SupportModifierTargetBatch> targetSupportIdBatches)
    {
        List<Guid> result = new List<Guid>();
        HashSet<Guid> seenIds = new HashSet<Guid>();

        if (targetSupportIds != null)
        {
            AddTargetSupportIds(targetSupportIds, result, seenIds, nameof(targetSupportIds));
        }

        for (int i = 0; i < targetSupportIdBatches.Count; i++)
        {
            AddTargetSupportIds(targetSupportIdBatches[i].TargetSupportIds, result, seenIds, nameof(targetSupportIdBatches));
        }

        return result;
    }

    /// <summary>
    /// Adds valid unique support identities to the flattened target list.
    /// </summary>
    private static void AddTargetSupportIds(IReadOnlyList<Guid> targetSupportIds, List<Guid> result, HashSet<Guid> seenIds, string parameterName)
    {
        for (int i = 0; i < targetSupportIds.Count; i++)
        {
            Guid targetSupportId = targetSupportIds[i];

            if (targetSupportId == Guid.Empty)
            {
                throw new ArgumentException("Support modifier targets cannot contain an empty support id.", parameterName);
            }

            if (seenIds.Add(targetSupportId))
            {
                result.Add(targetSupportId);
            }
        }
    }

    /// <summary>
    /// Verifies whole-layer and selection-scoped modifier metadata.
    /// </summary>
    private void ValidateScope()
    {
        if (Scope == SupportModifierScope.WholeLayer)
        {
            if (_targetSupportIds.Count > 0 || SourceGeneratorRevision.HasValue)
            {
                throw new ArgumentException("Whole-layer modifiers cannot store selection targets or a source generator revision.");
            }

            return;
        }

        if (_targetSupportIds.Count == 0)
        {
            throw new ArgumentException("Selection-scoped modifiers require at least one target support identity.");
        }

        if (!SourceGeneratorRevision.HasValue || SourceGeneratorRevision.Value < 0)
        {
            throw new ArgumentException("Selection-scoped modifiers require a non-negative source generator revision.");
        }
    }

    /// <summary>
    /// Verifies operation-specific parameter payloads.
    /// </summary>
    private void ValidateSettings()
    {
        if (Kind == SupportModifierKind.Cluster && ClusterSettings == null)
        {
            throw new ArgumentException("Cluster modifiers require cluster settings.");
        }

        if (Kind != SupportModifierKind.Cluster && ClusterSettings != null)
        {
            throw new ArgumentException("Only Cluster modifiers can store cluster settings.");
        }
    }
}
