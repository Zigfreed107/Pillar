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
    /// Refreshes the Line Support preview when its spacing option changes.
    /// </summary>
    private void LineSupportToolOptionsControl_OptionsChanged(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Line)
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

        ShowToolOptionsControl(_ringSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
    }

    /// <summary>
    /// Applies the current Line Support preview as a new support group or edited support group.
    /// </summary>
    private void LineSupportToolOptionsControl_ApplyRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Line)
        {
            _viewModel.SetStatusText("Choose the Line Support tool before applying line supports.");
            return;
        }

        bool didApply = _manualSupportTool.ApplyActiveOperation();

        if (!didApply)
        {
            return;
        }

        ShowToolOptionsControl(_lineSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
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
    /// Closes the Line Support panel without applying supports.
    /// </summary>
    private void LineSupportToolOptionsControl_CloseRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Line)
        {
            return;
        }

        _manualSupportTool.Cancel();
        ExitLineSupportMode();
    }

    /// <summary>
    /// Deletes selected supports from the active Ring Support edit using the same path as the Delete key.
    /// </summary>
    private void RingSupportToolOptionsControl_DeleteRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        DeleteSelectedSupportsInActiveEditGroup();
    }

    /// <summary>
    /// Deletes selected supports from the active Line Support edit using the same path as the Delete key.
    /// </summary>
    private void LineSupportToolOptionsControl_DeleteRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        DeleteSelectedSupportsInActiveEditGroup();
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

        if (string.Equals(selectedToolName, "Translate", StringComparison.Ordinal))
        {
            ClearTransformScaleToolState();
            ShowPlaceholderToolOptions(
                "Translate Options",
                "Translate tool is active. Dedicated movement controls will be added here.",
                ToolSessionPanelSet.None,
                () => FinishPlaceholderToolSession("Finished translate tool"));
            return;
        }

        if (string.Equals(selectedToolName, "Rotate", StringComparison.Ordinal))
        {
            ClearTransformScaleToolState();
            ShowPlaceholderToolOptions(
                "Rotate Options",
                "Rotate tool is active. Dedicated orientation controls will be added here.",
                ToolSessionPanelSet.None,
                () => FinishPlaceholderToolSession("Finished rotate tool"));
            return;
        }

        if (string.Equals(selectedToolName, TransformScaleToolName, StringComparison.Ordinal))
        {
            ShowTransformScaleTool();
            return;
        }

        ClearTransformScaleToolState();
        if (string.Equals(selectedToolName, "Point Support", StringComparison.Ordinal))
        {
            ShowPlaceholderToolOptions(
                "Point Support Options",
                "Point Support is active. Click the selected model to place individual supports.",
                ToolSessionPanelSet.SupportPresets,
                () => FinishManualSupportPlaceholderToolSession("Finished point support tool"));
            return;
        }

        if (string.Equals(selectedToolName, "Line Support", StringComparison.Ordinal))
        {
            ShowToolOptionsControl(_lineSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
            return;
        }

        if (string.Equals(selectedToolName, "Ring Support", StringComparison.Ordinal))
        {
            ShowToolOptionsControl(_ringSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
            return;
        }

        HideToolOptionsOverlay();
    }

    /// <summary>
    /// Hides the active tool options overlay when no tool with options is selected.
    /// </summary>
    private void HideToolOptionsOverlay()
    {
        ClearTransformScaleToolState();
        _activePlaceholderToolFinishAction = null;
        _toolSessionOverlayCoordinator.EndSession();
        UpdateGeneratedSupportDeleteButtonState();
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
    /// Leaves the Line Support operation and clears all transient Line Support previews.
    /// </summary>
    private void ExitLineSupportMode()
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
        ShowToolOptionsControl(_ringSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
        UpdateGeneratedSupportDeleteButtonState();
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
        if (IsSupportToolEditActive())
        {
            return;
        }

        if (_layerPanelViewModel.HasSelectedSupportGroupLayer)
        {
            if (IsSelectedSupportGroupBeingEdited())
            {
                return;
            }

            HideToolOptionsOverlay();
            return;
        }

        if (IsModePanelSelectionPromptVisible())
        {
            HideToolOptionsOverlay();
        }
    }

    /// <summary>
    /// Gets whether a generated support tool owns the Tool Options panel until its explicit Close action exits editing.
    /// </summary>
    private bool IsSupportToolEditActive()
    {
        return _activeModeId == WorkspaceModeId.ManualSupport
            && _manualSupportTool.ActiveEditingSupportLayerGroupId.HasValue;
    }

    /// <summary>
    /// Gets whether the layer panel is selecting the same support group currently loaded in a support edit operation.
    /// </summary>
    private bool IsSelectedSupportGroupBeingEdited()
    {
        Guid? selectedSupportLayerGroupId = _layerPanelViewModel.GetSelectedSupportLayerGroupId();
        Guid? activeEditingSupportLayerGroupId = _manualSupportTool.ActiveEditingSupportLayerGroupId;

        return selectedSupportLayerGroupId.HasValue
            && activeEditingSupportLayerGroupId.HasValue
            && selectedSupportLayerGroupId.Value == activeEditingSupportLayerGroupId.Value;
    }

    /// <summary>
    /// Opens generated support settings when the requested support group was created by an editable support tool.
    /// </summary>
    private void LayerPanel_EditSupportGroupRequested(object? sender, LayerSupportGroupEditRequestedEventArgs e)
    {
        _ = sender;

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(e.SupportLayerGroupId);

        if (supportLayerGroup == null)
        {
            HideToolOptionsOverlay();
            _viewModel.SetStatusText("This support group does not have editable tool settings yet.");
            return;
        }

        if (supportLayerGroup.GeneratorKind == SupportGroupGeneratorKind.LineSupport)
        {
            LineSupportSettings? settings = supportLayerGroup.LineSupportSettings;

            if (settings == null)
            {
                HideToolOptionsOverlay();
                _viewModel.SetStatusText("This support group is missing Line Support settings.");
                return;
            }

            SetActiveMode(WorkspaceModeId.ManualSupport);
            _lineSupportToolOptionsControl.SetLineSupportSpacing(settings.Spacing);
            _lineSupportToolOptionsControl.SetPlaceSupportsAtBends(settings.PlaceSupportsAtBends);
            ShowToolOptionsControl(_lineSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
            _manualSupportTool.EditLineSupportGroup(supportLayerGroup);
            SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.Line);
            return;
        }

        if (supportLayerGroup.GeneratorKind == SupportGroupGeneratorKind.RingSupport)
        {
            RingSupportSettings? settings = supportLayerGroup.RingSupportSettings;

            if (settings == null)
            {
                HideToolOptionsOverlay();
                _viewModel.SetStatusText("This support group is missing Ring Support settings.");
                return;
            }

            SetActiveMode(WorkspaceModeId.ManualSupport);
            _ringSupportToolOptionsControl.SetRingSupportSpacing(settings.Spacing);
            ShowToolOptionsControl(_ringSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
            _manualSupportTool.EditRingSupportGroup(supportLayerGroup);
            SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.Ring);
            return;
        }

        HideToolOptionsOverlay();
        _viewModel.SetStatusText("This support group does not have editable tool settings yet.");
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
    /// Shows one self-contained tool options panel in the shell-owned overlay location.
    /// </summary>
    private void ShowToolOptionsControl(System.Windows.Controls.Control toolOptionsControl, ToolSessionPanelSet panels)
    {
        _activePlaceholderToolFinishAction = null;
        _toolSessionOverlayCoordinator.BeginSession(toolOptionsControl, panels);
        UpdateGeneratedSupportDeleteButtonState();
    }

    /// <summary>
    /// Clears the active tool options panel without changing any non-tool overlay panels.
    /// </summary>
    private void HideToolOptionsHostOnly()
    {
        _toolSessionOverlayCoordinator.HideOptionsHostOnly();
        UpdateGeneratedSupportDeleteButtonState();
    }

    /// <summary>
    /// Shows the reusable Finish-only options panel for tools that do not yet have dedicated settings.
    /// </summary>
    private void ShowPlaceholderToolOptions(string title, string description, ToolSessionPanelSet panels, Action finishAction)
    {
        _activePlaceholderToolFinishAction = finishAction ?? throw new ArgumentNullException(nameof(finishAction));
        _toolSessionOptionsControl.SetSessionText(title, description);
        _toolSessionOverlayCoordinator.BeginSession(_toolSessionOptionsControl, panels);
        UpdateGeneratedSupportDeleteButtonState();
    }

    /// <summary>
    /// Runs the active placeholder tool's finish behavior.
    /// </summary>
    private void ToolSessionOptionsControl_FinishRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        Action? finishAction = _activePlaceholderToolFinishAction;
        _activePlaceholderToolFinishAction = null;

        if (finishAction == null)
        {
            HideToolOptionsOverlay();
            return;
        }

        finishAction();
    }

    /// <summary>
    /// Finishes a placeholder transform-style tool session.
    /// </summary>
    private void FinishPlaceholderToolSession(string statusText)
    {
        HideToolOptionsOverlay();
        _activeToolStatusText = statusText;
        _viewModel.SetStatusText(statusText);
        _viewModel.SetToolPanelText(statusText);
    }

    /// <summary>
    /// Finishes a placeholder Manual Support operation and returns Manual Support mode to operation selection.
    /// </summary>
    private void FinishManualSupportPlaceholderToolSession(string statusText)
    {
        HideToolOptionsOverlay();
        _manualSupportTool.SetActiveOperation(ManualSupportOperationKind.None, true);
        SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.None);
        _activeToolStatusText = GetManualSupportStatusText(ManualSupportOperationKind.None);
        _viewModel.SetStatusText(statusText);
        _viewModel.SetToolPanelText(_activeToolStatusText);
    }

    /// <summary>
    /// Keeps generated-support Delete buttons aligned with selected supports in the active edit group.
    /// </summary>
    private void UpdateGeneratedSupportDeleteButtonState()
    {
        bool canDeleteSelectedSupports = (ToolOptionsHostOverlay.Content == _ringSupportToolOptionsControl
                || ToolOptionsHostOverlay.Content == _lineSupportToolOptionsControl)
            && _manualSupportTool.HasSelectedSupportsInActiveEditGroup();

        _ringSupportToolOptionsControl.SetDeleteSelectedSupportsEnabled(
            ToolOptionsHostOverlay.Content == _ringSupportToolOptionsControl && canDeleteSelectedSupports);
        _lineSupportToolOptionsControl.SetDeleteSelectedSupportsEnabled(
            ToolOptionsHostOverlay.Content == _lineSupportToolOptionsControl && canDeleteSelectedSupports);
    }
}
