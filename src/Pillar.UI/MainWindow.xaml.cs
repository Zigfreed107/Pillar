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
using System.Windows;
using SelectionWindowOverlayController = Pillar.UI.Overlays.SelectionWindowOverlay;

namespace Pillar.UI;

public partial class MainWindow : Window
{
    /// <summary>
    /// Tracks the shell-owned middle-mouse pan interaction while Helix owns rotation behavior.
    /// </summary>
    private enum ViewportNavigationMode
    {
        None,
        Pan
    }

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
    private readonly Dictionary<WorkspaceModeId, WorkspaceModeDefinition> _modeDefinitions = new Dictionary<WorkspaceModeId, WorkspaceModeDefinition>();
    private WorkspaceModeId _activeModeId = WorkspaceModeId.Select;
    private string _activeToolStatusText = "Select tool active";
    private bool _hasFramedStartupView;
    private ViewportNavigationMode _activeViewportNavigationMode = ViewportNavigationMode.None;
    private Point _lastViewportNavigationPoint;

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
        ConfigureViewportNavigation();
        Viewport.MouseWheel += Viewport_MouseWheel;

        _document = new CadDocument();
        _scene = new SceneManager(Viewport, _document);
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
            _layerPanelViewModel.GetSelectedSupportLayerGroupId);
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
        ContentRendered += MainWindow_ContentRendered;
        SetActiveMode(WorkspaceModeId.Select);
    }

    /// <summary>
    /// Centralizes viewport navigation setup so Helix owns rotation and the shell keeps middle-mouse pan.
    /// </summary>
    private void ConfigureViewportNavigation()
    {
        Viewport.UseDefaultGestures = true;
        Viewport.IsPanEnabled = true;
        Viewport.IsRotationEnabled = true;
        Viewport.IsZoomEnabled = true;
        Viewport.IsInertiaEnabled = false;
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
        _viewModel.SetSelectedEntity(null);
        UpdateUndoRedoButtonState();
    }

    /// <summary>
    /// Connects Layer Panel UI requests to undoable document commands.
    /// </summary>
    private void WireLayerPanel()
    {
        LayerPanelOverlay.DataContext = _layerPanelViewModel;
        LayerPanelOverlay.ImportModelRequested += LayerPanel_ImportModelRequested;
        LayerPanelOverlay.RemoveModelRequested += LayerPanel_RemoveModelRequested;
        LayerPanelOverlay.AddSupportGroupRequested += LayerPanel_AddSupportGroupRequested;
        LayerPanelOverlay.RemoveSupportGroupRequested += LayerPanel_RemoveSupportGroupRequested;
        LayerPanelOverlay.RenameSupportGroupRequested += LayerPanel_RenameSupportGroupRequested;
        LayerPanelOverlay.ChangeSupportGroupColorRequested += LayerPanel_ChangeSupportGroupColorRequested;
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
        _viewModel.SetSelectedEntity(null);
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
}
