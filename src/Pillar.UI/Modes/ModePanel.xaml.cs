// ModePanel.xaml.cs
// Provides the code-behind shell for the always-visible ribbon-style mode selection panel and forwards ribbon tool toggle changes to the owning window.
using System;
using System.Windows;
using System.Windows.Controls;
using Pillar.Core.Tools;

namespace Pillar.UI.Modes;

/// <summary>
/// Interaction logic for the ribbon-style workflow mode panel.
/// </summary>
public partial class ModePanel : UserControl
{
    private bool _isSynchronizingSupportOperationButtons;

    /// <summary>
    /// Describes a tool selection made from the ribbon panel.
    /// </summary>
    public sealed class ToolSelectedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates event data for one selected tool.
        /// </summary>
        public ToolSelectedEventArgs(string toolName)
        {
            ToolName = toolName;
        }

        /// <summary>
        /// Gets the display name of the selected tool.
        /// </summary>
        public string ToolName { get; }
    }

    /// <summary>
    /// Describes a support operation toggle change requested from the ribbon panel.
    /// </summary>
    public sealed class SupportOperationToggleRequestedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates event data for one support operation toggle request.
        /// </summary>
        public SupportOperationToggleRequestedEventArgs(ManualSupportOperationKind operationKind, bool isEnabled)
        {
            OperationKind = operationKind;
            IsEnabled = isEnabled;
        }

        /// <summary>
        /// Gets the support operation affected by the toggle request.
        /// </summary>
        public ManualSupportOperationKind OperationKind { get; }

        /// <summary>
        /// Gets whether the ribbon requested that the operation be enabled or disabled.
        /// </summary>
        public bool IsEnabled { get; }
    }

    /// <summary>
    /// Raised when the ribbon support-operation toggles request a workflow change.
    /// </summary>
    public event EventHandler<SupportOperationToggleRequestedEventArgs>? SupportOperationToggleRequested;

    /// <summary>
    /// Raised when any visible ribbon tool is selected and its options panel should be shown.
    /// </summary>
    public event EventHandler<ToolSelectedEventArgs>? ToolSelected;

    /// <summary>
    /// Creates the ribbon-style workflow mode panel.
    /// </summary>
    public ModePanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Synchronizes ribbon toggle state with the shell-owned active support operation.
    /// </summary>
    public void SetSelectedSupportOperation(ManualSupportOperationKind operationKind)
    {
        _isSynchronizingSupportOperationButtons = true;
        PointSupportButton.IsChecked = operationKind == ManualSupportOperationKind.Point;
        _isSynchronizingSupportOperationButtons = false;
    }

    /// <summary>
    /// Requests point-support activation when the point toggle is turned on.
    /// </summary>
    private void PointSupportButton_Checked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingSupportOperationButtons)
        {
            return;
        }

        SupportOperationToggleRequested?.Invoke(
            this,
            new SupportOperationToggleRequestedEventArgs(ManualSupportOperationKind.Point, true));
        RaiseToolSelected("Point Support");
    }

    /// <summary>
    /// Requests point-support deactivation when the point toggle is turned off.
    /// </summary>
    private void PointSupportButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingSupportOperationButtons)
        {
            return;
        }

        SupportOperationToggleRequested?.Invoke(
            this,
            new SupportOperationToggleRequestedEventArgs(ManualSupportOperationKind.Point, false));
    }

    /// <summary>
    /// Shows mock options for the planned translate tool.
    /// </summary>
    private void TranslateButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseToolSelected("Translate");
    }

    /// <summary>
    /// Shows mock options for the planned rotate tool.
    /// </summary>
    private void RotateButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseToolSelected("Rotate");
    }

    /// <summary>
    /// Shows mock options for the planned scale tool.
    /// </summary>
    private void ScaleButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseToolSelected("Scale");
    }

    /// <summary>
    /// Selects the planned line-support operation and shows its mock options.
    /// </summary>
    private void LineSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SupportOperationToggleRequested?.Invoke(
            this,
            new SupportOperationToggleRequestedEventArgs(ManualSupportOperationKind.Line, true));
        RaiseToolSelected("Line Support");
    }

    /// <summary>
    /// Selects the ring-support operation and shows its options.
    /// </summary>
    private void RingSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SupportOperationToggleRequested?.Invoke(
            this,
            new SupportOperationToggleRequestedEventArgs(ManualSupportOperationKind.Ring, true));
        RaiseToolSelected("Ring Support");
    }

    /// <summary>
    /// Publishes one selected tool name to the owning shell.
    /// </summary>
    private void RaiseToolSelected(string toolName)
    {
        ToolSelected?.Invoke(this, new ToolSelectedEventArgs(toolName));
    }
}
