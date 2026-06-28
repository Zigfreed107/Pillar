// ReplaceSupportModifiersCommand.cs
// Replaces one support layer's modifier stack as an undoable document edit.
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;

namespace Pillar.Commands;

/// <summary>
/// Updates the modifier stack owned by one support group without coupling tools to UI or rendering state.
/// </summary>
public sealed class ReplaceSupportModifiersCommand : ICadCommand
{
    private readonly SupportLayerGroup _supportLayerGroup;
    private readonly IReadOnlyList<SupportModifierDefinition> _oldModifiers;
    private readonly IReadOnlyList<SupportModifierDefinition> _newModifiers;
    private readonly string _displayName;
    private bool _hasExecuted;

    /// <summary>
    /// Creates an undoable support modifier stack replacement.
    /// </summary>
    public ReplaceSupportModifiersCommand(
        SupportLayerGroup supportLayerGroup,
        IReadOnlyList<SupportModifierDefinition> oldModifiers,
        IReadOnlyList<SupportModifierDefinition> newModifiers,
        string? displayName = null)
    {
        _supportLayerGroup = supportLayerGroup ?? throw new ArgumentNullException(nameof(supportLayerGroup));
        _oldModifiers = CloneModifiers(oldModifiers ?? throw new ArgumentNullException(nameof(oldModifiers)));
        _newModifiers = CloneModifiers(newModifiers ?? throw new ArgumentNullException(nameof(newModifiers)));
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "Update Support Edits" : displayName.Trim();
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return _displayName; }
    }

    /// <summary>
    /// Applies the new modifier stack.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        _supportLayerGroup.SetSupportModifiers(_newModifiers);
        _hasExecuted = true;
    }

    /// <summary>
    /// Restores the previous modifier stack.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        _supportLayerGroup.SetSupportModifiers(_oldModifiers);
        _hasExecuted = false;
    }

    /// <summary>
    /// Captures defensive copies so later caller mutations cannot affect undo state.
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
}
