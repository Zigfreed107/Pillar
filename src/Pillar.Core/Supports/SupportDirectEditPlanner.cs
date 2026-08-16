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
            startSettings.OriginalModelBaseLength,
            startSettings.TipPosition,
            startSettings.HeadDirection,
            startSettings.OriginalTipPosition,
            startSettings.OriginalHeadDirection);
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
            startSettings.OriginalModelBaseLength,
            startSettings.TipPosition,
            startSettings.HeadDirection,
            startSettings.OriginalTipPosition,
            startSettings.OriginalHeadDirection);
    }

    /// <summary>
    /// Moves a head contact to one valid model-surface hit and adopts its constrained surface direction.
    /// </summary>
    public static bool TryCreateHeadContactDraggedSettings(
        SupportEntity support,
        SupportDirectEditSettings startSettings,
        Vector3 tipPosition,
        Vector3 headDirection,
        out SupportDirectEditSettings settings)
    {
        if (support == null)
        {
            throw new ArgumentNullException(nameof(support));
        }

        if (startSettings == null)
        {
            throw new ArgumentNullException(nameof(startSettings));
        }

        Vector3 basePosition = startSettings.BasePosition;

        if (!IsFinite(tipPosition) || tipPosition.Z <= basePosition.Z + GeometryTolerance)
        {
            settings = startSettings.Clone();
            return false;
        }

        Vector3 constrainedHeadDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(
            headDirection,
            support.Profile);
        settings = CreateHeadGeometrySettings(
            support,
            startSettings,
            tipPosition,
            constrainedHeadDirection);
        return true;
    }

    /// <summary>
    /// Reorients a fixed-length head toward an XY-dragged head base while retaining its model contact.
    /// </summary>
    public static SupportDirectEditSettings CreateHeadBaseDraggedSettings(
        SupportEntity support,
        SupportDirectEditSettings startSettings,
        Vector3 xyDelta)
    {
        if (support == null)
        {
            throw new ArgumentNullException(nameof(support));
        }

        if (startSettings == null)
        {
            throw new ArgumentNullException(nameof(startSettings));
        }

        if (!IsFinite(xyDelta))
        {
            throw new ArgumentException("Direct Edit head-base displacement must be finite.", nameof(xyDelta));
        }

        Vector3 tipPosition = startSettings.TipPosition ?? support.TipPosition;
        Vector3 startHeadDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(
            startSettings.HeadDirection ?? support.HeadDirection,
            support.Profile);
        Vector3 startHeadBase = tipPosition - (startHeadDirection * support.Profile.HeadHeight);
        Vector3 requestedHeadBase = startHeadBase + new Vector3(xyDelta.X, xyDelta.Y, 0.0f);
        Vector3 requestedDirection = tipPosition - requestedHeadBase;
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(
            requestedDirection,
            support.Profile);
        return CreateHeadGeometrySettings(support, startSettings, tipPosition, headDirection);
    }

    /// <summary>
    /// Moves a model-connected base contact to one valid upward-facing model-surface hit.
    /// </summary>
    public static bool TryCreateModelBaseContactDraggedSettings(
        SupportEntity support,
        SupportDirectEditSettings startSettings,
        Vector3 basePosition,
        Vector3 baseDirection,
        out SupportDirectEditSettings settings)
    {
        if (support == null)
        {
            throw new ArgumentNullException(nameof(support));
        }

        if (startSettings == null)
        {
            throw new ArgumentNullException(nameof(startSettings));
        }

        float modelBaseLength = startSettings.ModelBaseLength ?? support.Profile.ModelBaseHeight;
        SupportProfile profile = support.Profile.WithModelBaseHeight(modelBaseLength);
        Vector3 constrainedBaseDirection = SupportBaseDirectionCalculator.ClampDirectionToProfile(
            baseDirection,
            profile);
        Vector3 stemBase = CalculateStemBase(
            basePosition,
            SupportBaseAttachmentKind.Model,
            constrainedBaseDirection,
            profile);
        Vector3 tipPosition = startSettings.TipPosition ?? support.TipPosition;

        if (!IsFinite(basePosition)
            || basePosition.Z >= tipPosition.Z - GeometryTolerance
            || stemBase.Z >= startSettings.StemTopZ - GeometryTolerance)
        {
            settings = startSettings.Clone();
            return false;
        }

        settings = new SupportDirectEditSettings(
            basePosition,
            startSettings.StemTopZ,
            SupportBaseAttachmentKind.Model,
            constrainedBaseDirection,
            startSettings.OriginalBasePosition,
            startSettings.OriginalStemTopZ,
            startSettings.OriginalBaseAttachmentKind,
            startSettings.OriginalBaseDirection,
            modelBaseLength,
            startSettings.OriginalModelBaseLength,
            startSettings.TipPosition,
            startSettings.HeadDirection,
            startSettings.OriginalTipPosition,
            startSettings.OriginalHeadDirection);
        return true;
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
        Vector3 tipPosition = settings.TipPosition ?? support.TipPosition;
        Vector3 requestedHeadDirection = settings.HeadDirection ?? support.HeadDirection;
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(requestedHeadDirection, profile);
        float usableHeadLength = CalculateUsableHeadLength(
            tipPosition,
            settings.BasePosition.Z,
            headDirection,
            profile.HeadHeight);
        Vector3 headJoint = tipPosition - (headDirection * usableHeadLength);
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
            tipPosition,
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
        Vector3 headJoint = CalculateHeadBase(support);
        return support.BranchLength > GeometryTolerance
            ? headJoint - (Vector3.Normalize(support.BranchDirection) * support.BranchLength)
            : headJoint;
    }

    /// <summary>
    /// Calculates the support head base used to position its optional branch XY gizmo.
    /// </summary>
    public static Vector3 CalculateHeadBase(SupportEntity support)
    {
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
        float usableHeadLength = CalculateUsableHeadLength(
            support.TipPosition,
            support.BasePosition.Z,
            headDirection,
            support.Profile.HeadHeight);
        return support.TipPosition - (headDirection * usableHeadLength);
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

    /// <summary>
    /// Creates explicit reversible head geometry while retaining the current stem and base edit state.
    /// </summary>
    private static SupportDirectEditSettings CreateHeadGeometrySettings(
        SupportEntity support,
        SupportDirectEditSettings startSettings,
        Vector3 tipPosition,
        Vector3 headDirection)
    {
        Vector3 originalTipPosition = startSettings.OriginalTipPosition ?? support.TipPosition;
        Vector3 originalHeadDirection = startSettings.OriginalHeadDirection ?? support.HeadDirection;
        return new SupportDirectEditSettings(
            startSettings.BasePosition,
            startSettings.StemTopZ,
            startSettings.BaseAttachmentKind,
            startSettings.BaseDirection,
            startSettings.OriginalBasePosition,
            startSettings.OriginalStemTopZ,
            startSettings.OriginalBaseAttachmentKind,
            startSettings.OriginalBaseDirection,
            startSettings.ModelBaseLength,
            startSettings.OriginalModelBaseLength,
            tipPosition,
            headDirection,
            originalTipPosition,
            originalHeadDirection);
    }

    /// <summary>
    /// Tests whether all vector components are finite.
    /// </summary>
    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X)
            && float.IsFinite(value.Y)
            && float.IsFinite(value.Z);
    }
}
