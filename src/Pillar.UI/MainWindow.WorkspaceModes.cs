// MainWindow.WorkspaceModes.cs
// Owns shell-level workspace mode registration, activation, and ribbon synchronization without reviving legacy per-mode overlays.
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using Pillar.UI.Layers;
using Pillar.Core.Tools;
using Pillar.Rendering.Tools;
using Pillar.UI.Modes;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;

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

            case ManualSupportOperationKind.Contour:
                return "Manual support mode: contour support operation active";

            case ManualSupportOperationKind.Area:
                return "Manual support mode: area support operation active";

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
    /// Refreshes the Contour Support preview when one of its options changes.
    /// </summary>
    private void ContourSupportToolOptionsControl_OptionsChanged(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Contour)
        {
            return;
        }

        _manualSupportTool.RefreshActiveOperationPreview();
    }

    /// <summary>
    /// Refreshes the Area Support preview when one of its options changes.
    /// </summary>
    private void AreaSupportToolOptionsControl_OptionsChanged(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Area)
        {
            return;
        }

        _manualSupportTool.RefreshActiveOperationPreview();
    }

    /// <summary>
    /// Launches reusable face selection for the active Area Support operation.
    /// </summary>
    private void AreaSupportToolOptionsControl_SelectFacesRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Area)
        {
            _viewModel.SetStatusText("Choose the Area Support tool before selecting faces.");
            return;
        }

        _manualSupportTool.BeginAreaSupportFaceSelection();
    }

    /// <summary>
    /// Puts the active Contour Support operation into model-click Z-height selection mode.
    /// </summary>
    private void ContourSupportToolOptionsControl_PickZHeightRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Contour)
        {
            _viewModel.SetStatusText("Choose the Contour Support tool before picking a contour Z height.");
            return;
        }

        _manualSupportTool.BeginPickContourSupportZHeight();
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
    /// Applies the current Contour Support preview as a new support group or edited support group.
    /// </summary>
    private void ContourSupportToolOptionsControl_ApplyRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Contour)
        {
            _viewModel.SetStatusText("Choose the Contour Support tool before applying contour supports.");
            return;
        }

        bool didApply = _manualSupportTool.ApplyActiveOperation();

        if (!didApply)
        {
            return;
        }

        ShowToolOptionsControl(_contourSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
    }

    /// <summary>
    /// Applies the current Area Support preview as a new support group or edited support group.
    /// </summary>
    private void AreaSupportToolOptionsControl_ApplyRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Area)
        {
            _viewModel.SetStatusText("Choose the Area Support tool before applying area supports.");
            return;
        }

        bool didApply = _manualSupportTool.ApplyActiveOperation();

        if (!didApply)
        {
            return;
        }

        ShowToolOptionsControl(_areaSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
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
    /// Closes the Contour Support panel without applying supports.
    /// </summary>
    private void ContourSupportToolOptionsControl_CloseRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Contour)
        {
            return;
        }

        _manualSupportTool.Cancel();
        ExitContourSupportMode();
    }

    /// <summary>
    /// Closes the Area Support panel without applying supports.
    /// </summary>
    private void AreaSupportToolOptionsControl_CloseRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_activeModeId != WorkspaceModeId.ManualSupport
            || _manualSupportTool.ActiveOperationKind != ManualSupportOperationKind.Area)
        {
            return;
        }

        _manualSupportTool.Cancel();
        ExitAreaSupportMode();
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
    /// Deletes selected supports from the active Contour Support edit using the same path as the Delete key.
    /// </summary>
    private void ContourSupportToolOptionsControl_DeleteRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        DeleteSelectedSupportsInActiveEditGroup();
    }

    /// <summary>
    /// Deletes selected supports from the active Area Support edit using the same path as the Delete key.
    /// </summary>
    private void AreaSupportToolOptionsControl_DeleteRequested(object? sender, System.EventArgs e)
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
        if (ToolOptionsHostOverlay.Content == _directEditToolOptionsControl
            && !string.Equals(selectedToolName, "Direct Edit Supports", StringComparison.Ordinal))
        {
            ClearDirectEditSessionState();
        }
        if (IsModePanelSelectionPromptVisible())
        {
            HideToolOptionsOverlay();
            return;
        }

        _selectTool.ResetSelectionFilter();

        if (string.Equals(selectedToolName, "Translate", StringComparison.Ordinal))
        {
            ClearTransformScaleToolState();
            ClearTransformRotationToolState();
            ShowPlaceholderToolOptions(
                "Translate Options",
                "Translate tool is active. Dedicated movement controls will be added here.",
                ToolSessionPanelSet.None,
                () => FinishPlaceholderToolSession("Finished translate tool"));
            return;
        }

        if (string.Equals(selectedToolName, TransformRotationToolName, StringComparison.Ordinal))
        {
            ClearTransformScaleToolState();
            ShowTransformRotationTool();
            return;
        }

        if (string.Equals(selectedToolName, TransformScaleToolName, StringComparison.Ordinal))
        {
            ClearTransformRotationToolState();
            ShowTransformScaleTool();
            return;
        }

        ClearTransformScaleToolState();
        ClearTransformRotationToolState();
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

        if (string.Equals(selectedToolName, "Contour Support", StringComparison.Ordinal))
        {
            ShowToolOptionsControl(_contourSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
            return;
        }

        if (string.Equals(selectedToolName, "Area Support", StringComparison.Ordinal))
        {
            ShowToolOptionsControl(_areaSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
            return;
        }

        if (string.Equals(selectedToolName, "Direct Edit Supports", StringComparison.Ordinal))
        {
            ShowDirectEditTool(null);
            return;
        }

        if (string.Equals(selectedToolName, "Cluster Supports", StringComparison.Ordinal))
        {
            ShowSupportClusterTool(null);
            return;
        }

        if (string.Equals(selectedToolName, "Brace Supports", StringComparison.Ordinal))
        {
            ShowSupportBracingTool(null);
            return;
        }

        HideToolOptionsOverlay();
    }

    /// <summary>
    /// Hides the active tool options overlay when no tool with options is selected.
    /// </summary>
    private void HideToolOptionsOverlay()
    {
        if (ToolOptionsHostOverlay.Content == _directEditToolOptionsControl)
        {
            ClearDirectEditSessionState();
        }
        _selectTool.ResetSelectionFilter();
        ClearTransformScaleToolState();
        ClearTransformRotationToolState();
        _activePlaceholderToolFinishAction = null;
        _toolSessionOverlayCoordinator.EndSession();
        UpdateGeneratedSupportDeleteButtonState();
    }

    /// <summary>
    /// Cancels transient tool previews before undo, redo, or destructive document mutations change entity identity.
    /// </summary>
    private void CancelActiveDocumentMutationSessions()
    {
        _selectTool.ResetSelectionFilter();
        _toolManager.CancelActiveTool();
        _manualSupportTool.SetActiveOperation(ManualSupportOperationKind.None, true);
        HideToolOptionsOverlay();
        SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.None);
    }

    /// <summary>
    /// Cancels mutation sessions unless history navigation is preserving the active bracing helper.
    /// </summary>
    private void PrepareForDocumentHistoryChange(bool preserveSupportBracingTool)
    {
        if (!preserveSupportBracingTool)
        {
            CancelActiveDocumentMutationSessions();
            return;
        }

        _selectTool.ResetSelectionFilter();
        _scene.SelectionManager.ClearSelection();
    }

    /// <summary>
    /// Gets whether undo and redo should retain the active Brace and Buttress options session.
    /// </summary>
    private bool IsSupportBracingToolActiveForHistory()
    {
        return ToolOptionsHostOverlay.Content == _supportBracingToolOptionsControl;
    }

    /// <summary>
    /// Reconciles the active bracing editor with the modifier stack after undo or redo.
    /// </summary>
    private void RestoreSupportBracingToolAfterHistoryChange(Guid? supportLayerGroupId)
    {
        if (supportLayerGroupId.HasValue)
        {
            _layerPanelViewModel.SelectSupportGroupLayer(supportLayerGroupId.Value);
        }

        SupportLayerGroup? supportLayerGroup = supportLayerGroupId.HasValue
            ? _document.FindSupportLayerGroupById(supportLayerGroupId.Value)
            : null;
        SupportModifierDefinition? modifier = supportLayerGroup != null && _activeEditingBracingModifierId.HasValue
            ? FindLastModifierByToolSession(supportLayerGroup.SupportModifiers, _activeEditingBracingModifierId.Value)
            : null;

        bool wasEditingExistingModifier = _supportBracingToolOptionsControl.EditingModifierKind.HasValue;

        if (modifier?.Kind == SupportModifierKind.Brace || modifier?.Kind == SupportModifierKind.Buttress)
        {
            _activeEditingBracingModifierKind = modifier.Kind;

            if (!wasEditingExistingModifier)
            {
                SupportBraceModifierSettings braceSettings = modifier.Kind == SupportModifierKind.Buttress
                    ? modifier.ButtressSettings?.BraceSettings ?? SupportBraceModifierSettings.CreateDefault()
                    : modifier.BraceSettings ?? SupportBraceModifierSettings.CreateDefault();
                _supportBracingToolOptionsControl.SetBraceSettings(braceSettings, modifier.Kind == SupportModifierKind.Brace);
                _supportBracingToolOptionsControl.SetButtressSettings(
                    modifier.ButtressSettings ?? SupportButtressModifierSettings.CreateDefault(),
                    modifier.Kind == SupportModifierKind.Buttress);
            }
            else
            {
                _supportBracingToolOptionsControl.SetEditingModifierKind(modifier.Kind);
            }
        }
        else
        {
            _supportBracingToolOptionsControl.SetEditingModifierKind(null);
        }

        if (supportLayerGroup != null)
        {
            FocusSupportLayerForClusterTool(supportLayerGroup.Id);
            ActivateNormalSelectionForSupportClusterTool(supportLayerGroup.Id);
        }

        ShowToolOptionsControl(_supportBracingToolOptionsControl, ToolSessionPanelSet.None);
        RefreshSupportBracingToolStatus();
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
    /// Leaves the Contour Support operation and clears all transient Contour Support previews.
    /// </summary>
    private void ExitContourSupportMode()
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
    /// Leaves the Area Support operation and clears all transient Area Support previews.
    /// </summary>
    private void ExitAreaSupportMode()
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
        if (IsSupportToolEditActive() || IsSupportModifierToolActive())
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
    /// Gets whether a support modifier tool owns Tool Options until Close is clicked.
    /// </summary>
    private bool IsSupportModifierToolActive()
    {
        return ToolOptionsHostOverlay.Content == _directEditToolOptionsControl
            || ToolOptionsHostOverlay.Content == _supportClusterToolOptionsControl
            || ToolOptionsHostOverlay.Content == _supportBracingToolOptionsControl;
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

            if (!ConfirmEditSupportLayerWillRemoveModifiers(supportLayerGroup))
            {
                _viewModel.SetStatusText("Support layer edit cancelled.");
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

            if (!ConfirmEditSupportLayerWillRemoveModifiers(supportLayerGroup))
            {
                _viewModel.SetStatusText("Support layer edit cancelled.");
                return;
            }

            SetActiveMode(WorkspaceModeId.ManualSupport);
            _ringSupportToolOptionsControl.SetRingSupportSpacing(settings.Spacing);
            ShowToolOptionsControl(_ringSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
            _manualSupportTool.EditRingSupportGroup(supportLayerGroup);
            SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.Ring);
            return;
        }

        if (supportLayerGroup.GeneratorKind == SupportGroupGeneratorKind.ContourSupport)
        {
            ContourSupportSettings? settings = supportLayerGroup.ContourSupportSettings;

            if (settings == null)
            {
                HideToolOptionsOverlay();
                _viewModel.SetStatusText("This support group is missing Contour Support settings.");
                return;
            }

            if (!ConfirmEditSupportLayerWillRemoveModifiers(supportLayerGroup))
            {
                _viewModel.SetStatusText("Support layer edit cancelled.");
                return;
            }

            SetActiveMode(WorkspaceModeId.ManualSupport);
            _contourSupportToolOptionsControl.SetContourSupportSettings(settings);
            ShowToolOptionsControl(_contourSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
            _manualSupportTool.EditContourSupportGroup(supportLayerGroup);
            SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.Contour);
            return;
        }

        if (supportLayerGroup.GeneratorKind == SupportGroupGeneratorKind.AreaSupport)
        {
            AreaSupportSettings? settings = supportLayerGroup.AreaSupportSettings;

            if (settings == null)
            {
                HideToolOptionsOverlay();
                _viewModel.SetStatusText("This support group is missing Area Support settings.");
                return;
            }

            if (!ConfirmEditSupportLayerWillRemoveModifiers(supportLayerGroup))
            {
                _viewModel.SetStatusText("Support layer edit cancelled.");
                return;
            }

            SetActiveMode(WorkspaceModeId.ManualSupport);
            _areaSupportToolOptionsControl.SetAreaSupportSettings(settings);
            ShowToolOptionsControl(_areaSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
            _manualSupportTool.EditAreaSupportGroup(supportLayerGroup);
            SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.Area);
            return;
        }

        HideToolOptionsOverlay();
        _viewModel.SetStatusText("This support group does not have editable tool settings yet.");
    }

    /// <summary>
    /// Opens a support modifier editor when the backing editing tool has been implemented.
    /// </summary>
    private void LayerPanel_EditSupportModifierRequested(object? sender, LayerSupportModifierEditRequestedEventArgs e)
    {
        _ = sender;

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(e.SupportLayerGroupId);

        if (supportLayerGroup == null)
        {
            _layerPanelViewModel.RefreshFromDocument();
            _viewModel.SetStatusText("The support edit could not be found.");
            return;
        }

        IReadOnlyList<SupportModifierDefinition> modifiers = supportLayerGroup.SupportModifiers;

        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].ToolSessionId == e.ModifierId)
            {
                if (modifiers[i].Kind == SupportModifierKind.Cluster)
                {
                    if (!ConfirmEditSupportModifierWillRemoveDownstreamModifiers(modifiers, FindLastToolSessionModifierIndex(modifiers, e.ModifierId)))
                    {
                        _viewModel.SetStatusText("Support modifier edit cancelled.");
                        return;
                    }

                    SetActiveMode(WorkspaceModeId.ManualSupport);
                    _layerPanelViewModel.SelectSupportGroupLayer(supportLayerGroup.Id);
                    ShowSupportClusterTool(modifiers[i]);
                    return;
                }

                if (modifiers[i].Kind == SupportModifierKind.Brace || modifiers[i].Kind == SupportModifierKind.Buttress)
                {
                    if (!ConfirmEditSupportModifierWillRemoveDownstreamModifiers(modifiers, FindLastToolSessionModifierIndex(modifiers, e.ModifierId)))
                    {
                        _viewModel.SetStatusText("Support modifier edit cancelled.");
                        return;
                    }

                    SetActiveMode(WorkspaceModeId.ManualSupport);
                    _layerPanelViewModel.SelectSupportGroupLayer(supportLayerGroup.Id);
                    ShowSupportBracingTool(modifiers[i]);
                    return;
                }

                if (modifiers[i].Kind == SupportModifierKind.DirectEdit)
                {
                    int cutoffIndex = FindLastToolSessionModifierIndex(modifiers, e.ModifierId);

                    if (!ConfirmEditSupportModifierWillRemoveDownstreamModifiers(modifiers, cutoffIndex))
                    {
                        _viewModel.SetStatusText("Support modifier edit cancelled.");
                        return;
                    }

                    SetActiveMode(WorkspaceModeId.ManualSupport);
                    _layerPanelViewModel.SelectSupportGroupLayer(supportLayerGroup.Id);
                    ShowDirectEditTool(modifiers[i], cutoffIndex);
                    return;
                }
                _viewModel.SetStatusText($"{modifiers[i].DisplayName} editing will be available when its tool is implemented.");
                return;
            }
        }

        _layerPanelViewModel.RefreshFromDocument();
        _viewModel.SetStatusText("The support edit could not be found.");
    }

    /// <summary>
    /// Confirms support generator edits that will invalidate the complete modifier stack.
    /// </summary>
    private bool ConfirmEditSupportLayerWillRemoveModifiers(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup.SupportModifiers.Count == 0)
        {
            return true;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            "Editing this support layer will regenerate its supports and delete all modifiers below it.\n\nContinue?",
            "Edit Support Layer",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.OK;
    }

    /// <summary>
    /// Finds the last internal action owned by one visible tool-session modifier.
    /// </summary>
    private static int FindLastToolSessionModifierIndex(
        IReadOnlyList<SupportModifierDefinition> modifiers,
        Guid toolSessionId)
    {
        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            if (modifiers[i].ToolSessionId == toolSessionId)
            {
                return i;
            }
        }

        return -1;
    }
    /// <summary>
    /// Confirms modifier edits that will remove later modifier stack entries.
    /// </summary>
    private bool ConfirmEditSupportModifierWillRemoveDownstreamModifiers(IReadOnlyList<SupportModifierDefinition> modifiers, int modifierIndex)
    {
        if (modifierIndex >= modifiers.Count - 1)
        {
            return true;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            "Editing this modifier will delete all modifiers underneath it.\n\nContinue?",
            "Edit Support Modifier",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.OK;
    }

    /// <summary>
    /// Confirms cluster operations that will durably remove Brace or Buttress targets.
    /// </summary>
    private bool ConfirmClusterWillRemoveReinforcement(SupportReinforcementReconciliationResult reconciliation)
    {
        if (!reconciliation.HasChanges)
        {
            return true;
        }

        string removedModifierText = reconciliation.RemovedModifierCount > 0
            ? $" {reconciliation.RemovedModifierCount} complete modifier(s) will also be removed because too few targets remain."
            : string.Empty;
        MessageBoxResult result = MessageBox.Show(
            this,
            $"Clustering these supports will remove {reconciliation.RemovedTargetCount} reinforcement target(s)."
                + removedModifierText
                + "\n\nContinue?",
            "Cluster Supports",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.OK;
    }

    /// <summary>
    /// Opens a new or existing Direct Edit modifier session for the selected support layer.
    /// </summary>
    private void ShowDirectEditTool(SupportModifierDefinition? modifier, int? cutoffIndex = null)
    {
        Guid? supportLayerGroupId = _layerPanelViewModel.GetSelectedSupportLayerGroupId();
        SupportLayerGroup? supportLayerGroup = supportLayerGroupId.HasValue
            ? _document.FindSupportLayerGroupById(supportLayerGroupId.Value)
            : null;

        if (supportLayerGroup == null)
        {
            _viewModel.SetStatusText("Select one support layer before using Direct Edit.");
            return;
        }

        _activeDirectEditToolSessionId = modifier?.ToolSessionId ?? Guid.NewGuid();
        _activeDirectEditSupportLayerGroupId = supportLayerGroup.Id;
        _activeDirectEditCutoffIndex = cutoffIndex;
        _directEditToolOptionsControl.HighlightAngleDegrees = Properties.Settings.Default.DirectEditHighlightAngleDegrees;
        ApplyDirectEditAngleHighlight();
        _directEditTool.Begin(
            supportLayerGroup.Id,
            (float)Properties.Settings.Default.DirectEditXYGizmoScale,
            (float)Properties.Settings.Default.DirectEditZGizmoScale);
        _toolManager.SetTool(_directEditTool);
        ShowToolOptionsControl(_directEditToolOptionsControl, ToolSessionPanelSet.None);
        _viewModel.SetStatusText("Direct Edit: click a support stem to show its handles.");
    }

    /// <summary>
    /// Applies one completed multi-selection gesture as a single undoable modifier-stack replacement.
    /// </summary>
    private void DirectEditTool_EditCommitted(IReadOnlyList<DirectEditCommitAction> actions)
    {
        if (!_activeDirectEditSupportLayerGroupId.HasValue
            || !_activeDirectEditToolSessionId.HasValue
            || actions.Count == 0)
        {
            return;
        }

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(
            _activeDirectEditSupportLayerGroupId.Value);

        if (supportLayerGroup == null)
        {
            return;
        }

        IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        IReadOnlyList<SupportModifierDefinition> oldModifiers = supportLayerGroup.SupportModifiers;
        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition>();
        int retainedCount = _activeDirectEditCutoffIndex.HasValue
            ? Math.Min(oldModifiers.Count, _activeDirectEditCutoffIndex.Value + 1)
            : oldModifiers.Count;

        for (int i = 0; i < retainedCount; i++)
        {
            newModifiers.Add(oldModifiers[i].CloneWithOrder(newModifiers.Count));
        }

        HashSet<Guid> affectedSupportIds = new HashSet<Guid>();

        for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
        {
            DirectEditCommitAction action = actions[actionIndex];

            for (int targetIndex = 0; targetIndex < action.TargetSupportIds.Count; targetIndex++)
            {
                affectedSupportIds.Add(action.TargetSupportIds[targetIndex]);
            }

            newModifiers.Add(new SupportModifierDefinition(
                Guid.NewGuid(),
                SupportModifierKind.DirectEdit,
                true,
                newModifiers.Count,
                null,
                null,
                null,
                action.TargetSupportIds,
                null,
                supportLayerGroup.SourceGeneratorRevision,
                null,
                null,
                _activeDirectEditToolSessionId,
                action.Settings));
        }

        IReadOnlyList<SupportEntity> sourceSupportEntities = RestoreSourceSupportsForModifierReplay(
            oldSupportEntities,
            supportLayerGroup.SupportModifiers);
        IReadOnlyList<SupportEntity> newSupportEntities = SupportModifierPipeline.ApplyModifiers(
            sourceSupportEntities,
            newModifiers);
        _commandRunner.Execute(new ReplaceSupportLayerOutputAndModifiersCommand(
            _document,
            supportLayerGroup,
            oldSupportEntities,
            newSupportEntities,
            oldModifiers,
            newModifiers,
            "Direct Edit Supports"));

        _activeDirectEditCutoffIndex = newModifiers.Count - 1;
        _viewModel.SetStatusText($"Direct Edit applied to {affectedSupportIds.Count} support(s).");
    }

    /// <summary>
    /// Mirrors Direct Edit tool guidance into the shell status bar.
    /// </summary>
    private void DirectEditTool_StatusMessageRequested(string statusMessage)
    {
        _viewModel.SetStatusText(statusMessage);
    }

    /// <summary>
    /// Saves and applies the support head and branch angle highlight threshold.
    /// </summary>
    private void DirectEditToolOptionsControl_HighlightAngleChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        Properties.Settings.Default.DirectEditHighlightAngleDegrees = _directEditToolOptionsControl.HighlightAngleDegrees;
        Properties.Settings.Default.Save();
        ApplyDirectEditAngleHighlight();
    }

    /// <summary>
    /// Closes the Direct Edit session and returns viewport input to the active mode's normal tool.
    /// </summary>
    private void DirectEditToolOptionsControl_CloseRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        CloseDirectEditTool();
    }

    /// <summary>
    /// Clears Direct Edit session state and transient visuals.
    /// </summary>
    private void CloseDirectEditTool()
    {
        ClearDirectEditSessionState();
        HideToolOptionsOverlay();
        RestoreViewportToolForActiveMode();
        _viewModel.SetStatusText("Direct Edit closed.");
    }

    /// <summary>
    /// Cancels viewport state and disables render-only highlighting for the active Direct Edit session.
    /// </summary>
    private void ClearDirectEditSessionState()
    {
        _directEditTool.Cancel();
        _scene.ConfigureSupportAngleHighlight(false, 0.0, ReadFaceAngleHighlightColor());
        _activeDirectEditToolSessionId = null;
        _activeDirectEditSupportLayerGroupId = null;
        _activeDirectEditCutoffIndex = null;
    }

    /// <summary>
    /// Applies support angle highlighting only while Direct Edit options are active.
    /// </summary>
    private void ApplyDirectEditAngleHighlight()
    {
        _scene.ConfigureSupportAngleHighlight(
            true,
            _directEditToolOptionsControl.HighlightAngleDegrees,
            ReadFaceAngleHighlightColor());
    }

    /// <summary>
    /// Refreshes Cluster Supports diagnostics when options change.
    /// </summary>
    private void SupportClusterToolOptionsControl_OptionsChanged(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshSupportClusterPreviewStatus();
    }

    /// <summary>
    /// Applies the Cluster Supports modifier to the current support selection.
    /// </summary>
    private void SupportClusterToolOptionsControl_ApplyToSelectedRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() => ApplyClusterSupports(false));
    }

    /// <summary>
    /// Applies the Cluster Supports modifier to the whole selected support layer.
    /// </summary>
    private void SupportClusterToolOptionsControl_ApplyToAllRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() => ApplyClusterSupports(true));
    }

    /// <summary>
    /// Applies the Cluster Supports modifier to explicit support targets.
    /// </summary>
    private void ApplyClusterSupports(bool targetAllSupports)
    {
        if (!TryGetSelectedSupportLayerGroupForClustering(out SupportLayerGroup? supportLayerGroup))
        {
            return;
        }

        if (!_supportClusterToolOptionsControl.TryGetClusterSettings(out SupportClusterModifierSettings settings, out string errorMessage))
        {
            MessageBox.Show(this, errorMessage, "Invalid Cluster Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        IReadOnlyList<SupportModifierDefinition> oldModifiers = supportLayerGroup.SupportModifiers;
        SupportModifierDefinition? replacementClusterModifier = FindClusterModifierForApply(oldModifiers);
        bool isReplacingExistingClusterModifier = replacementClusterModifier != null;
        IReadOnlyList<SupportEntity> sourceSupportEntities = RestoreSourceSupportsForModifierReplay(oldSupportEntities, supportLayerGroup.SupportModifiers);
        IReadOnlyList<Guid> targetSupportIds = targetAllSupports
            ? CreateAllClusterTargetSupportIds(oldSupportEntities, supportLayerGroup.Id)
            : CreateSelectedClusterTargetSupportIds(oldSupportEntities, supportLayerGroup.Id);

        if (targetSupportIds.Count < 2)
        {
            _viewModel.SetStatusText(targetAllSupports
                ? "The selected support layer needs at least two supports before applying clustering."
                : "Select at least two supports in this support layer before applying clustering.");
            return;
        }

        IReadOnlyList<SupportModifierTargetBatch> effectiveTargetSupportIdBatches = CreateEffectiveClusterTargetSupportIdBatches(
            replacementClusterModifier,
            targetSupportIds);
        List<SupportModifierDefinition> provisionalModifiers = CreateClusterModifierReplacementList(
            oldModifiers,
            replacementClusterModifier,
            settings,
            effectiveTargetSupportIdBatches,
            supportLayerGroup.SourceGeneratorRevision);
        IReadOnlyList<SupportEntity> provisionalSupportEntities = SupportModifierPipeline.ApplyModifiers(
            sourceSupportEntities,
            provisionalModifiers);
        HashSet<Guid> clusteredSupportIds = CreateClusteredSupportIds(provisionalSupportEntities);
        SupportReinforcementReconciliationResult reconciliation = SupportReinforcementReconciler.RemoveClusteredTargets(
            provisionalModifiers,
            clusteredSupportIds);

        if (!ConfirmClusterWillRemoveReinforcement(reconciliation))
        {
            return;
        }

        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition>(reconciliation.Modifiers);
        IReadOnlyList<SupportEntity> newSupportEntities = reconciliation.HasChanges
            ? SupportModifierPipeline.ApplyModifiers(sourceSupportEntities, newModifiers)
            : provisionalSupportEntities;
        SupportModifierDefinition appliedClusterModifier = FindAppliedClusterModifier(newModifiers, replacementClusterModifier?.Id);
        SupportClusterEvaluationResult previewResult = SupportClusterPlanner.Evaluate(
            sourceSupportEntities,
            appliedClusterModifier);

        _commandRunner.Execute(new ReplaceSupportLayerOutputAndModifiersCommand(
            _document,
            supportLayerGroup,
            oldSupportEntities,
            newSupportEntities,
            oldModifiers,
            newModifiers,
            isReplacingExistingClusterModifier ? "Update Cluster Supports" : "Cluster Supports"));

        _activeEditingClusterModifierId = appliedClusterModifier.ToolSessionId;

        bool isEditingAfterApply = _activeEditingClusterModifierId.HasValue;
        _supportClusterToolOptionsControl.SetClusterSettings(settings, isEditingAfterApply);
        ShowToolOptionsControl(_supportClusterToolOptionsControl, ToolSessionPanelSet.None);
        string statusText = CreateClusterStatusText(previewResult) + CreateReinforcementCleanupStatusText(reconciliation);
        _supportClusterToolOptionsControl.SetStatusText(statusText);
        _viewModel.SetStatusText(statusText);
        UpdateGeneratedSupportDeleteButtonState();
    }

    /// <summary>
    /// Restores the selected shared-stem cluster to individual supports.
    /// </summary>
    private void SupportClusterToolOptionsControl_UnclusterSelectedRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() =>
        {
            if (!TryGetSelectedSupportLayerGroupForClustering(out SupportLayerGroup? supportLayerGroup))
            {
                return;
            }

            IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
            List<Vector2> selectedClusterCenters = CreateSelectedClusterCenters(oldSupportEntities);

            if (selectedClusterCenters.Count == 0)
            {
                _viewModel.SetStatusText("Select a clustered support before using Uncluster Selected.");
                return;
            }

            SupportModifierDefinition? clusterModifier = FindFirstClusterModifier(supportLayerGroup.SupportModifiers);

            if (clusterModifier == null || clusterModifier.ClusterSettings == null)
            {
                _viewModel.SetStatusText("No Cluster modifier is available for the selected support layer.");
                return;
            }

            IReadOnlyList<SupportEntity> restoredSourceEntities = RestoreSourceSupportsForModifierReplay(oldSupportEntities, supportLayerGroup.SupportModifiers);
            IReadOnlyList<SupportModifierTargetBatch> remainingClusterTargetBatches = CreateRemainingClusterTargetBatches(
                clusterModifier,
                oldSupportEntities,
                selectedClusterCenters);

            IReadOnlyList<SupportModifierDefinition> oldModifiers = supportLayerGroup.SupportModifiers;
            List<SupportModifierDefinition> newModifiers = CreateModifiersAfterUnclusterSelected(
                oldModifiers,
                clusterModifier,
                remainingClusterTargetBatches,
                supportLayerGroup.SourceGeneratorRevision);
            IReadOnlyList<SupportEntity> newSupportEntities = SupportModifierPipeline.ApplyModifiers(restoredSourceEntities, newModifiers);
            _commandRunner.Execute(new ReplaceSupportLayerOutputAndModifiersCommand(
                _document,
                supportLayerGroup,
                oldSupportEntities,
                newSupportEntities,
                oldModifiers,
                newModifiers,
                "Uncluster Selected Supports"));

            _viewModel.SetStatusText("Unclustered selected support cluster.");
            RefreshSupportClusterPreviewStatus();
            UpdateGeneratedSupportDeleteButtonState();
        });
    }

    /// <summary>
    /// Removes the Cluster modifier currently being edited.
    /// </summary>
    private void SupportClusterToolOptionsControl_RemoveAllRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() =>
        {
            if (!_activeEditingClusterModifierId.HasValue || !TryGetSelectedSupportLayerGroupForClustering(out SupportLayerGroup? supportLayerGroup))
            {
                return;
            }

            IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
            IReadOnlyList<SupportEntity> sourceSupportEntities = RestoreSourceSupportsForModifierReplay(oldSupportEntities, supportLayerGroup.SupportModifiers);
            IReadOnlyList<SupportModifierDefinition> oldModifiers = supportLayerGroup.SupportModifiers;
            List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition>();

            for (int i = 0; i < oldModifiers.Count; i++)
            {
                if (oldModifiers[i].ToolSessionId != _activeEditingClusterModifierId.Value)
                {
                    newModifiers.Add(oldModifiers[i]);
                }
            }

            IReadOnlyList<SupportEntity> newSupportEntities = SupportModifierPipeline.ApplyModifiers(sourceSupportEntities, newModifiers);
            _commandRunner.Execute(new ReplaceSupportLayerOutputAndModifiersCommand(
                _document,
                supportLayerGroup,
                oldSupportEntities,
                newSupportEntities,
                oldModifiers,
                newModifiers,
                "Remove Cluster Supports"));

            _activeEditingClusterModifierId = null;
            _supportClusterToolOptionsControl.SetClusterSettings(SupportClusterModifierSettings.CreateDefault(), false);
            _supportClusterToolOptionsControl.SetStatusText("Cluster modifier removed.");
            _viewModel.SetStatusText("Cluster modifier removed.");
            UpdateGeneratedSupportDeleteButtonState();
        });
    }

    /// <summary>
    /// Closes the Cluster Supports tool options without applying further changes.
    /// </summary>
    private void SupportClusterToolOptionsControl_CloseRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        _activeEditingClusterModifierId = null;
        RestoreSupportLayerVisibilityAfterClusterTool();
        _selectTool.ResetSelectionFilter();
        HideToolOptionsOverlay();
        RestoreViewportToolForActiveMode();
    }

    /// <summary>
    /// Opens the Cluster Supports tool with default settings or a saved modifier.
    /// </summary>
    private void ShowSupportClusterTool(SupportModifierDefinition? modifier)
    {
        if (!TryGetSelectedSupportLayerGroupForClustering(out SupportLayerGroup? supportLayerGroup))
        {
            return;
        }

        _activeEditingClusterModifierId = modifier?.ToolSessionId ?? Guid.NewGuid();
        SupportClusterModifierSettings settings = modifier?.ClusterSettings ?? SupportClusterModifierSettings.CreateDefault();
        _supportClusterToolOptionsControl.SetClusterSettings(settings, modifier != null);
        FocusSupportLayerForClusterTool(supportLayerGroup.Id);
        ActivateNormalSelectionForSupportClusterTool(supportLayerGroup.Id);
        ShowToolOptionsControl(_supportClusterToolOptionsControl, ToolSessionPanelSet.None);
        RefreshSupportClusterPreviewStatus();
        _viewModel.SetStatusText("Cluster Supports tool active.");
        _ = supportLayerGroup;
    }

    /// <summary>
    /// Routes Cluster Supports viewport input through the normal selection tool so support targeting behaves like ordinary selection.
    /// </summary>
    private void ActivateNormalSelectionForSupportClusterTool(Guid supportLayerGroupId)
    {
        _manualSupportTool.SetActiveOperation(ManualSupportOperationKind.None, true);
        SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.None);
        _selectTool.SetSelectionFilter(SelectionFilter.SupportsInLayer(supportLayerGroupId));
        _selectTool.PruneSelectionToActiveFilter();
        _toolManager.SetTool(_selectTool);
    }

    /// <summary>
    /// Restores viewport routing to the current workspace mode after a helper tool temporarily used normal selection.
    /// </summary>
    private void RestoreViewportToolForActiveMode()
    {
        if (!_modeDefinitions.TryGetValue(_activeModeId, out WorkspaceModeDefinition? mode)
            || !mode.IsAvailable
            || mode.Tool == null)
        {
            return;
        }

        _toolManager.SetTool(GetViewportToolForMode(mode));
    }

    /// <summary>
    /// Updates the Cluster Supports status text from the current options without mutating the document.
    /// </summary>
    private void RefreshSupportClusterPreviewStatus()
    {
        if (!_supportClusterToolOptionsControl.IsPreviewEnabled)
        {
            _supportClusterToolOptionsControl.SetStatusText("Preview disabled.");
            return;
        }

        if (!TryGetSelectedSupportLayerGroupForClustering(out SupportLayerGroup? supportLayerGroup))
        {
            _supportClusterToolOptionsControl.SetStatusText("Select one support layer to preview clustering.");
            return;
        }

        if (!_supportClusterToolOptionsControl.TryGetClusterSettings(out SupportClusterModifierSettings settings, out string errorMessage))
        {
            _supportClusterToolOptionsControl.SetStatusText(errorMessage);
            return;
        }

        IReadOnlyList<SupportEntity> currentSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        IReadOnlyList<SupportEntity> previewSourceEntities = _activeEditingClusterModifierId.HasValue
            ? RestoreSourceSupportsForModifierReplay(currentSupportEntities, supportLayerGroup.SupportModifiers)
            : currentSupportEntities;
        IReadOnlyList<Guid> selectedTargetSupportIds = CreateSelectedClusterTargetSupportIds(currentSupportEntities, supportLayerGroup.Id);
        bool isPreviewingSelection = selectedTargetSupportIds.Count > 0;
        IReadOnlyList<Guid> previewTargetSupportIds = isPreviewingSelection
            ? selectedTargetSupportIds
            : CreateAllClusterTargetSupportIds(currentSupportEntities, supportLayerGroup.Id);

        if (previewTargetSupportIds.Count < 2)
        {
            _supportClusterToolOptionsControl.SetStatusText(isPreviewingSelection
                ? "Select at least two supports in this support layer to preview selected clustering."
                : "The selected support layer needs at least two supports to preview clustering.");
            return;
        }

        SupportModifierDefinition previewModifier = SupportModifierDefinition.CreateNew(
            SupportModifierKind.Cluster,
            0,
            settings,
            previewTargetSupportIds,
            supportLayerGroup.SourceGeneratorRevision);
        SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(previewSourceEntities, previewModifier);
        _supportClusterToolOptionsControl.SetStatusText($"{GetClusterPreviewTargetLabel(isPreviewingSelection)} preview: {CreateClusterStatusText(result)}");
        SetAutomaticStemDiameterFields(previewSourceEntities, previewTargetSupportIds);
    }

    /// <summary>
    /// Keeps selected-support clustering feedback aligned with normal viewport selection changes.
    /// </summary>
    private void RefreshSupportClusterPreviewStatusForSelectionChange()
    {
        if (ToolOptionsHostOverlay.Content != _supportClusterToolOptionsControl)
        {
            return;
        }

        RefreshSupportClusterPreviewStatus();
    }


    /// <summary>
    /// Finds the currently selected support layer group or reports why clustering cannot start.
    /// </summary>
    private bool TryGetSelectedSupportLayerGroupForClustering(out SupportLayerGroup supportLayerGroup)
    {
        supportLayerGroup = null!;
        Guid? selectedSupportLayerGroupId = _layerPanelViewModel.GetSelectedSupportLayerGroupId();

        if (!selectedSupportLayerGroupId.HasValue)
        {
            _viewModel.SetStatusText("Select one support layer before clustering supports.");
            return false;
        }

        SupportLayerGroup? foundSupportLayerGroup = _document.FindSupportLayerGroupById(selectedSupportLayerGroupId.Value);

        if (foundSupportLayerGroup == null)
        {
            _viewModel.SetStatusText("The selected support layer could not be found.");
            return false;
        }

        supportLayerGroup = foundSupportLayerGroup;
        return true;
    }

    /// <summary>
    /// Builds target support identities from the current viewport support selection.
    /// </summary>
    private IReadOnlyList<Guid> CreateSelectedClusterTargetSupportIds(
        IReadOnlyList<SupportEntity> supportEntities,
        Guid supportLayerGroupId)
    {
        HashSet<Guid> targetSupportIds = new HashSet<Guid>();
        HashSet<Guid> selectedEntityIds = new HashSet<Guid>(_scene.SelectionManager.SelectedEntityIds);
        List<Vector2> selectedClusterCenters = new List<Vector2>();

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.SupportLayerGroupId != supportLayerGroupId
                || (support.Style.Kind == SupportStyleKind.BraceMember || support.Style.Kind == SupportStyleKind.Buttress)
                || !selectedEntityIds.Contains(support.Id))
            {
                continue;
            }

            targetSupportIds.Add(support.Id);

            if (support.Style.Kind == SupportStyleKind.Clustered)
            {
                Vector2 center = new Vector2(support.BasePosition.X, support.BasePosition.Y);

                if (!ContainsCenter(selectedClusterCenters, center))
                {
                    selectedClusterCenters.Add(center);
                }
            }
        }

        if (selectedClusterCenters.Count > 0)
        {
            for (int i = 0; i < supportEntities.Count; i++)
            {
                SupportEntity support = supportEntities[i];

                if (support.SupportLayerGroupId == supportLayerGroupId
                    && support.Style.Kind == SupportStyleKind.Clustered
                    && IsClusterCenterSelected(support.BasePosition, selectedClusterCenters))
                {
                    targetSupportIds.Add(support.Id);
                }
            }
        }

        return SortClusterTargetSupportIds(targetSupportIds);
    }

    /// <summary>
    /// Builds target support identities from every support in the selected support layer.
    /// </summary>
    private static IReadOnlyList<Guid> CreateAllClusterTargetSupportIds(
        IReadOnlyList<SupportEntity> supportEntities,
        Guid supportLayerGroupId)
    {
        List<Guid> targetSupportIds = new List<Guid>();

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.SupportLayerGroupId == supportLayerGroupId
                && support.Style.Kind != SupportStyleKind.BraceMember
                && support.Style.Kind != SupportStyleKind.Buttress)
            {
                targetSupportIds.Add(support.Id);
            }
        }

        targetSupportIds.Sort();
        return targetSupportIds;
    }

    /// <summary>
    /// Captures identities that successfully became clustered in provisional pipeline output.
    /// </summary>
    private static HashSet<Guid> CreateClusteredSupportIds(IReadOnlyList<SupportEntity> supportEntities)
    {
        HashSet<Guid> clusteredSupportIds = new HashSet<Guid>();

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.Style.Kind == SupportStyleKind.Clustered)
            {
                clusteredSupportIds.Add(support.Id);
            }
        }

        return clusteredSupportIds;
    }

    /// <summary>
    /// Creates a stable ordered target list from unique support ids.
    /// </summary>
    private static IReadOnlyList<Guid> SortClusterTargetSupportIds(HashSet<Guid> targetSupportIds)
    {
        List<Guid> sortedTargetSupportIds = new List<Guid>(targetSupportIds);
        sortedTargetSupportIds.Sort();
        return sortedTargetSupportIds;
    }

    /// <summary>
    /// Creates a new modifier stack with the active Cluster modifier inserted or replaced.
    /// </summary>
    private List<SupportModifierDefinition> CreateClusterModifierReplacementList(
        IReadOnlyList<SupportModifierDefinition> oldModifiers,
        SupportModifierDefinition? replacementClusterModifier,
        SupportClusterModifierSettings settings,
        IReadOnlyList<SupportModifierTargetBatch> targetSupportIdBatches,
        int sourceGeneratorRevision)
    {
        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition>();
        bool replacedExistingModifier = false;

        for (int i = 0; i < oldModifiers.Count; i++)
        {
            SupportModifierDefinition oldModifier = oldModifiers[i];

            if (replacementClusterModifier != null && oldModifier.Id == replacementClusterModifier.Id)
            {
                newModifiers.Add(new SupportModifierDefinition(
                    oldModifier.Id,
                    SupportModifierKind.Cluster,
                    oldModifier.IsEnabled,
                    newModifiers.Count,
                    settings,
                    null,
                    null,
                    null,
                    targetSupportIdBatches,
                    sourceGeneratorRevision,
                    null,
                    null,
                    oldModifier.ToolSessionId));
                replacedExistingModifier = true;
                break;
            }

            newModifiers.Add(oldModifier);
        }

        if (!replacedExistingModifier)
        {
            newModifiers.Add(new SupportModifierDefinition(
                Guid.NewGuid(),
                SupportModifierKind.Cluster,
                true,
                newModifiers.Count,
                settings,
                null,
                null,
                null,
                targetSupportIdBatches,
                sourceGeneratorRevision,
                null,
                null,
                _activeEditingClusterModifierId));
        }

        return newModifiers;
    }

    /// <summary>
    /// Finds the Cluster modifier that was just added or replaced.
    /// </summary>
    private static SupportModifierDefinition FindAppliedClusterModifier(List<SupportModifierDefinition> modifiers, Guid? replacementClusterModifierId)
    {
        if (replacementClusterModifierId.HasValue)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].Id == replacementClusterModifierId.Value)
                {
                    return modifiers[i];
                }
            }
        }

        return modifiers[modifiers.Count - 1];
    }

    /// <summary>
    /// Chooses the existing Cluster modifier that repeated Apply clicks should update.
    /// </summary>
    private SupportModifierDefinition? FindClusterModifierForApply(IReadOnlyList<SupportModifierDefinition> modifiers)
    {
        if (_activeEditingClusterModifierId.HasValue)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].ToolSessionId == _activeEditingClusterModifierId.Value && modifiers[i].Kind == SupportModifierKind.Cluster)
                {
                    return modifiers[i];
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Appends the current target set as a separate clustering batch on the layer's cumulative Cluster modifier.
    /// </summary>
    private static IReadOnlyList<SupportModifierTargetBatch> CreateEffectiveClusterTargetSupportIdBatches(
        SupportModifierDefinition? activeClusterModifier,
        IReadOnlyList<Guid> selectedTargetSupportIds)
    {
        List<SupportModifierTargetBatch> targetSupportIdBatches = new List<SupportModifierTargetBatch>();

        if (activeClusterModifier != null)
        {
            for (int batchIndex = 0; batchIndex < activeClusterModifier.TargetSupportIdBatches.Count; batchIndex++)
            {
                targetSupportIdBatches.Add(activeClusterModifier.TargetSupportIdBatches[batchIndex].Clone());
            }
        }

        targetSupportIdBatches.Add(new SupportModifierTargetBatch(selectedTargetSupportIds));
        return targetSupportIdBatches;
    }

    /// <summary>
    /// Converts branched clustered output back to individual supports for modifier editing and removal.
    /// </summary>
    private static IReadOnlyList<SupportEntity> RestoreSourceSupportsForModifierReplay(
        IReadOnlyList<SupportEntity> supportEntities,
        IReadOnlyList<SupportModifierDefinition> modifiers)
    {
        return SupportModifierSourceRestorer.Restore(supportEntities, modifiers);
    }

    /// <summary>
    /// Updates the automatic diameter fields from the current candidate population.
    /// </summary>
    private void SetAutomaticStemDiameterFields(
        IReadOnlyList<SupportEntity> supportEntities,
        IReadOnlyList<Guid> targetSupportIds)
    {
        HashSet<Guid> targetIdSet = new HashSet<Guid>(targetSupportIds);
        float bottomSum = 0.0f;
        float topSum = 0.0f;

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.Style.Kind != SupportStyleKind.Individual || !targetIdSet.Contains(support.Id))
            {
                continue;
            }

            bottomSum += support.Profile.StemBottomDiameter * support.Profile.StemBottomDiameter;
            topSum += support.Profile.StemTopDiameter * support.Profile.StemTopDiameter;
        }

        _supportClusterToolOptionsControl.SetAutomaticDiameters(
            Math.Clamp(MathF.Sqrt(bottomSum), SupportClusterModifierSettings.MinimumCentralStemDiameter, SupportClusterModifierSettings.MaximumCentralStemDiameter),
            Math.Clamp(MathF.Sqrt(topSum), SupportClusterModifierSettings.MinimumCentralStemDiameter, SupportClusterModifierSettings.MaximumCentralStemDiameter));
    }

    /// <summary>
    /// Gets selected cluster centers from selected branched supports in the current support layer.
    /// </summary>
    private List<Vector2> CreateSelectedClusterCenters(IReadOnlyList<SupportEntity> supportEntities)
    {
        HashSet<Guid> selectedEntityIds = new HashSet<Guid>(_scene.SelectionManager.SelectedEntityIds);
        List<Vector2> centers = new List<Vector2>();

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.Style.Kind == SupportStyleKind.Clustered && selectedEntityIds.Contains(support.Id))
            {
                Vector2 center = new Vector2(support.BasePosition.X, support.BasePosition.Y);

                if (!ContainsCenter(centers, center))
                {
                    centers.Add(center);
                }
            }
        }

        return centers;
    }

    /// <summary>
    /// Gets whether the current viewport selection contains clustered supports in the selected support layer.
    /// </summary>
    private bool HasSelectedClusteredSupportsInSelectedSupportLayer()
    {
        Guid? selectedSupportLayerGroupId = _layerPanelViewModel.GetSelectedSupportLayerGroupId();

        if (!selectedSupportLayerGroupId.HasValue)
        {
            return false;
        }

        HashSet<Guid> selectedEntityIds = new HashSet<Guid>(_scene.SelectionManager.SelectedEntityIds);
        IReadOnlyList<SupportEntity> supportEntities = _document.GetSupportEntitiesForGroup(selectedSupportLayerGroupId.Value);

        for (int i = 0; i < supportEntities.Count; i++)
        {
            if (supportEntities[i].Style.Kind == SupportStyleKind.Clustered && selectedEntityIds.Contains(supportEntities[i].Id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the current support selection can be applied as a Cluster modifier target set.
    /// </summary>
    private bool HasValidClusterSelectionForSelectedApply()
    {
        Guid? selectedSupportLayerGroupId = _layerPanelViewModel.GetSelectedSupportLayerGroupId();

        if (!selectedSupportLayerGroupId.HasValue)
        {
            return false;
        }

        IReadOnlyList<SupportEntity> supportEntities = _document.GetSupportEntitiesForGroup(selectedSupportLayerGroupId.Value);
        IReadOnlyList<Guid> targetSupportIds = CreateSelectedClusterTargetSupportIds(
            supportEntities,
            selectedSupportLayerGroupId.Value);
        return targetSupportIds.Count >= 2;
    }

    /// <summary>
    /// Gets whether every support in the selected layer can be applied as one Cluster target set.
    /// </summary>
    private bool HasEnoughSupportsForApplyAll()
    {
        Guid? selectedSupportLayerGroupId = _layerPanelViewModel.GetSelectedSupportLayerGroupId();

        if (!selectedSupportLayerGroupId.HasValue)
        {
            return false;
        }

        IReadOnlyList<SupportEntity> supportEntities = _document.GetSupportEntitiesForGroup(selectedSupportLayerGroupId.Value);
        IReadOnlyList<Guid> targetSupportIds = CreateAllClusterTargetSupportIds(
            supportEntities,
            selectedSupportLayerGroupId.Value);
        return targetSupportIds.Count >= 2;
    }

    /// <summary>
    /// Removes selected cluster members from stored Apply batches while preserving surviving batch boundaries.
    /// </summary>
    private static IReadOnlyList<SupportModifierTargetBatch> CreateRemainingClusterTargetBatches(
        SupportModifierDefinition clusterModifier,
        IReadOnlyList<SupportEntity> supportEntities,
        List<Vector2> selectedClusterCenters)
    {
        HashSet<Guid> selectedClusterSupportIds = new HashSet<Guid>();

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.Style.Kind == SupportStyleKind.Clustered && IsClusterCenterSelected(support.BasePosition, selectedClusterCenters))
            {
                selectedClusterSupportIds.Add(support.Id);
            }
        }

        List<SupportModifierTargetBatch> remainingBatches = new List<SupportModifierTargetBatch>();
        IReadOnlyList<SupportModifierTargetBatch> existingBatches = clusterModifier.TargetSupportIdBatches;

        for (int batchIndex = 0; batchIndex < existingBatches.Count; batchIndex++)
        {
            IReadOnlyList<Guid> batchTargetIds = existingBatches[batchIndex].TargetSupportIds;
            List<Guid> remainingTargetIds = new List<Guid>();

            for (int targetIndex = 0; targetIndex < batchTargetIds.Count; targetIndex++)
            {
                if (!selectedClusterSupportIds.Contains(batchTargetIds[targetIndex]))
                {
                    remainingTargetIds.Add(batchTargetIds[targetIndex]);
                }
            }

            if (remainingTargetIds.Count >= 2)
            {
                remainingBatches.Add(new SupportModifierTargetBatch(remainingTargetIds));
            }
        }

        return remainingBatches;
    }

    /// <summary>
    /// Builds the remaining Cluster modifier stack after selected shared stems are restored.
    /// </summary>
    private static List<SupportModifierDefinition> CreateModifiersAfterUnclusterSelected(
        IReadOnlyList<SupportModifierDefinition> oldModifiers,
        SupportModifierDefinition removedClusterModifier,
        IReadOnlyList<SupportModifierTargetBatch> remainingClusterTargetBatches,
        int sourceGeneratorRevision)
    {
        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition>();

        for (int i = 0; i < oldModifiers.Count; i++)
        {
            SupportModifierDefinition oldModifier = oldModifiers[i];

            if (oldModifier.Id != removedClusterModifier.Id)
            {
                newModifiers.Add(oldModifier);
                continue;
            }

            if (remainingClusterTargetBatches.Count > 0)
            {
                newModifiers.Add(new SupportModifierDefinition(
                    oldModifier.Id,
                    SupportModifierKind.Cluster,
                    oldModifier.IsEnabled,
                    i,
                    removedClusterModifier.ClusterSettings,
                    null,
                    null,
                    null,
                    remainingClusterTargetBatches,
                    sourceGeneratorRevision,
                    null,
                    null,
                    oldModifier.ToolSessionId));
            }
        }

        return newModifiers;
    }

    /// <summary>
    /// Finds the first Cluster modifier in a stack.
    /// </summary>
    private static SupportModifierDefinition? FindFirstClusterModifier(IReadOnlyList<SupportModifierDefinition> modifiers)
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].Kind == SupportModifierKind.Cluster)
            {
                return modifiers[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Checks whether a cluster center has already been recorded.
    /// </summary>
    private static bool ContainsCenter(List<Vector2> centers, Vector2 center)
    {
        for (int i = 0; i < centers.Count; i++)
        {
            if (Vector2.DistanceSquared(centers[i], center) <= 0.000001f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether one support belongs to a selected shared-stem center.
    /// </summary>
    private static bool IsClusterCenterSelected(Vector3 basePosition, List<Vector2> selectedClusterCenters)
    {
        Vector2 center = new Vector2(basePosition.X, basePosition.Y);
        return ContainsCenter(selectedClusterCenters, center);
    }

    /// <summary>
    /// Creates concise user-facing cluster diagnostics.
    /// </summary>
    private static string CreateClusterStatusText(SupportClusterEvaluationResult result)
    {
        return $"{result.ClusterCount} cluster(s), {result.ClusteredSupportCount} clustered support(s), {result.UnchangedSupportCount} unchanged, {result.RejectedCandidateCount} rejected candidate(s).";
    }

    /// <summary>
    /// Reports durable reinforcement cleanup performed by a cluster operation.
    /// </summary>
    private static string CreateReinforcementCleanupStatusText(SupportReinforcementReconciliationResult reconciliation)
    {
        if (!reconciliation.HasChanges)
        {
            return string.Empty;
        }

        return $" Removed {reconciliation.RemovedTargetCount} reinforcement target(s) and {reconciliation.RemovedModifierCount} complete modifier(s).";
    }

    /// <summary>
    /// Converts the current preview target source into concise preview text.
    /// </summary>
    private static string GetClusterPreviewTargetLabel(bool isPreviewingSelection)
    {
        return isPreviewingSelection
            ? "Selected"
            : "All supports";
    }

    /// <summary>
    /// Refreshes the bracing panel after option edits.
    /// </summary>
    private void SupportBracingToolOptionsControl_OptionsChanged(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshSupportBracingToolStatus();
        UpdateGeneratedSupportDeleteButtonState();
    }

    private void SupportBracingToolOptionsControl_BraceSelectedRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() => ApplySupportBracingModifier(SupportModifierKind.Brace, false));
    }

    private void SupportBracingToolOptionsControl_BraceAllRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() => ApplySupportBracingModifier(SupportModifierKind.Brace, true));
    }

    private void SupportBracingToolOptionsControl_RemoveBracingFromSelectedRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() => RemoveSelectedTargetsFromBracingModifier(SupportModifierKind.Brace));
    }

    private void SupportBracingToolOptionsControl_ButtressSelectedRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() => ApplySupportBracingModifier(SupportModifierKind.Buttress, false));
    }

    private void SupportBracingToolOptionsControl_ButtressAllRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() => ApplySupportBracingModifier(SupportModifierKind.Buttress, true));
    }

    private void SupportBracingToolOptionsControl_RemoveButtressingFromSelectedRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() => RemoveSelectedTargetsFromBracingModifier(SupportModifierKind.Buttress));
    }

    private void SupportBracingToolOptionsControl_RemoveAllBracingRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() => RemoveAllBracingModifiers(SupportModifierKind.Brace));
    }

    private void SupportBracingToolOptionsControl_RemoveAllButtressesRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RunWithWaitCursor(() => RemoveAllBracingModifiers(SupportModifierKind.Buttress));
    }

    private void SupportBracingToolOptionsControl_CloseRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        _activeEditingBracingModifierId = null;
        _activeEditingBracingModifierKind = null;
        RestoreSupportLayerVisibilityAfterClusterTool();
        _selectTool.ResetSelectionFilter();
        HideToolOptionsOverlay();
        RestoreViewportToolForActiveMode();
    }

    /// <summary>
    /// Opens the Brace and Buttress tool with defaults or an existing modifier.
    /// </summary>
    private void ShowSupportBracingTool(SupportModifierDefinition? modifier)
    {
        if (!TryGetSelectedSupportLayerGroupForBracing(out SupportLayerGroup? supportLayerGroup))
        {
            return;
        }

        _activeEditingBracingModifierId = modifier?.ToolSessionId ?? Guid.NewGuid();
        SupportModifierDefinition? braceAction = modifier == null
            ? null
            : FindModifierByToolSessionAndKind(supportLayerGroup.SupportModifiers, modifier.ToolSessionId, SupportModifierKind.Brace);
        SupportModifierDefinition? buttressAction = modifier == null
            ? null
            : FindModifierByToolSessionAndKind(supportLayerGroup.SupportModifiers, modifier.ToolSessionId, SupportModifierKind.Buttress);
        SupportModifierDefinition? latestAction = modifier == null
            ? null
            : FindLastModifierByToolSession(supportLayerGroup.SupportModifiers, modifier.ToolSessionId);
        _activeEditingBracingModifierKind = latestAction?.Kind;
        SupportBraceModifierSettings visibleBraceSettings = braceAction?.BraceSettings
            ?? buttressAction?.ButtressSettings?.BraceSettings
            ?? SupportBraceModifierSettings.CreateDefault();
        _supportBracingToolOptionsControl.SetBraceSettings(visibleBraceSettings, false);
        _supportBracingToolOptionsControl.SetButtressSettings(
            buttressAction?.ButtressSettings ?? SupportButtressModifierSettings.CreateDefault(),
            false);
        _supportBracingToolOptionsControl.SetEditingModifierKinds(braceAction != null, buttressAction != null);
        FocusSupportLayerForClusterTool(supportLayerGroup.Id);
        ActivateNormalSelectionForSupportClusterTool(supportLayerGroup.Id);
        ShowToolOptionsControl(_supportBracingToolOptionsControl, ToolSessionPanelSet.None);
        RefreshSupportBracingToolStatus();
        _viewModel.SetStatusText("Support Bracing tool active.");
    }

    /// <summary>
    /// Applies or updates a Brace or Buttress modifier.
    /// </summary>
    private void ApplySupportBracingModifier(SupportModifierKind modifierKind, bool targetAllSupports)
    {
        if (!TryGetSelectedSupportLayerGroupForBracing(out SupportLayerGroup? supportLayerGroup))
        {
            return;
        }

        SupportBraceModifierSettings? braceSettings = null;
        SupportButtressModifierSettings? buttressSettings = null;
        float? minimumHeight = null;

        if (!_supportBracingToolOptionsControl.TryGetBraceSettings(out SupportBraceModifierSettings visibleBraceSettings, out string braceErrorMessage))
        {
            MessageBox.Show(this, braceErrorMessage, "Invalid Brace Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (modifierKind == SupportModifierKind.Brace)
        {
            braceSettings = visibleBraceSettings;
        }
        else
        {
            if (!_supportBracingToolOptionsControl.TryGetButtressSettings(out SupportButtressModifierSettings settings, out string errorMessage))
            {
                MessageBox.Show(this, errorMessage, "Invalid Buttress Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            buttressSettings = new SupportButtressModifierSettings(
                settings.MinimumButtressHeight,
                settings.ButtressSpacing,
                visibleBraceSettings);
            minimumHeight = settings.MinimumButtressHeight;
        }
        IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        IReadOnlyList<Guid> targetIds = targetAllSupports
            ? CreateAllBracingTargetSupportIds(oldSupportEntities, supportLayerGroup.Id, minimumHeight)
            : CreateSelectedBracingTargetSupportIds(oldSupportEntities, supportLayerGroup.Id, minimumHeight);
        int minimumTargets = modifierKind == SupportModifierKind.Brace ? 2 : 1;

        if (targetIds.Count < minimumTargets)
        {
            _viewModel.SetStatusText(targetAllSupports ? "The selected support layer does not have enough eligible supports." : "Select enough eligible supports before applying.");
            return;
        }

        IReadOnlyList<SupportModifierDefinition> oldModifiers = supportLayerGroup.SupportModifiers;
        bool reappliesSelectedTargets = !targetAllSupports;
        bool replacesSelectedBracePairs = modifierKind == SupportModifierKind.Brace && reappliesSelectedTargets;
        bool replacesSelectedButtresses = modifierKind == SupportModifierKind.Buttress && reappliesSelectedTargets;
        SupportModifierDefinition? replacement = replacesSelectedBracePairs || replacesSelectedButtresses
            ? null
            : FindActiveBracingModifier(oldModifiers, modifierKind);
        List<SupportModifierDefinition> newModifiers;

        if (replacesSelectedBracePairs)
        {
            newModifiers = CreateBraceSelectedModifierList(oldModifiers, visibleBraceSettings, targetIds, supportLayerGroup.SourceGeneratorRevision, _activeEditingBracingModifierId!.Value);
        }
        else if (replacesSelectedButtresses)
        {
            newModifiers = new List<SupportModifierDefinition>(SupportReinforcementReconciler.ReapplyButtressTargets(
                oldModifiers,
                targetIds,
                buttressSettings!,
                supportLayerGroup.SourceGeneratorRevision,
                _activeEditingBracingModifierId!.Value));
        }
        else
        {
            newModifiers = CreateBracingModifierReplacementList(oldModifiers, replacement, modifierKind, braceSettings, buttressSettings, targetIds, supportLayerGroup.SourceGeneratorRevision, _activeEditingBracingModifierId!.Value);
        }
        IReadOnlyList<SupportEntity> sourceSupportEntities = RestoreSourceSupportsForModifierReplay(oldSupportEntities, supportLayerGroup.SupportModifiers);
        SupportModifierDefinition appliedModifier = replacement == null ? newModifiers[newModifiers.Count - 1] : FindModifierById(newModifiers, replacement.Id)!;
        SupportModifierPipelineEvaluation pipelineEvaluation = SupportModifierPipeline.EvaluateModifiers(sourceSupportEntities, newModifiers, appliedModifier.Id);
        IReadOnlyList<SupportEntity> newSupportEntities = pipelineEvaluation.SupportEntities;
        SupportBracingEvaluationResult result = pipelineEvaluation.CapturedBracingResult
            ?? throw new InvalidOperationException("The applied bracing modifier did not produce diagnostics.");
        string commandName = modifierKind == SupportModifierKind.Buttress ? "Buttress Supports" : "Brace Supports";

        _commandRunner.Execute(new ReplaceSupportLayerOutputAndModifiersCommand(_document, supportLayerGroup, oldSupportEntities, newSupportEntities, oldModifiers, newModifiers, replacement == null ? commandName : $"Update {commandName}"));
        _activeEditingBracingModifierId = appliedModifier.ToolSessionId;
        _activeEditingBracingModifierKind = appliedModifier.Kind;
        _supportBracingToolOptionsControl.SetBraceSettings(
            buttressSettings?.BraceSettings ?? braceSettings ?? SupportBraceModifierSettings.CreateDefault(),
            appliedModifier.Kind == SupportModifierKind.Brace);
        _supportBracingToolOptionsControl.SetButtressSettings(buttressSettings ?? SupportButtressModifierSettings.CreateDefault(), appliedModifier.Kind == SupportModifierKind.Buttress);
        _supportBracingToolOptionsControl.SetEditingModifierKinds(
            FindModifierByToolSessionAndKind(newModifiers, appliedModifier.ToolSessionId, SupportModifierKind.Brace) != null,
            FindModifierByToolSessionAndKind(newModifiers, appliedModifier.ToolSessionId, SupportModifierKind.Buttress) != null);
        string statusText = CreateBracingStatusText(appliedModifier.Kind, result);
        _supportBracingToolOptionsControl.SetStatusText(statusText);
        _viewModel.SetStatusText(statusText);
        UpdateGeneratedSupportDeleteButtonState();
    }

    /// <summary>
    /// Removes selected captured targets from the active bracing modifier.
    /// </summary>
    private void RemoveSelectedTargetsFromBracingModifier(SupportModifierKind modifierKind)
    {
        if (!_activeEditingBracingModifierId.HasValue || !TryGetSelectedSupportLayerGroupForBracing(out SupportLayerGroup? supportLayerGroup))
        {
            _viewModel.SetStatusText("Open an existing modifier before removing selected targets.");
            return;
        }

        IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        IReadOnlyList<Guid> selectedTargetIds = CreateSelectedBracingTargetSupportIds(oldSupportEntities, supportLayerGroup.Id, null);

        if (selectedTargetIds.Count == 0)
        {
            _viewModel.SetStatusText("Select captured supports before removing them.");
            return;
        }

        IReadOnlyList<SupportModifierDefinition> oldModifiers = supportLayerGroup.SupportModifiers;
        List<SupportModifierDefinition> newModifiers = CreateModifiersAfterRemovingBracingTargets(oldModifiers, _activeEditingBracingModifierId.Value, modifierKind, selectedTargetIds, supportLayerGroup.SourceGeneratorRevision);
        IReadOnlyList<SupportEntity> sourceSupportEntities = RestoreSourceSupportsForModifierReplay(oldSupportEntities, supportLayerGroup.SupportModifiers);
        IReadOnlyList<SupportEntity> newSupportEntities = SupportModifierPipeline.ApplyModifiers(sourceSupportEntities, newModifiers);
        _commandRunner.Execute(new ReplaceSupportLayerOutputAndModifiersCommand(_document, supportLayerGroup, oldSupportEntities, newSupportEntities, oldModifiers, newModifiers, "Remove Selected Bracing Targets"));

        SupportModifierDefinition? remainingSessionAction = FindLastModifierByToolSession(newModifiers, _activeEditingBracingModifierId.Value);
        _activeEditingBracingModifierKind = remainingSessionAction?.Kind;
        _supportBracingToolOptionsControl.SetEditingModifierKinds(
            FindModifierByToolSessionAndKind(newModifiers, _activeEditingBracingModifierId.Value, SupportModifierKind.Brace) != null,
            FindModifierByToolSessionAndKind(newModifiers, _activeEditingBracingModifierId.Value, SupportModifierKind.Buttress) != null);

        RefreshSupportBracingToolStatus();
        UpdateGeneratedSupportDeleteButtonState();
    }

    /// <summary>
    /// Removes every Brace or Buttress modifier of one kind and rebuilds through the surviving stack.
    /// </summary>
    private void RemoveAllBracingModifiers(SupportModifierKind modifierKind)
    {
        if ((modifierKind != SupportModifierKind.Brace && modifierKind != SupportModifierKind.Buttress)
            || !TryGetSelectedSupportLayerGroupForBracing(out SupportLayerGroup? supportLayerGroup))
        {
            return;
        }

        IReadOnlyList<SupportModifierDefinition> oldModifiers = supportLayerGroup.SupportModifiers;
        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition>(oldModifiers.Count);
        int removedModifierCount = 0;

        for (int i = 0; i < oldModifiers.Count; i++)
        {
            SupportModifierDefinition modifier = oldModifiers[i];

            if (modifier.Kind == modifierKind)
            {
                removedModifierCount++;
                continue;
            }

            newModifiers.Add(modifier);
        }

        if (removedModifierCount == 0)
        {
            UpdateGeneratedSupportDeleteButtonState();
            return;
        }

        IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        IReadOnlyList<SupportEntity> sourceSupportEntities = RestoreSourceSupportsForModifierReplay(oldSupportEntities, supportLayerGroup.SupportModifiers);
        IReadOnlyList<SupportEntity> newSupportEntities = SupportModifierPipeline.ApplyModifiers(sourceSupportEntities, newModifiers);
        string label = modifierKind == SupportModifierKind.Brace ? "Bracing" : "Buttresses";
        _commandRunner.Execute(new ReplaceSupportLayerOutputAndModifiersCommand(
            _document,
            supportLayerGroup,
            oldSupportEntities,
            newSupportEntities,
            oldModifiers,
            newModifiers,
            $"Remove All {label}"));

        if (_activeEditingBracingModifierId.HasValue)
        {
            SupportModifierDefinition? remainingSessionAction = FindLastModifierByToolSession(newModifiers, _activeEditingBracingModifierId.Value);
            _activeEditingBracingModifierKind = remainingSessionAction?.Kind;
            _supportBracingToolOptionsControl.SetEditingModifierKinds(
                FindModifierByToolSessionAndKind(newModifiers, _activeEditingBracingModifierId.Value, SupportModifierKind.Brace) != null,
                FindModifierByToolSessionAndKind(newModifiers, _activeEditingBracingModifierId.Value, SupportModifierKind.Buttress) != null);
        }

        _layerPanelViewModel.RefreshFromDocument();
        _layerPanelViewModel.SelectSupportGroupLayer(supportLayerGroup.Id);
        string statusText = $"Removed {removedModifierCount} {label.ToLowerInvariant()} modifier(s).";
        _supportBracingToolOptionsControl.SetStatusText(statusText);
        _viewModel.SetStatusText(statusText);
        UpdateGeneratedSupportDeleteButtonState();
    }

    /// <summary>
    /// Updates bracing status text from current selection.
    /// </summary>
    private void RefreshSupportBracingToolStatus()
    {
        if (!TryGetSelectedSupportLayerGroupForBracing(out SupportLayerGroup? supportLayerGroup))
        {
            _supportBracingToolOptionsControl.SetStatusText("Select one support layer to use bracing.");
            return;
        }

        IReadOnlyList<SupportEntity> supportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        int selectedCount = CreateSelectedBracingTargetSupportIds(supportEntities, supportLayerGroup.Id, null).Count;
        int allCount = CreateAllBracingTargetSupportIds(supportEntities, supportLayerGroup.Id, null).Count;
        _supportBracingToolOptionsControl.SetStatusText($"{selectedCount} selected eligible support(s), {allCount} eligible support(s) in layer.");
    }

    private void RefreshSupportBracingToolStatusForSelectionChange()
    {
        if (ToolOptionsHostOverlay.Content == _supportBracingToolOptionsControl)
        {
            RefreshSupportBracingToolStatus();
        }
    }

    private bool TryGetSelectedSupportLayerGroupForBracing(out SupportLayerGroup supportLayerGroup)
    {
        supportLayerGroup = null!;
        Guid? selectedSupportLayerGroupId = _layerPanelViewModel.GetSelectedSupportLayerGroupId();

        if (!selectedSupportLayerGroupId.HasValue)
        {
            _viewModel.SetStatusText("Select one support layer before bracing supports.");
            return false;
        }

        SupportLayerGroup? foundSupportLayerGroup = _document.FindSupportLayerGroupById(selectedSupportLayerGroupId.Value);

        if (foundSupportLayerGroup == null)
        {
            _viewModel.SetStatusText("The selected support layer could not be found.");
            return false;
        }

        supportLayerGroup = foundSupportLayerGroup;
        return true;
    }

    private IReadOnlyList<Guid> CreateSelectedBracingTargetSupportIds(IReadOnlyList<SupportEntity> supportEntities, Guid supportLayerGroupId, float? minimumHeight)
    {
        HashSet<Guid> selectedEntityIds = new HashSet<Guid>(_scene.SelectionManager.SelectedEntityIds);
        List<Guid> targetIds = new List<Guid>();

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.SupportLayerGroupId == supportLayerGroupId && selectedEntityIds.Contains(support.Id) && IsEligibleBracingTarget(support, minimumHeight))
            {
                targetIds.Add(support.Id);
            }
        }

        targetIds.Sort();
        return targetIds;
    }

    private static IReadOnlyList<Guid> CreateAllBracingTargetSupportIds(IReadOnlyList<SupportEntity> supportEntities, Guid supportLayerGroupId, float? minimumHeight)
    {
        List<Guid> targetIds = new List<Guid>();

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.SupportLayerGroupId == supportLayerGroupId && IsEligibleBracingTarget(support, minimumHeight))
            {
                targetIds.Add(support.Id);
            }
        }

        targetIds.Sort();
        return targetIds;
    }

    private static bool IsEligibleBracingTarget(SupportEntity support, float? minimumHeight)
    {
        if (support.Style.Kind != SupportStyleKind.Individual)
        {
            return false;
        }

        return !minimumHeight.HasValue || CalculateSupportStemHeight(support) > minimumHeight.Value;
    }

    private static List<SupportModifierDefinition> CreateBracingModifierReplacementList(IReadOnlyList<SupportModifierDefinition> oldModifiers, SupportModifierDefinition? replacement, SupportModifierKind kind, SupportBraceModifierSettings? braceSettings, SupportButtressModifierSettings? buttressSettings, IReadOnlyList<Guid> targetIds, int sourceRevision, Guid toolSessionId)
    {
        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition>();
        bool replaced = false;

        for (int i = 0; i < oldModifiers.Count; i++)
        {
            SupportModifierDefinition oldModifier = oldModifiers[i];

            if (replacement != null && oldModifier.Id == replacement.Id)
            {
                newModifiers.Add(new SupportModifierDefinition(oldModifier.Id, kind, oldModifier.IsEnabled, newModifiers.Count, null, braceSettings, buttressSettings, targetIds, null, sourceRevision, null, null, oldModifier.ToolSessionId));
                replaced = true;
                continue;
            }

            newModifiers.Add(oldModifier);
        }

        if (!replaced)
        {
            newModifiers.Add(new SupportModifierDefinition(Guid.NewGuid(), kind, true, newModifiers.Count, null, braceSettings, buttressSettings, targetIds, null, sourceRevision, null, null, toolSessionId));
        }

        return newModifiers;
    }

    /// <summary>
    /// Suppresses old selected-selected pairs and appends one selected-only Brace modifier.
    /// </summary>
    private static List<SupportModifierDefinition> CreateBraceSelectedModifierList(
        IReadOnlyList<SupportModifierDefinition> oldModifiers,
        SupportBraceModifierSettings settings,
        IReadOnlyList<Guid> selectedTargetIds,
        int sourceRevision,
        Guid toolSessionId)
    {
        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition>(oldModifiers.Count + 1);
        HashSet<Guid> selectedTargetIdSet = new HashSet<Guid>(selectedTargetIds);

        for (int i = 0; i < oldModifiers.Count; i++)
        {
            SupportModifierDefinition oldModifier = oldModifiers[i];

            if (oldModifier.Kind != SupportModifierKind.Brace)
            {
                newModifiers.Add(oldModifier.CloneWithOrder(newModifiers.Count));
                continue;
            }

            HashSet<Guid> oldTargetIds = new HashSet<Guid>(oldModifier.TargetSupportIds);
            List<Guid> excludedTargetIds = new List<Guid>(Math.Min(selectedTargetIdSet.Count, oldTargetIds.Count));

            foreach (Guid selectedTargetId in selectedTargetIdSet)
            {
                if (oldTargetIds.Contains(selectedTargetId))
                {
                    excludedTargetIds.Add(selectedTargetId);
                }
            }

            excludedTargetIds.Sort();
            List<SupportModifierTargetBatch> exclusionBatches = new List<SupportModifierTargetBatch>(oldModifier.ExcludedBraceTargetBatches.Count + 1);

            for (int batchIndex = 0; batchIndex < oldModifier.ExcludedBraceTargetBatches.Count; batchIndex++)
            {
                exclusionBatches.Add(oldModifier.ExcludedBraceTargetBatches[batchIndex].Clone());
            }

            if (excludedTargetIds.Count >= 2)
            {
                exclusionBatches.Add(new SupportModifierTargetBatch(excludedTargetIds));
            }

            newModifiers.Add(new SupportModifierDefinition(
                oldModifier.Id,
                oldModifier.Kind,
                oldModifier.IsEnabled,
                newModifiers.Count,
                null,
                oldModifier.BraceSettings,
                null,
                oldModifier.TargetSupportIds,
                oldModifier.TargetSupportIdBatches,
                sourceRevision,
                oldModifier.ExcludedBracePairs,
                exclusionBatches,
                oldModifier.ToolSessionId));
        }

        newModifiers.Add(new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.Brace,
            true,
            newModifiers.Count,
            null,
            settings,
            null,
            selectedTargetIds,
            null,
            sourceRevision,
            null,
            null,
            toolSessionId));
        return newModifiers;
    }

    private static List<SupportModifierDefinition> CreateModifiersAfterRemovingBracingTargets(IReadOnlyList<SupportModifierDefinition> oldModifiers, Guid modifierId, SupportModifierKind kind, IReadOnlyList<Guid> selectedTargetIds, int sourceRevision)
    {
        HashSet<Guid> selected = new HashSet<Guid>(selectedTargetIds);
        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition>();
        int minimumTargets = kind == SupportModifierKind.Brace ? 2 : 1;

        for (int i = 0; i < oldModifiers.Count; i++)
        {
            SupportModifierDefinition oldModifier = oldModifiers[i];

            if (oldModifier.ToolSessionId != modifierId || oldModifier.Kind != kind)
            {
                newModifiers.Add(oldModifier);
                continue;
            }

            List<Guid> remaining = new List<Guid>();

            for (int targetIndex = 0; targetIndex < oldModifier.TargetSupportIds.Count; targetIndex++)
            {
                if (!selected.Contains(oldModifier.TargetSupportIds[targetIndex]))
                {
                    remaining.Add(oldModifier.TargetSupportIds[targetIndex]);
                }
            }

            if (remaining.Count >= minimumTargets)
            {
                newModifiers.Add(new SupportModifierDefinition(
                    oldModifier.Id,
                    oldModifier.Kind,
                    oldModifier.IsEnabled,
                    newModifiers.Count,
                    null,
                    oldModifier.BraceSettings,
                    oldModifier.ButtressSettings,
                    remaining,
                    null,
                    sourceRevision,
                    CreateBracePairExclusionsForTargets(oldModifier.ExcludedBracePairs, remaining),
                    CreateBraceExclusionBatchesForTargets(oldModifier.ExcludedBraceTargetBatches, remaining),
                    oldModifier.ToolSessionId));
            }
        }

        return newModifiers;
    }

    /// <summary>
    /// Retains only exclusions whose endpoints remain targeted by a reduced Brace modifier.
    /// </summary>
    private static IReadOnlyList<SupportBracePair> CreateBracePairExclusionsForTargets(
        IReadOnlyList<SupportBracePair> exclusions,
        IReadOnlyList<Guid> targetSupportIds)
    {
        HashSet<Guid> targets = new HashSet<Guid>(targetSupportIds);
        List<SupportBracePair> result = new List<SupportBracePair>();

        for (int i = 0; i < exclusions.Count; i++)
        {
            SupportBracePair pair = exclusions[i];

            if (targets.Contains(pair.FirstSupportId) && targets.Contains(pair.SecondSupportId))
            {
                result.Add(pair.Clone());
            }
        }

        return result;
    }

    /// <summary>
    /// Retains compact exclusion batches with at least two surviving target supports.
    /// </summary>
    private static IReadOnlyList<SupportModifierTargetBatch> CreateBraceExclusionBatchesForTargets(
        IReadOnlyList<SupportModifierTargetBatch> exclusionBatches,
        IReadOnlyList<Guid> targetSupportIds)
    {
        HashSet<Guid> targets = new HashSet<Guid>(targetSupportIds);
        List<SupportModifierTargetBatch> result = new List<SupportModifierTargetBatch>();

        for (int batchIndex = 0; batchIndex < exclusionBatches.Count; batchIndex++)
        {
            IReadOnlyList<Guid> excludedTargetIds = exclusionBatches[batchIndex].TargetSupportIds;
            List<Guid> remainingExcludedTargetIds = new List<Guid>(excludedTargetIds.Count);

            for (int targetIndex = 0; targetIndex < excludedTargetIds.Count; targetIndex++)
            {
                if (targets.Contains(excludedTargetIds[targetIndex]))
                {
                    remainingExcludedTargetIds.Add(excludedTargetIds[targetIndex]);
                }
            }

            if (remainingExcludedTargetIds.Count >= 2)
            {
                result.Add(new SupportModifierTargetBatch(remainingExcludedTargetIds));
            }
        }

        return result;
    }

    private SupportModifierDefinition? FindActiveBracingModifier(IReadOnlyList<SupportModifierDefinition> modifiers, SupportModifierKind kind)
    {
        if (!_activeEditingBracingModifierId.HasValue)
        {
            return null;
        }

        return FindModifierByToolSessionAndKind(modifiers, _activeEditingBracingModifierId.Value, kind);
    }

    /// <summary>
    /// Finds the latest internal action of one kind in a tool-launch session.
    /// </summary>
    private static SupportModifierDefinition? FindModifierByToolSessionAndKind(
        IReadOnlyList<SupportModifierDefinition> modifiers,
        Guid toolSessionId,
        SupportModifierKind kind)
    {
        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            if (modifiers[i].ToolSessionId == toolSessionId && modifiers[i].Kind == kind)
            {
                return modifiers[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the latest internal action in a tool-launch session.
    /// </summary>
    private static SupportModifierDefinition? FindLastModifierByToolSession(
        IReadOnlyList<SupportModifierDefinition> modifiers,
        Guid toolSessionId)
    {
        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            if (modifiers[i].ToolSessionId == toolSessionId)
            {
                return modifiers[i];
            }
        }

        return null;
    }

    private static SupportModifierDefinition? FindModifierById(IReadOnlyList<SupportModifierDefinition> modifiers, Guid modifierId)
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].Id == modifierId)
            {
                return modifiers[i];
            }
        }

        return null;
    }

    private static float CalculateSupportStemHeight(SupportEntity support)
    {
        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
        Vector3 headJointPosition = support.TipPosition - (headDirection * support.Profile.HeadHeight);
        Vector3 stemTop = support.BranchLength > 0.0001f
            ? headJointPosition - (Vector3.Normalize(support.BranchDirection) * support.BranchLength)
            : headJointPosition;
        return stemTop.Z - support.BasePosition.Z;
    }

    private static string CreateBracingStatusText(SupportModifierKind kind, SupportBracingEvaluationResult result)
    {
        string label = kind == SupportModifierKind.Buttress ? "Buttress" : "Brace";
        return $"{label}: {result.AddedMemberCount} member(s), {result.TargetSupportCount} target support(s), {result.RejectedCandidateCount} rejected candidate(s).";
    }

    /// <summary>
    /// Gets whether the selected support layer contains a modifier of one reinforcement kind.
    /// </summary>
    private bool HasBracingModifierOfKind(SupportModifierKind modifierKind)
    {
        Guid? supportLayerGroupId = _layerPanelViewModel.GetSelectedSupportLayerGroupId();
        SupportLayerGroup? supportLayerGroup = supportLayerGroupId.HasValue
            ? _document.FindSupportLayerGroupById(supportLayerGroupId.Value)
            : null;

        if (supportLayerGroup == null)
        {
            return false;
        }

        for (int i = 0; i < supportLayerGroup.SupportModifiers.Count; i++)
        {
            if (supportLayerGroup.SupportModifiers[i].Kind == modifierKind)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether the current selected supports can receive a bracing modifier.
    /// </summary>
    private bool HasEnoughBracingSupportsForSelectedApply(SupportModifierKind modifierKind)
    {
        if (!TryGetSelectedSupportLayerGroupForBracing(out SupportLayerGroup? supportLayerGroup))
        {
            return false;
        }

        float? minimumHeight = null;

        if (modifierKind == SupportModifierKind.Buttress
            && _supportBracingToolOptionsControl.TryGetButtressSettings(out SupportButtressModifierSettings settings, out _))
        {
            minimumHeight = settings.MinimumButtressHeight;
        }

        IReadOnlyList<SupportEntity> supportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        int count = CreateSelectedBracingTargetSupportIds(supportEntities, supportLayerGroup.Id, minimumHeight).Count;
        return count >= (modifierKind == SupportModifierKind.Brace ? 2 : 1);
    }

    /// <summary>
    /// Gets whether every eligible support in the current layer can receive a bracing modifier.
    /// </summary>
    private bool HasEnoughBracingSupportsForApplyAll(SupportModifierKind modifierKind)
    {
        if (!TryGetSelectedSupportLayerGroupForBracing(out SupportLayerGroup? supportLayerGroup))
        {
            return false;
        }

        float? minimumHeight = null;

        if (modifierKind == SupportModifierKind.Buttress
            && _supportBracingToolOptionsControl.TryGetButtressSettings(out SupportButtressModifierSettings settings, out _))
        {
            minimumHeight = settings.MinimumButtressHeight;
        }

        IReadOnlyList<SupportEntity> supportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        int count = CreateAllBracingTargetSupportIds(supportEntities, supportLayerGroup.Id, minimumHeight).Count;
        return count >= (modifierKind == SupportModifierKind.Brace ? 2 : 1);
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
        RestoreViewportToolForActiveMode();
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
        if (_activeDirectEditToolSessionId.HasValue)
        {
            ClearDirectEditSessionState();

            if (ToolOptionsHostOverlay.Content == _directEditToolOptionsControl)
            {
                _toolSessionOverlayCoordinator.EndSession();
            }
        }
        WorkspaceModeDefinition mode = _modeDefinitions[modeId];

        if (!mode.IsAvailable || mode.Tool == null)
        {
            _viewModel.SetStatusText($"{mode.DisplayName} mode is not available yet");
            return;
        }

        _activeModeId = modeId;
        _selectTool.ResetSelectionFilter();
        string statusText = GetWorkspaceModeStatusText(mode);
        _activeToolStatusText = statusText;
        _toolManager.SetTool(GetViewportToolForMode(mode));
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
                || ToolOptionsHostOverlay.Content == _lineSupportToolOptionsControl
                || ToolOptionsHostOverlay.Content == _contourSupportToolOptionsControl
                || ToolOptionsHostOverlay.Content == _areaSupportToolOptionsControl)
            && _manualSupportTool.HasSelectedSupportsInActiveEditGroup();

        _ringSupportToolOptionsControl.SetDeleteSelectedSupportsEnabled(
            ToolOptionsHostOverlay.Content == _ringSupportToolOptionsControl && canDeleteSelectedSupports);
        _lineSupportToolOptionsControl.SetDeleteSelectedSupportsEnabled(
            ToolOptionsHostOverlay.Content == _lineSupportToolOptionsControl && canDeleteSelectedSupports);
        _contourSupportToolOptionsControl.SetDeleteSelectedSupportsEnabled(
            ToolOptionsHostOverlay.Content == _contourSupportToolOptionsControl && canDeleteSelectedSupports);
        _areaSupportToolOptionsControl.SetDeleteSelectedSupportsEnabled(
            ToolOptionsHostOverlay.Content == _areaSupportToolOptionsControl && canDeleteSelectedSupports);
        _supportClusterToolOptionsControl.SetApplyToSelectedEnabled(
            ToolOptionsHostOverlay.Content == _supportClusterToolOptionsControl && HasValidClusterSelectionForSelectedApply());
        _supportClusterToolOptionsControl.SetApplyToAllEnabled(
            ToolOptionsHostOverlay.Content == _supportClusterToolOptionsControl && HasEnoughSupportsForApplyAll());
        _supportClusterToolOptionsControl.SetUnclusterSelectedEnabled(
            ToolOptionsHostOverlay.Content == _supportClusterToolOptionsControl && HasSelectedClusteredSupportsInSelectedSupportLayer());
        _supportBracingToolOptionsControl.SetBraceSelectedEnabled(
            ToolOptionsHostOverlay.Content == _supportBracingToolOptionsControl && HasEnoughBracingSupportsForSelectedApply(SupportModifierKind.Brace));
        _supportBracingToolOptionsControl.SetBraceAllEnabled(
            ToolOptionsHostOverlay.Content == _supportBracingToolOptionsControl && HasEnoughBracingSupportsForApplyAll(SupportModifierKind.Brace));
        _supportBracingToolOptionsControl.SetButtressSelectedEnabled(
            ToolOptionsHostOverlay.Content == _supportBracingToolOptionsControl && HasEnoughBracingSupportsForSelectedApply(SupportModifierKind.Buttress));
        _supportBracingToolOptionsControl.SetButtressAllEnabled(
            ToolOptionsHostOverlay.Content == _supportBracingToolOptionsControl && HasEnoughBracingSupportsForApplyAll(SupportModifierKind.Buttress));
        _supportBracingToolOptionsControl.SetRemoveAllBracingEnabled(
            ToolOptionsHostOverlay.Content == _supportBracingToolOptionsControl && HasBracingModifierOfKind(SupportModifierKind.Brace));
        _supportBracingToolOptionsControl.SetRemoveAllButtressesEnabled(
            ToolOptionsHostOverlay.Content == _supportBracingToolOptionsControl && HasBracingModifierOfKind(SupportModifierKind.Buttress));
    }

    /// <summary>
    /// Chooses the viewport input controller for a workspace mode, including idle Manual Support selection.
    /// </summary>
    private ITool GetViewportToolForMode(WorkspaceModeDefinition mode)
    {
        if (mode.Id == WorkspaceModeId.ManualSupport
            && _manualSupportTool.ActiveOperationKind == ManualSupportOperationKind.None)
        {
            return _selectTool;
        }

        return mode.Tool!;
    }
}
