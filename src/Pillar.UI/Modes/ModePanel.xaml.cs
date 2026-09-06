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
    /// Describes a support operation requested from the ribbon panel.
    /// </summary>
    public sealed class SupportOperationRequestedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates event data for one support operation request.
        /// </summary>
        public SupportOperationRequestedEventArgs(ManualSupportOperationKind operationKind)
        {
            OperationKind = operationKind;
        }

        /// <summary>
        /// Gets the support operation affected by the toggle request.
        /// </summary>
        public ManualSupportOperationKind OperationKind { get; }
    }

    /// <summary>
    /// Raised when a ribbon support-operation button requests a workflow change.
    /// </summary>
    public event EventHandler<SupportOperationRequestedEventArgs>? SupportOperationRequested;

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
    /// Selects the point-support operation and shows its options.
    /// </summary>
    private void PointSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        SupportOperationRequested?.Invoke(
            this,
            new SupportOperationRequestedEventArgs(ManualSupportOperationKind.Point));
        RaiseToolSelected("Point Support");
    }

    /// <summary>
    /// Shows options for the Transform Translate tool.
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
    /// Requests that the selected model be placed on the build plate immediately.
    /// </summary>
    private void MoveToPlateButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseToolSelected("Move to Plate");
    }

    /// <summary>
    /// Selects the planned line-support operation and shows its mock options.
    /// </summary>
    private void LineSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SupportOperationRequested?.Invoke(
            this,
            new SupportOperationRequestedEventArgs(ManualSupportOperationKind.Line));
        RaiseToolSelected("Line Support");
    }

    /// <summary>
    /// Selects the ring-support operation and shows its options.
    /// </summary>
    private void RingSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SupportOperationRequested?.Invoke(
            this,
            new SupportOperationRequestedEventArgs(ManualSupportOperationKind.Ring));
        RaiseToolSelected("Ring Support");
    }

    /// <summary>
    /// Selects the contour-support operation and shows its options.
    /// </summary>
    private void ContourSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SupportOperationRequested?.Invoke(
            this,
            new SupportOperationRequestedEventArgs(ManualSupportOperationKind.Contour));
        RaiseToolSelected("Contour Support");
    }

    /// <summary>
    /// Selects the area-support operation and shows its options.
    /// </summary>
    private void AreaSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SupportOperationRequested?.Invoke(
            this,
            new SupportOperationRequestedEventArgs(ManualSupportOperationKind.Area));
        RaiseToolSelected("Area Support");
    }

    /// <summary>
    /// Selects support clustering and shows its options.
    /// </summary>
    private void ClusterSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SupportOperationRequested?.Invoke(
            this,
            new SupportOperationRequestedEventArgs(ManualSupportOperationKind.None));
        RaiseToolSelected("Cluster Supports");
    }

    /// <summary>
    /// Selects support bracing and shows its options.
    /// </summary>
    private void BraceSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SupportOperationRequested?.Invoke(
            this,
            new SupportOperationRequestedEventArgs(ManualSupportOperationKind.None));
        RaiseToolSelected("Brace Supports");
    }

    /// <summary>
    /// Publishes one selected tool name to the owning shell.
    /// </summary>
    /// <summary>
    /// Selects direct support editing and activates its viewport gizmos.
    /// </summary>
    private void DirectEditSupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        SupportOperationRequested?.Invoke(
            this,
            new SupportOperationRequestedEventArgs(ManualSupportOperationKind.None));
        RaiseToolSelected("Direct Edit Supports");
    }

    /// <summary>
    /// Opens the procedural raft tool for the current eligible model.
    /// </summary>
    private void RaftButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseToolSelected("Raft");
    }

    /// <summary>
    /// Opens the raft Tag tool for the current eligible model.
    /// </summary>
    private void TagButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseToolSelected("Tag");
    }

    /// <summary>
    /// Opens the Raft Text tool for the current eligible model.
    /// </summary>
    private void RaftTextButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseToolSelected("Raft Text");
    }

    private void RaiseToolSelected(string toolName)
    {
        ToolSelected?.Invoke(this, new ToolSelectedEventArgs(toolName));
    }
}
