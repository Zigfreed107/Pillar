// RemoveModelWithSupportGroupsCommand.cs
// Removes one imported model and its support layer groups as a single undoable document operation.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;

namespace Pillar.Commands;

/// <summary>
/// Removes an imported model and all support groups owned by that model.
/// </summary>
public sealed class RemoveModelWithSupportGroupsCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly MeshEntity _meshEntity;
    private readonly List<SupportLayerGroup> _supportLayerGroups;
    private readonly List<SupportEntity> _supportEntities;
    private bool _hasExecuted;

    /// <summary>
    /// Creates a command that owns removing the supplied model and captured support groups.
    /// </summary>
    public RemoveModelWithSupportGroupsCommand(
        CadDocument document,
        MeshEntity meshEntity,
        IEnumerable<SupportLayerGroup> supportLayerGroups)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _meshEntity = meshEntity ?? throw new ArgumentNullException(nameof(meshEntity));

        if (supportLayerGroups == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroups));
        }

        _supportLayerGroups = new List<SupportLayerGroup>(supportLayerGroups);
        _supportEntities = new List<SupportEntity>();

        foreach (SupportLayerGroup supportLayerGroup in _supportLayerGroups)
        {
            _supportEntities.AddRange(_document.GetSupportEntitiesForGroup(supportLayerGroup.Id));
        }
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Remove Model"; }
    }

    /// <summary>
    /// Removes the imported model, which also removes its document-owned support groups.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        _document.RemoveEntity(_meshEntity);
        _hasExecuted = true;
    }

    /// <summary>
    /// Restores the imported model and the support groups it owned.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        _document.AddEntity(_meshEntity);

        foreach (SupportLayerGroup supportLayerGroup in _supportLayerGroups)
        {
            _document.AddSupportLayerGroup(supportLayerGroup);
        }

        foreach (SupportEntity supportEntity in _supportEntities)
        {
            _document.AddEntity(supportEntity);
        }

        _hasExecuted = false;
    }
}
