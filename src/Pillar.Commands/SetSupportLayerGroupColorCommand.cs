// SetSupportLayerGroupColorCommand.cs
// Provides the undoable command boundary for changing one support group's display color.
using Pillar.Core.Document;
using Pillar.Core.Layers;
using System;

namespace Pillar.Commands;

/// <summary>
/// Changes a support group's display color and can restore the previous color during undo.
/// </summary>
public sealed class SetSupportLayerGroupColorCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly SupportLayerGroup _supportLayerGroup;
    private readonly SupportLayerColor _oldColor;
    private readonly SupportLayerColor _newColor;

    /// <summary>
    /// Creates a command that owns one completed support group color edit.
    /// </summary>
    public SetSupportLayerGroupColorCommand(
        CadDocument document,
        SupportLayerGroup supportLayerGroup,
        SupportLayerColor oldColor,
        SupportLayerColor newColor)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _supportLayerGroup = supportLayerGroup ?? throw new ArgumentNullException(nameof(supportLayerGroup));
        _oldColor = oldColor;
        _newColor = newColor;
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return "Change Support Group Color"; }
    }

    /// <summary>
    /// Applies the requested support group color.
    /// </summary>
    public void Execute()
    {
        _document.SetSupportLayerGroupColor(_supportLayerGroup, _newColor);
    }

    /// <summary>
    /// Restores the support group color that existed before the edit.
    /// </summary>
    public void Undo()
    {
        _document.SetSupportLayerGroupColor(_supportLayerGroup, _oldColor);
    }
}
