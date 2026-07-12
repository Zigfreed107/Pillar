// SupportOutputReferenceReconciler.cs
// Retains equivalent generated reinforcement entities so unchanged meshes survive support-stack replay.
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Core.Supports;

/// <summary>
/// Reuses existing generated supports when regenerated output has exactly equivalent render geometry.
/// </summary>
public static class SupportOutputReferenceReconciler
{
    /// <summary>
    /// Replaces equivalent newly generated reinforcement instances with their current document instances.
    /// </summary>
    public static IReadOnlyList<SupportEntity> ReuseEquivalentGeneratedSupports(
        IReadOnlyList<SupportEntity> currentSupports,
        IReadOnlyList<SupportEntity> regeneratedSupports)
    {
        if (currentSupports == null)
        {
            throw new ArgumentNullException(nameof(currentSupports));
        }

        if (regeneratedSupports == null)
        {
            throw new ArgumentNullException(nameof(regeneratedSupports));
        }

        Dictionary<GeneratedSupportGeometryKey, Queue<SupportEntity>> currentByGeometry =
            new Dictionary<GeneratedSupportGeometryKey, Queue<SupportEntity>>();
        HashSet<SupportEntity> currentReferences = new HashSet<SupportEntity>(
            currentSupports,
            ReferenceEqualityComparer.Instance);

        for (int i = 0; i < currentSupports.Count; i++)
        {
            SupportEntity support = currentSupports[i];

            if (!IsGeneratedReinforcement(support))
            {
                continue;
            }

            GeneratedSupportGeometryKey key = new GeneratedSupportGeometryKey(support);

            if (!currentByGeometry.TryGetValue(key, out Queue<SupportEntity>? matchingSupports))
            {
                matchingSupports = new Queue<SupportEntity>();
                currentByGeometry.Add(key, matchingSupports);
            }

            matchingSupports.Enqueue(support);
        }

        List<SupportEntity> reconciled = new List<SupportEntity>(regeneratedSupports.Count);

        for (int i = 0; i < regeneratedSupports.Count; i++)
        {
            SupportEntity support = regeneratedSupports[i];

            if (currentReferences.Contains(support) || !IsGeneratedReinforcement(support))
            {
                reconciled.Add(support);
                continue;
            }

            GeneratedSupportGeometryKey key = new GeneratedSupportGeometryKey(support);

            if (currentByGeometry.TryGetValue(key, out Queue<SupportEntity>? matchingSupports)
                && matchingSupports.Count > 0)
            {
                reconciled.Add(matchingSupports.Dequeue());
                continue;
            }

            reconciled.Add(support);
        }

        return reconciled;
    }

    /// <summary>
    /// Gets whether an entity is regenerated downstream reinforcement rather than editable source output.
    /// </summary>
    private static bool IsGeneratedReinforcement(SupportEntity support)
    {
        return support.Style.Kind == SupportStyleKind.BraceMember
            || support.Style.Kind == SupportStyleKind.Buttress;
    }

    /// <summary>
    /// Captures every value that affects generated reinforcement mesh geometry and appearance classification.
    /// </summary>
    private readonly struct GeneratedSupportGeometryKey : IEquatable<GeneratedSupportGeometryKey>
    {
        private readonly Guid _supportLayerGroupId;
        private readonly string _name;
        private readonly Vector3 _tipPosition;
        private readonly Vector3 _basePosition;
        private readonly Vector3 _headDirection;
        private readonly float _branchLength;
        private readonly Vector3 _branchDirection;
        private readonly SupportStyleKind _styleKind;
        private readonly float _styleDiameter;
        private readonly float _baseBottomRadius;
        private readonly float _baseHeight;
        private readonly float _stemBottomDiameter;
        private readonly float _stemTopDiameter;
        private readonly float _maximumBranchLength;
        private readonly float _modelClearance;
        private readonly float _branchAngleFromVerticalDegrees;
        private readonly float _headHeight;
        private readonly float _headPenetrationDepth;
        private readonly float _headTopDiameter;
        private readonly float _maxHeadAngleFromVerticalDegrees;

        /// <summary>
        /// Creates one exact key from deterministic generated geometry.
        /// </summary>
        public GeneratedSupportGeometryKey(SupportEntity support)
        {
            SupportProfile profile = support.Profile;
            _supportLayerGroupId = support.SupportLayerGroupId;
            _name = support.Name;
            _tipPosition = support.TipPosition;
            _basePosition = support.BasePosition;
            _headDirection = support.HeadDirection;
            _branchLength = support.BranchLength;
            _branchDirection = support.BranchDirection;
            _styleKind = support.Style.Kind;
            _styleDiameter = support.Style is BraceMemberSupportStyle brace
                ? brace.Diameter
                : ((ButtressSupportStyle)support.Style).BranchDiameter;
            _baseBottomRadius = profile.BaseBottomRadius;
            _baseHeight = profile.BaseHeight;
            _stemBottomDiameter = profile.StemBottomDiameter;
            _stemTopDiameter = profile.StemTopDiameter;
            _maximumBranchLength = profile.MaximumBranchLength;
            _modelClearance = profile.ModelClearance;
            _branchAngleFromVerticalDegrees = profile.BranchAngleFromVerticalDegrees;
            _headHeight = profile.HeadHeight;
            _headPenetrationDepth = profile.HeadPenetrationDepth;
            _headTopDiameter = profile.HeadTopDiameter;
            _maxHeadAngleFromVerticalDegrees = profile.MaxHeadAngleFromVerticalDegrees;
        }

        /// <summary>
        /// Tests exact deterministic geometry equality.
        /// </summary>
        public bool Equals(GeneratedSupportGeometryKey other)
        {
            return _supportLayerGroupId == other._supportLayerGroupId
                && string.Equals(_name, other._name, StringComparison.Ordinal)
                && _tipPosition == other._tipPosition
                && _basePosition == other._basePosition
                && _headDirection == other._headDirection
                && _branchLength == other._branchLength
                && _branchDirection == other._branchDirection
                && _styleKind == other._styleKind
                && _styleDiameter == other._styleDiameter
                && _baseBottomRadius == other._baseBottomRadius
                && _baseHeight == other._baseHeight
                && _stemBottomDiameter == other._stemBottomDiameter
                && _stemTopDiameter == other._stemTopDiameter
                && _maximumBranchLength == other._maximumBranchLength
                && _modelClearance == other._modelClearance
                && _branchAngleFromVerticalDegrees == other._branchAngleFromVerticalDegrees
                && _headHeight == other._headHeight
                && _headPenetrationDepth == other._headPenetrationDepth
                && _headTopDiameter == other._headTopDiameter
                && _maxHeadAngleFromVerticalDegrees == other._maxHeadAngleFromVerticalDegrees;
        }

        /// <summary>
        /// Tests boxed key equality for dictionary use.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is GeneratedSupportGeometryKey other && Equals(other);
        }

        /// <summary>
        /// Builds a stable hash from every exact geometry component.
        /// </summary>
        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(_supportLayerGroupId);
            hash.Add(_name, StringComparer.Ordinal);
            hash.Add(_tipPosition);
            hash.Add(_basePosition);
            hash.Add(_headDirection);
            hash.Add(_branchLength);
            hash.Add(_branchDirection);
            hash.Add(_styleKind);
            hash.Add(_styleDiameter);
            hash.Add(_baseBottomRadius);
            hash.Add(_baseHeight);
            hash.Add(_stemBottomDiameter);
            hash.Add(_stemTopDiameter);
            hash.Add(_maximumBranchLength);
            hash.Add(_modelClearance);
            hash.Add(_branchAngleFromVerticalDegrees);
            hash.Add(_headHeight);
            hash.Add(_headPenetrationDepth);
            hash.Add(_headTopDiameter);
            hash.Add(_maxHeadAngleFromVerticalDegrees);
            return hash.ToHashCode();
        }
    }
}