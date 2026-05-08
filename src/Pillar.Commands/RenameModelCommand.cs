// RenameModelCommand.cs
// Provides the undoable command boundary for changing an imported model's user-visible layer name.
using Pillar.Core.Entities;
using System;

namespace Pillar.Commands;

/// <summary>
/// Renames an imported model and can restore the previous name during undo.
/// </summary>
public sealed class RenameModelCommand : ICadCommand
{
    private const string DefaultModelName = "Imported mesh";

    private readonly MeshEntity _mesh;
    private readonly string _oldName;
    private readonly string _newName;

    /// <summary>
    /// Creates a command that owns one completed imported-model rename edit.
    /// </summary>
    public RenameModelCommand(MeshEntity mesh, string oldName, string newName)
    {
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        _oldName = NormalizeName(oldName);
        _newName = NormalizeName(newName);
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Rename Model"; }
    }

    /// <summary>
    /// Applies the requested model name.
    /// </summary>
    public void Execute()
    {
        _mesh.Name = _newName;
    }

    /// <summary>
    /// Restores the model name that existed before the rename.
    /// </summary>
    public void Undo()
    {
        _mesh.Name = _oldName;
    }

    /// <summary>
    /// Keeps command-applied names consistent with imported model name rules.
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return DefaultModelName;
        }

        return name.Trim();
    }
}
