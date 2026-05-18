// RemoveSupportEntitiesCommand.cs
// Provides the undoable command boundary for deleting individual generated support entities without removing their support group metadata.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;

namespace Pillar.Commands;

/// <summary>
/// Removes individual support entities from the document and can restore them during undo.
/// </summary>
public sealed class RemoveSupportEntitiesCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly IReadOnlyList<SupportEntity> _supportEntities;
    private bool _hasExecuted;

    /// <summary>
    /// Creates a command that owns deleting one or more generated supports.
    /// </summary>
    public RemoveSupportEntitiesCommand(CadDocument document, IReadOnlyList<SupportEntity> supportEntities, string? displayName = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _supportEntities = supportEntities ?? throw new ArgumentNullException(nameof(supportEntities));

        if (_supportEntities.Count == 0)
        {
            throw new ArgumentException("At least one support entity is required.", nameof(supportEntities));
        }

        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? CreateDisplayName(_supportEntities.Count)
            : displayName.Trim();
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Removes the requested support entities while leaving their owning support group intact.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        for (int i = _supportEntities.Count - 1; i >= 0; i--)
        {
            _document.RemoveEntity(_supportEntities[i]);
        }

        _hasExecuted = true;
    }

    /// <summary>
    /// Restores the removed support entities to their original support groups.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        for (int i = 0; i < _supportEntities.Count; i++)
        {
            _document.AddEntity(_supportEntities[i]);
        }

        _hasExecuted = false;
    }

    /// <summary>
    /// Builds a concise command name from the number of supports being deleted.
    /// </summary>
    private static string CreateDisplayName(int supportCount)
    {
        if (supportCount == 1)
        {
            return "Delete Support";
        }

        return "Delete Supports";
    }
}
