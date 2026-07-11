// SupportBracingPlanner.cs
// Evaluates Brace and Buttress support modifiers without depending on rendering types.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Core.Supports;

/// <summary>
/// Creates generated reinforcement members for Brace and Buttress support modifiers.
/// </summary>
public static class SupportBracingPlanner
{
    private const int MaximumBraceConnectionsPerSupport = 3;
    private const float GeometryTolerance = 0.0001f;

    /// <summary>
    /// Evaluates a Brace modifier against the current support output.
    /// </summary>
    public static SupportBracingEvaluationResult EvaluateBrace(
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

        if (modifier.Kind != SupportModifierKind.Brace || modifier.BraceSettings == null)
        {
            throw new ArgumentException("A Brace modifier with settings is required.", nameof(modifier));
        }

        List<SupportEntity> result = new List<SupportEntity>(sourceSupports);
        List<SupportEntity> targets = CreateEligibleTargetSupports(sourceSupports, modifier.TargetSupportIds, null);
        List<BraceCandidate> candidates = CreateBraceCandidates(targets, modifier.BraceSettings);
        int feasibleCandidateCount = candidates.Count;
        List<BraceCandidate> nearestCandidates = CreateRelativeNeighborhoodCandidates(candidates);
        HashSet<SupportBracePair> excludedPairs = new HashSet<SupportBracePair>(modifier.ExcludedBracePairs);
        Dictionary<Guid, int> connectionCounts = new Dictionary<Guid, int>();
        int addedMemberCount = 0;
        int rejectedCandidateCount = feasibleCandidateCount - nearestCandidates.Count;

        nearestCandidates.Sort(CompareBraceCandidates);

        for (int i = 0; i < nearestCandidates.Count; i++)
        {
            BraceCandidate candidate = nearestCandidates[i];
            int startCount = GetConnectionCount(connectionCounts, candidate.StartSupportId);
            int endCount = GetConnectionCount(connectionCounts, candidate.EndSupportId);

            if (startCount >= MaximumBraceConnectionsPerSupport || endCount >= MaximumBraceConnectionsPerSupport)
            {
                rejectedCandidateCount++;
                continue;
            }

            connectionCounts[candidate.StartSupportId] = startCount + 1;
            connectionCounts[candidate.EndSupportId] = endCount + 1;

            if (excludedPairs.Contains(new SupportBracePair(candidate.StartSupportId, candidate.EndSupportId)))
            {
                rejectedCandidateCount++;
                continue;
            }

            addedMemberCount += AppendBraceCandidateMembers(result, candidate, modifier.BraceSettings.BraceDiameter);
        }

        return new SupportBracingEvaluationResult(result, addedMemberCount, targets.Count, rejectedCandidateCount);
    }

    /// <summary>
    /// Evaluates a Buttress modifier against the current support output.
    /// </summary>
    public static SupportBracingEvaluationResult EvaluateButtress(
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

        if (modifier.Kind != SupportModifierKind.Buttress || modifier.ButtressSettings == null)
        {
            throw new ArgumentException("A Buttress modifier with settings is required.", nameof(modifier));
        }

        SupportButtressModifierSettings settings = modifier.ButtressSettings;
        List<SupportEntity> result = new List<SupportEntity>(sourceSupports);
        List<SupportEntity> targets = CreateEligibleTargetSupports(
            sourceSupports,
            modifier.TargetSupportIds,
            settings.MinimumButtressHeight);
        int addedMemberCount = 0;
        int rejectedCandidateCount = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            SupportEntity support = targets[i];

            if (!TryCreateButtressPair(support, settings, out SupportEntity firstButtress, out SupportEntity secondButtress))
            {
                rejectedCandidateCount++;
                continue;
            }

            result.Add(firstButtress);
            result.Add(secondButtress);
            addedMemberCount += 2;

            if (TryCreateDirectionalBraceCandidate(firstButtress, support, settings.BraceSettings, out BraceCandidate firstBrace))
            {
                addedMemberCount += AppendBraceCandidateMembers(result, firstBrace, settings.BraceSettings.BraceDiameter);
            }
            else
            {
                rejectedCandidateCount++;
            }

            if (TryCreateDirectionalBraceCandidate(secondButtress, support, settings.BraceSettings, out BraceCandidate secondBrace))
            {
                addedMemberCount += AppendBraceCandidateMembers(result, secondBrace, settings.BraceSettings.BraceDiameter);
            }
            else
            {
                rejectedCandidateCount++;
            }

            if (TryCreateDirectionalBraceCandidate(firstButtress, secondButtress, settings.BraceSettings, out BraceCandidate betweenButtressesBrace))
            {
                addedMemberCount += AppendBraceCandidateMembers(result, betweenButtressesBrace, settings.BraceSettings.BraceDiameter);
            }
            else
            {
                rejectedCandidateCount++;
            }
        }

        return new SupportBracingEvaluationResult(result, addedMemberCount, targets.Count, rejectedCandidateCount);
    }

    /// <summary>
    /// Builds feasible pairwise brace candidates using bounded XY neighbor lookup.
    /// </summary>
    private static List<BraceCandidate> CreateBraceCandidates(IReadOnlyList<SupportEntity> targets, SupportBraceModifierSettings settings)
    {
        List<BraceCandidate> candidates = new List<BraceCandidate>();

        if (settings.MaximumBraceLength <= 0.0f)
        {
            return candidates;
        }

        BraceTargetSpatialGrid grid = new BraceTargetSpatialGrid(targets, settings.MaximumBraceLength);

        for (int i = 0; i < targets.Count; i++)
        {
            SupportEntity target = targets[i];
            List<SupportEntity> neighbors = grid.FindNeighbors(target.BasePosition, settings.MaximumBraceLength);

            for (int j = 0; j < neighbors.Count; j++)
            {
                SupportEntity neighbor = neighbors[j];

                if (target.Id.CompareTo(neighbor.Id) >= 0)
                {
                    continue;
                }

                if (TryCreateBraceCandidate(target, neighbor, settings, out BraceCandidate candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        return candidates;
    }


    /// <summary>
    /// Keeps only feasible pairs that have no closer feasible support in their relative neighborhood.
    /// </summary>
    private static List<BraceCandidate> CreateRelativeNeighborhoodCandidates(IReadOnlyList<BraceCandidate> candidates)
    {
        Dictionary<Guid, List<BraceCandidate>> candidatesBySupportId = new Dictionary<Guid, List<BraceCandidate>>();
        Dictionary<BracePairKey, BraceCandidate> candidatesByPair = new Dictionary<BracePairKey, BraceCandidate>();
        List<BraceCandidate> result = new List<BraceCandidate>();

        for (int i = 0; i < candidates.Count; i++)
        {
            BraceCandidate candidate = candidates[i];
            AddCandidateForSupport(candidatesBySupportId, candidate.StartSupportId, candidate);
            AddCandidateForSupport(candidatesBySupportId, candidate.EndSupportId, candidate);
            candidatesByPair[new BracePairKey(candidate.StartSupportId, candidate.EndSupportId)] = candidate;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            BraceCandidate candidate = candidates[i];

            if (!HasCloserFeasibleCommonNeighbor(candidate, candidatesBySupportId, candidatesByPair))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    /// <summary>
    /// Adds one pair candidate to a support's local feasible adjacency list.
    /// </summary>
    private static void AddCandidateForSupport(
        Dictionary<Guid, List<BraceCandidate>> candidatesBySupportId,
        Guid supportId,
        BraceCandidate candidate)
    {
        if (!candidatesBySupportId.TryGetValue(supportId, out List<BraceCandidate>? supportCandidates))
        {
            supportCandidates = new List<BraceCandidate>();
            candidatesBySupportId.Add(supportId, supportCandidates);
        }

        supportCandidates.Add(candidate);
    }

    /// <summary>
    /// Checks the constrained relative-neighborhood lens for a closer feasible common neighbor.
    /// </summary>
    private static bool HasCloserFeasibleCommonNeighbor(
        BraceCandidate candidate,
        Dictionary<Guid, List<BraceCandidate>> candidatesBySupportId,
        Dictionary<BracePairKey, BraceCandidate> candidatesByPair)
    {
        if (!candidatesBySupportId.TryGetValue(candidate.StartSupportId, out List<BraceCandidate>? startCandidates))
        {
            return false;
        }

        for (int i = 0; i < startCandidates.Count; i++)
        {
            BraceCandidate startNeighborCandidate = startCandidates[i];
            Guid commonNeighborId = startNeighborCandidate.GetOtherSupportId(candidate.StartSupportId);

            if (commonNeighborId == candidate.EndSupportId
                || !IsStrictlyCloser(startNeighborCandidate.HorizontalDistanceSquared, candidate.HorizontalDistanceSquared))
            {
                continue;
            }

            if (candidatesByPair.TryGetValue(
                    new BracePairKey(candidate.EndSupportId, commonNeighborId),
                    out BraceCandidate endNeighborCandidate)
                && IsStrictlyCloser(endNeighborCandidate.HorizontalDistanceSquared, candidate.HorizontalDistanceSquared))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Compares XY distances while preserving equal-distance neighbors within the geometry tolerance.
    /// </summary>
    private static bool IsStrictlyCloser(float candidateDistanceSquared, float referenceDistanceSquared)
    {
        float tolerance = GeometryTolerance * MathF.Max(1.0f, referenceDistanceSquared);
        return candidateDistanceSquared + tolerance < referenceDistanceSquared;
    }

    /// <summary>
    /// Creates the shortest deterministic directional brace candidate for an unordered target pair.
    /// </summary>
    private static bool TryCreateBraceCandidate(
        SupportEntity firstSupport,
        SupportEntity secondSupport,
        SupportBraceModifierSettings settings,
        out BraceCandidate candidate)
    {
        bool hasForwardCandidate = TryCreateDirectionalBraceCandidate(firstSupport, secondSupport, settings, out BraceCandidate forwardCandidate);
        bool hasReverseCandidate = TryCreateDirectionalBraceCandidate(secondSupport, firstSupport, settings, out BraceCandidate reverseCandidate);

        if (!hasForwardCandidate && !hasReverseCandidate)
        {
            candidate = default;
            return false;
        }

        if (!hasReverseCandidate || (hasForwardCandidate && CompareDirectionalCandidates(forwardCandidate, reverseCandidate) <= 0))
        {
            candidate = forwardCandidate;
            return true;
        }

        candidate = reverseCandidate;
        return true;
    }

    /// <summary>
    /// Creates one brace from a base top to the opposite stem, plus one feasible rising return member.
    /// </summary>
    private static bool TryCreateDirectionalBraceCandidate(
        SupportEntity startSupport,
        SupportEntity endSupport,
        SupportBraceModifierSettings settings,
        out BraceCandidate candidate)
    {
        Vector3 startPosition = CalculateBaseTopPosition(startSupport);
        Vector3 maximumEndPosition = CalculateStemTopPosition(endSupport);
        Vector2 startXy = new Vector2(startPosition.X, startPosition.Y);
        Vector2 endXy = new Vector2(maximumEndPosition.X, maximumEndPosition.Y);
        float horizontalDistanceSquared = Vector2.DistanceSquared(startXy, endXy);

        candidate = default;

        if (!TryCreateBraceSegment(
                startPosition,
                maximumEndPosition,
                settings,
                out Vector3 endPosition,
                out float length,
                out float angleDegrees))
        {
            return false;
        }

        Vector3 returnStartPosition = endPosition;
        Vector3 maximumReturnEndPosition = CalculateStemTopPosition(startSupport);
        bool hasReturnMember = TryCreateBraceSegment(
            returnStartPosition,
            maximumReturnEndPosition,
            settings,
            out Vector3 returnEndPosition,
            out float _,
            out float _);

        candidate = new BraceCandidate(
            startSupport.Id,
            endSupport.Id,
            startSupport.SupportLayerGroupId,
            startPosition,
            endPosition,
            length,
            horizontalDistanceSquared,
            angleDegrees,
            hasReturnMember,
            returnStartPosition,
            returnEndPosition);
        return true;
    }

    /// <summary>
    /// Creates one rising brace segment within the configured angle and length limits.
    /// </summary>
    private static bool TryCreateBraceSegment(
        Vector3 startPosition,
        Vector3 maximumEndPosition,
        SupportBraceModifierSettings settings,
        out Vector3 endPosition,
        out float length,
        out float angleDegrees)
    {
        Vector2 startXy = new Vector2(startPosition.X, startPosition.Y);
        Vector2 endXy = new Vector2(maximumEndPosition.X, maximumEndPosition.Y);
        float horizontalDistance = Vector2.Distance(startXy, endXy);
        float verticalDistance = maximumEndPosition.Z - startPosition.Z;

        endPosition = maximumEndPosition;
        length = 0.0f;
        angleDegrees = 0.0f;

        if (horizontalDistance <= GeometryTolerance || verticalDistance <= GeometryTolerance)
        {
            return false;
        }

        angleDegrees = RadiansToDegrees(MathF.Atan2(verticalDistance, horizontalDistance));

        if (angleDegrees < settings.MinimumBraceAngleDegrees)
        {
            return false;
        }

        if (angleDegrees > settings.MaximumBraceAngleDegrees)
        {
            float adjustedZ = startPosition.Z + (horizontalDistance * MathF.Tan(DegreesToRadians(settings.MaximumBraceAngleDegrees)));

            if (adjustedZ <= startPosition.Z + GeometryTolerance || adjustedZ > maximumEndPosition.Z)
            {
                return false;
            }

            endPosition = new Vector3(maximumEndPosition.X, maximumEndPosition.Y, adjustedZ);
            angleDegrees = settings.MaximumBraceAngleDegrees;
        }

        length = Vector3.Distance(startPosition, endPosition);
        return length <= settings.MaximumBraceLength;
    }

    /// <summary>
    /// Orders directional alternatives by printable length and stable support identity.
    /// </summary>
    private static int CompareDirectionalCandidates(BraceCandidate left, BraceCandidate right)
    {
        int lengthCompare = left.Length.CompareTo(right.Length);

        if (lengthCompare != 0)
        {
            return lengthCompare;
        }

        int startCompare = left.StartSupportId.CompareTo(right.StartSupportId);
        return startCompare != 0 ? startCompare : left.EndSupportId.CompareTo(right.EndSupportId);
    }

    /// <summary>
    /// Creates target supports from saved ids while excluding clustered and generated members.
    /// </summary>
    private static List<SupportEntity> CreateEligibleTargetSupports(
        IReadOnlyList<SupportEntity> sourceSupports,
        IReadOnlyList<Guid> targetSupportIds,
        float? minimumHeight)
    {
        HashSet<Guid> targetIdSet = new HashSet<Guid>(targetSupportIds);
        List<SupportEntity> targets = new List<SupportEntity>();

        for (int i = 0; i < sourceSupports.Count; i++)
        {
            SupportEntity support = sourceSupports[i];

            if (!targetIdSet.Contains(support.Id) || support.Style.Kind != SupportStyleKind.Individual)
            {
                continue;
            }

            if (minimumHeight.HasValue && CalculateSupportHeight(support) <= minimumHeight.Value)
            {
                continue;
            }

            targets.Add(support);
        }

        return targets;
    }

    /// <summary>
    /// Appends the primary and optional return cylinders for one accepted support pair.
    /// </summary>
    private static int AppendBraceCandidateMembers(
        List<SupportEntity> result,
        BraceCandidate candidate,
        float diameter)
    {
        result.Add(CreateBraceMember(candidate.LayerGroupId, "Brace", candidate.StartPosition, candidate.EndPosition, diameter));

        if (!candidate.HasReturnMember)
        {
            return 1;
        }

        result.Add(CreateBraceMember(candidate.LayerGroupId, "Brace", candidate.ReturnStartPosition, candidate.ReturnEndPosition, diameter));
        return 2;
    }

    /// <summary>
    /// Creates both headless buttress supports at the rear vertices of an equilateral plan triangle.
    /// </summary>
    private static bool TryCreateButtressPair(
        SupportEntity support,
        SupportButtressModifierSettings settings,
        out SupportEntity firstButtress,
        out SupportEntity secondButtress)
    {
        firstButtress = null!;
        secondButtress = null!;

        if (settings.ButtressSpacing <= GeometryTolerance)
        {
            return false;
        }

        ButtressBasePair basePair = CalculateButtressBasePair(support, settings.ButtressSpacing);
        return TryCreateButtressSupport(support, basePair.First, settings.BraceSettings.MinimumBraceAngleDegrees, out firstButtress)
            && TryCreateButtressSupport(support, basePair.Second, settings.BraceSettings.MinimumBraceAngleDegrees, out secondButtress);
    }

    /// <summary>
    /// Creates one normal base and stem whose headless branch joins the original support stem top.
    /// </summary>
    private static bool TryCreateButtressSupport(
        SupportEntity support,
        Vector3 basePosition,
        float branchAngleDegrees,
        out SupportEntity buttress)
    {
        Vector3 branchEndPosition = CalculateStemTopPosition(support);
        Vector2 baseXy = new Vector2(basePosition.X, basePosition.Y);
        Vector2 endXy = new Vector2(branchEndPosition.X, branchEndPosition.Y);
        float horizontalDistance = Vector2.Distance(baseXy, endXy);
        float verticalRise = horizontalDistance * MathF.Tan(DegreesToRadians(branchAngleDegrees));
        float stemJointZ = branchEndPosition.Z - verticalRise;
        float minimumStemJointZ = basePosition.Z + support.Profile.BaseHeight + GeometryTolerance;

        buttress = null!;

        if (horizontalDistance <= GeometryTolerance || stemJointZ <= minimumStemJointZ)
        {
            return false;
        }

        Vector3 stemJointPosition = new Vector3(basePosition.X, basePosition.Y, stemJointZ);
        Vector3 branchVector = branchEndPosition - stemJointPosition;
        float branchLength = branchVector.Length();

        if (branchLength <= GeometryTolerance || !float.IsFinite(branchLength))
        {
            return false;
        }

        Vector3 branchDirection = branchVector / branchLength;
        float branchDiameter = SupportDimensionResolver.Resolve(support.Profile, support.Style).BranchDiameter;
        buttress = SupportEntity.CreateLoaded(
            Guid.NewGuid(),
            "Buttress",
            support.SupportLayerGroupId,
            branchEndPosition,
            basePosition,
            Vector3.UnitZ,
            branchLength,
            branchDirection,
            support.Profile,
            new ButtressSupportStyle(branchDiameter));
        return true;
    }

    /// <summary>
    /// Places two bases behind the source support at the remaining vertices of an equilateral triangle.
    /// </summary>
    private static ButtressBasePair CalculateButtressBasePair(SupportEntity support, float spacing)
    {
        Vector2 forwardDirection = CalculateSupportPlanDirection(support);
        Vector2 rearDirection = -forwardDirection;
        Vector2 lateralDirection = new Vector2(-rearDirection.Y, rearDirection.X);
        Vector2 originalBase = new Vector2(support.BasePosition.X, support.BasePosition.Y);
        float triangleAltitude = spacing * (MathF.Sqrt(3.0f) * 0.5f);
        Vector2 rearCenter = originalBase + (rearDirection * triangleAltitude);
        Vector2 lateralOffset = lateralDirection * (spacing * 0.5f);
        Vector2 first = rearCenter + lateralOffset;
        Vector2 second = rearCenter - lateralOffset;
        return new ButtressBasePair(
            new Vector3(first.X, first.Y, support.BasePosition.Z),
            new Vector3(second.X, second.Y, support.BasePosition.Z));
    }

    /// <summary>
    /// Resolves the source head's plan direction with a stable fallback for vertical heads.
    /// </summary>
    private static Vector2 CalculateSupportPlanDirection(SupportEntity support)
    {
        Vector2 direction = new Vector2(support.HeadDirection.X, support.HeadDirection.Y);

        if (direction.LengthSquared() <= GeometryTolerance * GeometryTolerance)
        {
            direction = new Vector2(
                support.TipPosition.X - support.BasePosition.X,
                support.TipPosition.Y - support.BasePosition.Y);
        }

        return direction.LengthSquared() > GeometryTolerance * GeometryTolerance
            ? Vector2.Normalize(direction)
            : Vector2.UnitX;
    }

    /// <summary>
    /// Creates a generated cylindrical support member entity for modifier output.
    /// </summary>
    private static SupportEntity CreateBraceMember(Guid supportLayerGroupId, string name, Vector3 startPosition, Vector3 endPosition, float diameter)
    {
        Vector3 axisDirection = Vector3.Normalize(endPosition - startPosition);
        SupportProfile profile = new SupportProfile(
            diameter * 0.5f,
            SupportDefaults.DefaultBaseHeight,
            diameter,
            diameter,
            0.0f,
            0.0f,
            SupportDefaults.DefaultBranchAngleFromVerticalDegrees,
            SupportDefaults.DefaultHeadHeight,
            SupportDefaults.DefaultHeadPenetrationDepth,
            diameter,
            90.0f);
        return SupportEntity.CreateLoaded(
            Guid.NewGuid(),
            name,
            supportLayerGroupId,
            endPosition,
            startPosition,
            axisDirection,
            0.0f,
            Vector3.UnitZ,
            profile,
            new BraceMemberSupportStyle(diameter));
    }

    /// <summary>
    /// Gets the top center of a support's vertical stem.
    /// </summary>
    private static Vector3 CalculateStemTopPosition(SupportEntity support)
    {
        if (support.Style.Kind == SupportStyleKind.Buttress && support.BranchLength > GeometryTolerance)
        {
            return support.TipPosition - (Vector3.Normalize(support.BranchDirection) * support.BranchLength);
        }
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
        Vector3 headJointPosition = support.TipPosition - (headDirection * support.Profile.HeadHeight);

        if (support.BranchLength > GeometryTolerance)
        {
            Vector3 branchDirection = Vector3.Normalize(support.BranchDirection);
            return headJointPosition - (branchDirection * support.BranchLength);
        }

        return headJointPosition;
    }

    /// <summary>
    /// Gets the center of the printable base's top surface for a brace start endpoint.
    /// </summary>
    private static Vector3 CalculateBaseTopPosition(SupportEntity support)
    {
        float supportHeight = MathF.Max(0.0f, CalculateSupportHeight(support));
        float baseHeight = MathF.Min(support.Profile.BaseHeight, supportHeight);
        return support.BasePosition + (Vector3.UnitZ * baseHeight);
    }

    /// <summary>
    /// Gets the support height from base to stem top.
    /// </summary>
    private static float CalculateSupportHeight(SupportEntity support)
    {
        return CalculateStemTopPosition(support).Z - support.BasePosition.Z;
    }

    /// <summary>
    /// Gets a current connection count with a zero default.
    /// </summary>
    private static int GetConnectionCount(Dictionary<Guid, int> connectionCounts, Guid supportId)
    {
        return connectionCounts.TryGetValue(supportId, out int count) ? count : 0;
    }

    /// <summary>
    /// Orders brace candidates by XY neighborhood distance, printable length, and stable identity.
    /// </summary>
    private static int CompareBraceCandidates(BraceCandidate left, BraceCandidate right)
    {
        int horizontalCompare = left.HorizontalDistanceSquared.CompareTo(right.HorizontalDistanceSquared);

        if (horizontalCompare != 0)
        {
            return horizontalCompare;
        }

        int lengthCompare = left.Length.CompareTo(right.Length);

        if (lengthCompare != 0)
        {
            return lengthCompare;
        }

        int startCompare = left.StartSupportId.CompareTo(right.StartSupportId);
        return startCompare != 0 ? startCompare : left.EndSupportId.CompareTo(right.EndSupportId);
    }

    /// <summary>
    /// Converts radians to degrees.
    /// </summary>
    private static float RadiansToDegrees(float radians)
    {
        return radians * (180.0f / MathF.PI);
    }

    /// <summary>
    /// Converts degrees to radians.
    /// </summary>
    private static float DegreesToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180.0f);
    }

    /// <summary>
    /// Stores the two equilateral-triangle buttress base positions for one source support.
    /// </summary>
    private readonly struct ButtressBasePair
    {
        /// <summary>
        /// Creates one pair of buttress base positions.
        /// </summary>
        public ButtressBasePair(Vector3 first, Vector3 second)
        {
            First = first;
            Second = second;
        }

        public Vector3 First { get; }

        public Vector3 Second { get; }
    }

    /// <summary>
    /// Stores one candidate generated brace member.
    /// </summary>
    private readonly struct BraceCandidate
    {
        /// <summary>
        /// Creates one brace candidate.
        /// </summary>
        public BraceCandidate(
            Guid startSupportId,
            Guid endSupportId,
            Guid layerGroupId,
            Vector3 startPosition,
            Vector3 endPosition,
            float length,
            float horizontalDistanceSquared,
            float angleDegrees,
            bool hasReturnMember,
            Vector3 returnStartPosition,
            Vector3 returnEndPosition)
        {
            StartSupportId = startSupportId;
            EndSupportId = endSupportId;
            LayerGroupId = layerGroupId;
            StartPosition = startPosition;
            EndPosition = endPosition;
            Length = length;
            HorizontalDistanceSquared = horizontalDistanceSquared;
            AngleDegrees = angleDegrees;
            HasReturnMember = hasReturnMember;
            ReturnStartPosition = returnStartPosition;
            ReturnEndPosition = returnEndPosition;
        }

        public Guid StartSupportId { get; }

        public Guid EndSupportId { get; }

        public Guid LayerGroupId { get; }

        public Vector3 StartPosition { get; }

        public Vector3 EndPosition { get; }

        public float Length { get; }

        public float HorizontalDistanceSquared { get; }

        public float AngleDegrees { get; }

        public bool HasReturnMember { get; }

        public Vector3 ReturnStartPosition { get; }

        public Vector3 ReturnEndPosition { get; }

        /// <summary>
        /// Gets the opposite endpoint identity for one support in this pair.
        /// </summary>
        public Guid GetOtherSupportId(Guid supportId)
        {
            return supportId == StartSupportId ? EndSupportId : StartSupportId;
        }
    }

    /// <summary>
    /// Provides identity-based lookup for one unordered feasible support pair.
    /// </summary>
    private readonly struct BracePairKey : IEquatable<BracePairKey>
    {
        /// <summary>
        /// Creates one key with endpoints stored in stable identity order.
        /// </summary>
        public BracePairKey(Guid firstSupportId, Guid secondSupportId)
        {
            if (firstSupportId.CompareTo(secondSupportId) <= 0)
            {
                FirstSupportId = firstSupportId;
                SecondSupportId = secondSupportId;
            }
            else
            {
                FirstSupportId = secondSupportId;
                SecondSupportId = firstSupportId;
            }
        }

        public Guid FirstSupportId { get; }

        public Guid SecondSupportId { get; }

        /// <summary>
        /// Compares unordered pair identities.
        /// </summary>
        public bool Equals(BracePairKey other)
        {
            return FirstSupportId == other.FirstSupportId && SecondSupportId == other.SecondSupportId;
        }

        /// <summary>
        /// Compares an object with this pair key.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is BracePairKey other && Equals(other);
        }

        /// <summary>
        /// Builds a stable hash from both endpoint identities.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(FirstSupportId, SecondSupportId);
        }
    }

    /// <summary>
    /// Provides bounded XY lookup for brace targets without introducing rendering dependencies.
    /// </summary>
    private sealed class BraceTargetSpatialGrid
    {
        private readonly Dictionary<BraceGridKey, List<SupportEntity>> _cells = new Dictionary<BraceGridKey, List<SupportEntity>>();
        private readonly float _cellSize;

        /// <summary>
        /// Indexes target support bases using the maximum brace length as the cell size.
        /// </summary>
        public BraceTargetSpatialGrid(IReadOnlyList<SupportEntity> targets, float maximumBraceLength)
        {
            _cellSize = MathF.Max(maximumBraceLength, GeometryTolerance);

            for (int i = 0; i < targets.Count; i++)
            {
                SupportEntity target = targets[i];
                BraceGridKey key = ToKey(target.BasePosition);

                if (!_cells.TryGetValue(key, out List<SupportEntity>? cell))
                {
                    cell = new List<SupportEntity>();
                    _cells.Add(key, cell);
                }

                cell.Add(target);
            }
        }

        /// <summary>
        /// Returns supports from cells intersecting an XY radius around one support base.
        /// </summary>
        public List<SupportEntity> FindNeighbors(Vector3 origin, float radius)
        {
            List<SupportEntity> result = new List<SupportEntity>();
            int minimumX = ToCellCoordinate(origin.X - radius);
            int maximumX = ToCellCoordinate(origin.X + radius);
            int minimumY = ToCellCoordinate(origin.Y - radius);
            int maximumY = ToCellCoordinate(origin.Y + radius);
            float radiusSquared = radius * radius;

            for (int x = minimumX; x <= maximumX; x++)
            {
                for (int y = minimumY; y <= maximumY; y++)
                {
                    if (!_cells.TryGetValue(new BraceGridKey(x, y), out List<SupportEntity>? cell))
                    {
                        continue;
                    }

                    for (int i = 0; i < cell.Count; i++)
                    {
                        SupportEntity candidate = cell[i];
                        float deltaX = candidate.BasePosition.X - origin.X;
                        float deltaY = candidate.BasePosition.Y - origin.Y;

                        if ((deltaX * deltaX) + (deltaY * deltaY) <= radiusSquared + GeometryTolerance)
                        {
                            result.Add(candidate);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Converts one support base to its grid key.
        /// </summary>
        private BraceGridKey ToKey(Vector3 position)
        {
            return new BraceGridKey(ToCellCoordinate(position.X), ToCellCoordinate(position.Y));
        }

        /// <summary>
        /// Converts one world coordinate to a stable floor-based cell coordinate.
        /// </summary>
        private int ToCellCoordinate(float coordinate)
        {
            return (int)MathF.Floor(coordinate / _cellSize);
        }
    }

    /// <summary>
    /// Identifies one XY brace-target grid cell.
    /// </summary>
    private readonly struct BraceGridKey : IEquatable<BraceGridKey>
    {
        /// <summary>
        /// Creates one grid key.
        /// </summary>
        public BraceGridKey(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        /// <summary>
        /// Compares grid coordinates.
        /// </summary>
        public bool Equals(BraceGridKey other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <summary>
        /// Compares an object with this grid key.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is BraceGridKey other && Equals(other);
        }

        /// <summary>
        /// Builds a stable hash from both grid coordinates.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }
    }
}