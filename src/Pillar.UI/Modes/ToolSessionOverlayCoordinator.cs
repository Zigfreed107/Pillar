// ToolSessionOverlayCoordinator.cs
// Centralizes active-tool overlay visibility so future tool panels can share one predictable shell workflow.
using System;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Modes;

/// <summary>
/// Describes optional companion panels that should be visible for an active tool session.
/// </summary>
public sealed class ToolSessionPanelSet
{
    /// <summary>
    /// Creates a panel set for one tool session.
    /// </summary>
    public ToolSessionPanelSet(bool showSupportPresetPanel)
    {
        ShowSupportPresetPanel = showSupportPresetPanel;
    }

    /// <summary>
    /// Gets a panel set with no companion panels visible.
    /// </summary>
    public static ToolSessionPanelSet None { get; } = new ToolSessionPanelSet(false);

    /// <summary>
    /// Gets a panel set that shows the compact Support Preset panel.
    /// </summary>
    public static ToolSessionPanelSet SupportPresets { get; } = new ToolSessionPanelSet(true);

    /// <summary>
    /// Gets whether the compact Support Preset panel should be shown with the active tool options.
    /// </summary>
    public bool ShowSupportPresetPanel { get; }
}

/// <summary>
/// Owns WPF overlay visibility for active tool sessions without depending on CAD document or rendering logic.
/// </summary>
public sealed class ToolSessionOverlayCoordinator
{
    private readonly FrameworkElement _modePanel;
    private readonly ContentControl _toolOptionsHost;
    private readonly FrameworkElement? _supportPresetPanel;

    /// <summary>
    /// Creates a coordinator for the shell's mode picker, tool options host, and optional companion panels.
    /// </summary>
    public ToolSessionOverlayCoordinator(
        FrameworkElement modePanel,
        ContentControl toolOptionsHost,
        FrameworkElement? supportPresetPanel)
    {
        _modePanel = modePanel ?? throw new ArgumentNullException(nameof(modePanel));
        _toolOptionsHost = toolOptionsHost ?? throw new ArgumentNullException(nameof(toolOptionsHost));
        _supportPresetPanel = supportPresetPanel;
    }

    /// <summary>
    /// Starts a tool session by hiding the mode picker and showing the supplied options control.
    /// </summary>
    public void BeginSession(Control optionsControl, ToolSessionPanelSet panels)
    {
        ShowSession(optionsControl, panels);
    }

    /// <summary>
    /// Replaces the active options control while preserving the active tool-session workflow.
    /// </summary>
    public void ReplaceOptions(Control optionsControl, ToolSessionPanelSet panels)
    {
        ShowSession(optionsControl, panels);
    }

    /// <summary>
    /// Ends the active tool session and restores the mode picker.
    /// </summary>
    public void EndSession()
    {
        _toolOptionsHost.Content = null;
        _toolOptionsHost.Visibility = Visibility.Collapsed;
        SetCompanionPanelVisibility(ToolSessionPanelSet.None);
        _modePanel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Clears only the options host when callers need to swap or suppress options without restoring the mode picker.
    /// </summary>
    public void HideOptionsHostOnly()
    {
        _toolOptionsHost.Content = null;
        _toolOptionsHost.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Applies the common active-session visibility state.
    /// </summary>
    private void ShowSession(Control optionsControl, ToolSessionPanelSet panels)
    {
        if (optionsControl == null)
        {
            throw new ArgumentNullException(nameof(optionsControl));
        }

        if (panels == null)
        {
            throw new ArgumentNullException(nameof(panels));
        }

        _modePanel.Visibility = Visibility.Collapsed;
        _toolOptionsHost.Content = optionsControl;
        _toolOptionsHost.Visibility = Visibility.Visible;
        SetCompanionPanelVisibility(panels);
    }

    /// <summary>
    /// Synchronizes optional companion panel visibility for the current tool session.
    /// </summary>
    private void SetCompanionPanelVisibility(ToolSessionPanelSet panels)
    {
        if (_supportPresetPanel != null)
        {
            _supportPresetPanel.Visibility = panels.ShowSupportPresetPanel
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
