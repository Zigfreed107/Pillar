// SupportModifierTargetBatch.cs
// Stores one selection batch inside a cumulative support modifier without coupling it to UI selection state.
using System;
using System.Collections.Generic;

namespace Pillar.Core.Layers;

/// <summary>
/// Represents one Apply operation's selected support identities inside a cumulative modifier.
/// </summary>
public sealed class SupportModifierTargetBatch
{
    private readonly List<Guid> _targetSupportIds;

    /// <summary>
    /// Creates one immutable target batch from selected support identities.
    /// </summary>
    public SupportModifierTargetBatch(IReadOnlyList<Guid> targetSupportIds)
    {
        _targetSupportIds = CreateTargetSupportIdList(targetSupportIds ?? throw new ArgumentNullException(nameof(targetSupportIds)));
    }

    /// <summary>
    /// Gets the support identities captured by this Apply operation.
    /// </summary>
    public IReadOnlyList<Guid> TargetSupportIds
    {
        get { return _targetSupportIds; }
    }

    /// <summary>
    /// Creates a defensive copy for document ownership and undo snapshots.
    /// </summary>
    public SupportModifierTargetBatch Clone()
    {
        return new SupportModifierTargetBatch(_targetSupportIds);
    }

    /// <summary>
    /// Copies and validates one non-empty target support batch.
    /// </summary>
    private static List<Guid> CreateTargetSupportIdList(IReadOnlyList<Guid> targetSupportIds)
    {
        if (targetSupportIds.Count == 0)
        {
            throw new ArgumentException("Support modifier target batches cannot be empty.", nameof(targetSupportIds));
        }

        List<Guid> result = new List<Guid>(targetSupportIds.Count);
        HashSet<Guid> seenIds = new HashSet<Guid>();

        for (int i = 0; i < targetSupportIds.Count; i++)
        {
            Guid targetSupportId = targetSupportIds[i];

            if (targetSupportId == Guid.Empty)
            {
                throw new ArgumentException("Support modifier targets cannot contain an empty support id.", nameof(targetSupportIds));
            }

            if (!seenIds.Add(targetSupportId))
            {
                throw new ArgumentException("Support modifier target batches cannot contain duplicate support ids.", nameof(targetSupportIds));
            }

            result.Add(targetSupportId);
        }

        return result;
    }
}
