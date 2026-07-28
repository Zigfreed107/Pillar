// SetTagColorCommand.cs
// Provides the undoable command boundary for changing one raft tag's display color.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;

namespace Pillar.Commands;

/// <summary>
/// Changes a tag's display color and can restore the previous color during undo.
/// </summary>
public sealed class SetTagColorCommand : ICadCommand
{
    private readonly CadDocument _document;
    private readonly TagEntity _tag;
    private readonly SupportLayerColor _oldColor;
    private readonly SupportLayerColor _newColor;

    /// <summary>
    /// Creates one completed tag color edit.
    /// </summary>
    public SetTagColorCommand(
        CadDocument document,
        TagEntity tag,
        SupportLayerColor oldColor,
        SupportLayerColor newColor)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _tag = tag ?? throw new ArgumentNullException(nameof(tag));
        _oldColor = oldColor;
        _newColor = newColor;
    }

    public string DisplayName
    {
        get { return "Change Tag Color"; }
    }

    /// <summary>
    /// Applies the requested tag color.
    /// </summary>
    public void Execute()
    {
        _document.SetTagColor(_tag, _newColor);
    }

    /// <summary>
    /// Restores the tag's previous color.
    /// </summary>
    public void Undo()
    {
        _document.SetTagColor(_tag, _oldColor);
    }
}
