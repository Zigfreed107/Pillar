// UpdateLineSupportGroupCommand.cs
// Replaces the generated children and stored parameters for an existing Line Support group as one undoable edit.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using System;
using System.Collections.Generic;

namespace Pillar.Commands;

/// <summary>
/// Updates one Line Support generated group by replacing its generated supports and parametric settings.
/// </summary>
public sealed class UpdateLineSupportGroupCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly SupportLayerGroup _supportLayerGroup;
    private readonly LineSupportSettings _oldSettings;
    private readonly LineSupportSettings _newSettings;
    private readonly IReadOnlyList<SupportEntity> _oldSupportEntities;
    private readonly IReadOnlyList<SupportEntity> _newSupportEntities;
    private readonly IReadOnlyList<SupportModifierDefinition> _oldModifiers;
    private readonly IReadOnlyList<SupportModifierDefinition> _newModifiers;
    private readonly int _oldSourceGeneratorRevision;
    private readonly int _newSourceGeneratorRevision;
    private bool _hasExecuted;

    /// <summary>
    /// Creates an undoable Line Support group update.
    /// </summary>
    public UpdateLineSupportGroupCommand(
        CadDocument document,
        SupportLayerGroup supportLayerGroup,
        LineSupportSettings oldSettings,
        IReadOnlyList<SupportEntity> oldSupportEntities,
        LineSupportSettings newSettings,
        IReadOnlyList<SupportEntity> newSupportEntities)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _supportLayerGroup = supportLayerGroup ?? throw new ArgumentNullException(nameof(supportLayerGroup));
        _oldSettings = oldSettings?.Clone() ?? throw new ArgumentNullException(nameof(oldSettings));
        _newSettings = newSettings?.Clone() ?? throw new ArgumentNullException(nameof(newSettings));
        _oldSupportEntities = oldSupportEntities ?? throw new ArgumentNullException(nameof(oldSupportEntities));
        IReadOnlyList<SupportEntity> rawNewSupportEntities = newSupportEntities ?? throw new ArgumentNullException(nameof(newSupportEntities));

        ValidateSupportOwnership(_oldSupportEntities, _supportLayerGroup.Id, nameof(oldSupportEntities), false);
        ValidateSupportOwnership(rawNewSupportEntities, _supportLayerGroup.Id, nameof(newSupportEntities), true);

        _oldSourceGeneratorRevision = _supportLayerGroup.SourceGeneratorRevision;
        _newSourceGeneratorRevision = _oldSourceGeneratorRevision + 1;
        _oldModifiers = _supportLayerGroup.SupportModifiers;
        _newModifiers = CreateModifiersForRegeneratedOutput(_oldModifiers);
        _newSupportEntities = SupportModifierPipeline.ApplyModifiers(rawNewSupportEntities, _newModifiers);
        ValidateSupportOwnership(_newSupportEntities, _supportLayerGroup.Id, nameof(newSupportEntities), true);
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Update Line Supports"; }
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

        ReplaceSupports(_oldSupportEntities, _newSupportEntities, _newSettings, _newModifiers, _newSourceGeneratorRevision);
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

        ReplaceSupports(_newSupportEntities, _oldSupportEntities, _oldSettings, _oldModifiers, _oldSourceGeneratorRevision);
        _hasExecuted = false;
    }

    /// <summary>
    /// Removes one generated support set, stores settings, and adds the replacement support set.
    /// </summary>
    private void ReplaceSupports(
        IReadOnlyList<SupportEntity> supportsToRemove,
        IReadOnlyList<SupportEntity> supportsToAdd,
        LineSupportSettings settings,
        IReadOnlyList<SupportModifierDefinition> modifiers,
        int sourceGeneratorRevision)
    {
        for (int i = supportsToRemove.Count - 1; i >= 0; i--)
        {
            _document.RemoveEntity(supportsToRemove[i]);
        }

        _supportLayerGroup.SetLineSupportSettings(settings);
        _supportLayerGroup.SetSourceGeneratorRevision(sourceGeneratorRevision);
        _supportLayerGroup.SetSupportModifiers(modifiers);

        for (int i = 0; i < supportsToAdd.Count; i++)
        {
            _document.AddEntity(supportsToAdd[i]);
        }
    }

    /// <summary>
    /// Keeps replayable whole-layer modifiers and discards revision-bound selection modifiers after regeneration.
    /// </summary>
    private static IReadOnlyList<SupportModifierDefinition> CreateModifiersForRegeneratedOutput(IReadOnlyList<SupportModifierDefinition> oldModifiers)
    {
        List<SupportModifierDefinition> retainedModifiers = new List<SupportModifierDefinition>();

        for (int i = 0; i < oldModifiers.Count; i++)
        {
            if (oldModifiers[i].Scope == SupportModifierScope.WholeLayer)
            {
                retainedModifiers.Add(oldModifiers[i]);
            }
        }

        return retainedModifiers;
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
