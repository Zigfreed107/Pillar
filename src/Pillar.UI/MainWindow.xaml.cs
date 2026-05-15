// MainWindow.xaml.cs
// Composes the WPF workspace shell by constructing services, wiring shell state, and performing startup-only setup.
using Pillar.Core.Document;
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.Core.Import;
using Pillar.Core.Persistence;
using Pillar.Core.Snapping;
using Pillar.Core.Supports;
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
    private readonly SupportPresetService _supportPresetService;
    private readonly CadCommandRunner _commandRunner;
    private readonly LayerPanelViewModel _layerPanelViewModel;
    private readonly ViewportCameraService _viewportCameraService;
    private readonly RingSupportToolOptionsControl _ringSupportToolOptionsControl;
    private readonly ScaleToolOptionsControl _scaleToolOptionsControl;
    private readonly Dictionary<WorkspaceModeId, WorkspaceModeDefinition> _modeDefinitions = new Dictionary<WorkspaceModeId, WorkspaceModeDefinition>();
    private WorkspaceModeId _activeModeId = WorkspaceModeId.Select;
    private string _activeToolStatusText = "Select tool active";
    private bool _hasFramedStartupView;
    private bool _isSynchronizingLayerAndViewportSelection;
    private bool _isPrecisionSelectCursorActive;
    private Cursor? _viewportCursorBeforePrecisionSelect;
    private int _previewCalculationCursorDepth;
    private Cursor? _cursorBeforePreviewCalculation;

    public DefaultEffectsManager EffectsManager { get; }

    /// <summary>
    /// Creates the main application window and composes the current CAD services.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        _selectionWindowOverlay = new SelectionWindowOverlayController(this, SelectionWindowOverlay);
        _ringSupportToolOptionsControl = new RingSupportToolOptionsControl();
        _scaleToolOptionsControl = new ScaleToolOptionsControl();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        EffectsManager = new DefaultEffectsManager();
        Viewport.EffectsManager = EffectsManager; // Needed since EffectsManager is initialised AFTER Main window is initialised. Data binding needs to be updated.

        _document = new CadDocument();
        _scene = new SceneManager(Viewport, _document, Properties.Settings.Default.SupportSides, ReadSelectionOutlineColor());
        _viewportCameraService = new ViewportCameraService(Viewport, _document, GetViewportFallbackBounds);
        _snapManager = new SnapManager(_document.SpatialGrid);
        _projection = new ProjectionService(Viewport);
        _toolManager = new ToolManager();
        _commandRunner = new CadCommandRunner(Properties.Settings.Default.UndoHistoryLimit);
        _supportPresetService = new SupportPresetService();
        _selectTool = new SelectTool(Viewport, _document, _scene, _scene.SelectionManager);
        _lineTool = new LineTool(_document, _projection, _scene, _snapManager, _commandRunner);
        _layerPanelViewModel = new LayerPanelViewModel(_document);
        _manualSupportTool = new ManualSupportTool(
            _document,
            _projection,
            _scene,
            _commandRunner,
            _layerPanelViewModel.GetSelectedModelEntityId,
            GetRingSupportSpacingOrDefault,
            GetSelectedSupportProfile);
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
        InitializeFaceAngleHighlightControls();
        _selectTool.SelectionWindowChanged += _selectionWindowOverlay.Update;
        _manualSupportTool.StatusMessageRequested += ManualSupportTool_StatusMessageRequested;
        _manualSupportTool.PrecisionSelectCursorRequested += ManualSupportTool_PrecisionSelectCursorRequested;
        _manualSupportTool.PreviewCalculationStateChanged += ManualSupportTool_PreviewCalculationStateChanged;
        ContentRendered += MainWindow_ContentRendered;
        Closed += MainWindow_Closed;
        SetActiveMode(WorkspaceModeId.Select);
        ApplyDocumentFileResult(_documentFileService.TryOpenAtStartup(Properties.Settings.Default.LoadAtStartup));
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
        _manualSupportTool.PreviewCalculationStateChanged -= ManualSupportTool_PreviewCalculationStateChanged;
        ClearPreviewCalculationCursor();
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

        Viewport.ZoomExtents(GetStartupViewportBounds(), 0.0);
        _hasFramedStartupView = true;
    }

    /// <summary>
    /// Gets startup camera bounds for either the loaded project or the empty workspace grid.
    /// </summary>
    private Rect3D GetStartupViewportBounds()
    {
        bool hasEntityBounds = false;
        Rect3D startupBounds = Rect3D.Empty;

        foreach (CadEntity entity in _document.Entities)
        {
            (System.Numerics.Vector3 Min, System.Numerics.Vector3 Max) bounds = entity.GetBounds();
            double width = global::System.Math.Max(bounds.Max.X - bounds.Min.X, 0.01f);
            double height = global::System.Math.Max(bounds.Max.Y - bounds.Min.Y, 0.01f);
            double depth = global::System.Math.Max(bounds.Max.Z - bounds.Min.Z, 0.01f);
            Rect3D entityBounds = new Rect3D(bounds.Min.X, bounds.Min.Y, bounds.Min.Z, width, height, depth);

            if (!hasEntityBounds)
            {
                startupBounds = entityBounds;
                hasEntityBounds = true;
                continue;
            }

            startupBounds.Union(entityBounds);
        }

        if (!hasEntityBounds)
        {
            return _scene.BackgroundGridBounds;
        }

        return startupBounds;
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
        _ringSupportToolOptionsControl.OptionsChanged += RingSupportToolOptionsControl_OptionsChanged;
        _ringSupportToolOptionsControl.ApplyRequested += RingSupportToolOptionsControl_ApplyRequested;
        _ringSupportToolOptionsControl.CloseRequested += RingSupportToolOptionsControl_CloseRequested;
        _scaleToolOptionsControl.OptionsChanged += ScaleToolOptionsControl_OptionsChanged;
        _scaleToolOptionsControl.FinishRequested += ScaleToolOptionsControl_FinishRequested;
        SupportPresetPanelOverlay.SetPresets(_supportPresetService.Presets);
        SupportPresetPanelOverlay.SelectPreset(_supportPresetService.SelectedPreset);
        SupportPresetPanelOverlay.PresetSelected += SupportPresetPanelOverlay_PresetSelected;
        SupportPresetPanelOverlay.AdvancedRequested += SupportPresetPanelOverlay_AdvancedRequested;
        _supportPresetService.SelectedPresetChanged += SupportPresetService_SelectedPresetChanged;
        LayerPanelOverlay.ImportModelRequested += LayerPanel_ImportModelRequested;
        LayerPanelOverlay.RemoveModelRequested += LayerPanel_RemoveModelRequested;
        LayerPanelOverlay.ExportModelRequested += LayerPanel_ExportModelRequested;
        LayerPanelOverlay.ExportSupportGroupRequested += LayerPanel_ExportSupportGroupRequested;
        LayerPanelOverlay.AddSupportGroupRequested += LayerPanel_AddSupportGroupRequested;
        LayerPanelOverlay.RemoveSupportGroupRequested += LayerPanel_RemoveSupportGroupRequested;
        LayerPanelOverlay.RenameLayerRequested += LayerPanel_RenameLayerRequested;
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
    /// Reads Ring Support spacing from the active Ring Support options panel while keeping WPF controls out of rendering tools.
    /// </summary>
    private float GetRingSupportSpacingOrDefault()
    {
        if (_ringSupportToolOptionsControl.TryGetRingSupportSpacing(out float spacing))
        {
            return spacing;
        }

        _viewModel.SetStatusText("Ring support spacing is invalid; using 5.00 mm.");
        return RingSupportToolOptionsControl.DefaultRingSupportSpacing;
    }

    /// <summary>
    /// Gets a fresh copy of the selected support preset profile for support creation tools.
    /// </summary>
    private SupportProfile GetSelectedSupportProfile()
    {
        return _supportPresetService.CreateSelectedProfile();
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
            Mouse.OverrideCursor = previousCursor;
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
    /// Applies or restores the busy cursor requested by expensive synchronous support preview calculations.
    /// </summary>
    private void ManualSupportTool_PreviewCalculationStateChanged(bool isCalculating)
    {
        SetPreviewCalculationCursor(isCalculating);
    }

    /// <summary>
    /// Shows a wait cursor while support tools perform synchronous preview projection or regeneration.
    /// </summary>
    private void SetPreviewCalculationCursor(bool isCalculating)
    {
        if (isCalculating)
        {
            if (_previewCalculationCursorDepth == 0)
            {
                _cursorBeforePreviewCalculation = Mouse.OverrideCursor;
                Mouse.OverrideCursor = Cursors.Wait;
            }

            _previewCalculationCursorDepth++;
            return;
        }

        if (_previewCalculationCursorDepth == 0)
        {
            return;
        }

        _previewCalculationCursorDepth--;

        if (_previewCalculationCursorDepth == 0)
        {
            Mouse.OverrideCursor = _cursorBeforePreviewCalculation;
            _cursorBeforePreviewCalculation = null;
        }
    }

    /// <summary>
    /// Restores the cursor if the window closes while support preview feedback is active.
    /// </summary>
    private void ClearPreviewCalculationCursor()
    {
        if (_previewCalculationCursorDepth == 0)
        {
            return;
        }

        _previewCalculationCursorDepth = 0;
        Mouse.OverrideCursor = _cursorBeforePreviewCalculation;
        _cursorBeforePreviewCalculation = null;
    }

    /// <summary>
    /// Shows a crosshair cursor over the 3D viewport while an operation is collecting exact pick points.
    /// </summary>
    private void SetPrecisionSelectCursor(bool isPrecisionSelectCursorRequested)
    {
        if (isPrecisionSelectCursorRequested)
        {
            if (_isPrecisionSelectCursorActive)
            {
                return;
            }

            _viewportCursorBeforePrecisionSelect = Viewport.Cursor;
            Viewport.Cursor = Cursors.Cross;
            _isPrecisionSelectCursorActive = true;
            return;
        }

        if (!_isPrecisionSelectCursorActive)
        {
            return;
        }

        _isPrecisionSelectCursorActive = false;
        Viewport.Cursor = _viewportCursorBeforePrecisionSelect;
        _viewportCursorBeforePrecisionSelect = null;
    }
}
