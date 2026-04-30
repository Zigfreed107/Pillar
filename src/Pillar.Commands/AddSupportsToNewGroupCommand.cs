// AddSupportsToNewGroupCommand.cs
// Adds a new support layer group together with multiple supports so one generated support pattern remains one undoable user action.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;

namespace Pillar.Commands;

/// <summary>
/// Adds one support layer group and all supports generated for that group as a single undoable command.
/// </summary>
public sealed class AddSupportsToNewGroupCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly SupportLayerGroup _supportLayerGroup;
    private readonly IReadOnlyList<SupportEntity> _supportEntities;
    private bool _hasExecuted;

    /// <summary>
    /// Creates a command that owns adding a generated support group and all of its child supports.
    /// </summary>
    public AddSupportsToNewGroupCommand(CadDocument document, SupportLayerGroup supportLayerGroup, IReadOnlyList<SupportEntity> supportEntities, string displayName)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _supportLayerGroup = supportLayerGroup ?? throw new ArgumentNullException(nameof(supportLayerGroup));
        _supportEntities = supportEntities ?? throw new ArgumentNullException(nameof(supportEntities));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Add Supports" : displayName.Trim();

        if (_supportEntities.Count == 0)
        {
            throw new ArgumentException("At least one support entity is required.", nameof(supportEntities));
        }

        for (int i = 0; i < _supportEntities.Count; i++)
        {
            if (_supportEntities[i].SupportLayerGroupId != _supportLayerGroup.Id)
            {
                throw new ArgumentException("Every support entity must belong to the supplied support group.", nameof(supportEntities));
            }
        }
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Adds the new support group first, then adds each generated support into it.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        _document.AddSupportLayerGroup(_supportLayerGroup);

        for (int i = 0; i < _supportEntities.Count; i++)
        {
            _document.AddEntity(_supportEntities[i]);
        }

        _hasExecuted = true;
    }

    /// <summary>
    /// Removes generated supports before removing their now-empty support group.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        for (int i = _supportEntities.Count - 1; i >= 0; i--)
        {
            _document.RemoveEntity(_supportEntities[i]);
        }

        _document.RemoveSupportLayerGroup(_supportLayerGroup);
        _hasExecuted = false;
    }
}
