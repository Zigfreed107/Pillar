// ReplaceRaftCommand.cs
// Provides one undoable mutation for adding, updating, or removing a model-owned raft.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using System;

namespace Pillar.Commands;

/// <summary>
/// Atomically replaces the optional raft owned by one model.
/// </summary>
public sealed class ReplaceRaftCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly RaftEntity? _oldRaft;
    private readonly RaftEntity? _newRaft;
    private bool _hasExecuted;

    /// <summary>
    /// Creates one reversible raft replacement.
    /// </summary>
    public ReplaceRaftCommand(CadDocument document, RaftEntity? oldRaft, RaftEntity? newRaft)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _oldRaft = oldRaft;
        _newRaft = newRaft;
        Guid modelEntityId = newRaft?.ModelEntityId ?? oldRaft?.ModelEntityId
            ?? throw new ArgumentException("A raft replacement requires an old or new raft.");

        if ((oldRaft != null && oldRaft.ModelEntityId != modelEntityId)
            || (newRaft != null && newRaft.ModelEntityId != modelEntityId))
        {
            throw new ArgumentException("A raft replacement cannot move a raft between models.");
        }

        DisplayName = oldRaft == null ? "Add Raft" : newRaft == null ? "Remove Raft" : "Update Raft";
    }

    public string DisplayName { get; }

    /// <summary>
    /// Applies the requested raft replacement.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted) return;
        Replace(_oldRaft, _newRaft);
        _hasExecuted = true;
    }

    /// <summary>
    /// Restores the prior raft.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted) return;
        Replace(_newRaft, _oldRaft);
        _hasExecuted = false;
    }

    /// <summary>
    /// Changes the document as one logical edit.
    /// </summary>
    private void Replace(RaftEntity? removedRaft, RaftEntity? addedRaft)
    {
        using IDisposable batch = _document.BeginEntityBatchUpdate();
        if (removedRaft != null) _document.RemoveEntity(removedRaft);
        if (addedRaft != null) _document.AddEntity(addedRaft);
    }
}
