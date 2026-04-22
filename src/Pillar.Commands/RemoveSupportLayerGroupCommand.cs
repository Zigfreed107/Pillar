// RemoveSupportLayerGroupCommand.cs
// Provides the undoable command boundary for removing one support group from an imported model layer.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;

namespace Pillar.Commands;

/// <summary>
/// Removes a support layer group from the document and can restore it during undo.
/// </summary>
public sealed class RemoveSupportLayerGroupCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly SupportLayerGroup _supportLayerGroup;
    private readonly List<SupportEntity> _supportEntities;
    private bool _hasExecuted;

    /// <summary>
    /// Creates a command that owns removing the supplied support group.
    /// </summary>
    public RemoveSupportLayerGroupCommand(CadDocument document, SupportLayerGroup supportLayerGroup)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _supportLayerGroup = supportLayerGroup ?? throw new ArgumentNullException(nameof(supportLayerGroup));
        _supportEntities = new List<SupportEntity>(_document.GetSupportEntitiesForGroup(_supportLayerGroup.Id));
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Remove Support Group"; }
    }

    /// <summary>
    /// Removes the support group from the document.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        _document.RemoveSupportLayerGroup(_supportLayerGroup);
        _hasExecuted = true;
    }

    /// <summary>
    /// Restores the support group to the document.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        _document.AddSupportLayerGroup(_supportLayerGroup);

        foreach (SupportEntity supportEntity in _supportEntities)
        {
            _document.AddEntity(supportEntity);
        }

        _hasExecuted = false;
    }
}
