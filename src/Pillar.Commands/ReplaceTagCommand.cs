// ReplaceTagCommand.cs
// Provides one undoable mutation for adding, updating, or removing a model-owned raft tag.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using System;

namespace Pillar.Commands;

/// <summary>
/// Atomically replaces one optional prior tag with one completed tag state.
/// </summary>
public sealed class ReplaceTagCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly TagEntity? _oldTag;
    private readonly TagEntity? _newTag;
    private bool _hasExecuted;

    /// <summary>
    /// Creates one reversible tag replacement.
    /// </summary>
    public ReplaceTagCommand(CadDocument document, TagEntity? oldTag, TagEntity? newTag)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _oldTag = oldTag;
        _newTag = newTag;
        Guid modelEntityId = newTag?.ModelEntityId ?? oldTag?.ModelEntityId
            ?? throw new ArgumentException("A tag replacement requires an old or new tag.");

        if ((oldTag != null && oldTag.ModelEntityId != modelEntityId)
            || (newTag != null && newTag.ModelEntityId != modelEntityId))
        {
            throw new ArgumentException("A tag replacement cannot move a tag between models.");
        }

        DisplayName = oldTag == null ? "Add Tag" : newTag == null ? "Remove Tag" : "Update Tag";
    }

    public string DisplayName { get; }

    /// <summary>
    /// Applies the requested tag replacement.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        Replace(_oldTag, _newTag);
        _hasExecuted = true;
    }

    /// <summary>
    /// Restores the prior tag.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        Replace(_newTag, _oldTag);
        _hasExecuted = false;
    }

    /// <summary>
    /// Changes the document as one logical edit.
    /// </summary>
    private void Replace(TagEntity? removedTag, TagEntity? addedTag)
    {
        using IDisposable batch = _document.BeginEntityBatchUpdate();
        if (removedTag != null) _document.RemoveEntity(removedTag);
        if (addedTag != null) _document.AddEntity(addedTag);
    }
}
