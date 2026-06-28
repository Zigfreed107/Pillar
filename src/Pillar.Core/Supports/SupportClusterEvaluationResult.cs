// SupportClusterEvaluationResult.cs
// Carries renderer-independent output and diagnostics for evaluating support Cluster modifiers.
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;

namespace Pillar.Core.Supports;

/// <summary>
/// Describes the support population produced by one support clustering evaluation.
/// </summary>
public sealed class SupportClusterEvaluationResult
{
    /// <summary>
    /// Creates one immutable cluster evaluation snapshot.
    /// </summary>
    public SupportClusterEvaluationResult(
        IReadOnlyList<SupportEntity> supportEntities,
        int clusterCount,
        int clusteredSupportCount,
        int unchangedSupportCount,
        int rejectedCandidateCount)
    {
        SupportEntities = new List<SupportEntity>(supportEntities ?? throw new ArgumentNullException(nameof(supportEntities)));
        ClusterCount = clusterCount;
        ClusteredSupportCount = clusteredSupportCount;
        UnchangedSupportCount = unchangedSupportCount;
        RejectedCandidateCount = rejectedCandidateCount;
    }

    /// <summary>
    /// Gets the final support entities after clustering is applied.
    /// </summary>
    public IReadOnlyList<SupportEntity> SupportEntities { get; }

    /// <summary>
    /// Gets the number of cluster groups created.
    /// </summary>
    public int ClusterCount { get; }

    /// <summary>
    /// Gets the number of original supports redirected into clusters.
    /// </summary>
    public int ClusteredSupportCount { get; }

    /// <summary>
    /// Gets the number of supports left as individual supports.
    /// </summary>
    public int UnchangedSupportCount { get; }

    /// <summary>
    /// Gets the number of candidate supports rejected by grouping or geometry rules.
    /// </summary>
    public int RejectedCandidateCount { get; }
}
