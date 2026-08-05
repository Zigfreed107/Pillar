// SetRaftTextColorCommand.cs
// Provides the undoable command boundary for changing one raft text display color.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;

namespace Pillar.Commands;

/// <summary>
/// Changes a raft text color and can restore the previous color during undo.
/// </summary>
public sealed class SetRaftTextColorCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly RaftTextEntity _raftText;
    private readonly SupportLayerColor _oldColor;
    private readonly SupportLayerColor _newColor;

    /// <summary>
    /// Creates one completed raft text color edit.
    /// </summary>
    public SetRaftTextColorCommand(
        CadDocument document,
        RaftTextEntity raftText,
        SupportLayerColor oldColor,
        SupportLayerColor newColor)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _raftText = raftText ?? throw new ArgumentNullException(nameof(raftText));
        _oldColor = oldColor;
        _newColor = newColor;
    }

    public string DisplayName
    {
        get { return "Change Raft Text Color"; }
    }

    /// <summary>
    /// Applies the requested color.
    /// </summary>
    public void Execute()
    {
        _document.SetRaftTextColor(_raftText, _newColor);
    }

    /// <summary>
    /// Restores the prior color.
    /// </summary>
    public void Undo()
    {
        _document.SetRaftTextColor(_raftText, _oldColor);
    }
}
