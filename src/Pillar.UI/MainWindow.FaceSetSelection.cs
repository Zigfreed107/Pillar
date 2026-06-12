// MainWindow.FaceSetSelection.cs
// Hosts the reusable face-set selection helper from the shell while keeping the temporary face set outside document state.
using HelixToolkit.Maths;
using Pillar.Core.Selection;
using Pillar.Core.Tools;
using Pillar.Rendering.Tools;
using Pillar.UI.Modes;
using Pillar.UI.Properties;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Pillar.UI;

public partial class MainWindow
{
    private const string DefaultFaceSetSelectionColor = "#CCFFD700";
    private readonly HashSet<FaceSelectionKey> _lastAcceptedFaceSelectionForToolbarTest = new HashSet<FaceSelectionKey>();
    private FaceSetSelectionTool? _faceSetSelectionTool;
    private FaceSetSelectionToolPanel? _faceSetSelectionToolPanel;

    /// <summary>
    /// Launches the face-set selection helper from the temporary toolbar test entry point.
    /// </summary>
    private void FaceSetSelectionLaunchButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        StartFaceSetSelectionSession(_lastAcceptedFaceSelectionForToolbarTest, AcceptToolbarTestFaceSelection);
    }

    /// <summary>
    /// Starts one reusable face-selection session using the supplied initial set and accept callback.
    /// </summary>
    private void StartFaceSetSelectionSession(
        IReadOnlyCollection<FaceSelectionKey> initialSelection,
        Action<IReadOnlyCollection<FaceSelectionKey>> acceptedCallback)
    {
        StartFaceSetSelectionSession(initialSelection, acceptedCallback, true);
    }

    /// <summary>
    /// Starts one reusable face-selection session and optionally preserves the launching tool's state.
    /// </summary>
    private void StartFaceSetSelectionSession(
        IReadOnlyCollection<FaceSelectionKey> initialSelection,
        Action<IReadOnlyCollection<FaceSelectionKey>> acceptedCallback,
        bool cancelLaunchingTool)
    {
        if (acceptedCallback == null)
        {
            throw new ArgumentNullException(nameof(acceptedCallback));
        }

        CloseFaceSetSelectionSession(false);

        if (cancelLaunchingTool)
        {
            _toolManager.CancelActiveTool();
        }

        FaceSetSelectionTool tool = new FaceSetSelectionTool(
            _document,
            _scene,
            initialSelection,
            ReadFaceSetSelectionColor());
        FaceSetSelectionToolPanel panel = new FaceSetSelectionToolPanel
        {
            CoplanarThresholdDegrees = 15.0
        };

        tool.CoplanarThresholdDegrees = panel.CoplanarThresholdDegrees;
        panel.ToolKindChanged += FaceSetSelectionPanel_ToolKindChanged;
        panel.ModifierChanged += FaceSetSelectionPanel_ModifierChanged;
        panel.CoplanarThresholdChanged += FaceSetSelectionPanel_CoplanarThresholdChanged;
        panel.ClearRequested += FaceSetSelectionPanel_ClearRequested;
        panel.UndoRequested += FaceSetSelectionPanel_UndoRequested;
        panel.RedoRequested += FaceSetSelectionPanel_RedoRequested;
        panel.Accepted += () =>
        {
            IReadOnlyCollection<FaceSelectionKey> acceptedSelection = tool.CreateResult();
            acceptedCallback(acceptedSelection);
            CloseFaceSetSelectionSession(true);
        };
        tool.StateChanged += FaceSetSelectionTool_StateChanged;
        tool.LineSelectionPreviewChanged += FaceSetSelectionTool_LineSelectionPreviewChanged;

        _faceSetSelectionTool = tool;
        _faceSetSelectionToolPanel = panel;
        FaceSetSelectionToolHostOverlay.Content = panel;
        FaceSetSelectionToolHostOverlay.Visibility = Visibility.Visible;
        _toolManager.SetTool(tool, cancelLaunchingTool);
        UpdateFaceSetSelectionPanelState();
        _viewModel.SetStatusText("Face Set Selection active");
    }

    /// <summary>
    /// Starts face selection for Area Support and restores the Area Support operation after the helper accepts.
    /// </summary>
    private void StartAreaSupportFaceSelectionSession(
        IReadOnlyCollection<FaceSelectionKey> initialSelection,
        Action<IReadOnlyCollection<FaceSelectionKey>> acceptedCallback)
    {
        if (acceptedCallback == null)
        {
            throw new ArgumentNullException(nameof(acceptedCallback));
        }

        List<FaceSelectionKey> initialSelectionSnapshot = new List<FaceSelectionKey>(initialSelection);

        StartFaceSetSelectionSession(
            initialSelectionSnapshot,
            acceptedSelection =>
            {
                acceptedCallback(acceptedSelection);
                SetActiveMode(WorkspaceModeId.ManualSupport);
                _manualSupportTool.SetActiveOperation(ManualSupportOperationKind.Area);
                ShowToolOptionsControl(_areaSupportToolOptionsControl, ToolSessionPanelSet.SupportPresets);
                SynchronizeWorkflowModePanelSupportOperation(ManualSupportOperationKind.Area);
            },
            false);
    }

    /// <summary>
    /// Stores the toolbar-launched test result until a real client tool owns face selections.
    /// </summary>
    private void AcceptToolbarTestFaceSelection(IReadOnlyCollection<FaceSelectionKey> acceptedSelection)
    {
        _lastAcceptedFaceSelectionForToolbarTest.Clear();

        foreach (FaceSelectionKey selectedFace in acceptedSelection)
        {
            _lastAcceptedFaceSelectionForToolbarTest.Add(selectedFace);
        }

        _viewModel.SetStatusText($"Accepted {_lastAcceptedFaceSelectionForToolbarTest.Count} selected faces");
    }

    /// <summary>
    /// Closes the active helper session and optionally leaves the caller's accepted result intact.
    /// </summary>
    private void CloseFaceSetSelectionSession(bool wasAccepted)
    {
        _ = wasAccepted;

        if (_faceSetSelectionToolPanel != null)
        {
            _faceSetSelectionToolPanel.ToolKindChanged -= FaceSetSelectionPanel_ToolKindChanged;
            _faceSetSelectionToolPanel.ModifierChanged -= FaceSetSelectionPanel_ModifierChanged;
            _faceSetSelectionToolPanel.CoplanarThresholdChanged -= FaceSetSelectionPanel_CoplanarThresholdChanged;
            _faceSetSelectionToolPanel.ClearRequested -= FaceSetSelectionPanel_ClearRequested;
            _faceSetSelectionToolPanel.UndoRequested -= FaceSetSelectionPanel_UndoRequested;
            _faceSetSelectionToolPanel.RedoRequested -= FaceSetSelectionPanel_RedoRequested;
        }

        if (_faceSetSelectionTool != null)
        {
            _faceSetSelectionTool.StateChanged -= FaceSetSelectionTool_StateChanged;
            _faceSetSelectionTool.LineSelectionPreviewChanged -= FaceSetSelectionTool_LineSelectionPreviewChanged;
            _faceSetSelectionTool.Cancel();
        }

        _faceSetSelectionTool = null;
        _faceSetSelectionToolPanel = null;
        FaceSetSelectionToolHostOverlay.Content = null;
        FaceSetSelectionToolHostOverlay.Visibility = Visibility.Collapsed;
        HideFaceSetLineSelectionPreview();

        if (_toolManager.ActiveTool is FaceSetSelectionTool)
        {
            SetActiveMode(WorkspaceModeId.Select);
        }
    }

    /// <summary>
    /// Routes panel tool selection into the active helper.
    /// </summary>
    private void FaceSetSelectionPanel_ToolKindChanged(FaceSetSelectionToolKind toolKind)
    {
        _faceSetSelectionTool?.SetToolKind(toolKind);
    }

    /// <summary>
    /// Routes add/remove modifier changes into the active helper.
    /// </summary>
    private void FaceSetSelectionPanel_ModifierChanged(FaceSetSelectionModifier modifier)
    {
        _faceSetSelectionTool?.SetModifier(modifier);
    }

    /// <summary>
    /// Routes angle threshold edits into the active helper.
    /// </summary>
    private void FaceSetSelectionPanel_CoplanarThresholdChanged(double thresholdDegrees)
    {
        if (_faceSetSelectionTool != null)
        {
            _faceSetSelectionTool.CoplanarThresholdDegrees = thresholdDegrees;
        }
    }

    /// <summary>
    /// Clears the temporary face selection from the panel.
    /// </summary>
    private void FaceSetSelectionPanel_ClearRequested()
    {
        _faceSetSelectionTool?.ClearSelection();
    }

    /// <summary>
    /// Applies session-local face-selection undo from the panel.
    /// </summary>
    private void FaceSetSelectionPanel_UndoRequested()
    {
        _faceSetSelectionTool?.Undo();
    }

    /// <summary>
    /// Applies session-local face-selection redo from the panel.
    /// </summary>
    private void FaceSetSelectionPanel_RedoRequested()
    {
        _faceSetSelectionTool?.Redo();
    }

    /// <summary>
    /// Refreshes count and undo state when the helper selection changes.
    /// </summary>
    private void FaceSetSelectionTool_StateChanged()
    {
        UpdateFaceSetSelectionPanelState();
    }

    /// <summary>
    /// Draws or hides the Line Select screen-space preview segment over the viewport.
    /// </summary>
    private void FaceSetSelectionTool_LineSelectionPreviewChanged(FaceSetLineSelectionPreviewState previewState)
    {
        if (!previewState.IsVisible)
        {
            HideFaceSetLineSelectionPreview();
            return;
        }

        FaceSetLineSelectionPreview.X1 = previewState.Start.X;
        FaceSetLineSelectionPreview.Y1 = previewState.Start.Y;
        FaceSetLineSelectionPreview.X2 = previewState.End.X;
        FaceSetLineSelectionPreview.Y2 = previewState.End.Y;
        FaceSetLineSelectionPreview.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides the Line Select overlay segment.
    /// </summary>
    private void HideFaceSetLineSelectionPreview()
    {
        FaceSetLineSelectionPreview.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Copies active helper state into the floating panel.
    /// </summary>
    private void UpdateFaceSetSelectionPanelState()
    {
        if (_faceSetSelectionTool == null || _faceSetSelectionToolPanel == null)
        {
            return;
        }

        _faceSetSelectionToolPanel.SetToolKind(_faceSetSelectionTool.ToolKind);
        _faceSetSelectionToolPanel.SetModifier(_faceSetSelectionTool.Modifier);
        _faceSetSelectionToolPanel.UpdateState(
            _faceSetSelectionTool.SelectedFaceCount,
            _faceSetSelectionTool.CanUndo,
            _faceSetSelectionTool.CanRedo);
    }

    /// <summary>
    /// Lets shell-level Ctrl+Z/Ctrl+Y prefer the active temporary face-selection session.
    /// </summary>
    private bool TryHandleFaceSetSelectionUndoRedo(bool isRedo)
    {
        if (_faceSetSelectionTool == null)
        {
            return false;
        }

        if (isRedo)
        {
            _faceSetSelectionTool.Redo();
            return true;
        }

        _faceSetSelectionTool.Undo();
        return true;
    }

    /// <summary>
    /// Exits the face helper's Line Select operation without closing the full face-selection session.
    /// </summary>
    private bool TryExitFaceSetLineSelectionTool()
    {
        if (_faceSetSelectionTool == null)
        {
            return false;
        }

        if (_faceSetSelectionTool.ToolKind != FaceSetSelectionToolKind.LineSelect)
        {
            return false;
        }

        _faceSetSelectionTool.SetToolKind(FaceSetSelectionToolKind.Select);
        HideFaceSetLineSelectionPreview();
        _viewModel.SetStatusText("Face Select tool active");
        return true;
    }

    /// <summary>
    /// Mirrors Shift and Alt temporary add/remove overrides into the floating panel buttons.
    /// </summary>
    private void UpdateFaceSetSelectionModifierFromKeyboard()
    {
        if (_faceSetSelectionTool == null || _faceSetSelectionToolPanel == null)
        {
            return;
        }

        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt)
        {
            _faceSetSelectionToolPanel.SetModifier(FaceSetSelectionModifier.Remove);
            return;
        }

        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
        {
            _faceSetSelectionToolPanel.SetModifier(FaceSetSelectionModifier.Add);
            return;
        }

        _faceSetSelectionToolPanel.SetModifier(_faceSetSelectionTool.Modifier);
    }

    /// <summary>
    /// Reads the application-level selected-face color, falling back to translucent yellow.
    /// </summary>
    private static Color4 ReadFaceSetSelectionColor()
    {
        string configuredColor = Settings.Default.FaceSetSelectionColor;

        if (string.IsNullOrWhiteSpace(configuredColor))
        {
            configuredColor = DefaultFaceSetSelectionColor;
        }

        try
        {
            object? convertedColor = ColorConverter.ConvertFromString(configuredColor);

            if (convertedColor is System.Windows.Media.Color color)
            {
                return new Color4(
                    color.R / 255.0f,
                    color.G / 255.0f,
                    color.B / 255.0f,
                    color.A / 255.0f);
            }
        }
        catch (FormatException)
        {
        }

        return new Color4(1.0f, 1.0f, 0.0f, 0.8f);
    }
}
