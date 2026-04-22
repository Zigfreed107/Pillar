// ImportMeshWithSupportGroupCommand.cs
// Adds an imported mesh and its initial support layer group as one undoable document operation.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;

namespace Pillar.Commands;

/// <summary>
/// Imports a mesh entity and creates its first support group in one undoable command.
/// </summary>
public sealed class ImportMeshWithSupportGroupCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly MeshEntity _meshEntity;
    private readonly SupportLayerGroup _initialSupportLayerGroup;
    private bool _hasExecuted;

    /// <summary>
    /// Creates a command that owns importing one mesh and its initial layer metadata.
    /// </summary>
    public ImportMeshWithSupportGroupCommand(
        CadDocument document,
        MeshEntity meshEntity,
        SupportLayerGroup initialSupportLayerGroup)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _meshEntity = meshEntity ?? throw new ArgumentNullException(nameof(meshEntity));
        _initialSupportLayerGroup = initialSupportLayerGroup ?? throw new ArgumentNullException(nameof(initialSupportLayerGroup));

        if (_initialSupportLayerGroup.ModelEntityId != _meshEntity.Id)
        {
            throw new ArgumentException("The initial support group must belong to the imported mesh.", nameof(initialSupportLayerGroup));
        }
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Import Mesh"; }
    }

    /// <summary>
    /// Adds the mesh and its initial support group to the document.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        _document.AddEntity(_meshEntity);
        _document.AddSupportLayerGroup(_initialSupportLayerGroup);
        _hasExecuted = true;
    }

    /// <summary>
    /// Removes the initial support group and imported mesh from the document.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        _document.RemoveSupportLayerGroup(_initialSupportLayerGroup);
        _document.RemoveEntity(_meshEntity);
        _hasExecuted = false;
    }
}
