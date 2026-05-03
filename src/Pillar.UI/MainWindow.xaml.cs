// MainWindow.xaml.cs
// Composes the WPF workspace shell by constructing services, wiring shell state, and performing startup-only setup.
using Pillar.Core.Document;
using Pillar.Commands;
using Pillar.Core.Import;
using Pillar.Core.Persistence;
using Pillar.Core.Snapping;
using Pillar.Core.Tools;
using Pillar.Rendering.Math;
using Pillar.Rendering.Scene;
using Pillar.Rendering.Tools;
using Pillar.UI.Layers;
using Pillar.UI.Modes;
using Pillar.UI.Services;
using Pillar.ViewModels;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using SelectionWindowOverlayController = Pillar.UI.Overlays.SelectionWindowOverlay;

namespace Pillar.UI;

public partial class MainWindow : Window
{
    private readonly CadDocument _document;
    private readonly SceneManager _scene;
    private readonly MainViewModel _viewModel;
    private readonly ProjectionService _projection;
    private readonly ToolManager _toolManager;
    private readonly SelectTool _selectTool;
    private readonly LineTool _lineTool;
    private readonly ManualSupportTool _manualSupportTool;
    private readonly IModelImporter _stlImporter;
    private readonly SnapManager _snapManager;
    private readonly SelectionWindowOverlayController _selectionWindowOverlay;
    private readonly DocumentFileService _documentFileService;
    private readonly CadCommandRunner _commandRunner;
    private readonly LayerPanelViewModel _layerPanelViewModel;
    private readonly ViewportCameraService _viewportCameraService;
    private readonly Dictionary<WorkspaceModeId, WorkspaceModeDefinition> _modeDefinitions = new Dictionary<WorkspaceModeId, WorkspaceModeDefinition>();
    private WorkspaceModeId _activeModeId = WorkspaceModeId.Select;
    private string _activeToolStatusText = "Select tool active";
    private bool _hasFramedStartupView;
    private bool _isSynchronizingLayerAndViewportSelection;
    private bool _isPrecisionSelectCursorActive;
    private Cursor? _cursorBeforePrecisionSelect;

    public DefaultEffectsManager EffectsManager { get; }

    /// <summary>
    /// Creates the main application window and composes the current CAD services.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        _selectionWindowOverlay = new SelectionWindowOverlayController(this, SelectionWindowOverlay);

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        EffectsManager = new DefaultEffectsManager();
        Viewport.EffectsManager = EffectsManager; // Needed since EffectsManager is initialised AFTER Main window is initialised. Data binding needs to be updated.

        _document = new CadDocument();
        _scene = new SceneManager(Viewport, _document);
        _viewportCameraService = new ViewportCameraService(Viewport, _document, GetViewportFallbackBounds);
        _snapManager = new SnapManager(_document.SpatialGrid);
        _projection = new ProjectionService(Viewport);
        _toolManager = new ToolManager();
        _commandRunner = new CadCommandRunner(Properties.Settings.Default.UndoHistoryLimit);
        _selectTool = new SelectTool(Viewport, _document, _scene, _scene.SelectionManager);
        _lineTool = new LineTool(_document, _projection, _scene, _snapManager, _commandRunner);
        _layerPanelViewModel = new LayerPanelViewModel(_document);
        _manualSupportTool = new ManualSupportTool(
            _document,
            _projection,
            _scene,
            _commandRunner,
            _layerPanelViewModel.GetSelectedModelEntityId,
            GetRingSupportSpacingOrDefault);
        _stlImporter = new StlImporter();
        WireLayerPanel();
        _documentFileService = new DocumentFileService(
            this,
            _document,
            _scene.SelectionManager,
            new GphDocumentSerializer(),
            CancelTransientToolState,
            _commandRunner.ClearHistory,
            ActivateSelectToolForDocumentCommand);

        WireWorkspaceState();
        RegisterWorkspaceModes();
        _selectTool.SelectionWindowChanged += _selectionWindowOverlay.Update;
        _manualSupportTool.StatusMessageRequested += ManualSupportTool_StatusMessageRequested;
        _manualSupportTool.PrecisionSelectCursorRequested += ManualSupportTool_PrecisionSelectCursorRequested;
        ContentRendered += MainWindow_ContentRendered;
        Closed += MainWindow_Closed;
        SetActiveMode(WorkspaceModeId.Select);
    }

    /// <summary>
    /// Gets the fallback bounds used for camera framing and clip-plane management when no entities are present.
    /// </summary>
    private Rect3D GetViewportFallbackBounds()
    {
        return _scene.BackgroundGridBounds;
    }

    /// <summary>
    /// Disposes shell-owned camera helpers when the window closes.
    /// </summary>
    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        SetPrecisionSelectCursor(false);
        _manualSupportTool.PrecisionSelectCursorRequested -= ManualSupportTool_PrecisionSelectCursorRequested;
        _viewportCameraService.Dispose();
    }

    /// <summary>
    /// Frames the background grid once the first viewport layout has completed.
    /// </summary>
    private void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_hasFramedStartupView)
        {
            return;
        }

        if (Viewport.ActualWidth <= 0.0 || Viewport.ActualHeight <= 0.0)
        {
            return;
        }

        Viewport.ZoomExtents(_scene.BackgroundGridBounds, 0.0);
        _hasFramedStartupView = true;
    }

    /// <summary>
    /// Connects domain events to shell UI state so workspace feedback stays outside the renderer.
    /// </summary>
    private void WireWorkspaceState()
    {
        _scene.SelectionManager.SelectionChanged += OnSelectionChanged;
        _commandRunner.HistoryChanged += UpdateUndoRedoButtonState;
        _viewModel.SetStatusText("Ready");
        UpdateUndoRedoButtonState();
    }

    /// <summary>
    /// Connects Layer Panel UI requests to undoable document commands.
    /// </summary>
    private void WireLayerPanel()
    {
        LayerPanelOverlay.DataContext = _layerPanelViewModel;
        WorkflowModePanelOverlay.DataContext = _layerPanelViewModel;
        _layerPanelViewModel.PropertyChanged += LayerPanelViewModel_PropertyChanged;
        WorkflowModePanelOverlay.SupportOperationToggleRequested += WorkflowModePanelOverlay_SupportOperationToggleRequested;
        WorkflowModePanelOverlay.ToolSelected += WorkflowModePanelOverlay_ToolSelected;
        ToolOptionsPanelOverlay.RingSupportOptionsChanged += ToolOptionsPanelOverlay_RingSupportOptionsChanged;
        ToolOptionsPanelOverlay.RingSupportApplyRequested += ToolOptionsPanelOverlay_RingSupportApplyRequested;
        ToolOptionsPanelOverlay.RingSupportCancelRequested += ToolOptionsPanelOverlay_RingSupportCancelRequested;
        LayerPanelOverlay.ImportModelRequested += LayerPanel_ImportModelRequested;
        LayerPanelOverlay.RemoveModelRequested += LayerPanel_RemoveModelRequested;
        LayerPanelOverlay.AddSupportGroupRequested += LayerPanel_AddSupportGroupRequested;
        LayerPanelOverlay.RemoveSupportGroupRequested += LayerPanel_RemoveSupportGroupRequested;
        LayerPanelOverlay.RenameSupportGroupRequested += LayerPanel_RenameSupportGroupRequested;
        LayerPanelOverlay.ChangeSupportGroupColorRequested += LayerPanel_ChangeSupportGroupColorRequested;
        LayerPanelOverlay.EditSupportGroupRequested += LayerPanel_EditSupportGroupRequested;
    }

    /// <summary>
    /// Cancels active tool previews before document-level file commands change entities.
    /// </summary>
    private void CancelTransientToolState()
    {
        _toolManager.CancelActiveTool();
    }

    /// <summary>
    /// Returns the workspace to selection mode after a document-level file command.
    /// </summary>
    private void ActivateSelectToolForDocumentCommand()
    {
        SetActiveMode(WorkspaceModeId.Select);
    }

    /// <summary>
    /// Applies user-facing status updates returned by document file commands.
    /// </summary>
    private void ApplyDocumentFileResult(DocumentFileOperationResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StatusText))
        {
            _viewModel.SetStatusText(result.StatusText);
        }

        if (!string.IsNullOrWhiteSpace(result.ToolPanelText))
        {
            _viewModel.SetToolPanelText(result.ToolPanelText);
        }
    }

    /// <summary>
    /// Reads Ring Support spacing from the Tool Options Panel while keeping WPF controls out of rendering tools.
    /// </summary>
    private float GetRingSupportSpacingOrDefault()
    {
        if (ToolOptionsPanelOverlay.TryGetRingSupportSpacing(out float spacing))
        {
            return spacing;
        }

        _viewModel.SetStatusText("Ring support spacing is invalid; using 5.00 mm.");
        return ToolOptionsPanel.DefaultRingSupportSpacing;
    }

    /// <summary>
    /// Shows a wait cursor while synchronous geometry or hit-test work blocks the UI thread.
    /// </summary>
    private void RunWithWaitCursor(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        Cursor? previousCursor = Mouse.OverrideCursor;
        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            action();
        }
        finally
        {
            if (ReferenceEquals(previousCursor, Cursors.Cross) && !_isPrecisionSelectCursorActive)
            {
                Mouse.OverrideCursor = _cursorBeforePrecisionSelect;
                _cursorBeforePrecisionSelect = null;
            }
            else
            {
                Mouse.OverrideCursor = previousCursor;
            }
        }
    }

    /// <summary>
    /// Applies or restores the precision-selection cursor requested by transient CAD operations.
    /// </summary>
    private void ManualSupportTool_PrecisionSelectCursorRequested(bool isPrecisionSelectCursorRequested)
    {
        SetPrecisionSelectCursor(isPrecisionSelectCursorRequested);
    }

    /// <summary>
    /// Shows a crosshair cursor while an operation is collecting exact pick points.
    /// </summary>
    private void SetPrecisionSelectCursor(bool isPrecisionSelectCursorRequested)
    {
        if (isPrecisionSelectCursorRequested)
        {
            if (_isPrecisionSelectCursorActive)
            {
                return;
            }

            _cursorBeforePrecisionSelect = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Cross;
            _isPrecisionSelectCursorActive = true;
            return;
        }

        if (!_isPrecisionSelectCursorActive)
        {
            return;
        }

        _isPrecisionSelectCursorActive = false;

        if (ReferenceEquals(Mouse.OverrideCursor, Cursors.Cross))
        {
            Mouse.OverrideCursor = _cursorBeforePrecisionSelect;
            _cursorBeforePrecisionSelect = null;
        }
    }
}
