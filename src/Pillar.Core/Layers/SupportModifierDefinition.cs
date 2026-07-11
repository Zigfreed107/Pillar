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
    private readonly List<SupportBracePair> _excludedBracePairs;
    private readonly List<SupportModifierTargetBatch> _excludedBraceTargetBatches;

    /// <summary>
    /// Creates one support modifier definition with a stable document identity.
    /// </summary>
    public SupportModifierDefinition(
        Guid id,
        SupportModifierKind kind,
        bool isEnabled,
        int order,
        SupportClusterModifierSettings? clusterSettings,
        IReadOnlyList<Guid>? targetSupportIds,
        int? sourceGeneratorRevision)
        : this(id, kind, isEnabled, order, clusterSettings, null, null, targetSupportIds, null, sourceGeneratorRevision)
    {
    }

    /// <summary>
    /// Creates one support modifier definition with ordered target batches for cumulative edits.
    /// </summary>
    public SupportModifierDefinition(
        Guid id,
        SupportModifierKind kind,
        bool isEnabled,
        int order,
        SupportClusterModifierSettings? clusterSettings,
        IReadOnlyList<Guid>? targetSupportIds,
        IReadOnlyList<SupportModifierTargetBatch>? targetSupportIdBatches,
        int? sourceGeneratorRevision)
        : this(id, kind, isEnabled, order, clusterSettings, null, null, targetSupportIds, targetSupportIdBatches, sourceGeneratorRevision)
    {
    }

    /// <summary>
    /// Creates one support modifier definition with operation-specific settings and ordered target batches.
    /// </summary>
    public SupportModifierDefinition(
        Guid id,
        SupportModifierKind kind,
        bool isEnabled,
        int order,
        SupportClusterModifierSettings? clusterSettings,
        SupportBraceModifierSettings? braceSettings,
        SupportButtressModifierSettings? buttressSettings,
        IReadOnlyList<Guid>? targetSupportIds,
        IReadOnlyList<SupportModifierTargetBatch>? targetSupportIdBatches,
        int? sourceGeneratorRevision,
        IReadOnlyList<SupportBracePair>? excludedBracePairs = null,
        IReadOnlyList<SupportModifierTargetBatch>? excludedBraceTargetBatches = null,
        Guid? toolSessionId = null)
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
        ToolSessionId = toolSessionId ?? id;

        if (ToolSessionId == Guid.Empty)
        {
            throw new ArgumentException("A support modifier must belong to a stable tool session.", nameof(toolSessionId));
        }

        Kind = kind;
        IsEnabled = isEnabled;
        Order = order;
        ClusterSettings = clusterSettings?.Clone();
        BraceSettings = braceSettings?.Clone();
        ButtressSettings = buttressSettings?.Clone();
        SourceGeneratorRevision = sourceGeneratorRevision;
        _targetSupportIdBatches = CreateTargetSupportIdBatchList(targetSupportIds, targetSupportIdBatches);
        _targetSupportIds = CreateFlattenedTargetSupportIdList(targetSupportIds, _targetSupportIdBatches);
        _excludedBracePairs = CreateBracePairList(excludedBracePairs);
        _excludedBraceTargetBatches = CreateExcludedBraceTargetBatchList(excludedBraceTargetBatches);

        ValidateTargets();
        ValidateSettings();
    }

    /// <summary>
    /// Creates a new modifier definition with a generated identity.
    /// </summary>
    public static SupportModifierDefinition CreateNew(
        SupportModifierKind kind,
        int order,
        SupportClusterModifierSettings? clusterSettings,
        IReadOnlyList<Guid>? targetSupportIds,
        int? sourceGeneratorRevision)
    {
        return CreateNew(kind, order, clusterSettings, targetSupportIds, null, sourceGeneratorRevision);
    }

    /// <summary>
    /// Creates a new modifier definition with a generated identity and ordered target batches.
    /// </summary>
    public static SupportModifierDefinition CreateNew(
        SupportModifierKind kind,
        int order,
        SupportClusterModifierSettings? clusterSettings,
        IReadOnlyList<Guid>? targetSupportIds,
        IReadOnlyList<SupportModifierTargetBatch>? targetSupportIdBatches,
        int? sourceGeneratorRevision)
    {
        return new SupportModifierDefinition(
            Guid.NewGuid(),
            kind,
            true,
            order,
            clusterSettings,
            null,
            null,
            targetSupportIds,
            targetSupportIdBatches,
            sourceGeneratorRevision);
    }

    /// <summary>
    /// Creates a new Brace modifier definition with a generated identity.
    /// </summary>
    public static SupportModifierDefinition CreateNewBrace(
        int order,
        SupportBraceModifierSettings settings,
        IReadOnlyList<Guid> targetSupportIds,
        int sourceGeneratorRevision)
    {
        return new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.Brace,
            true,
            order,
            null,
            settings,
            null,
            targetSupportIds,
            null,
            sourceGeneratorRevision);
    }

    /// <summary>
    /// Creates a new Buttress modifier definition with a generated identity.
    /// </summary>
    public static SupportModifierDefinition CreateNewButtress(
        int order,
        SupportButtressModifierSettings settings,
        IReadOnlyList<Guid> targetSupportIds,
        int sourceGeneratorRevision)
    {
        return new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.Buttress,
            true,
            order,
            null,
            null,
            settings,
            targetSupportIds,
            null,
            sourceGeneratorRevision);
    }

    /// <summary>
    /// Gets the stable identity of this modifier stack entry.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the tool-launch session represented by this internal modifier action.
    /// </summary>
    public Guid ToolSessionId { get; }

    /// <summary>
    /// Gets the editing operation type stored by this modifier.
    /// </summary>
    public SupportModifierKind Kind { get; }

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
    /// Gets bracing settings when this modifier was created by the Brace tool.
    /// </summary>
    public SupportBraceModifierSettings? BraceSettings { get; }

    /// <summary>
    /// Gets buttressing settings when this modifier was created by the Buttress tool.
    /// </summary>
    public SupportButtressModifierSettings? ButtressSettings { get; }

    /// <summary>
    /// Gets the support identities targeted by this revision-bound modifier.
    /// </summary>
    public IReadOnlyList<Guid> TargetSupportIds
    {
        get { return _targetSupportIds; }
    }

    /// <summary>
    /// Gets the ordered target batches captured by cumulative edits.
    /// </summary>
    public IReadOnlyList<SupportModifierTargetBatch> TargetSupportIdBatches
    {
        get { return _targetSupportIdBatches; }
    }

    /// <summary>
    /// Gets pairs intentionally suppressed when a later Brace Selected operation replaces their bracing.
    /// </summary>
    public IReadOnlyList<SupportBracePair> ExcludedBracePairs
    {
        get { return _excludedBracePairs; }
    }

    /// <summary>
    /// Gets compact target batches whose internal support pairs are suppressed by later Brace Selected operations.
    /// </summary>
    public IReadOnlyList<SupportModifierTargetBatch> ExcludedBraceTargetBatches
    {
        get { return _excludedBraceTargetBatches; }
    }

    /// <summary>
    /// Gets the generator revision captured by this revision-bound modifier.
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
            return $"{kindText} ({_targetSupportIds.Count})";
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
            IsEnabled,
            Order,
            ClusterSettings,
            BraceSettings,
            ButtressSettings,
            _targetSupportIds,
            _targetSupportIdBatches,
            SourceGeneratorRevision,
            _excludedBracePairs,
            _excludedBraceTargetBatches,
            ToolSessionId);
    }

    /// <summary>
    /// Creates a defensive copy at a new ordered stack position.
    /// </summary>
    public SupportModifierDefinition CloneWithOrder(int order)
    {
        return new SupportModifierDefinition(
            Id,
            Kind,
            IsEnabled,
            order,
            ClusterSettings,
            BraceSettings,
            ButtressSettings,
            _targetSupportIds,
            _targetSupportIdBatches,
            SourceGeneratorRevision,
            _excludedBracePairs,
            _excludedBraceTargetBatches,
            ToolSessionId);
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

            case SupportModifierKind.Buttress:
                return "Buttress";

            case SupportModifierKind.Delete:
                return "Delete";

            default:
                return kind.ToString();
        }
    }

    /// <summary>
    /// Copies ordered target batches, or creates one batch for flat target data.
    /// </summary>
    private static List<SupportModifierTargetBatch> CreateTargetSupportIdBatchList(
        IReadOnlyList<Guid>? targetSupportIds,
        IReadOnlyList<SupportModifierTargetBatch>? targetSupportIdBatches)
    {
        List<SupportModifierTargetBatch> result = new List<SupportModifierTargetBatch>();

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
    /// Builds the unique target list used by labels, validation, and persistence fields.
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
    /// Copies unique unordered Brace exclusions into durable modifier ownership.
    /// </summary>
    private static List<SupportBracePair> CreateBracePairList(IReadOnlyList<SupportBracePair>? excludedBracePairs)
    {
        List<SupportBracePair> result = new List<SupportBracePair>();

        if (excludedBracePairs == null)
        {
            return result;
        }

        HashSet<SupportBracePair> seenPairs = new HashSet<SupportBracePair>();

        for (int i = 0; i < excludedBracePairs.Count; i++)
        {
            SupportBracePair pair = excludedBracePairs[i] ?? throw new ArgumentException("Brace pair exclusions cannot contain null entries.", nameof(excludedBracePairs));

            if (seenPairs.Add(pair))
            {
                result.Add(pair.Clone());
            }
        }

        result.Sort((left, right) =>
        {
            int firstCompare = left.FirstSupportId.CompareTo(right.FirstSupportId);
            return firstCompare != 0
                ? firstCompare
                : left.SecondSupportId.CompareTo(right.SecondSupportId);
        });
        return result;
    }

    /// <summary>
    /// Copies compact Brace exclusion batches into durable modifier ownership.
    /// </summary>
    private static List<SupportModifierTargetBatch> CreateExcludedBraceTargetBatchList(
        IReadOnlyList<SupportModifierTargetBatch>? excludedBraceTargetBatches)
    {
        List<SupportModifierTargetBatch> result = new List<SupportModifierTargetBatch>();

        if (excludedBraceTargetBatches == null)
        {
            return result;
        }

        for (int i = 0; i < excludedBraceTargetBatches.Count; i++)
        {
            SupportModifierTargetBatch batch = excludedBraceTargetBatches[i]
                ?? throw new ArgumentException("Brace exclusion batches cannot contain null entries.", nameof(excludedBraceTargetBatches));

            if (batch.TargetSupportIds.Count < 2)
            {
                throw new ArgumentException("Brace exclusion batches must contain at least two support ids.", nameof(excludedBraceTargetBatches));
            }

            result.Add(batch.Clone());
        }

        return result;
    }

    /// <summary>
    /// Verifies revision-bound modifier target metadata.
    /// </summary>
    private void ValidateTargets()
    {
        if (_targetSupportIds.Count == 0)
        {
            throw new ArgumentException("Support modifiers require at least one target support identity.");
        }

        if (!SourceGeneratorRevision.HasValue || SourceGeneratorRevision.Value < 0)
        {
            throw new ArgumentException("Support modifiers require a non-negative source generator revision.");
        }
        HashSet<Guid> targetIds = new HashSet<Guid>(_targetSupportIds);

        for (int i = 0; i < _excludedBracePairs.Count; i++)
        {
            SupportBracePair pair = _excludedBracePairs[i];

            if (!targetIds.Contains(pair.FirstSupportId) || !targetIds.Contains(pair.SecondSupportId))
            {
                throw new ArgumentException("Brace pair exclusions must reference supports targeted by the same modifier.");
            }
        }

        for (int batchIndex = 0; batchIndex < _excludedBraceTargetBatches.Count; batchIndex++)
        {
            IReadOnlyList<Guid> excludedTargetIds = _excludedBraceTargetBatches[batchIndex].TargetSupportIds;

            for (int targetIndex = 0; targetIndex < excludedTargetIds.Count; targetIndex++)
            {
                if (!targetIds.Contains(excludedTargetIds[targetIndex]))
                {
                    throw new ArgumentException("Brace exclusion batches must reference supports targeted by the same modifier.");
                }
            }
        }
    }

    /// <summary>
    /// Verifies operation-specific parameter payloads.
    /// </summary>
    private void ValidateSettings()
    {
        bool hasClusterSettings = ClusterSettings != null;
        bool hasBraceSettings = BraceSettings != null;
        bool hasButtressSettings = ButtressSettings != null;

        if (Kind == SupportModifierKind.Cluster && !hasClusterSettings)
        {
            throw new ArgumentException("Cluster modifiers require cluster settings.");
        }

        if (Kind == SupportModifierKind.Brace && !hasBraceSettings)
        {
            throw new ArgumentException("Brace modifiers require brace settings.");
        }

        if (Kind == SupportModifierKind.Buttress && !hasButtressSettings)
        {
            throw new ArgumentException("Buttress modifiers require buttress settings.");
        }

        if (Kind != SupportModifierKind.Cluster && hasClusterSettings)
        {
            throw new ArgumentException("Only Cluster modifiers can store cluster settings.");
        }

        if (Kind != SupportModifierKind.Brace && hasBraceSettings)
        {
            throw new ArgumentException("Only Brace modifiers can store brace settings.");
        }

        if (Kind != SupportModifierKind.Buttress && hasButtressSettings)
        {
            throw new ArgumentException("Only Buttress modifiers can store buttress settings.");
        }
        if (Kind != SupportModifierKind.Brace && (_excludedBracePairs.Count > 0 || _excludedBraceTargetBatches.Count > 0))
        {
            throw new ArgumentException("Only Brace modifiers can store excluded support pairs or target batches.");
        }
    }
}