// UpdateContourSupportGroupCommand.cs
// Replaces the generated children and stored parameters for an existing Contour Support group as one undoable edit.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;

namespace Pillar.Commands;

/// <summary>
/// Updates one Contour Support generated group by replacing its generated supports and parametric settings.
/// </summary>
public sealed class UpdateContourSupportGroupCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly SupportLayerGroup _supportLayerGroup;
    private readonly ContourSupportSettings _oldSettings;
    private readonly ContourSupportSettings _newSettings;
    private readonly IReadOnlyList<SupportEntity> _oldSupportEntities;
    private readonly IReadOnlyList<SupportEntity> _newSupportEntities;
    private bool _hasExecuted;

    /// <summary>
    /// Creates an undoable Contour Support group update.
    /// </summary>
    public UpdateContourSupportGroupCommand(
        CadDocument document,
        SupportLayerGroup supportLayerGroup,
        ContourSupportSettings oldSettings,
        IReadOnlyList<SupportEntity> oldSupportEntities,
        ContourSupportSettings newSettings,
        IReadOnlyList<SupportEntity> newSupportEntities)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _supportLayerGroup = supportLayerGroup ?? throw new ArgumentNullException(nameof(supportLayerGroup));
        _oldSettings = oldSettings?.Clone() ?? throw new ArgumentNullException(nameof(oldSettings));
        _newSettings = newSettings?.Clone() ?? throw new ArgumentNullException(nameof(newSettings));
        _oldSupportEntities = oldSupportEntities ?? throw new ArgumentNullException(nameof(oldSupportEntities));
        _newSupportEntities = newSupportEntities ?? throw new ArgumentNullException(nameof(newSupportEntities));

        ValidateSupportOwnership(_oldSupportEntities, _supportLayerGroup.Id, nameof(oldSupportEntities), false);
        ValidateSupportOwnership(_newSupportEntities, _supportLayerGroup.Id, nameof(newSupportEntities), true);
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Update Contour Supports"; }
    }

    /// <summary>
    /// Replaces the previous generated supports with the new generated supports and settings.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        ReplaceSupports(_oldSupportEntities, _newSupportEntities, _newSettings);
        _hasExecuted = true;
    }

    /// <summary>
    /// Restores the previous generated supports and settings.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        ReplaceSupports(_newSupportEntities, _oldSupportEntities, _oldSettings);
        _hasExecuted = false;
    }

    /// <summary>
    /// Removes one generated support set, stores settings, and adds the replacement support set.
    /// </summary>
    private void ReplaceSupports(
        IReadOnlyList<SupportEntity> supportsToRemove,
        IReadOnlyList<SupportEntity> supportsToAdd,
        ContourSupportSettings settings)
    {
        for (int i = supportsToRemove.Count - 1; i >= 0; i--)
        {
            _document.RemoveEntity(supportsToRemove[i]);
        }

        _supportLayerGroup.SetContourSupportSettings(settings);

        for (int i = 0; i < supportsToAdd.Count; i++)
        {
            _document.AddEntity(supportsToAdd[i]);
        }
    }

    /// <summary>
    /// Verifies every generated support belongs to the group being updated.
    /// </summary>
    private static void ValidateSupportOwnership(
        IReadOnlyList<SupportEntity> supportEntities,
        Guid supportLayerGroupId,
        string parameterName,
        bool requireAtLeastOneSupport)
    {
        if (requireAtLeastOneSupport && supportEntities.Count == 0)
        {
            throw new ArgumentException("At least one generated support is required.", parameterName);
        }

        for (int i = 0; i < supportEntities.Count; i++)
        {
            if (supportEntities[i].SupportLayerGroupId != supportLayerGroupId)
            {
                throw new ArgumentException("Every generated support must belong to the supplied support group.", parameterName);
            }
        }
    }
}
