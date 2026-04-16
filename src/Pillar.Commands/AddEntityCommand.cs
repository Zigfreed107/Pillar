// AddEntityCommand.cs
// Provides the undoable command boundary for adding one CAD entity to the document.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using System;

namespace Pillar.Commands;

/// <summary>
/// Adds a CAD entity to the document and can undo that addition.
/// </summary>
public sealed class AddEntityCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly CadEntity _entity;
    private bool _hasExecuted;

    /// <summary>
    /// Creates a command that owns adding the specified entity to the specified document.
    /// </summary>
    public AddEntityCommand(CadDocument document, CadEntity entity, string? displayName = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        DisplayName = CreateDisplayName(entity, displayName);
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Builds a clear fallback display name from the entity type when the caller does not provide one.
    /// </summary>
    private static string CreateDisplayName(CadEntity entity, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        string entityTypeName = entity.GetType().Name;

        if (entityTypeName.EndsWith("Entity", StringComparison.Ordinal))
        {
            entityTypeName = entityTypeName.Substring(0, entityTypeName.Length - "Entity".Length);
        }

        return $"Add {entityTypeName}";
    }

    /// <summary>
    /// Adds the entity to the document.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        _document.AddEntity(_entity);
        _hasExecuted = true;
    }

    /// <summary>
    /// Removes the entity that was added by this command.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        _document.RemoveEntity(_entity);
        _hasExecuted = false;
    }
}
