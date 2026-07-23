// SetRaftColorCommand.cs
// Provides the undoable command boundary for changing one raft's display color.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;

namespace Pillar.Commands;

/// <summary>
/// Changes a raft's display color and can restore the previous color during undo.
/// </summary>
public sealed class SetRaftColorCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly RaftEntity _raft;
    private readonly SupportLayerColor _oldColor;
    private readonly SupportLayerColor _newColor;

    /// <summary>
    /// Creates a command that owns one completed raft color edit.
    /// </summary>
    public SetRaftColorCommand(CadDocument document, RaftEntity raft, SupportLayerColor oldColor, SupportLayerColor newColor)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _raft = raft ?? throw new ArgumentNullException(nameof(raft));
        _oldColor = oldColor;
        _newColor = newColor;
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Change Raft Color"; }
    }

    /// <summary>
    /// Applies the requested raft color.
    /// </summary>
    public void Execute()
    {
        _document.SetRaftColor(_raft, _newColor);
    }

    /// <summary>
    /// Restores the raft color that existed before the edit.
    /// </summary>
    public void Undo()
    {
        _document.SetRaftColor(_raft, _oldColor);
    }
}