// RenameSupportLayerGroupCommand.cs
// Provides the undoable command boundary for changing a support group's user-visible name.
using Pillar.Core.Document;
using Pillar.Core.Layers;
using System;

namespace Pillar.Commands;

/// <summary>
/// Renames a support layer group and can restore the previous name during undo.
/// </summary>
public sealed class RenameSupportLayerGroupCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly SupportLayerGroup _supportLayerGroup;
    private readonly string _oldName;
    private readonly string _newName;

    /// <summary>
    /// Creates a command that owns one completed support group rename edit.
    /// </summary>
    public RenameSupportLayerGroupCommand(
        CadDocument document,
        SupportLayerGroup supportLayerGroup,
        string oldName,
        string newName)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _supportLayerGroup = supportLayerGroup ?? throw new ArgumentNullException(nameof(supportLayerGroup));
        _oldName = NormalizeName(oldName);
        _newName = NormalizeName(newName);
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Rename Support Group"; }
    }

    /// <summary>
    /// Applies the requested support group name.
    /// </summary>
    public void Execute()
    {
        _document.RenameSupportLayerGroup(_supportLayerGroup, _newName);
    }

    /// <summary>
    /// Restores the support group name that existed before the rename.
    /// </summary>
    public void Undo()
    {
        _document.RenameSupportLayerGroup(_supportLayerGroup, _oldName);
    }

    /// <summary>
    /// Keeps command-applied names consistent with support group name rules.
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Supports Group";
        }

        return name.Trim();
    }
}
