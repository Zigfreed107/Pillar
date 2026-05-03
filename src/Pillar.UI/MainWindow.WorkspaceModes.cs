// MainWindow.WorkspaceModes.cs
// Owns shell-level workspace mode registration, activation, and ribbon synchronization without reviving legacy per-mode overlays.
using Pillar.Core.Layers;
using Pillar.UI.Layers;
using Pillar.Core.Tools;
using Pillar.UI.Modes;
using System;

namespace Pillar.UI;

public partial class MainWindow
{
    /// <summary>
    /// Registers the available and planned workspace modes used by the shell.
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
                _selectTool));

        _modeDefinitions.Add(
            WorkspaceModeId.Line,
            new WorkspaceModeDefinition(
                WorkspaceModeId.Line,
                "Line",
                "Line tool active: click two points",
                true,
                _lineTool));

        _modeDefinitions.Add(
            WorkspaceModeId.Transform,
            new WorkspaceModeDefinition(
                WorkspaceModeId.Transform,
                "Transform",
                "Transform mode is planned",
                false,
                null));

        _modeDefinitions.Add(
            WorkspaceModeId.ManualSupport,
            new WorkspaceModeDefinition(
                WorkspaceModeId.ManualSupport,
                "Manual Support",
                "Manual support mode: choose an operation",
                true,
                _manualSupportTool));
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

            case ManualSupportOperationKind.Ring:
                return "Manual support mode: ring support operation active";

            case ManualSupportOperationKind.None:
            default:
                return "Manual support mode: choose an operation";
        }
    }

    /// <summary>
    /// Applies one support-operation toggle request from the ribbon-style mode panel.
    /// </summary>
    private void WorkflowModePanelOverlay_SupportOperationToggleRequested(object? sender, ModePanel.SupportOperationToggleRequestedEventArgs e)
    {
        _ = sender;

        ManualSupportOperationKind requestedOperation = e.IsEnabled
            ? e.OperationKind
            : ManualSupportOperationKind.None;

        ApplyManualSupportOperationSelection(requestedOperation, e.IsEnabled);
    }

    /// <summary>
    /// Shows the active-tool options mock-up for one selected ribbon tool.
    /// </summary>
    private void WorkflowModePanelOverlay_ToolSelected(object? sender, ModePanel.ToolSelectedEventArgs e)
    {
        _ = sender;
        ShowToolOptionsPanel(e.ToolName);
    }

    /// <summary>
    /// Refreshes the Ring Support preview when its spacing option changes.
    /// </summary>
    private void ToolOptionsPanelOverlay_RingSupportOptionsChanged(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Ring)
        {
            return;
        }

        RunWithWaitCursor(_manualSupportTool.RefreshActiveOperationPreview);
    }

    /// <summary>
    /// Applies the current Ring Support preview as a new support group.
    /// </summary>
    private void ToolOptionsPanelOverlay_RingSupportApplyRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Ring)
        {
            _viewModel.SetStatusText("Choose the Ring Support tool before applying ring supports.");
            return;
        }

        bool didApply = false;
        RunWithWaitCursor(() => didApply = _manualSupportTool.ApplyActiveOperation());

        if (!didApply)
        {
            return;
        }

        ExitRingSupportMode();
    }

    /// <summary>
    /// Cancels the current Ring Support operation and exits the Ring Support tool.
    /// </summary>
    private void ToolOptionsPanelOverlay_RingSupportCancelRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Ring)
        {
            return;
        }

        _manualSupportTool.Cancel();
        ExitRingSupportMode();
    }

    /// <summary>
    /// Updates and reveals the Tool Options Panel for the selected tool.
    /// </summary>
    private void ShowToolOptionsPanel(string selectedToolName)
    {
        if (IsModePanelSelectionPromptVisible())
        {
            HideToolOptionsPanel();
            return;
        }

        ToolOptionsPanelOverlay.SetSelectedTool(selectedToolName);
        ToolOptionsPanelOverlay.Visibility = System.Windows.Visibility.Visible;
    }

    /// <summary>
    /// Hides the Tool Options Panel when no tool is selected.
    /// </summary>
    private void HideToolOptionsPanel()
    {
        ToolOptionsPanelOverlay.Visibility = System.Windows.Visibility.Collapsed;
    }

    /// <summary>
    /// Leaves the Ring Support operation and clears all transient Ring Support previews.
    /// </summary>
    private void ExitRingSupportMode()
    {
        HideToolOptionsPanel();
        _manualSupportTool.SetActiveOperation(ManualSupportOperationKind.None, true);
        SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.None);

        string statusText = GetManualSupportStatusText(ManualSupportOperationKind.None);
        _activeToolStatusText = statusText;
        _viewModel.SetStatusText(statusText);
        _viewModel.SetToolPanelText(statusText);
    }

    /// <summary>
    /// Gets whether the Mode Panel is currently asking the user to select one imported model.
    /// </summary>
    private bool IsModePanelSelectionPromptVisible()
    {
        return _layerPanelViewModel.HasImportedModels
            && !_layerPanelViewModel.CanShowWorkflowTabs
            && !_layerPanelViewModel.HasSelectedModelLayer;
    }

    /// <summary>
    /// Collapses tool options when the current workflow context cannot show mode tabs.
    /// </summary>
    private void UpdateToolOptionsPanelVisibilityForWorkflowContext()
    {
        if (_layerPanelViewModel.HasSelectedSupportGroupLayer)
        {
            HideToolOptionsPanel();
            return;
        }

        if (IsModePanelSelectionPromptVisible())
        {
            HideToolOptionsPanel();
        }
    }

    /// <summary>
    /// Opens Ring Support settings when the requested support group was generated by the Ring Support tool.
    /// </summary>
    private void LayerPanel_EditSupportGroupRequested(object? sender, LayerSupportGroupEditRequestedEventArgs e)
    {
        _ = sender;

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(e.SupportLayerGroupId);

        if (supportLayerGroup == null || supportLayerGroup.GeneratorKind != SupportGroupGeneratorKind.RingSupport)
        {
            HideToolOptionsPanel();
            _viewModel.SetStatusText("This support group does not have editable tool settings yet.");
            return;
        }

        RingSupportSettings? settings = supportLayerGroup.RingSupportSettings;

        if (settings == null)
        {
            HideToolOptionsPanel();
            _viewModel.SetStatusText("This support group is missing Ring Support settings.");
            return;
        }

        SetActiveMode(WorkspaceModeId.ManualSupport);
        ToolOptionsPanelOverlay.SetSelectedTool("Ring Support");
        ToolOptionsPanelOverlay.SetRingSupportSpacing(settings.Spacing);
        ToolOptionsPanelOverlay.Visibility = System.Windows.Visibility.Visible;
        RunWithWaitCursor(() => _manualSupportTool.EditRingSupportGroup(supportLayerGroup));
        SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.Ring);
    }

    /// <summary>
    /// Applies one manual support operation selection from the permanent ribbon-style mode panel.
    /// </summary>
    private void ApplyManualSupportOperationSelection(ManualSupportOperationKind operationKind, bool activateMode)
    {
        if (activateMode)
        {
            SetActiveMode(WorkspaceModeId.ManualSupport);
        }

        if (_activeModeId != WorkspaceModeId.ManualSupport)
        {
            return;
        }

        if (operationKind == ManualSupportOperationKind.None)
        {
            HideToolOptionsPanel();
        }

        string statusText = GetManualSupportStatusText(operationKind);
        _activeToolStatusText = statusText;
        _viewModel.SetStatusText(statusText);
        _viewModel.SetToolPanelText(statusText);
        _manualSupportTool.SetActiveOperation(operationKind, true);
        SynchronizeWorkflowModePanelSupportOperation(operationKind);
    }

    /// <summary>
    /// Keeps the permanent ribbon-style support buttons aligned with the active support operation.
    /// </summary>
    private void SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind operationKind)
    {
        WorkflowModePanelOverlay.SetSelectedSupportOperation(operationKind);
    }

    /// <summary>
    /// Switches the active workspace mode and updates tool and shell guidance state.
    /// </summary>
    private void SetActiveMode(WorkspaceModeId modeId)
    {
        WorkspaceModeDefinition mode = _modeDefinitions[modeId];

        if (!mode.IsAvailable || mode.Tool == null)
        {
            _viewModel.SetStatusText($"{mode.DisplayName} mode is not available yet");
            return;
        }

        _activeModeId = modeId;
        string statusText = GetWorkspaceModeStatusText(mode);
        _activeToolStatusText = statusText;
        _toolManager.SetTool(mode.Tool);
        _viewModel.SetStatusText(statusText);
        _viewModel.SetToolPanelText(statusText);
        SynchronizeWorkflowModePanelSupportOperation(
            modeId == WorkspaceModeId.ManualSupport
                ? _manualSupportTool.ActiveOperationKind
                : ManualSupportOperationKind.None);

        if (modeId == WorkspaceModeId.Select)
        {
            HideToolOptionsPanel();
        }
    }
}
