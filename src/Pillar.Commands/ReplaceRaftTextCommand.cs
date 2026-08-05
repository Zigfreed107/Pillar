// ReplaceRaftTextCommand.cs
// Provides one undoable mutation for adding, updating, or removing model-owned raft text.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using System;

namespace Pillar.Commands;

/// <summary>
/// Atomically replaces one optional prior raft text with one completed state.
/// </summary>
public sealed class ReplaceRaftTextCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly RaftTextEntity? _oldRaftText;
    private readonly RaftTextEntity? _newRaftText;
    private bool _hasExecuted;

    /// <summary>
    /// Creates one reversible raft text replacement.
    /// </summary>
    public ReplaceRaftTextCommand(
        CadDocument document,
        RaftTextEntity? oldRaftText,
        RaftTextEntity? newRaftText)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _oldRaftText = oldRaftText;
        _newRaftText = newRaftText;
        Guid modelEntityId = newRaftText?.ModelEntityId ?? oldRaftText?.ModelEntityId
            ?? throw new ArgumentException("A raft text replacement requires an old or new entity.");

        if ((oldRaftText != null && oldRaftText.ModelEntityId != modelEntityId)
            || (newRaftText != null && newRaftText.ModelEntityId != modelEntityId))
        {
            throw new ArgumentException("A raft text replacement cannot move between models.");
        }

        DisplayName = oldRaftText == null
            ? "Add Raft Text"
            : newRaftText == null
                ? "Remove Raft Text"
                : "Update Raft Text";
    }

    public string DisplayName { get; }

    /// <summary>
    /// Applies the requested replacement.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        Replace(_oldRaftText, _newRaftText);
        _hasExecuted = true;
    }

    /// <summary>
    /// Restores the prior raft text.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        Replace(_newRaftText, _oldRaftText);
        _hasExecuted = false;
    }

    /// <summary>
    /// Changes the document as one logical edit.
    /// </summary>
    private void Replace(RaftTextEntity? removedRaftText, RaftTextEntity? addedRaftText)
    {
        using IDisposable batch = _document.BeginEntityBatchUpdate();
        if (removedRaftText != null) _document.RemoveEntity(removedRaftText);
        if (addedRaftText != null) _document.AddEntity(addedRaftText);
    }
}
