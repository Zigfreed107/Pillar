// AddSupportToNewGroupCommand.cs
// Adds a new support layer group together with its first support so one placement remains one undoable user action.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;

namespace Pillar.Commands;

/// <summary>
/// Adds one new support group and one support entity that belongs to it.
/// </summary>
public sealed class AddSupportToNewGroupCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly SupportLayerGroup _supportLayerGroup;
    private readonly SupportEntity _supportEntity;
    private bool _hasExecuted;

    /// <summary>
    /// Creates a command that owns adding the first support into a new group.
    /// </summary>
    public AddSupportToNewGroupCommand(CadDocument document, SupportLayerGroup supportLayerGroup, SupportEntity supportEntity)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _supportLayerGroup = supportLayerGroup ?? throw new ArgumentNullException(nameof(supportLayerGroup));
        _supportEntity = supportEntity ?? throw new ArgumentNullException(nameof(supportEntity));

        if (_supportEntity.SupportLayerGroupId != _supportLayerGroup.Id)
        {
            throw new ArgumentException("The support entity must belong to the supplied support group.", nameof(supportEntity));
        }
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Add Point Support"; }
    }

    /// <summary>
    /// Adds the new support group first, then adds the first support into it.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        _document.AddSupportLayerGroup(_supportLayerGroup);
        _document.AddEntity(_supportEntity);
        _hasExecuted = true;
    }

    /// <summary>
    /// Removes the first support, then removes the empty support group created for it.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        _document.RemoveEntity(_supportEntity);
        _document.RemoveSupportLayerGroup(_supportLayerGroup);
        _hasExecuted = false;
    }
}
