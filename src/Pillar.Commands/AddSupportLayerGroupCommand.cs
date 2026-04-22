// AddSupportLayerGroupCommand.cs
// Provides the undoable command boundary for adding one support group under an imported model layer.
using Pillar.Core.Document;
using Pillar.Core.Layers;
using System;

namespace Pillar.Commands;

/// <summary>
/// Adds a support layer group to the document and can undo that addition.
/// </summary>
public sealed class AddSupportLayerGroupCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly SupportLayerGroup _supportLayerGroup;
    private bool _hasExecuted;

    /// <summary>
    /// Creates a command that owns adding the supplied support group.
    /// </summary>
    public AddSupportLayerGroupCommand(CadDocument document, SupportLayerGroup supportLayerGroup)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _supportLayerGroup = supportLayerGroup ?? throw new ArgumentNullException(nameof(supportLayerGroup));
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Add Support Group"; }
    }

    /// <summary>
    /// Adds the support group to the document.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        _document.AddSupportLayerGroup(_supportLayerGroup);
        _hasExecuted = true;
    }

    /// <summary>
    /// Removes the support group that this command added.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        _document.RemoveSupportLayerGroup(_supportLayerGroup);
        _hasExecuted = false;
    }
}
