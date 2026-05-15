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
    /// Applies the compact Support Preset panel selection to future support creation.
    /// </summary>
    private void SupportPresetPanelOverlay_PresetSelected(object? sender, SupportPresetSelectedEventArgs e)
    {
        _ = sender;
        _supportPresetService.SelectPreset(e.Preset);
        _viewModel.SetStatusText($"Selected support preset {e.Preset.Name}");
    }

    /// <summary>
    /// Opens the floating support preset editor window.
    /// </summary>
    private void SupportPresetPanelOverlay_AdvancedRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        SupportPresetEditorWindow editorWindow = new SupportPresetEditorWindow(_supportPresetService)
        {
            Owner = this
        };
        editorWindow.Show();
    }

    /// <summary>
    /// Keeps the compact Support Preset panel synchronized with editor-window selection changes.
    /// </summary>
    private void SupportPresetService_SelectedPresetChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        SupportPresetPanelOverlay.SelectPreset(_supportPresetService.SelectedPreset);
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
    /// Shows the active tool options overlay for one selected ribbon tool.
    /// </summary>
    private void WorkflowModePanelOverlay_ToolSelected(object? sender, ModePanel.ToolSelectedEventArgs e)
    {
        _ = sender;
        ShowToolOptionsForSelectedTool(e.ToolName);
    }

    /// <summary>
    /// Refreshes the Ring Support preview when its spacing option changes.
    /// </summary>
    private void RingSupportToolOptionsControl_OptionsChanged(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Ring)
        {
            return;
        }

        _manualSupportTool.RefreshActiveOperationPreview();
    }

    /// <summary>
    /// Applies the current Ring Support preview as a new support group.
    /// </summary>
    private void RingSupportToolOptionsControl_ApplyRequested(object? sender, System.EventArgs e)
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
        didApply = _manualSupportTool.ApplyActiveOperation();

        if (!didApply)
        {
            return;
        }

        RestartRingSupportOperationWithPanelsVisible();
    }

    /// <summary>
    /// Closes the Ring Support panel without applying supports.
    /// </summary>
    private void RingSupportToolOptionsControl_CloseRequested(object? sender, System.EventArgs e)
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
    /// Updates and reveals the selected tool's self-contained options panel.
    /// </summary>
    private void ShowToolOptionsForSelectedTool(string selectedToolName)
    {
        if (IsModePanelSelectionPromptVisible())
        {
            HideToolOptionsOverlay();
            return;
        }

        if (string.Equals(selectedToolName, TransformScaleToolName, StringComparison.Ordinal))
        {
            SupportPresetPanelOverlay.Visibility = System.Windows.Visibility.Collapsed;
            ShowTransformScaleTool();
            return;
        }

        ClearTransformScaleToolState();
        if (string.Equals(selectedToolName, "Ring Support", StringComparison.Ordinal))
        {
            ShowToolOptionsControl(_ringSupportToolOptionsControl);
        }
        else
        {
            HideToolOptionsHostOnly();
        }

        SupportPresetPanelOverlay.Visibility = IsSupportPresetTool(selectedToolName)
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
    }

    /// <summary>
    /// Hides the active tool options overlay when no tool with options is selected.
    /// </summary>
    private void HideToolOptionsOverlay()
    {
        ClearTransformScaleToolState();
        HideToolOptionsHostOnly();
        SupportPresetPanelOverlay.Visibility = System.Windows.Visibility.Collapsed;
    }

    /// <summary>
    /// Leaves the Ring Support operation and clears all transient Ring Support previews.
    /// </summary>
    private void ExitRingSupportMode()
    {
        HideToolOptionsOverlay();
        _manualSupportTool.SetActiveOperation(ManualSupportOperationKind.None, true);
        SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.None);

        string statusText = GetManualSupportStatusText(ManualSupportOperationKind.None);
        _activeToolStatusText = statusText;
        _viewModel.SetStatusText(statusText);
        _viewModel.SetToolPanelText(statusText);
    }

    /// <summary>
    /// Clears the accepted Ring Support preview while keeping Ring Support controls open for the next placement.
    /// </summary>
    private void RestartRingSupportOperationWithPanelsVisible()
    {
        _manualSupportTool.SetActiveOperation(ManualSupportOperationKind.Ring, true);
        SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.Ring);
        ShowToolOptionsControl(_ringSupportToolOptionsControl);
        SupportPresetPanelOverlay.Visibility = System.Windows.Visibility.Visible;
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
    private void UpdateToolOptionsHostVisibilityForWorkflowContext()
    {
        if (_layerPanelViewModel.HasSelectedSupportGroupLayer)
        {
            HideToolOptionsOverlay();
            return;
        }

        if (IsModePanelSelectionPromptVisible())
        {
            HideToolOptionsOverlay();
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
            HideToolOptionsOverlay();
            _viewModel.SetStatusText("This support group does not have editable tool settings yet.");
            return;
        }

        RingSupportSettings? settings = supportLayerGroup.RingSupportSettings;

        if (settings == null)
        {
            HideToolOptionsOverlay();
            _viewModel.SetStatusText("This support group is missing Ring Support settings.");
            return;
        }

        SetActiveMode(WorkspaceModeId.ManualSupport);
        _ringSupportToolOptionsControl.SetRingSupportSpacing(settings.Spacing);
        ShowToolOptionsControl(_ringSupportToolOptionsControl);
        SupportPresetPanelOverlay.Visibility = System.Windows.Visibility.Visible;
        _manualSupportTool.EditRingSupportGroup(supportLayerGroup);
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
            HideToolOptionsOverlay();
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
            HideToolOptionsOverlay();
        }
    }

    /// <summary>
    /// Gets whether the named tool creates or edits support geometry and should show the Support Preset panel.
    /// </summary>
    private static bool IsSupportPresetTool(string selectedToolName)
    {
        return string.Equals(selectedToolName, "Point Support", StringComparison.Ordinal)
            || string.Equals(selectedToolName, "Ring Support", StringComparison.Ordinal);
    }

    /// <summary>
    /// Shows one self-contained tool options panel in the shell-owned overlay location.
    /// </summary>
    private void ShowToolOptionsControl(System.Windows.Controls.Control toolOptionsControl)
    {
        ToolOptionsHostOverlay.Content = toolOptionsControl;
        ToolOptionsHostOverlay.Visibility = System.Windows.Visibility.Visible;
    }

    /// <summary>
    /// Clears the active tool options panel without changing any non-tool overlay panels.
    /// </summary>
    private void HideToolOptionsHostOnly()
    {
        ToolOptionsHostOverlay.Content = null;
        ToolOptionsHostOverlay.Visibility = System.Windows.Visibility.Collapsed;
    }
}
