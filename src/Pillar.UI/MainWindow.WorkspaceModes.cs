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

        if (string.Equals(selectedToolName, "Cluster Supports", StringComparison.Ordinal))
        {
            ShowSupportClusterTool(null);
            return;
        }

        HideToolOptionsOverlay();
    }

    /// <summary>
    /// Hides the active tool options overlay when no tool with options is selected.
    /// </summary>
    private void HideToolOptionsOverlay()
    {
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
        if (IsSupportToolEditActive() || IsSupportClusterToolActive())
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
    /// Gets whether the Cluster Supports panel owns Tool Options until Close is clicked.
    /// </summary>
    private bool IsSupportClusterToolActive()
    {
        return ToolOptionsHostOverlay.Content == _supportClusterToolOptionsControl;
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

        if (supportLayerGroup.GeneratorKind == SupportGroupGeneratorKind.ContourSupport)
        {
            ContourSupportSettings? settings = supportLayerGroup.ContourSupportSettings;

            if (settings == null)
            {
                HideToolOptionsOverlay();
                _viewModel.SetStatusText("This support group is missing Contour Support settings.");
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
            if (modifiers[i].Id == e.ModifierId)
            {
                SetActiveMode(WorkspaceModeId.ManualSupport);

                if (modifiers[i].Kind == SupportModifierKind.Cluster)
                {
                    _layerPanelViewModel.SelectSupportGroupLayer(supportLayerGroup.Id);
                    ShowSupportClusterTool(modifiers[i]);
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
    /// Refreshes Cluster Supports diagnostics when options change.
    /// </summary>
    private void SupportClusterToolOptionsControl_OptionsChanged(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshSupportClusterPreviewStatus();
    }

    /// <summary>
    /// Applies the Cluster Supports modifier to the selected support layer.
    /// </summary>
    private void SupportClusterToolOptionsControl_ApplyRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

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
        IReadOnlyList<SupportEntity> sourceSupportEntities = isReplacingExistingClusterModifier
            ? RestoreIndividualSupportsForClusteredOutputs(oldSupportEntities)
            : oldSupportEntities;
        IReadOnlyList<Guid> targetSupportIds = CreateClusterTargetSupportIds(oldSupportEntities, supportLayerGroup.Id, _supportClusterToolOptionsControl.SelectedScope);

        if (_supportClusterToolOptionsControl.SelectedScope == SupportModifierScope.Selection && targetSupportIds.Count < 2)
        {
            _viewModel.SetStatusText("Select at least two supports in this support layer before applying selection clustering.");
            return;
        }

        IReadOnlyList<SupportModifierTargetBatch> effectiveTargetSupportIdBatches = CreateEffectiveClusterTargetSupportIdBatches(
            oldModifiers,
            _supportClusterToolOptionsControl.SelectedScope,
            targetSupportIds);
        List<SupportModifierDefinition> newModifiers = CreateClusterModifierReplacementList(
            oldModifiers,
            replacementClusterModifier,
            settings,
            _supportClusterToolOptionsControl.SelectedScope,
            effectiveTargetSupportIdBatches,
            supportLayerGroup.SourceGeneratorRevision);
        IReadOnlyList<SupportEntity> newSupportEntities = SupportModifierPipeline.ApplyModifiers(sourceSupportEntities, newModifiers);
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

        _activeEditingClusterModifierId = appliedClusterModifier.Id;

        bool isEditingAfterApply = _activeEditingClusterModifierId.HasValue;
        _supportClusterToolOptionsControl.SetClusterSettings(settings, _supportClusterToolOptionsControl.SelectedScope, isEditingAfterApply);
        ShowToolOptionsControl(_supportClusterToolOptionsControl, ToolSessionPanelSet.None);
        _supportClusterToolOptionsControl.SetStatusText(CreateClusterStatusText(previewResult));
        _viewModel.SetStatusText(CreateClusterStatusText(previewResult));
    }

    /// <summary>
    /// Restores the selected shared-stem cluster to individual supports.
    /// </summary>
    private void SupportClusterToolOptionsControl_UnclusterSelectedRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

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

        IReadOnlyList<SupportEntity> restoredSourceEntities = RestoreIndividualSupportsForClusteredOutputs(oldSupportEntities);
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
    }

    /// <summary>
    /// Removes the Cluster modifier currently being edited.
    /// </summary>
    private void SupportClusterToolOptionsControl_RemoveAllRequested(object? sender, System.EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_activeEditingClusterModifierId.HasValue || !TryGetSelectedSupportLayerGroupForClustering(out SupportLayerGroup? supportLayerGroup))
        {
            return;
        }

        IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        IReadOnlyList<SupportEntity> sourceSupportEntities = RestoreIndividualSupportsForClusteredOutputs(oldSupportEntities);
        IReadOnlyList<SupportModifierDefinition> oldModifiers = supportLayerGroup.SupportModifiers;
        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition>();

        for (int i = 0; i < oldModifiers.Count; i++)
        {
            if (oldModifiers[i].Id != _activeEditingClusterModifierId.Value)
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
        _supportClusterToolOptionsControl.SetClusterSettings(SupportClusterModifierSettings.CreateDefault(), SupportModifierScope.WholeLayer, false);
        _supportClusterToolOptionsControl.SetStatusText("Cluster modifier removed.");
        _viewModel.SetStatusText("Cluster modifier removed.");
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

        _activeEditingClusterModifierId = modifier?.Id;
        SupportClusterModifierSettings settings = modifier?.ClusterSettings ?? SupportClusterModifierSettings.CreateDefault();
        SupportModifierScope scope = modifier?.Scope ?? SupportModifierScope.WholeLayer;
        _supportClusterToolOptionsControl.SetClusterSettings(settings, scope, modifier != null);
        FocusSupportLayerForClusterTool(supportLayerGroup.Id);
        ActivateNormalSelectionForSupportClusterTool(supportLayerGroup.Id);
        ShowToolOptionsControl(_supportClusterToolOptionsControl, ToolSessionPanelSet.None);
        RefreshSupportClusterPreviewStatus();
        _viewModel.SetStatusText("Cluster Supports tool active.");
        _ = supportLayerGroup;
    }

    /// <summary>
    /// Routes Cluster Supports viewport input through the normal selection tool so selected-support scope behaves like ordinary selection.
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
            ? RestoreIndividualSupportsForClusteredOutputs(currentSupportEntities)
            : currentSupportEntities;
        IReadOnlyList<Guid> targetSupportIds = CreateClusterTargetSupportIds(currentSupportEntities, supportLayerGroup.Id, _supportClusterToolOptionsControl.SelectedScope);

        if (_supportClusterToolOptionsControl.SelectedScope == SupportModifierScope.Selection && targetSupportIds.Count < 2)
        {
            _supportClusterToolOptionsControl.SetStatusText("Select at least two supports in this support layer to preview selection clustering.");
            return;
        }

        SupportModifierDefinition previewModifier = SupportModifierDefinition.CreateNew(
            SupportModifierKind.Cluster,
            _supportClusterToolOptionsControl.SelectedScope,
            0,
            settings,
            _supportClusterToolOptionsControl.SelectedScope == SupportModifierScope.Selection ? targetSupportIds : null,
            _supportClusterToolOptionsControl.SelectedScope == SupportModifierScope.Selection ? supportLayerGroup.SourceGeneratorRevision : null);
        SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(previewSourceEntities, previewModifier);
        _supportClusterToolOptionsControl.SetStatusText(CreateClusterStatusText(result));
        SetAutomaticStemDiameterFields(previewSourceEntities, targetSupportIds, _supportClusterToolOptionsControl.SelectedScope);
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
    /// Builds the target support identities for the selected clustering scope.
    /// </summary>
    private IReadOnlyList<Guid> CreateClusterTargetSupportIds(
        IReadOnlyList<SupportEntity> supportEntities,
        Guid supportLayerGroupId,
        SupportModifierScope scope)
    {
        if (scope == SupportModifierScope.WholeLayer)
        {
            return Array.Empty<Guid>();
        }

        HashSet<Guid> targetSupportIds = new HashSet<Guid>();
        HashSet<Guid> selectedEntityIds = new HashSet<Guid>(_scene.SelectionManager.SelectedEntityIds);
        List<Vector2> selectedClusterCenters = new List<Vector2>();

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.SupportLayerGroupId != supportLayerGroupId || !selectedEntityIds.Contains(support.Id))
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
        SupportModifierScope scope,
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
                    scope,
                    oldModifier.IsEnabled,
                    i,
                    settings,
                    null,
                    scope == SupportModifierScope.Selection ? targetSupportIdBatches : null,
                    scope == SupportModifierScope.Selection ? sourceGeneratorRevision : null));
                replacedExistingModifier = true;
            }
            else if (replacementClusterModifier != null && oldModifier.Kind == SupportModifierKind.Cluster)
            {
                continue;
            }
            else
            {
                newModifiers.Add(oldModifier);
            }
        }

        if (!replacedExistingModifier)
        {
            newModifiers.Add(SupportModifierDefinition.CreateNew(
                SupportModifierKind.Cluster,
                scope,
                newModifiers.Count,
                settings,
                null,
                scope == SupportModifierScope.Selection ? targetSupportIdBatches : null,
                scope == SupportModifierScope.Selection ? sourceGeneratorRevision : null));
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
                if (modifiers[i].Id == _activeEditingClusterModifierId.Value && modifiers[i].Kind == SupportModifierKind.Cluster)
                {
                    return modifiers[i];
                }
            }
        }

        return FindFirstClusterModifier(modifiers);
    }

    /// <summary>
    /// Appends the current selection as a separate clustering batch on the layer's cumulative Cluster modifier.
    /// </summary>
    private static IReadOnlyList<SupportModifierTargetBatch> CreateEffectiveClusterTargetSupportIdBatches(
        IReadOnlyList<SupportModifierDefinition> oldModifiers,
        SupportModifierScope scope,
        IReadOnlyList<Guid> selectedTargetSupportIds)
    {
        if (scope == SupportModifierScope.WholeLayer)
        {
            return Array.Empty<SupportModifierTargetBatch>();
        }

        List<SupportModifierTargetBatch> targetSupportIdBatches = new List<SupportModifierTargetBatch>();

        for (int i = 0; i < oldModifiers.Count; i++)
        {
            if (oldModifiers[i].Kind != SupportModifierKind.Cluster || oldModifiers[i].Scope != SupportModifierScope.Selection)
            {
                continue;
            }

            IReadOnlyList<SupportModifierTargetBatch> oldBatches = oldModifiers[i].TargetSupportIdBatches;

            for (int batchIndex = 0; batchIndex < oldBatches.Count; batchIndex++)
            {
                targetSupportIdBatches.Add(oldBatches[batchIndex].Clone());
            }
        }

        targetSupportIdBatches.Add(new SupportModifierTargetBatch(selectedTargetSupportIds));
        return targetSupportIdBatches;
    }

    /// <summary>
    /// Converts branched clustered output back to individual supports for modifier editing and removal.
    /// </summary>
    private static IReadOnlyList<SupportEntity> RestoreIndividualSupportsForClusteredOutputs(IReadOnlyList<SupportEntity> supportEntities)
    {
        List<SupportEntity> restoredSupports = new List<SupportEntity>(supportEntities.Count);

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.Style.Kind != SupportStyleKind.Clustered)
            {
                restoredSupports.Add(support);
                continue;
            }

            Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
            Vector3 headJointPosition = support.TipPosition - (headDirection * support.Profile.HeadHeight);
            Vector3 basePosition = new Vector3(headJointPosition.X, headJointPosition.Y, support.BasePosition.Z);
            restoredSupports.Add(SupportEntity.CreateLoaded(
                support.Id,
                support.Name,
                support.SupportLayerGroupId,
                support.TipPosition,
                basePosition,
                support.HeadDirection,
                0.0f,
                Vector3.UnitZ,
                support.Profile));
        }

        return restoredSupports;
    }

    /// <summary>
    /// Updates the automatic diameter fields from the current candidate population.
    /// </summary>
    private void SetAutomaticStemDiameterFields(
        IReadOnlyList<SupportEntity> supportEntities,
        IReadOnlyList<Guid> targetSupportIds,
        SupportModifierScope scope)
    {
        HashSet<Guid>? targetIdSet = scope == SupportModifierScope.Selection
            ? new HashSet<Guid>(targetSupportIds)
            : null;
        float bottomSum = 0.0f;
        float topSum = 0.0f;

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.Style.Kind != SupportStyleKind.Individual || (targetIdSet != null && !targetIdSet.Contains(support.Id)))
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
    /// Creates concise user-facing cluster diagnostics.
    /// </summary>
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
                    SupportModifierScope.Selection,
                    oldModifier.IsEnabled,
                    i,
                    removedClusterModifier.ClusterSettings,
                    null,
                    remainingClusterTargetBatches,
                    sourceGeneratorRevision));
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
    private static string CreateClusterStatusText(SupportClusterEvaluationResult result)
    {
        return $"{result.ClusterCount} cluster(s), {result.ClusteredSupportCount} clustered support(s), {result.UnchangedSupportCount} unchanged, {result.RejectedCandidateCount} rejected candidate(s).";
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
        _supportClusterToolOptionsControl.SetUnclusterSelectedEnabled(
            ToolOptionsHostOverlay.Content == _supportClusterToolOptionsControl && HasSelectedClusteredSupportsInSelectedSupportLayer());
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
