// SupportGroupRegeneration.cs
// Carries a renderer-agnostic snapshot of regenerated support geometry so transform commands can update supports atomically.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;

namespace Pillar.Core.Supports;

/// <summary>
/// Describes the old and new generated support state for one support layer group.
/// </summary>
public sealed class SupportGroupRegeneration
{
    /// <summary>
    /// Creates an immutable support regeneration snapshot for one support group.
    /// </summary>
    public SupportGroupRegeneration(
        SupportLayerGroup supportLayerGroup,
        IReadOnlyList<SupportEntity> oldSupportEntities,
        IReadOnlyList<SupportEntity> newSupportEntities,
        RingSupportSettings? oldRingSupportSettings,
        RingSupportSettings? newRingSupportSettings)
        : this(
            supportLayerGroup,
            oldSupportEntities,
            newSupportEntities,
            oldRingSupportSettings,
            newRingSupportSettings,
            null,
            null,
            null,
            null,
            null,
            null)
    {
    }

    /// <summary>
    /// Creates an immutable support regeneration snapshot for one support group.
    /// </summary>
    public SupportGroupRegeneration(
        SupportLayerGroup supportLayerGroup,
        IReadOnlyList<SupportEntity> oldSupportEntities,
        IReadOnlyList<SupportEntity> newSupportEntities,
        RingSupportSettings? oldRingSupportSettings,
        RingSupportSettings? newRingSupportSettings,
        LineSupportSettings? oldLineSupportSettings,
        LineSupportSettings? newLineSupportSettings)
        : this(
            supportLayerGroup,
            oldSupportEntities,
            newSupportEntities,
            oldRingSupportSettings,
            newRingSupportSettings,
            oldLineSupportSettings,
            newLineSupportSettings,
            null,
            null,
            null,
            null)
    {
    }

    /// <summary>
    /// Creates an immutable support regeneration snapshot for one support group.
    /// </summary>
    public SupportGroupRegeneration(
        SupportLayerGroup supportLayerGroup,
        IReadOnlyList<SupportEntity> oldSupportEntities,
        IReadOnlyList<SupportEntity> newSupportEntities,
        RingSupportSettings? oldRingSupportSettings,
        RingSupportSettings? newRingSupportSettings,
        LineSupportSettings? oldLineSupportSettings,
        LineSupportSettings? newLineSupportSettings,
        ContourSupportSettings? oldContourSupportSettings,
        ContourSupportSettings? newContourSupportSettings)
        : this(
            supportLayerGroup,
            oldSupportEntities,
            newSupportEntities,
            oldRingSupportSettings,
            newRingSupportSettings,
            oldLineSupportSettings,
            newLineSupportSettings,
            oldContourSupportSettings,
            newContourSupportSettings,
            null,
            null)
    {
    }

    /// <summary>
    /// Creates an immutable support regeneration snapshot for one support group.
    /// </summary>
    public SupportGroupRegeneration(
        SupportLayerGroup supportLayerGroup,
        IReadOnlyList<SupportEntity> oldSupportEntities,
        IReadOnlyList<SupportEntity> newSupportEntities,
        RingSupportSettings? oldRingSupportSettings,
        RingSupportSettings? newRingSupportSettings,
        LineSupportSettings? oldLineSupportSettings,
        LineSupportSettings? newLineSupportSettings,
        ContourSupportSettings? oldContourSupportSettings,
        ContourSupportSettings? newContourSupportSettings,
        AreaSupportSettings? oldAreaSupportSettings,
        AreaSupportSettings? newAreaSupportSettings)
    {
        SupportLayerGroup = supportLayerGroup ?? throw new ArgumentNullException(nameof(supportLayerGroup));
        OldSupportEntities = oldSupportEntities ?? throw new ArgumentNullException(nameof(oldSupportEntities));
        NewSupportEntities = newSupportEntities ?? throw new ArgumentNullException(nameof(newSupportEntities));
        OldRingSupportSettings = oldRingSupportSettings?.Clone();
        NewRingSupportSettings = newRingSupportSettings?.Clone();
        OldLineSupportSettings = oldLineSupportSettings?.Clone();
        NewLineSupportSettings = newLineSupportSettings?.Clone();
        OldContourSupportSettings = oldContourSupportSettings?.Clone();
        NewContourSupportSettings = newContourSupportSettings?.Clone();
        OldAreaSupportSettings = oldAreaSupportSettings?.Clone();
        NewAreaSupportSettings = newAreaSupportSettings?.Clone();

        ValidateSupportOwnership(OldSupportEntities, SupportLayerGroup.Id, nameof(oldSupportEntities));
        ValidateSupportOwnership(NewSupportEntities, SupportLayerGroup.Id, nameof(newSupportEntities));
    }

    /// <summary>
    /// Gets the support group whose children will be regenerated.
    /// </summary>
    public SupportLayerGroup SupportLayerGroup { get; }

    /// <summary>
    /// Gets the support entities present before the owning model transform changes.
    /// </summary>
    public IReadOnlyList<SupportEntity> OldSupportEntities { get; }

    /// <summary>
    /// Gets the support entities regenerated for the owning model's new transform.
    /// </summary>
    public IReadOnlyList<SupportEntity> NewSupportEntities { get; }

    /// <summary>
    /// Gets the Ring Support generator settings present before the transform, when this is a Ring group.
    /// </summary>
    public RingSupportSettings? OldRingSupportSettings { get; }

    /// <summary>
    /// Gets the Ring Support generator settings transformed for the new model transform, when this is a Ring group.
    /// </summary>
    public RingSupportSettings? NewRingSupportSettings { get; }

    /// <summary>
    /// Gets the Line Support generator settings present before the transform, when this is a Line group.
    /// </summary>
    public LineSupportSettings? OldLineSupportSettings { get; }

    /// <summary>
    /// Gets the Line Support generator settings transformed for the new model transform, when this is a Line group.
    /// </summary>
    public LineSupportSettings? NewLineSupportSettings { get; }

    /// <summary>
    /// Gets the Contour Support generator settings present before the transform, when this is a Contour group.
    /// </summary>
    public ContourSupportSettings? OldContourSupportSettings { get; }

    /// <summary>
    /// Gets the Contour Support generator settings transformed for the new model transform, when this is a Contour group.
    /// </summary>
    public ContourSupportSettings? NewContourSupportSettings { get; }

    /// <summary>
    /// Gets the Area Support generator settings present before the transform, when this is an Area group.
    /// </summary>
    public AreaSupportSettings? OldAreaSupportSettings { get; }

    /// <summary>
    /// Gets the Area Support generator settings transformed for the new model transform, when this is an Area group.
    /// </summary>
    public AreaSupportSettings? NewAreaSupportSettings { get; }

    /// <summary>
    /// Verifies every support entity belongs to the group carried by this regeneration snapshot.
    /// </summary>
    private static void ValidateSupportOwnership(IReadOnlyList<SupportEntity> supportEntities, Guid supportLayerGroupId, string parameterName)
    {
        for (int i = 0; i < supportEntities.Count; i++)
        {
            if (supportEntities[i].SupportLayerGroupId != supportLayerGroupId)
            {
                throw new ArgumentException("Every support entity must belong to the supplied support group.", parameterName);
            }
        }
    }
}
