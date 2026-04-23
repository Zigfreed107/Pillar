// MainWindow.Modes.cs
// Owns workspace mode registration and shell-level mode switching so tool activation and overlay selection stay centralized in the UI shell.
using Pillar.Core.Tools;
using Pillar.UI.Modes;
using System;
using System.Windows;

namespace Pillar.UI;

public partial class MainWindow
{
    /// <summary>
    /// Activates selection mode from the mode toolbar.
    /// </summary>
    private void SelectMode_Click(object sender, RoutedEventArgs e)
    {
        SetActiveMode(WorkspaceModeId.Select);
    }

    /// <summary>
    /// Activates line drawing mode from the mode toolbar.
    /// </summary>
    private void LineMode_Click(object sender, RoutedEventArgs e)
    {
        SetActiveMode(WorkspaceModeId.Line);
    }

    /// <summary>
    /// Rechecks the toolbar when the planned transform mode is clicked programmatically.
    /// </summary>
    private void TransformMode_Click(object sender, RoutedEventArgs e)
    {
        SetActiveMode(WorkspaceModeId.Transform);
    }

    /// <summary>
    /// Rechecks the toolbar when the planned support mode is clicked programmatically.
    /// </summary>
    private void SupportMode_Click(object sender, RoutedEventArgs e)
    {
        SetActiveMode(WorkspaceModeId.ManualSupport);
    }

    /// <summary>
    /// Registers the available and planned workspace modes used by the toolbar and overlay host.
    /// </summary>
    private void RegisterWorkspaceModes()
    {
        _modeDefinitions.Add(
            WorkspaceModeId.Select,
            new WorkspaceModeDefinition(
                WorkspaceModeId.Select,
                "Select",
                "Select tool active",
                true,
                _selectTool,
                () => new SelectModeOverlay()));

        _modeDefinitions.Add(
            WorkspaceModeId.Line,
            new WorkspaceModeDefinition(
                WorkspaceModeId.Line,
                "Line",
                "Line tool active: click two points",
                true,
                _lineTool,
                () => new LineModeOverlay()));

        _modeDefinitions.Add(
            WorkspaceModeId.Transform,
            new WorkspaceModeDefinition(
                WorkspaceModeId.Transform,
                "Transform",
                "Transform mode is planned",
                false,
                null,
                () => new PlaceholderModeOverlay
                {
                    ModeName = "Transform",
                    Message = "Transform tools are planned but not available yet."
                }));

        _modeDefinitions.Add(
            WorkspaceModeId.ManualSupport,
            new WorkspaceModeDefinition(
                WorkspaceModeId.ManualSupport,
                "Manual Support",
                "Manual support mode: choose an operation",
                true,
                _manualSupportTool,
                CreateManualSupportModeOverlay));
    }

    /// <summary>
    /// Creates and wires the Manual Support overlay to the Manual Support tool operation state.
    /// </summary>
    private ManualSupportModeOverlay CreateManualSupportModeOverlay()
    {
        ManualSupportModeOverlay overlay = new ManualSupportModeOverlay();
        overlay.OperationChanged += ManualSupportModeOverlay_OperationChanged;

        return overlay;
    }

    /// <summary>
    /// Applies Manual Support overlay selections to the active Manual Support tool.
    /// </summary>
    private void ManualSupportModeOverlay_OperationChanged(object? sender, ManualSupportOperationChangedEventArgs e)
    {
        _ = sender;

        _manualSupportTool.SetActiveOperation(e.OperationKind);

        if (_activeModeId != WorkspaceModeId.ManualSupport)
        {
            return;
        }

        string statusText = GetManualSupportStatusText(e.OperationKind);
        _activeToolStatusText = statusText;
        _viewModel.SetStatusText(statusText);
        _viewModel.SetToolPanelText(statusText);
    }

    /// <summary>
    /// Applies support-operation status requests to the shell while Manual Support mode is active.
    /// </summary>
    private void ManualSupportTool_StatusMessageRequested(string statusMessage)
    {
        if (_activeModeId != WorkspaceModeId.ManualSupport)
        {
            return;
        }

        _viewModel.SetStatusText(statusMessage);
        _viewModel.SetToolPanelText(statusMessage);
    }

    /// <summary>
    /// Gets the status text that should be shown for a workspace mode activation.
    /// </summary>
    private string GetWorkspaceModeStatusText(WorkspaceModeDefinition mode)
    {
        if (mode.Id == WorkspaceModeId.ManualSupport)
        {
            return GetManualSupportStatusText(_manualSupportTool.ActiveOperationKind);
        }

        return mode.StatusText;
    }

    /// <summary>
    /// Converts a Manual Support operation selection into user-facing shell guidance.
    /// </summary>
    private static string GetManualSupportStatusText(ManualSupportOperationKind operationKind)
    {
        switch (operationKind)
        {
            case ManualSupportOperationKind.Point:
                return "Manual support mode: point support operation active";

            case ManualSupportOperationKind.Line:
                return "Manual support mode: line support operation active";

            case ManualSupportOperationKind.Circle:
                return "Manual support mode: circle support operation active";

            case ManualSupportOperationKind.None:
            default:
                return "Manual support mode: choose an operation";
        }
    }

    /// <summary>
    /// Switches the active workspace mode and updates tool, overlay, toolbar, and shell guidance state.
    /// </summary>
    private void SetActiveMode(WorkspaceModeId modeId)
    {
        WorkspaceModeDefinition mode = _modeDefinitions[modeId];

        if (!mode.IsAvailable || mode.Tool == null)
        {
            UpdateModeToolbarState(_activeModeId);
            _viewModel.SetStatusText($"{mode.DisplayName} mode is not available yet");
            return;
        }

        _activeModeId = modeId;
        string statusText = GetWorkspaceModeStatusText(mode);
        _activeToolStatusText = statusText;
        _toolManager.SetTool(mode.Tool);
        ModePanelHost.Content = mode.GetOverlay();
        UpdateModeToolbarState(modeId);
        _viewModel.SetStatusText(statusText);
        _viewModel.SetToolPanelText(statusText);
    }

    /// <summary>
    /// Keeps the mode toolbar as a visual reflection of the active workspace mode.
    /// </summary>
    private void UpdateModeToolbarState(WorkspaceModeId activeModeId)
    {
        SelectMode.IsEnabled = _modeDefinitions[WorkspaceModeId.Select].IsAvailable;
        LineMode.IsEnabled = _modeDefinitions[WorkspaceModeId.Line].IsAvailable;
        TransformMode.IsEnabled = _modeDefinitions[WorkspaceModeId.Transform].IsAvailable;
        SupportMode.IsEnabled = _modeDefinitions[WorkspaceModeId.ManualSupport].IsAvailable;

        SelectMode.IsChecked = activeModeId == WorkspaceModeId.Select;
        LineMode.IsChecked = activeModeId == WorkspaceModeId.Line;
        TransformMode.IsChecked = activeModeId == WorkspaceModeId.Transform;
        SupportMode.IsChecked = activeModeId == WorkspaceModeId.ManualSupport;
    }
}
