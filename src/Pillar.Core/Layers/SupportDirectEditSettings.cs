// SupportDirectEditSettings.cs
// Stores one durable direct-edit transition without depending on viewport or rendering types.
using Pillar.Core.Supports;
using System;
using System.Numerics;

namespace Pillar.Core.Layers;

/// <summary>
/// Describes the original and edited shared-stem geometry produced by one Direct Edit gesture.
/// </summary>
public sealed class SupportDirectEditSettings
{
    /// <summary>
    /// Creates settings whose original geometry is the same as its edited geometry.
    /// </summary>
    public SupportDirectEditSettings(Vector3 basePosition, float stemTopZ)
        : this(basePosition, stemTopZ, basePosition, stemTopZ)
    {
    }

    /// <summary>
    /// Creates validated original and edited geometry settings.
    /// </summary>
    public SupportDirectEditSettings(
        Vector3 basePosition,
        float stemTopZ,
        Vector3 originalBasePosition,
        float originalStemTopZ)
        : this(
            basePosition,
            stemTopZ,
            null,
            null,
            originalBasePosition,
            originalStemTopZ,
            null,
            null)
    {
    }

    /// <summary>
    /// Creates settings whose original geometry and explicit base attachment match the edited values.
    /// </summary>
    public SupportDirectEditSettings(
        Vector3 basePosition,
        float stemTopZ,
        SupportBaseAttachmentKind baseAttachmentKind,
        Vector3 baseDirection)
        : this(
            basePosition,
            stemTopZ,
            baseAttachmentKind,
            baseDirection,
            basePosition,
            stemTopZ,
            baseAttachmentKind,
            baseDirection)
    {
    }

    /// <summary>
    /// Creates validated original and edited geometry with optional explicit base attachments.
    /// </summary>
    public SupportDirectEditSettings(
        Vector3 basePosition,
        float stemTopZ,
        SupportBaseAttachmentKind? baseAttachmentKind,
        Vector3? baseDirection,
        Vector3 originalBasePosition,
        float originalStemTopZ,
        SupportBaseAttachmentKind? originalBaseAttachmentKind,
        Vector3? originalBaseDirection,
        float? modelBaseLength = null,
        float? originalModelBaseLength = null,
        Vector3? tipPosition = null,
        Vector3? headDirection = null,
        Vector3? originalTipPosition = null,
        Vector3? originalHeadDirection = null)
    {
        ValidateGeometry(basePosition, stemTopZ, nameof(basePosition), nameof(stemTopZ));
        ValidateGeometry(originalBasePosition, originalStemTopZ, nameof(originalBasePosition), nameof(originalStemTopZ));
        BasePosition = basePosition;
        StemTopZ = stemTopZ;
        BaseAttachmentKind = ValidateAttachment(baseAttachmentKind, nameof(baseAttachmentKind));
        BaseDirection = ValidateDirection(baseDirection, BaseAttachmentKind, nameof(baseDirection));
        ModelBaseLength = ValidateOptionalLength(modelBaseLength, nameof(modelBaseLength));
        OriginalBasePosition = originalBasePosition;
        OriginalStemTopZ = originalStemTopZ;
        OriginalBaseAttachmentKind = ValidateAttachment(originalBaseAttachmentKind, nameof(originalBaseAttachmentKind));
        OriginalBaseDirection = ValidateDirection(
            originalBaseDirection,
            OriginalBaseAttachmentKind,
            nameof(originalBaseDirection));
        OriginalModelBaseLength = ValidateOptionalLength(
            originalModelBaseLength,
            nameof(originalModelBaseLength));
        ValidateOptionalHeadGeometry(tipPosition, headDirection, nameof(tipPosition), nameof(headDirection));
        ValidateOptionalHeadGeometry(
            originalTipPosition,
            originalHeadDirection,
            nameof(originalTipPosition),
            nameof(originalHeadDirection));
        TipPosition = tipPosition;
        HeadDirection = NormalizeOptionalDirection(headDirection);
        OriginalTipPosition = originalTipPosition;
        OriginalHeadDirection = NormalizeOptionalDirection(originalHeadDirection);
    }

    /// <summary>
    /// Gets the edited base contact position on the build plate or model.
    /// </summary>
    public Vector3 BasePosition { get; }

    /// <summary>
    /// Gets the edited height of the shared stem joint.
    /// </summary>
    public float StemTopZ { get; }

    /// <summary>
    /// Gets the edited base attachment, or null when a legacy edit should preserve the current attachment.
    /// </summary>
    public SupportBaseAttachmentKind? BaseAttachmentKind { get; }

    /// <summary>
    /// Gets the edited model-base direction when an explicit attachment is stored.
    /// </summary>
    public Vector3? BaseDirection { get; }

    /// <summary>
    /// Gets the edited model-base length, or null when the current support profile length should be preserved.
    /// </summary>
    public float? ModelBaseLength { get; }

    /// <summary>
    /// Gets the pre-gesture base contact used to reverse or remove this modifier.
    /// </summary>
    public Vector3 OriginalBasePosition { get; }

    /// <summary>
    /// Gets the pre-gesture stem top used to reverse or remove this modifier.
    /// </summary>
    public float OriginalStemTopZ { get; }

    /// <summary>
    /// Gets the pre-gesture base attachment used to reverse or remove this modifier.
    /// </summary>
    public SupportBaseAttachmentKind? OriginalBaseAttachmentKind { get; }

    /// <summary>
    /// Gets the pre-gesture model-base direction used to reverse or remove this modifier.
    /// </summary>
    public Vector3? OriginalBaseDirection { get; }

    /// <summary>
    /// Gets the pre-gesture model-base length used to reverse or remove this modifier.
    /// </summary>
    public float? OriginalModelBaseLength { get; }

    /// <summary>
    /// Gets the edited model contact for the support head, or null when the contact is unchanged.
    /// </summary>
    public Vector3? TipPosition { get; }

    /// <summary>
    /// Gets the edited direction from the head base toward its model contact.
    /// </summary>
    public Vector3? HeadDirection { get; }

    /// <summary>
    /// Gets the pre-gesture model contact used to reverse a head edit.
    /// </summary>
    public Vector3? OriginalTipPosition { get; }

    /// <summary>
    /// Gets the pre-gesture head direction used to reverse a head edit.
    /// </summary>
    public Vector3? OriginalHeadDirection { get; }

    /// <summary>
    /// Creates a defensive copy for document and undo ownership.
    /// </summary>
    public SupportDirectEditSettings Clone()
    {
        return new SupportDirectEditSettings(
            BasePosition,
            StemTopZ,
            BaseAttachmentKind,
            BaseDirection,
            OriginalBasePosition,
            OriginalStemTopZ,
            OriginalBaseAttachmentKind,
            OriginalBaseDirection,
            ModelBaseLength,
            OriginalModelBaseLength,
            TipPosition,
            HeadDirection,
            OriginalTipPosition,
            OriginalHeadDirection);
    }

    /// <summary>
    /// Validates one base and stem-top pair.
    /// </summary>
    private static void ValidateGeometry(Vector3 basePosition, float stemTopZ, string baseParameterName, string topParameterName)
    {
        if (!float.IsFinite(basePosition.X) || !float.IsFinite(basePosition.Y) || !float.IsFinite(basePosition.Z))
        {
            throw new ArgumentException("A direct-edit base position must be finite.", baseParameterName);
        }

        if (!float.IsFinite(stemTopZ) || stemTopZ <= basePosition.Z)
        {
            throw new ArgumentOutOfRangeException(topParameterName, "A direct-edit stem top must be finite and above its base.");
        }
    }

    /// <summary>
    /// Rejects unknown optional attachment values before settings enter the modifier stack.
    /// </summary>
    private static SupportBaseAttachmentKind? ValidateAttachment(
        SupportBaseAttachmentKind? attachmentKind,
        string parameterName)
    {
        if (attachmentKind.HasValue && !Enum.IsDefined(attachmentKind.Value))
        {
            throw new ArgumentOutOfRangeException(parameterName, "The Direct Edit base attachment is not supported.");
        }

        return attachmentKind;
    }

    /// <summary>
    /// Normalizes an explicit attachment direction and preserves null for legacy geometry-only edits.
    /// </summary>
    private static Vector3? ValidateDirection(
        Vector3? direction,
        SupportBaseAttachmentKind? attachmentKind,
        string parameterName)
    {
        if (!attachmentKind.HasValue)
        {
            if (direction.HasValue)
            {
                throw new ArgumentException("A Direct Edit base direction requires an explicit attachment kind.", parameterName);
            }

            return null;
        }

        Vector3 resolvedDirection = attachmentKind.Value == SupportBaseAttachmentKind.BuildPlate
            ? Vector3.UnitZ
            : direction ?? Vector3.UnitZ;

        if (!float.IsFinite(resolvedDirection.X)
            || !float.IsFinite(resolvedDirection.Y)
            || !float.IsFinite(resolvedDirection.Z)
            || resolvedDirection.LengthSquared() <= 0.0f)
        {
            throw new ArgumentException("A Direct Edit base direction must be finite and non-zero.", parameterName);
        }

        resolvedDirection = Vector3.Normalize(resolvedDirection);

        if (resolvedDirection.Z <= 0.0f)
        {
            throw new ArgumentException("A Direct Edit model-base direction must point upward.", parameterName);
        }

        return resolvedDirection;
    }

    /// <summary>
    /// Rejects invalid optional model-base lengths while preserving null for legacy edits.
    /// </summary>
    private static float? ValidateOptionalLength(float? length, string parameterName)
    {
        if (length.HasValue
            && (!float.IsFinite(length.Value) || length.Value <= 0.0f))
        {
            throw new ArgumentOutOfRangeException(parameterName, "A Direct Edit model-base length must be finite and positive.");
        }

        return length;
    }

    /// <summary>
    /// Validates one optional contact and direction pair while preserving null for legacy stem-only edits.
    /// </summary>
    private static void ValidateOptionalHeadGeometry(
        Vector3? tipPosition,
        Vector3? headDirection,
        string tipParameterName,
        string directionParameterName)
    {
        if (tipPosition.HasValue != headDirection.HasValue)
        {
            throw new ArgumentException(
                "A Direct Edit head contact and direction must be stored together.",
                tipPosition.HasValue ? directionParameterName : tipParameterName);
        }

        if (!tipPosition.HasValue)
        {
            return;
        }

        Vector3 position = tipPosition.Value;
        Vector3 direction = headDirection!.Value;

        if (!IsFinite(position))
        {
            throw new ArgumentException("A Direct Edit head contact must be finite.", tipParameterName);
        }

        if (!IsFinite(direction) || direction.LengthSquared() <= 0.0f)
        {
            throw new ArgumentException("A Direct Edit head direction must be finite and non-zero.", directionParameterName);
        }
    }

    /// <summary>
    /// Normalizes an optional validated direction.
    /// </summary>
    private static Vector3? NormalizeOptionalDirection(Vector3? direction)
    {
        return direction.HasValue ? Vector3.Normalize(direction.Value) : null;
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
