// SupportDirectEditPlanner.cs
// Rebuilds edited support stems and branches from durable direct-edit intent.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Core.Supports;

/// <summary>
/// Applies one direct-edit modifier while preserving model contact points and support identities.
/// </summary>
public static class SupportDirectEditPlanner
{
    private const float GeometryTolerance = 0.0001f;

    /// <summary>
    /// Creates one drag edit, pivoting a model base around its fixed contact or translating a plate base.
    /// </summary>
    public static SupportDirectEditSettings CreateDraggedSettings(
        SupportEntity support,
        SupportDirectEditSettings startSettings,
        Vector3 xyDelta,
        float zDelta)
    {
        if (support == null)
        {
            throw new ArgumentNullException(nameof(support));
        }

        if (startSettings == null)
        {
            throw new ArgumentNullException(nameof(startSettings));
        }

        if (!float.IsFinite(xyDelta.X)
            || !float.IsFinite(xyDelta.Y)
            || !float.IsFinite(xyDelta.Z)
            || !float.IsFinite(zDelta))
        {
            throw new ArgumentException("Direct Edit drag displacement must be finite.");
        }

        SupportBaseAttachmentKind baseAttachmentKind = startSettings.BaseAttachmentKind
            ?? support.BaseAttachmentKind;
        float? modelBaseLength = baseAttachmentKind == SupportBaseAttachmentKind.Model
            ? startSettings.ModelBaseLength ?? support.Profile.ModelBaseHeight
            : startSettings.ModelBaseLength;
        SupportProfile profile = modelBaseLength.HasValue
            ? support.Profile.WithModelBaseHeight(modelBaseLength.Value)
            : support.Profile;
        Vector3 startBaseDirection = baseAttachmentKind == SupportBaseAttachmentKind.Model
            ? SupportBaseDirectionCalculator.ClampDirectionToProfile(
                startSettings.BaseDirection ?? support.BaseDirection,
                profile)
            : Vector3.UnitZ;
        Vector3 basePosition = startSettings.BasePosition;
        Vector3 baseDirection = startBaseDirection;

        if (baseAttachmentKind == SupportBaseAttachmentKind.Model)
        {
            Vector3 startStemBase = CalculateStemBase(
                basePosition,
                baseAttachmentKind,
                startBaseDirection,
                profile);
            Vector2 requestedStemPosition = new Vector2(
                startStemBase.X + xyDelta.X,
                startStemBase.Y + xyDelta.Y);
            baseDirection = SupportBaseDirectionCalculator.CreateDirectionTowardStem(
                basePosition,
                requestedStemPosition,
                profile);
        }
        else
        {
            basePosition += new Vector3(xyDelta.X, xyDelta.Y, 0.0f);
        }

        Vector3 stemBase = CalculateStemBase(
            basePosition,
            baseAttachmentKind,
            baseDirection,
            profile);
        float stemTopZ = MathF.Max(
            stemBase.Z + GeometryTolerance,
            startSettings.StemTopZ + zDelta);
        return new SupportDirectEditSettings(
            basePosition,
            stemTopZ,
            baseAttachmentKind,
            baseDirection,
            startSettings.OriginalBasePosition,
            startSettings.OriginalStemTopZ,
            startSettings.OriginalBaseAttachmentKind,
            startSettings.OriginalBaseDirection,
            modelBaseLength,
            startSettings.OriginalModelBaseLength);
    }

    /// <summary>
    /// Changes a model-base length along Z while retaining its contact and vertical-stem XY position.
    /// </summary>
    public static SupportDirectEditSettings CreateModelBaseLengthDraggedSettings(
        SupportEntity support,
        SupportDirectEditSettings startSettings,
        float zDelta)
    {
        if (support == null)
        {
            throw new ArgumentNullException(nameof(support));
        }

        if (startSettings == null)
        {
            throw new ArgumentNullException(nameof(startSettings));
        }

        if (!float.IsFinite(zDelta))
        {
            throw new ArgumentException("Direct Edit model-base displacement must be finite.", nameof(zDelta));
        }

        SupportBaseAttachmentKind baseAttachmentKind = startSettings.BaseAttachmentKind
            ?? support.BaseAttachmentKind;

        if (baseAttachmentKind != SupportBaseAttachmentKind.Model)
        {
            return startSettings.Clone();
        }

        float startModelBaseLength = startSettings.ModelBaseLength ?? support.Profile.ModelBaseHeight;
        SupportProfile startProfile = support.Profile.WithModelBaseHeight(startModelBaseLength);
        Vector3 startBaseDirection = SupportBaseDirectionCalculator.ClampDirectionToProfile(
            startSettings.BaseDirection ?? support.BaseDirection,
            startProfile);
        Vector3 startStemBase = CalculateStemBase(
            startSettings.BasePosition,
            baseAttachmentKind,
            startBaseDirection,
            startProfile);
        Vector2 horizontalOffset = new Vector2(
            startStemBase.X - startSettings.BasePosition.X,
            startStemBase.Y - startSettings.BasePosition.Y);
        float horizontalLength = horizontalOffset.Length();
        float maximumAngleRadians = startProfile.MaxModelBaseAngleFromVerticalDegrees * (MathF.PI / 180.0f);
        float minimumVerticalLength = CalculateMinimumVerticalLength(horizontalLength, maximumAngleRadians);
        float maximumVerticalLength = MathF.Max(
            GeometryTolerance,
            startSettings.StemTopZ - startSettings.BasePosition.Z - GeometryTolerance);
        minimumVerticalLength = MathF.Min(minimumVerticalLength, maximumVerticalLength);
        float requestedVerticalLength = startStemBase.Z + zDelta - startSettings.BasePosition.Z;
        float verticalLength = Math.Clamp(
            requestedVerticalLength,
            minimumVerticalLength,
            maximumVerticalLength);
        Vector3 baseVector = new Vector3(horizontalOffset.X, horizontalOffset.Y, verticalLength);
        float modelBaseLength = baseVector.Length();
        Vector3 baseDirection = modelBaseLength > GeometryTolerance
            ? baseVector / modelBaseLength
            : Vector3.UnitZ;

        return new SupportDirectEditSettings(
            startSettings.BasePosition,
            startSettings.StemTopZ,
            baseAttachmentKind,
            baseDirection,
            startSettings.OriginalBasePosition,
            startSettings.OriginalStemTopZ,
            startSettings.OriginalBaseAttachmentKind,
            startSettings.OriginalBaseDirection,
            modelBaseLength,
            startSettings.OriginalModelBaseLength);
    }

    /// <summary>
    /// Rebuilds every targeted support around the edited shared stem.
    /// </summary>
    public static IReadOnlyList<SupportEntity> Evaluate(IReadOnlyList<SupportEntity> supports, SupportModifierDefinition modifier)
    {
        if (supports == null)
        {
            throw new ArgumentNullException(nameof(supports));
        }

        if (modifier == null || modifier.Kind != SupportModifierKind.DirectEdit || modifier.DirectEditSettings == null)
        {
            throw new ArgumentException("Direct edit evaluation requires Direct Edit settings.", nameof(modifier));
        }

        HashSet<Guid> targetIds = new HashSet<Guid>(modifier.TargetSupportIds);
        List<SupportEntity> result = new List<SupportEntity>(supports.Count);

        for (int i = 0; i < supports.Count; i++)
        {
            SupportEntity support = supports[i];
            result.Add(targetIds.Contains(support.Id) ? RebuildSupport(support, modifier.DirectEditSettings) : support);
        }

        return result;
    }

    /// <summary>
    /// Recreates one support with a vertical edited stem and a branch ending at the original head joint.
    /// </summary>
    public static SupportEntity RebuildSupport(SupportEntity support, SupportDirectEditSettings settings)
    {
        SupportBaseAttachmentKind baseAttachmentKind = settings.BaseAttachmentKind ?? support.BaseAttachmentKind;
        float modelBaseLength = settings.ModelBaseLength ?? support.Profile.ModelBaseHeight;
        SupportProfile profile = settings.ModelBaseLength.HasValue
            ? support.Profile.WithModelBaseHeight(modelBaseLength)
            : support.Profile;
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, profile);
        float usableHeadLength = CalculateUsableHeadLength(
            support.TipPosition,
            settings.BasePosition.Z,
            headDirection,
            profile.HeadHeight);
        Vector3 headJoint = support.TipPosition - (headDirection * usableHeadLength);
        Vector3 requestedBaseDirection = settings.BaseDirection ?? support.BaseDirection;
        Vector3 baseDirection = baseAttachmentKind == SupportBaseAttachmentKind.Model
            ? SupportBaseDirectionCalculator.ClampDirectionToProfile(requestedBaseDirection, profile)
            : Vector3.UnitZ;
        Vector3 stemBase = CalculateStemBase(
            settings.BasePosition,
            baseAttachmentKind,
            baseDirection,
            profile);
        Vector3 stemTop = new Vector3(stemBase.X, stemBase.Y, settings.StemTopZ);
        Vector3 branchVector = headJoint - stemTop;
        float branchLength = branchVector.Length();
        Vector3 branchDirection = branchLength > GeometryTolerance ? branchVector / branchLength : Vector3.UnitZ;

        return SupportEntity.CreateLoaded(
            support.Id,
            support.Name,
            support.SupportLayerGroupId,
            support.TipPosition,
            settings.BasePosition,
            headDirection,
            branchLength > GeometryTolerance ? branchLength : 0.0f,
            branchDirection,
            profile,
            support.Style,
            baseAttachmentKind,
            baseDirection);
    }

    /// <summary>
    /// Calculates the current stem joint used to position the Z gizmo.
    /// </summary>
    public static Vector3 CalculateStemTop(SupportEntity support)
    {
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
        float usableHeadLength = CalculateUsableHeadLength(
            support.TipPosition,
            support.BasePosition.Z,
            headDirection,
            support.Profile.HeadHeight);
        Vector3 headJoint = support.TipPosition - (headDirection * usableHeadLength);
        return support.BranchLength > GeometryTolerance
            ? headJoint - (Vector3.Normalize(support.BranchDirection) * support.BranchLength)
            : headJoint;
    }

    /// <summary>
    /// Calculates the start of the vertical stem for the support's current base contact.
    /// </summary>
    public static Vector3 CalculateStemBase(SupportEntity support)
    {
        return CalculateStemBase(support, support.BasePosition);
    }

    /// <summary>
    /// Calculates the start of the vertical stem for an edited base contact.
    /// </summary>
    public static Vector3 CalculateStemBase(SupportEntity support, Vector3 basePosition)
    {
        Vector3 baseDirection = support.BaseAttachmentKind == SupportBaseAttachmentKind.Model
            ? SupportBaseDirectionCalculator.ClampDirectionToProfile(
                support.BaseDirection,
                support.Profile)
            : Vector3.UnitZ;
        return CalculateStemBase(
            basePosition,
            support.BaseAttachmentKind,
            baseDirection,
            support.Profile);
    }

    /// <summary>
    /// Calculates the vertical stem start from explicit base attachment data.
    /// </summary>
    public static Vector3 CalculateStemBase(
        Vector3 basePosition,
        SupportBaseAttachmentKind baseAttachmentKind,
        Vector3 baseDirection,
        SupportProfile profile)
    {
        return baseAttachmentKind == SupportBaseAttachmentKind.Model
            ? basePosition + (baseDirection * profile.ModelBaseHeight)
            : basePosition;
    }

    /// <summary>
    /// Gets the vertical component needed to keep an existing horizontal offset within the angle limit.
    /// </summary>
    private static float CalculateMinimumVerticalLength(float horizontalLength, float maximumAngleRadians)
    {
        if (horizontalLength <= GeometryTolerance)
        {
            return GeometryTolerance;
        }

        if (maximumAngleRadians >= (MathF.PI * 0.5f) - GeometryTolerance)
        {
            return GeometryTolerance;
        }

        float tangent = MathF.Tan(maximumAngleRadians);
        return tangent > GeometryTolerance
            ? horizontalLength / tangent
            : float.MaxValue;
    }

    /// <summary>
    /// Shortens an upward head when the support contact is too close to its base elevation.
    /// </summary>
    private static float CalculateUsableHeadLength(
        Vector3 tipPosition,
        float baseZ,
        Vector3 headDirection,
        float requestedHeadLength)
    {
        if (headDirection.Z <= GeometryTolerance)
        {
            return requestedHeadLength;
        }

        float maximumLengthByHeight = MathF.Max(
            0.0f,
            (tipPosition.Z - baseZ) / headDirection.Z);
        return MathF.Min(requestedHeadLength, maximumLengthByHeight);
    }
}
