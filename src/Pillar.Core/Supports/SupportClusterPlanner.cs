// SupportClusterPlanner.cs
// Plans deterministic support clusters from renderer-independent support entities and Cluster modifier settings.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Core.Supports;

/// <summary>
/// Builds clustered support output from individual support entities.
/// </summary>
public static class SupportClusterPlanner
{
    private const float AxialTolerance = 0.0001f;
    private const float ClusterCenterToleranceSquared = 0.000001f;

    /// <summary>
    /// Evaluates one Cluster modifier against the supplied support population.
    /// </summary>
    public static SupportClusterEvaluationResult Evaluate(
        IReadOnlyList<SupportEntity> sourceSupports,
        SupportModifierDefinition modifier)
    {
        if (sourceSupports == null)
        {
            throw new ArgumentNullException(nameof(sourceSupports));
        }

        if (modifier == null)
        {
            throw new ArgumentNullException(nameof(modifier));
        }

        if (modifier.Kind != SupportModifierKind.Cluster || modifier.ClusterSettings == null)
        {
            throw new ArgumentException("Only Cluster modifiers can be evaluated by the cluster planner.", nameof(modifier));
        }

        if (modifier.Scope == SupportModifierScope.Selection && modifier.TargetSupportIdBatches.Count > 1)
        {
            return EvaluateSelectionBatches(sourceSupports, modifier);
        }

        if (modifier.Scope == SupportModifierScope.Selection && HasTargetedClusteredSupport(sourceSupports, modifier.TargetSupportIds))
        {
            return EvaluateSelectionWithClusterTargets(sourceSupports, modifier);
        }

        SupportClusterModifierSettings settings = modifier.ClusterSettings;
        List<CandidateSupport> candidates = CreateEligibleCandidates(sourceSupports, modifier);
        Dictionary<Guid, SupportEntity> replacementsById = new Dictionary<Guid, SupportEntity>();
        ClusterPlanSummary summary = PlanCandidateGroups(candidates, settings, replacementsById);
        return CreateEvaluationResult(sourceSupports, replacementsById, summary.ClusterCount, summary.ClusteredSupportCount, summary.RejectedCandidateCount);
    }

    /// <summary>
    /// Replays cumulative selection batches independently while preserving one modifier stack entry.
    /// </summary>
    private static SupportClusterEvaluationResult EvaluateSelectionBatches(
        IReadOnlyList<SupportEntity> sourceSupports,
        SupportModifierDefinition modifier)
    {
        List<SupportEntity> currentSupports = new List<SupportEntity>(sourceSupports);
        int clusterCount = 0;
        int rejectedCandidateCount = 0;

        for (int i = 0; i < modifier.TargetSupportIdBatches.Count; i++)
        {
            SupportModifierTargetBatch batch = modifier.TargetSupportIdBatches[i];
            SupportModifierDefinition batchModifier = new SupportModifierDefinition(
                modifier.Id,
                modifier.Kind,
                modifier.Scope,
                modifier.IsEnabled,
                modifier.Order,
                modifier.ClusterSettings,
                batch.TargetSupportIds,
                modifier.SourceGeneratorRevision);
            SupportClusterEvaluationResult batchResult = Evaluate(currentSupports, batchModifier);
            currentSupports = new List<SupportEntity>(batchResult.SupportEntities);
            clusterCount += batchResult.ClusterCount;
            rejectedCandidateCount += batchResult.RejectedCandidateCount;
        }

        int clusteredSupportCount = CountClusteredSupports(currentSupports);
        return new SupportClusterEvaluationResult(
            currentSupports,
            clusterCount,
            clusteredSupportCount,
            currentSupports.Count - clusteredSupportCount,
            rejectedCandidateCount);
    }

    /// <summary>
    /// Evaluates a selection that includes existing clustered supports before clustering remaining selected individuals.
    /// </summary>
    private static SupportClusterEvaluationResult EvaluateSelectionWithClusterTargets(
        IReadOnlyList<SupportEntity> sourceSupports,
        SupportModifierDefinition modifier)
    {
        SupportClusterModifierSettings settings = modifier.ClusterSettings!;
        HashSet<Guid> targetIds = new HashSet<Guid>(modifier.TargetSupportIds);
        List<ClusterCandidateGroup> selectedClusters = CreateSelectedClusterGroups(sourceSupports, targetIds);
        List<CandidateSupport> individualCandidates = CreateSelectionIndividualCandidates(sourceSupports, targetIds);
        Dictionary<Guid, SupportEntity> replacementsById = new Dictionary<Guid, SupportEntity>();
        HashSet<Guid> mergedIndividualIds = new HashSet<Guid>();
        int clusterCount = 0;
        int clusteredSupportCount = 0;

        selectedClusters.Sort(CompareClusterGroupIdentity);
        individualCandidates.Sort(CompareCandidateIdentity);

        for (int i = 0; i < individualCandidates.Count; i++)
        {
            CandidateSupport individual = individualCandidates[i];

            if (TryFindNearestFeasibleCluster(selectedClusters, individual, settings, out ClusterCandidateGroup? selectedCluster) && selectedCluster != null)
            {
                selectedCluster.AddMember(individual);
                mergedIndividualIds.Add(individual.Support.Id);
            }
        }

        for (int i = 0; i < selectedClusters.Count; i++)
        {
            ClusterCandidateGroup selectedCluster = selectedClusters[i];

            if (!selectedCluster.HasAddedMembers)
            {
                continue;
            }

            if (!TryCreateClusterPlan(selectedCluster.Members, settings, out ClusterPlan plan))
            {
                continue;
            }

            ApplyClusterPlan(selectedCluster.Members, plan, replacementsById);
            clusterCount++;
            clusteredSupportCount += selectedCluster.Members.Count;
        }

        List<CandidateSupport> remainingCandidates = new List<CandidateSupport>();

        for (int i = 0; i < individualCandidates.Count; i++)
        {
            CandidateSupport individual = individualCandidates[i];

            if (!mergedIndividualIds.Contains(individual.Support.Id))
            {
                remainingCandidates.Add(individual);
            }
        }

        ClusterPlanSummary remainingSummary = PlanCandidateGroups(remainingCandidates, settings, replacementsById);
        clusterCount += remainingSummary.ClusterCount;
        clusteredSupportCount += remainingSummary.ClusteredSupportCount;

        return CreateEvaluationResult(
            sourceSupports,
            replacementsById,
            clusterCount,
            clusteredSupportCount,
            remainingSummary.RejectedCandidateCount);
    }

    /// <summary>
    /// Plans normal clusters from the supplied individual candidates and appends replacements to the caller-owned map.
    /// </summary>
    private static ClusterPlanSummary PlanCandidateGroups(
        List<CandidateSupport> candidates,
        SupportClusterModifierSettings settings,
        Dictionary<Guid, SupportEntity> replacementsById)
    {
        HashSet<Guid> assignedSupportIds = new HashSet<Guid>();
        SpatialCandidateGrid grid = new SpatialCandidateGrid(candidates, settings.MaximumClusterRadius);
        int clusterCount = 0;
        int clusteredSupportCount = 0;
        int rejectedCandidateCount = 0;

        while (TryFindSeed(candidates, grid, assignedSupportIds, settings.MaximumClusterRadius, out CandidateSupport seed))
        {
            List<CandidateSupport> group = new List<CandidateSupport> { seed };
            List<CandidateSupport> neighbors = grid.FindNeighbors(seed.HeadJointPosition, settings.MaximumClusterRadius);
            SortCandidatesByDistanceThenId(neighbors, seed.HeadJointPosition);

            for (int i = 0; i < neighbors.Count && group.Count < settings.MaximumSupportsPerCluster; i++)
            {
                CandidateSupport neighbor = neighbors[i];

                if (neighbor.Support.Id == seed.Support.Id
                    || assignedSupportIds.Contains(neighbor.Support.Id)
                    || ContainsCandidate(group, neighbor.Support.Id))
                {
                    continue;
                }

                group.Add(neighbor);

                if (!TryCreateClusterPlan(group, settings, out ClusterPlan _))
                {
                    group.RemoveAt(group.Count - 1);
                }
            }

            if (group.Count >= settings.MinimumSupportsPerCluster
                && TryCreateClusterPlan(group, settings, out ClusterPlan plan))
            {
                ApplyClusterPlan(group, plan, replacementsById);

                for (int i = 0; i < group.Count; i++)
                {
                    assignedSupportIds.Add(group[i].Support.Id);
                }

                clusterCount++;
                clusteredSupportCount += group.Count;
                continue;
            }

            assignedSupportIds.Add(seed.Support.Id);
            rejectedCandidateCount++;
        }

        return new ClusterPlanSummary(clusterCount, clusteredSupportCount, rejectedCandidateCount);
    }

    /// <summary>
    /// Creates the final ordered result from source supports and planned replacements.
    /// </summary>
    private static SupportClusterEvaluationResult CreateEvaluationResult(
        IReadOnlyList<SupportEntity> sourceSupports,
        Dictionary<Guid, SupportEntity> replacementsById,
        int clusterCount,
        int clusteredSupportCount,
        int rejectedCandidateCount)
    {
        List<SupportEntity> resultSupports = new List<SupportEntity>(sourceSupports.Count);

        for (int i = 0; i < sourceSupports.Count; i++)
        {
            SupportEntity sourceSupport = sourceSupports[i];

            if (replacementsById.TryGetValue(sourceSupport.Id, out SupportEntity? replacement))
            {
                resultSupports.Add(replacement);
            }
            else
            {
                resultSupports.Add(sourceSupport);
            }
        }

        return new SupportClusterEvaluationResult(
            resultSupports,
            clusterCount,
            clusteredSupportCount,
            resultSupports.Count - clusteredSupportCount,
            rejectedCandidateCount);
    }

    /// <summary>
    /// Creates eligible clustering candidates from individual supports in the requested scope.
    /// </summary>
    private static List<CandidateSupport> CreateEligibleCandidates(
        IReadOnlyList<SupportEntity> sourceSupports,
        SupportModifierDefinition modifier)
    {
        HashSet<Guid>? targetIds = modifier.Scope == SupportModifierScope.Selection
            ? new HashSet<Guid>(modifier.TargetSupportIds)
            : null;
        List<CandidateSupport> candidates = new List<CandidateSupport>();

        for (int i = 0; i < sourceSupports.Count; i++)
        {
            SupportEntity support = sourceSupports[i];

            if (targetIds != null && !targetIds.Contains(support.Id))
            {
                continue;
            }

            if (support.Style.Kind != SupportStyleKind.Individual || !TryCreateCandidate(support, out CandidateSupport candidate))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        candidates.Sort(CompareCandidateIdentity);
        return candidates;
    }

    /// <summary>
    /// Creates individual candidates explicitly selected by a selection-scoped modifier.
    /// </summary>
    private static List<CandidateSupport> CreateSelectionIndividualCandidates(
        IReadOnlyList<SupportEntity> sourceSupports,
        HashSet<Guid> targetIds)
    {
        List<CandidateSupport> candidates = new List<CandidateSupport>();

        for (int i = 0; i < sourceSupports.Count; i++)
        {
            SupportEntity support = sourceSupports[i];

            if (!targetIds.Contains(support.Id)
                || support.Style.Kind != SupportStyleKind.Individual
                || !TryCreateCandidate(support, out CandidateSupport candidate))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        return candidates;
    }

    /// <summary>
    /// Builds selected existing cluster groups, expanding any selected cluster member to the full shared-stem group.
    /// </summary>
    private static List<ClusterCandidateGroup> CreateSelectedClusterGroups(
        IReadOnlyList<SupportEntity> sourceSupports,
        HashSet<Guid> targetIds)
    {
        List<Vector2> selectedCenters = new List<Vector2>();

        for (int i = 0; i < sourceSupports.Count; i++)
        {
            SupportEntity support = sourceSupports[i];

            if (targetIds.Contains(support.Id) && support.Style.Kind == SupportStyleKind.Clustered)
            {
                AddClusterCenterIfMissing(selectedCenters, new Vector2(support.BasePosition.X, support.BasePosition.Y));
            }
        }

        List<ClusterCandidateGroup> groups = new List<ClusterCandidateGroup>();

        for (int centerIndex = 0; centerIndex < selectedCenters.Count; centerIndex++)
        {
            Vector2 selectedCenter = selectedCenters[centerIndex];
            ClusterCandidateGroup group = new ClusterCandidateGroup(selectedCenter);

            for (int supportIndex = 0; supportIndex < sourceSupports.Count; supportIndex++)
            {
                SupportEntity support = sourceSupports[supportIndex];

                if (support.Style.Kind != SupportStyleKind.Clustered
                    || !AreClusterCentersEqual(selectedCenter, new Vector2(support.BasePosition.X, support.BasePosition.Y))
                    || !TryCreateCandidate(support, out CandidateSupport candidate))
                {
                    continue;
                }

                group.AddExistingMember(candidate);
            }

            if (group.Members.Count > 0)
            {
                groups.Add(group);
            }
        }

        return groups;
    }

    /// <summary>
    /// Finds the nearest selected cluster that can absorb the individual support while preserving a valid cluster plan.
    /// </summary>
    private static bool TryFindNearestFeasibleCluster(
        List<ClusterCandidateGroup> selectedClusters,
        CandidateSupport individual,
        SupportClusterModifierSettings settings,
        out ClusterCandidateGroup? selectedCluster)
    {
        selectedCluster = null;
        float bestDistanceSquared = float.MaxValue;

        for (int i = 0; i < selectedClusters.Count; i++)
        {
            ClusterCandidateGroup candidateCluster = selectedClusters[i];

            if (candidateCluster.Members.Count >= settings.MaximumSupportsPerCluster)
            {
                continue;
            }

            List<CandidateSupport> trialMembers = new List<CandidateSupport>(candidateCluster.Members.Count + 1);
            trialMembers.AddRange(candidateCluster.Members);
            trialMembers.Add(individual);

            if (!TryCreateClusterPlan(trialMembers, settings, out ClusterPlan _))
            {
                continue;
            }

            float distanceSquared = Vector2.DistanceSquared(
                new Vector2(individual.HeadJointPosition.X, individual.HeadJointPosition.Y),
                candidateCluster.Center);

            if (selectedCluster == null
                || distanceSquared < bestDistanceSquared
                || (MathF.Abs(distanceSquared - bestDistanceSquared) <= AxialTolerance && CompareClusterGroupIdentity(candidateCluster, selectedCluster) < 0))
            {
                selectedCluster = candidateCluster;
                bestDistanceSquared = distanceSquared;
            }
        }

        return selectedCluster != null;
    }

    /// <summary>
    /// Converts one support into the geometry fields used by cluster planning.
    /// </summary>
    private static bool TryCreateCandidate(SupportEntity support, out CandidateSupport candidate)
    {
        candidate = default;

        if (!IsFinite(support.TipPosition)
            || !IsFinite(support.BasePosition)
            || !IsFinite(support.HeadDirection))
        {
            return false;
        }

        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
        Vector3 headJointPosition = support.TipPosition - (headDirection * support.Profile.HeadHeight);

        if (!IsFinite(headJointPosition) || headJointPosition.Z <= support.BasePosition.Z + AxialTolerance)
        {
            return false;
        }

        candidate = new CandidateSupport(support, headJointPosition);
        return true;
    }

    /// <summary>
    /// Selects the unassigned support with the most nearby eligible neighbors.
    /// </summary>
    private static bool TryFindSeed(
        List<CandidateSupport> candidates,
        SpatialCandidateGrid grid,
        HashSet<Guid> assignedSupportIds,
        float radius,
        out CandidateSupport seed)
    {
        seed = default;
        int bestNeighborCount = -1;

        for (int i = 0; i < candidates.Count; i++)
        {
            CandidateSupport candidate = candidates[i];

            if (assignedSupportIds.Contains(candidate.Support.Id))
            {
                continue;
            }

            int neighborCount = 0;
            List<CandidateSupport> neighbors = grid.FindNeighbors(candidate.HeadJointPosition, radius);

            for (int neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
            {
                if (!assignedSupportIds.Contains(neighbors[neighborIndex].Support.Id))
                {
                    neighborCount++;
                }
            }

            if (neighborCount > bestNeighborCount
                || (neighborCount == bestNeighborCount && CompareCandidateIdentity(candidate, seed) < 0))
            {
                seed = candidate;
                bestNeighborCount = neighborCount;
            }
        }

        return bestNeighborCount >= 0;
    }

    /// <summary>
    /// Calculates central stem, junction height, and stem sizing for one candidate group.
    /// </summary>
    private static bool TryCreateClusterPlan(
        List<CandidateSupport> group,
        SupportClusterModifierSettings settings,
        out ClusterPlan plan)
    {
        plan = default;

        if (group.Count == 0)
        {
            return false;
        }

        Vector2 center = CalculateCentroid(group);
        float maximumRadiusSquared = settings.MaximumClusterRadius * settings.MaximumClusterRadius;
        float maximumBaseZ = float.MinValue;
        float junctionZ = float.MaxValue;
        float angleRadians = settings.MaximumBranchAngleFromVerticalDegrees * (MathF.PI / 180.0f);
        float tangent = MathF.Tan(angleRadians);

        if (tangent <= AxialTolerance)
        {
            return false;
        }

        for (int i = 0; i < group.Count; i++)
        {
            CandidateSupport member = group[i];
            Vector2 head = new Vector2(member.HeadJointPosition.X, member.HeadJointPosition.Y);
            float radiusSquared = Vector2.DistanceSquared(center, head);

            if (radiusSquared > maximumRadiusSquared)
            {
                return false;
            }

            float radius = MathF.Sqrt(radiusSquared);
            float memberJunctionZ = member.HeadJointPosition.Z - (radius / tangent);
            junctionZ = MathF.Min(junctionZ, memberJunctionZ);
            maximumBaseZ = MathF.Max(maximumBaseZ, member.Support.BasePosition.Z + AxialTolerance);
        }

        if (junctionZ <= maximumBaseZ)
        {
            return false;
        }

        float centralStemBottomDiameter = CalculateCentralStemDiameter(group, true, settings);
        float centralStemTopDiameter = CalculateCentralStemDiameter(group, false, settings);
        plan = new ClusterPlan(center, junctionZ, centralStemBottomDiameter, centralStemTopDiameter, settings.ClusterBranchDiameter);
        return true;
    }

    /// <summary>
    /// Adds all member replacements for one valid cluster plan.
    /// </summary>
    private static void ApplyClusterPlan(
        List<CandidateSupport> group,
        ClusterPlan plan,
        Dictionary<Guid, SupportEntity> replacementsById)
    {
        for (int i = 0; i < group.Count; i++)
        {
            CandidateSupport member = group[i];
            replacementsById[member.Support.Id] = CreateClusteredSupport(member, plan);
        }
    }

    /// <summary>
    /// Creates a branched support that preserves the original contact and redirects the stem to the cluster axis.
    /// </summary>
    private static SupportEntity CreateClusteredSupport(CandidateSupport member, ClusterPlan plan)
    {
        Vector3 stemJointPosition = new Vector3(plan.Center.X, plan.Center.Y, plan.JunctionZ);
        Vector3 branchVector = member.HeadJointPosition - stemJointPosition;
        float branchLength = branchVector.Length();
        Vector3 branchDirection = branchLength > AxialTolerance
            ? branchVector / branchLength
            : Vector3.UnitZ;
        Vector3 basePosition = new Vector3(plan.Center.X, plan.Center.Y, member.Support.BasePosition.Z);
        SupportProfile profile = member.Support.Profile;

        return SupportEntity.CreateLoaded(
            member.Support.Id,
            member.Support.Name,
            member.Support.SupportLayerGroupId,
            member.Support.TipPosition,
            basePosition,
            member.Support.HeadDirection,
            branchLength,
            branchDirection,
            profile,
            new ClusteredSupportStyle(plan.CentralStemBottomDiameter, plan.CentralStemTopDiameter, plan.ClusterBranchDiameter));
    }


    /// <summary>
    /// Calculates automatic stem diameter from combined source cross-sectional area, or uses validated manual sizing.
    /// </summary>
    private static float CalculateCentralStemDiameter(
        List<CandidateSupport> group,
        bool useBottomDiameter,
        SupportClusterModifierSettings settings)
    {
        if (settings.StemSizingMode == SupportClusterStemSizingMode.Manual)
        {
            return useBottomDiameter
                ? settings.ManualCentralStemBottomDiameter
                : settings.ManualCentralStemTopDiameter;
        }

        float sumDiameterSquared = 0.0f;

        for (int i = 0; i < group.Count; i++)
        {
            float diameter = useBottomDiameter
                ? group[i].Support.Profile.StemBottomDiameter
                : group[i].Support.Profile.StemTopDiameter;
            sumDiameterSquared += diameter * diameter;
        }

        float calculatedDiameter = MathF.Sqrt(sumDiameterSquared);
        return Math.Clamp(
            calculatedDiameter,
            SupportClusterModifierSettings.MinimumCentralStemDiameter,
            SupportClusterModifierSettings.MaximumCentralStemDiameter);
    }

    /// <summary>
    /// Calculates a stable unweighted XY centroid for one candidate group.
    /// </summary>
    private static Vector2 CalculateCentroid(List<CandidateSupport> group)
    {
        Vector2 sum = Vector2.Zero;

        for (int i = 0; i < group.Count; i++)
        {
            sum += new Vector2(group[i].HeadJointPosition.X, group[i].HeadJointPosition.Y);
        }

        return sum / group.Count;
    }

    /// <summary>
    /// Sorts candidates by XY distance from a point and then by identity for deterministic output.
    /// </summary>
    private static void SortCandidatesByDistanceThenId(List<CandidateSupport> candidates, Vector3 origin)
    {
        candidates.Sort((CandidateSupport left, CandidateSupport right) =>
        {
            float leftDistance = Vector2.DistanceSquared(
                new Vector2(left.HeadJointPosition.X, left.HeadJointPosition.Y),
                new Vector2(origin.X, origin.Y));
            float rightDistance = Vector2.DistanceSquared(
                new Vector2(right.HeadJointPosition.X, right.HeadJointPosition.Y),
                new Vector2(origin.X, origin.Y));
            int distanceComparison = leftDistance.CompareTo(rightDistance);

            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            return CompareCandidateIdentity(left, right);
        });
    }

    /// <summary>
    /// Checks whether a candidate list already contains one support identity.
    /// </summary>
    private static bool ContainsCandidate(List<CandidateSupport> candidates, Guid supportId)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Support.Id == supportId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts final clustered supports for cumulative-batch diagnostics.
    /// </summary>
    private static int CountClusteredSupports(IReadOnlyList<SupportEntity> supportEntities)
    {
        int count = 0;

        for (int i = 0; i < supportEntities.Count; i++)
        {
            if (supportEntities[i].Style.Kind == SupportStyleKind.Clustered)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Checks whether target ids include at least one currently clustered support.
    /// </summary>
    private static bool HasTargetedClusteredSupport(IReadOnlyList<SupportEntity> sourceSupports, IReadOnlyList<Guid> targetSupportIds)
    {
        HashSet<Guid> targetIds = new HashSet<Guid>(targetSupportIds);

        for (int i = 0; i < sourceSupports.Count; i++)
        {
            if (targetIds.Contains(sourceSupports[i].Id) && sourceSupports[i].Style.Kind == SupportStyleKind.Clustered)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Adds a selected shared-stem center if it has not already been recorded.
    /// </summary>
    private static void AddClusterCenterIfMissing(List<Vector2> centers, Vector2 center)
    {
        for (int i = 0; i < centers.Count; i++)
        {
            if (AreClusterCentersEqual(centers[i], center))
            {
                return;
            }
        }

        centers.Add(center);
    }

    /// <summary>
    /// Checks whether two shared-stem centers represent the same cluster.
    /// </summary>
    private static bool AreClusterCentersEqual(Vector2 left, Vector2 right)
    {
        return Vector2.DistanceSquared(left, right) <= ClusterCenterToleranceSquared;
    }

    /// <summary>
    /// Orders candidates by support identity.
    /// </summary>
    private static int CompareCandidateIdentity(CandidateSupport left, CandidateSupport right)
    {
        return left.Support.Id.CompareTo(right.Support.Id);
    }

    /// <summary>
    /// Orders existing selected cluster groups by representative support identity.
    /// </summary>
    private static int CompareClusterGroupIdentity(ClusterCandidateGroup left, ClusterCandidateGroup right)
    {
        return left.RepresentativeSupportId.CompareTo(right.RepresentativeSupportId);
    }

    /// <summary>
    /// Checks whether a vector contains only finite coordinates.
    /// </summary>
    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    /// <summary>
    /// Stores one support and its redirectable head-joint position.
    /// </summary>
    private readonly struct CandidateSupport
    {
        /// <summary>
        /// Creates one candidate support record.
        /// </summary>
        public CandidateSupport(SupportEntity support, Vector3 headJointPosition)
        {
            Support = support;
            HeadJointPosition = headJointPosition;
        }

        /// <summary>
        /// Gets the source support entity.
        /// </summary>
        public SupportEntity Support { get; }

        /// <summary>
        /// Gets the preserved head joint that branches connect to.
        /// </summary>
        public Vector3 HeadJointPosition { get; }
    }

    /// <summary>
    /// Stores one selected existing cluster and any individual members merged into it.
    /// </summary>
    private sealed class ClusterCandidateGroup
    {
        /// <summary>
        /// Creates one selected cluster group for a shared-stem center.
        /// </summary>
        public ClusterCandidateGroup(Vector2 center)
        {
            Center = center;
            Members = new List<CandidateSupport>();
            RepresentativeSupportId = Guid.Empty;
        }

        /// <summary>
        /// Gets the original shared-stem XY center used for nearest-cluster assignment.
        /// </summary>
        public Vector2 Center { get; }

        /// <summary>
        /// Gets the current group members used for replanning.
        /// </summary>
        public List<CandidateSupport> Members { get; }

        /// <summary>
        /// Gets the stable representative identity for tie-breaking.
        /// </summary>
        public Guid RepresentativeSupportId { get; private set; }

        /// <summary>
        /// Gets whether this existing cluster has absorbed selected individual supports.
        /// </summary>
        public bool HasAddedMembers { get; private set; }

        /// <summary>
        /// Adds one original clustered member without marking the group as changed.
        /// </summary>
        public void AddExistingMember(CandidateSupport member)
        {
            Members.Add(member);
            UpdateRepresentativeSupportId(member.Support.Id);
        }

        /// <summary>
        /// Adds one selected individual member and marks the group for replanning.
        /// </summary>
        public void AddMember(CandidateSupport member)
        {
            Members.Add(member);
            HasAddedMembers = true;
            UpdateRepresentativeSupportId(member.Support.Id);
        }

        /// <summary>
        /// Updates the stable representative identity from a new member.
        /// </summary>
        private void UpdateRepresentativeSupportId(Guid supportId)
        {
            if (RepresentativeSupportId == Guid.Empty || supportId.CompareTo(RepresentativeSupportId) < 0)
            {
                RepresentativeSupportId = supportId;
            }
        }
    }

    /// <summary>
    /// Stores aggregate diagnostics from a candidate-planning pass.
    /// </summary>
    private readonly struct ClusterPlanSummary
    {
        /// <summary>
        /// Creates one cluster planning diagnostic summary.
        /// </summary>
        public ClusterPlanSummary(int clusterCount, int clusteredSupportCount, int rejectedCandidateCount)
        {
            ClusterCount = clusterCount;
            ClusteredSupportCount = clusteredSupportCount;
            RejectedCandidateCount = rejectedCandidateCount;
        }

        /// <summary>
        /// Gets the number of valid clusters produced by the pass.
        /// </summary>
        public int ClusterCount { get; }

        /// <summary>
        /// Gets the number of supports redirected by the pass.
        /// </summary>
        public int ClusteredSupportCount { get; }

        /// <summary>
        /// Gets the number of candidate seeds that could not form a cluster.
        /// </summary>
        public int RejectedCandidateCount { get; }
    }

    /// <summary>
    /// Stores a valid plan for one cluster group.
    /// </summary>
    private readonly struct ClusterPlan
    {
        /// <summary>
        /// Creates one planned cluster assembly.
        /// </summary>
        public ClusterPlan(Vector2 center, float junctionZ, float centralStemBottomDiameter, float centralStemTopDiameter, float clusterBranchDiameter)
        {
            Center = center;
            JunctionZ = junctionZ;
            CentralStemBottomDiameter = centralStemBottomDiameter;
            CentralStemTopDiameter = centralStemTopDiameter;
            ClusterBranchDiameter = clusterBranchDiameter;
        }

        /// <summary>
        /// Gets the shared stem XY axis.
        /// </summary>
        public Vector2 Center { get; }

        /// <summary>
        /// Gets the branch junction height.
        /// </summary>
        public float JunctionZ { get; }

        /// <summary>
        /// Gets the planned shared-stem bottom diameter.
        /// </summary>
        public float CentralStemBottomDiameter { get; }

        /// <summary>
        /// Gets the planned shared-stem top diameter.
        /// </summary>
        public float CentralStemTopDiameter { get; }

        /// <summary>
        /// Gets the planned clustered branch diameter.
        /// </summary>
        public float ClusterBranchDiameter { get; }
    }

    /// <summary>
    /// Provides a small XY grid for bounded neighbor lookup during deterministic grouping.
    /// </summary>
    private sealed class SpatialCandidateGrid
    {
        private readonly Dictionary<GridCell, List<CandidateSupport>> _candidatesByCell = new Dictionary<GridCell, List<CandidateSupport>>();
        private readonly float _cellSize;

        /// <summary>
        /// Creates a grid over the supplied candidates.
        /// </summary>
        public SpatialCandidateGrid(List<CandidateSupport> candidates, float cellSize)
        {
            _cellSize = MathF.Max(cellSize, AxialTolerance);

            for (int i = 0; i < candidates.Count; i++)
            {
                GridCell cell = CreateCell(candidates[i].HeadJointPosition);

                if (!_candidatesByCell.TryGetValue(cell, out List<CandidateSupport>? cellCandidates))
                {
                    cellCandidates = new List<CandidateSupport>();
                    _candidatesByCell.Add(cell, cellCandidates);
                }

                cellCandidates.Add(candidates[i]);
            }
        }

        /// <summary>
        /// Finds candidates within the supplied XY radius.
        /// </summary>
        public List<CandidateSupport> FindNeighbors(Vector3 position, float radius)
        {
            List<CandidateSupport> result = new List<CandidateSupport>();
            GridCell centerCell = CreateCell(position);
            int cellRadius = Math.Max(1, (int)MathF.Ceiling(radius / _cellSize));
            float radiusSquared = radius * radius;
            Vector2 origin = new Vector2(position.X, position.Y);

            for (int y = centerCell.Y - cellRadius; y <= centerCell.Y + cellRadius; y++)
            {
                for (int x = centerCell.X - cellRadius; x <= centerCell.X + cellRadius; x++)
                {
                    GridCell cell = new GridCell(x, y);

                    if (!_candidatesByCell.TryGetValue(cell, out List<CandidateSupport>? cellCandidates))
                    {
                        continue;
                    }

                    for (int i = 0; i < cellCandidates.Count; i++)
                    {
                        Vector2 candidatePosition = new Vector2(
                            cellCandidates[i].HeadJointPosition.X,
                            cellCandidates[i].HeadJointPosition.Y);

                        if (Vector2.DistanceSquared(origin, candidatePosition) <= radiusSquared)
                        {
                            result.Add(cellCandidates[i]);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a position into a grid cell key.
        /// </summary>
        private GridCell CreateCell(Vector3 position)
        {
            return new GridCell(
                (int)MathF.Floor(position.X / _cellSize),
                (int)MathF.Floor(position.Y / _cellSize));
        }
    }

    /// <summary>
    /// Stores one integer XY grid coordinate.
    /// </summary>
    private readonly struct GridCell : IEquatable<GridCell>
    {
        /// <summary>
        /// Creates one grid cell key.
        /// </summary>
        public GridCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Gets the X cell coordinate.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the Y cell coordinate.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Checks whether two grid cells are identical.
        /// </summary>
        public bool Equals(GridCell other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <summary>
        /// Checks whether an object is the same grid cell.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is GridCell other && Equals(other);
        }

        /// <summary>
        /// Gets the hash code for grid dictionary lookup.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }
    }
}



