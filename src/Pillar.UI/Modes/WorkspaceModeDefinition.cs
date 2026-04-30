// WorkspaceModeDefinition.cs
// Describes one workspace mode by pairing shell metadata with the viewport interaction tool it activates.
using Pillar.Core.Tools;
using System;

namespace Pillar.UI.Modes;

/// <summary>
/// Stores the metadata required to activate one workspace mode.
/// </summary>
public sealed class WorkspaceModeDefinition
{
    /// <summary>
    /// Creates a mode definition used by the shell to update active tool and user-facing status state.
    /// </summary>
    public WorkspaceModeDefinition(
        WorkspaceModeId id,
        string displayName,
        string statusText,
        bool isAvailable,
        ITool? tool)
    {
        Id = id;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        StatusText = statusText ?? throw new ArgumentNullException(nameof(statusText));
        IsAvailable = isAvailable;
        Tool = tool;
    }

    /// <summary>
    /// Gets the stable identifier used by toolbar buttons and mode switching.
    /// </summary>
    public WorkspaceModeId Id { get; }

    /// <summary>
    /// Gets the user-facing mode name displayed by toolbar and overlay UI.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the status bar text shown when this mode becomes active.
    /// </summary>
    public string StatusText { get; }

    /// <summary>
    /// Gets whether the mode can currently be activated by the user.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Gets the viewport interaction tool used while the mode is active.
    /// </summary>
    public ITool? Tool { get; }
}
