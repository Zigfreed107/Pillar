// WorkspaceModeDefinition.cs
// Describes a UI workspace mode by pairing its viewport tool with the overlay panel shown above the viewer.
using Pillar.Core.Tools;
using System;
using System.Windows;

namespace Pillar.UI.Modes;

/// <summary>
/// Stores the metadata and factories required to activate one workspace mode.
/// </summary>
public sealed class WorkspaceModeDefinition
{
    private readonly Func<FrameworkElement> _overlayFactory;
    private FrameworkElement? _overlay;

    /// <summary>
    /// Creates a mode definition used by the shell to update toolbar state, active tool, and overlay content.
    /// </summary>
    public WorkspaceModeDefinition(
        WorkspaceModeId id,
        string displayName,
        string statusText,
        bool isAvailable,
        ITool? tool,
        Func<FrameworkElement> overlayFactory)
    {
        Id = id;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        StatusText = statusText ?? throw new ArgumentNullException(nameof(statusText));
        IsAvailable = isAvailable;
        Tool = tool;
        _overlayFactory = overlayFactory ?? throw new ArgumentNullException(nameof(overlayFactory));
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

    /// <summary>
    /// Gets or creates the WPF overlay panel associated with this mode.
    /// </summary>
    public FrameworkElement GetOverlay()
    {
        if (_overlay == null)
        {
            _overlay = _overlayFactory();
        }

        return _overlay;
    }
}
