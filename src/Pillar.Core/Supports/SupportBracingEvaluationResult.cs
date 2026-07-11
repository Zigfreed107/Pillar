// SupportBracingEvaluationResult.cs
// Carries renderer-independent output and diagnostics for support bracing and buttressing modifiers.
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;

namespace Pillar.Core.Supports;

/// <summary>
/// Stores the evaluated support output and user-facing counts for a bracing modifier.
/// </summary>
public sealed class SupportBracingEvaluationResult
{
    /// <summary>
    /// Creates one bracing evaluation result.
    /// </summary>
    public SupportBracingEvaluationResult(
        IReadOnlyList<SupportEntity> supportEntities,
        int addedMemberCount,
        int targetSupportCount,
        int rejectedCandidateCount)
    {
        SupportEntities = supportEntities ?? throw new ArgumentNullException(nameof(supportEntities));
        AddedMemberCount = addedMemberCount;
        TargetSupportCount = targetSupportCount;
        RejectedCandidateCount = rejectedCandidateCount;
    }

    /// <summary>
    /// Gets the final support output after generated reinforcement members are appended.
    /// </summary>
    public IReadOnlyList<SupportEntity> SupportEntities { get; }

    /// <summary>
    /// Gets the number of generated brace or buttress members.
    /// </summary>
    public int AddedMemberCount { get; }

    /// <summary>
    /// Gets the number of eligible supports targeted by the modifier.
    /// </summary>
    public int TargetSupportCount { get; }

    /// <summary>
    /// Gets the number of candidate members rejected by constraints.
    /// </summary>
    public int RejectedCandidateCount { get; }
}