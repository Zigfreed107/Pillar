// SupportEntity.cs
// Defines the renderer-agnostic domain data for one procedural resin-print support owned by a support layer group.
using Pillar.Core.Supports;
using System;
using System.Numerics;

namespace Pillar.Core.Entities;

/// <summary>
/// Represents one procedural support placed onto an imported model.
/// </summary>
public sealed class SupportEntity : CadEntity
{
    /// <summary>
    /// Creates one validated support entity with the default support display name.
    /// </summary>
    public SupportEntity(Guid supportLayerGroupId, Vector3 tipPosition, Vector3 basePosition, SupportProfile profile)
        : this(supportLayerGroupId, tipPosition, basePosition, Vector3.UnitZ, profile)
    {
    }

    /// <summary>
    /// Creates one validated support entity with a stored head direction.
    /// </summary>
    public SupportEntity(Guid supportLayerGroupId, Vector3 tipPosition, Vector3 basePosition, Vector3 headDirection, SupportProfile profile)
        : this(supportLayerGroupId, tipPosition, basePosition, headDirection, 0.0f, Vector3.UnitZ, profile)
    {
    }

    /// <summary>
    /// Creates one validated support entity with stored head and branch directions.
    /// </summary>
    public SupportEntity(
        Guid supportLayerGroupId,
        Vector3 tipPosition,
        Vector3 basePosition,
        Vector3 headDirection,
        float branchLength,
        Vector3 branchDirection,
        SupportProfile profile)
        : base("Support")
    {
        if (supportLayerGroupId == Guid.Empty)
        {
            throw new ArgumentException("A support must belong to a support layer group.", nameof(supportLayerGroupId));
        }

        Profile = profile?.Clone() ?? throw new ArgumentNullException(nameof(profile));
        HeadDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(headDirection, Profile);
        BranchLength = ValidateBranchLength(branchLength, nameof(branchLength));
        BranchDirection = NormalizeOrDefault(branchDirection, Vector3.UnitZ);
        ValidateGeometry(tipPosition, basePosition, HeadDirection, BranchDirection, Profile);

        SupportLayerGroupId = supportLayerGroupId;
        TipPosition = tipPosition;
        BasePosition = basePosition;
    }

    /// <summary>
    /// Gets the support layer group that owns this support.
    /// </summary>
    public Guid SupportLayerGroupId { get; }

    /// <summary>
    /// Gets the tip contact position on the supported mesh.
    /// </summary>
    public Vector3 TipPosition { get; }

    /// <summary>
    /// Gets the base position on the build plane.
    /// </summary>
    public Vector3 BasePosition { get; }

    /// <summary>
    /// Gets the normalized direction from the head joint toward the model contact point.
    /// </summary>
    public Vector3 HeadDirection { get; }

    /// <summary>
    /// Gets the optional branch cylinder length between the stem joint and head joint.
    /// </summary>
    public float BranchLength { get; }

    /// <summary>
    /// Gets the normalized direction from the stem joint toward the head joint.
    /// </summary>
    public Vector3 BranchDirection { get; }

    /// <summary>
    /// Gets the geometric dimensions for this support.
    /// </summary>
    public SupportProfile Profile { get; }

    /// <summary>
    /// Recreates one saved support while preserving its document identity and name.
    /// </summary>
    public static SupportEntity CreateLoaded(
        Guid id,
        string name,
        Guid supportLayerGroupId,
        Vector3 tipPosition,
        Vector3 basePosition,
        Vector3 headDirection,
        float branchLength,
        Vector3 branchDirection,
        SupportProfile profile)
    {
        SupportEntity support = new SupportEntity(supportLayerGroupId, tipPosition, basePosition, headDirection, branchLength, branchDirection, profile);
        support.Id = id;
        support.Name = string.IsNullOrWhiteSpace(name) ? "Support" : name.Trim();
        return support;
    }

    /// <summary>
    /// Returns the support bounds used by selection and scene projection logic.
    /// </summary>
    public override (Vector3 Min, Vector3 Max) GetBounds()
    {
        float maximumRadius = MathF.Max(
            Profile.BaseBottomRadius,
            MathF.Max(
                Profile.StemBottomDiameter,
                MathF.Max(Profile.StemTopDiameter, MathF.Max(Profile.HeadBottomDiameter, Profile.HeadTopDiameter))) * 0.5f);
        Vector3 radiusPadding = new Vector3(maximumRadius, maximumRadius, maximumRadius);
        Vector3 headBottom = TipPosition - (HeadDirection * Profile.HeadHeight);
        Vector3 stemTop = BranchLength > 0.0f
            ? headBottom - (BranchDirection * BranchLength)
            : headBottom;
        Vector3 penetrationTip = TipPosition + (HeadDirection * Profile.HeadPenetrationDepth);
        Vector3 min = Vector3.Min(Vector3.Min(Vector3.Min(Vector3.Min(TipPosition, penetrationTip), BasePosition), headBottom), stemTop) - radiusPadding;
        Vector3 max = Vector3.Max(Vector3.Max(TipPosition, BasePosition), stemTop) + radiusPadding;
        max = Vector3.Max(max, penetrationTip + radiusPadding);
        max = Vector3.Max(max, headBottom + radiusPadding);
        return (min, max);
    }

    /// <summary>
    /// Validates the support axis and profile before the entity reaches document storage.
    /// </summary>
    private static void ValidateGeometry(Vector3 tipPosition, Vector3 basePosition, Vector3 headDirection, Vector3 branchDirection, SupportProfile profile)
    {
        if (basePosition.Z > tipPosition.Z)
        {
            throw new ArgumentException("A support base cannot be above its tip in v1 support placement.");
        }

        float totalLength = Vector3.Distance(basePosition, tipPosition);

        if (float.IsNaN(totalLength) || float.IsInfinity(totalLength) || totalLength <= 0.0f)
        {
            throw new ArgumentException("A support must have a non-zero finite length.");
        }

        if (totalLength <= 0.0f)
        {
            throw new ArgumentException("A support must have a positive length.");
        }

        if (!float.IsFinite(headDirection.X) || !float.IsFinite(headDirection.Y) || !float.IsFinite(headDirection.Z))
        {
            throw new ArgumentException("A support head direction must be finite.");
        }

        if (headDirection.LengthSquared() <= 0.0f)
        {
            throw new ArgumentException("A support head direction must be non-zero.");
        }

        if (!float.IsFinite(branchDirection.X) || !float.IsFinite(branchDirection.Y) || !float.IsFinite(branchDirection.Z))
        {
            throw new ArgumentException("A support branch direction must be finite.");
        }

        if (branchDirection.LengthSquared() <= 0.0f)
        {
            throw new ArgumentException("A support branch direction must be non-zero.");
        }
    }

    /// <summary>
    /// Rejects invalid branch lengths before they reach geometry code.
    /// </summary>
    private static float ValidateBranchLength(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0.0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Support branch length must be finite and non-negative.");
        }

        return value;
    }

    /// <summary>
    /// Normalizes a direction, returning a stable fallback for invalid or zero vectors.
    /// </summary>
    private static Vector3 NormalizeOrDefault(Vector3 direction, Vector3 fallback)
    {
        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z))
        {
            return fallback;
        }

        if (direction.LengthSquared() <= 0.000001f)
        {
            return fallback;
        }

        return Vector3.Normalize(direction);
    }
}
