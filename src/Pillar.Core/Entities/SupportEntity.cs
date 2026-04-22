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
        : base("Support")
    {
        if (supportLayerGroupId == Guid.Empty)
        {
            throw new ArgumentException("A support must belong to a support layer group.", nameof(supportLayerGroupId));
        }

        Profile = profile?.Clone() ?? throw new ArgumentNullException(nameof(profile));
        ValidateGeometry(tipPosition, basePosition, Profile);

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
        SupportProfile profile)
    {
        SupportEntity support = new SupportEntity(supportLayerGroupId, tipPosition, basePosition, profile);
        support.Id = id;
        support.Name = string.IsNullOrWhiteSpace(name) ? "Support" : name.Trim();
        return support;
    }

    /// <summary>
    /// Returns the support bounds used by selection and scene projection logic.
    /// </summary>
    public override (Vector3 Min, Vector3 Max) GetBounds()
    {
        float maximumRadius = MathF.Max(Profile.BaseDiameter, MathF.Max(Profile.BodyDiameter, Profile.TipDiameter)) * 0.5f;
        Vector3 radiusPadding = new Vector3(maximumRadius, maximumRadius, maximumRadius);
        Vector3 min = Vector3.Min(TipPosition, BasePosition) - radiusPadding;
        Vector3 max = Vector3.Max(TipPosition, BasePosition) + radiusPadding;
        return (min, max);
    }

    /// <summary>
    /// Validates the support axis and profile before the entity reaches document storage.
    /// </summary>
    private static void ValidateGeometry(Vector3 tipPosition, Vector3 basePosition, SupportProfile profile)
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

        if (totalLength <= profile.BaseHeight + profile.TipLength)
        {
            throw new ArgumentException("A support must be longer than its base height plus tip length.");
        }
    }
}
