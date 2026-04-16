// RenameEntityCommand.cs
// Provides the undoable command boundary for changing an entity's user-visible name.
using Pillar.Core.Entities;
using System;

namespace Pillar.Commands;

/// <summary>
/// Renames a CAD entity and can restore the previous name during undo.
/// </summary>
public sealed class RenameEntityCommand : ICadCommand
{
    private readonly CadEntity _entity;
    private readonly string _oldName;
    private readonly string _newName;

    /// <summary>
    /// Creates a command that owns one completed entity rename edit.
    /// </summary>
    public RenameEntityCommand(CadEntity entity, string oldName, string newName)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        _oldName = NormalizeName(oldName);
        _newName = NormalizeName(newName);
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Rename Entity"; }
    }

    /// <summary>
    /// Applies the requested entity name.
    /// </summary>
    public void Execute()
    {
        _entity.Name = _newName;
    }

    /// <summary>
    /// Restores the entity name that existed before the rename.
    /// </summary>
    public void Undo()
    {
        _entity.Name = _oldName;
    }

    /// <summary>
    /// Keeps command-applied names consistent with the rest of the document model.
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Entity";
        }

        return name.Trim();
    }
}
