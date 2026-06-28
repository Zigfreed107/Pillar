// ReplaceSupportLayerOutputAndModifiersCommand.cs
// Replaces one support layer's evaluated support entities and modifier stack as a single undoable edit.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;

namespace Pillar.Commands;

/// <summary>
/// Atomically swaps a support group's current output and saved modifier stack.
/// </summary>
public sealed class ReplaceSupportLayerOutputAndModifiersCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly SupportLayerGroup _supportLayerGroup;
    private readonly IReadOnlyList<SupportEntity> _oldSupportEntities;
    private readonly IReadOnlyList<SupportEntity> _newSupportEntities;
    private readonly IReadOnlyList<SupportModifierDefinition> _oldModifiers;
    private readonly IReadOnlyList<SupportModifierDefinition> _newModifiers;
    private readonly string _displayName;
    private bool _hasExecuted;

    /// <summary>
    /// Creates an undoable output and modifier replacement.
    /// </summary>
    public ReplaceSupportLayerOutputAndModifiersCommand(
        CadDocument document,
        SupportLayerGroup supportLayerGroup,
        IReadOnlyList<SupportEntity> oldSupportEntities,
        IReadOnlyList<SupportEntity> newSupportEntities,
        IReadOnlyList<SupportModifierDefinition> oldModifiers,
        IReadOnlyList<SupportModifierDefinition> newModifiers,
        string displayName)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _supportLayerGroup = supportLayerGroup ?? throw new ArgumentNullException(nameof(supportLayerGroup));
        _oldSupportEntities = oldSupportEntities ?? throw new ArgumentNullException(nameof(oldSupportEntities));
        _newSupportEntities = newSupportEntities ?? throw new ArgumentNullException(nameof(newSupportEntities));
        _oldModifiers = CloneModifiers(oldModifiers ?? throw new ArgumentNullException(nameof(oldModifiers)));
        _newModifiers = CloneModifiers(newModifiers ?? throw new ArgumentNullException(nameof(newModifiers)));
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "Update Support Edits" : displayName.Trim();

        ValidateSupportOwnership(_oldSupportEntities, _supportLayerGroup.Id, nameof(oldSupportEntities));
        ValidateSupportOwnership(_newSupportEntities, _supportLayerGroup.Id, nameof(newSupportEntities));
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return _displayName; }
    }

    /// <summary>
    /// Applies the new support output and modifier stack.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        ReplaceSupports(_oldSupportEntities, _newSupportEntities, _newModifiers);
        _hasExecuted = true;
    }

    /// <summary>
    /// Restores the previous support output and modifier stack.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        ReplaceSupports(_newSupportEntities, _oldSupportEntities, _oldModifiers);
        _hasExecuted = false;
    }

    /// <summary>
    /// Removes one output set, stores modifiers, and adds the replacement output set.
    /// </summary>
    private void ReplaceSupports(
        IReadOnlyList<SupportEntity> supportsToRemove,
        IReadOnlyList<SupportEntity> supportsToAdd,
        IReadOnlyList<SupportModifierDefinition> modifiers)
    {
        for (int i = supportsToRemove.Count - 1; i >= 0; i--)
        {
            _document.RemoveEntity(supportsToRemove[i]);
        }

        _supportLayerGroup.SetSupportModifiers(modifiers);

        for (int i = 0; i < supportsToAdd.Count; i++)
        {
            _document.AddEntity(supportsToAdd[i]);
        }
    }

    /// <summary>
    /// Captures defensive modifier copies so caller changes cannot affect undo.
    /// </summary>
    private static IReadOnlyList<SupportModifierDefinition> CloneModifiers(IReadOnlyList<SupportModifierDefinition> modifiers)
    {
        List<SupportModifierDefinition> result = new List<SupportModifierDefinition>(modifiers.Count);

        for (int i = 0; i < modifiers.Count; i++)
        {
            result.Add(modifiers[i].Clone());
        }

        return result;
    }

    /// <summary>
    /// Verifies every output support belongs to the edited group.
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
