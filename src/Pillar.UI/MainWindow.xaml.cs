// MainWindow.xaml.cs
// Composes the WPF workspace shell by constructing services, wiring shell state, and performing startup-only setup.
using Pillar.Core.Document;
using Pillar.Commands;
using Pillar.Core.Entities;
using Pillar.Core.Import;
using Pillar.Core.Layers;
using Pillar.Core.Persistence;
using Pillar.Core.Snapping;
using Pillar.Core.Supports;
using Pillar.Core.Tools;
using Pillar.Rendering.Math;
using Pillar.Rendering.BackgroundGrid;
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
    private readonly DirectEditTool _directEditTool;
    private readonly IModelImporter _stlImporter;
    private readonly SnapManager _snapManager;
    private readonly SelectionWindowOverlayController _selectionWindowOverlay;
    private readonly DocumentFileService _documentFileService;
    private readonly SupportPresetService _supportPresetService;
    private readonly CadCommandRunner _commandRunner;
    private readonly LayerPanelViewModel _layerPanelViewModel;
    private readonly PrintableVolumeDefinition _printableVolumeDefinition;
    private readonly ViewportCameraService _viewportCameraService;
    private readonly RingSupportToolOptionsControl _ringSupportToolOptionsControl;
    private readonly LineSupportToolOptionsControl _lineSupportToolOptionsControl;
    private readonly ContourSupportToolOptionsControl _contourSupportToolOptionsControl;
    private readonly AreaSupportToolOptionsControl _areaSupportToolOptionsControl;
    private readonly SupportClusterToolOptionsControl _supportClusterToolOptionsControl;
    private readonly SupportBracingToolOptionsControl _supportBracingToolOptionsControl;
    private readonly DirectEditToolOptionsControl _directEditToolOptionsControl;
    private readonly ScaleToolOptionsControl _scaleToolOptionsControl;
    private readonly RotationToolOptionsControl _rotationToolOptionsControl;
    private readonly ToolSessionOptionsControl _toolSessionOptionsControl;
    private readonly ToolSessionOverlayCoordinator _toolSessionOverlayCoordinator;
    private readonly Dictionary<WorkspaceModeId, WorkspaceModeDefinition> _modeDefinitions = new Dictionary<WorkspaceModeId, WorkspaceModeDefinition>();
    private Action? _activePlaceholderToolFinishAction;
    private WorkspaceModeId _activeModeId = WorkspaceModeId.Select;
    private string _activeToolStatusText = "Select tool active";
    private bool _hasFramedStartupView;
    private bool _isSynchronizingLayerAndViewportSelection;
    private Guid? _activeEditingClusterModifierId;
    private Guid? _activeEditingBracingModifierId;
    private Guid? _activeDirectEditToolSessionId;
    private Guid? _activeDirectEditSupportLayerGroupId;
    private int? _activeDirectEditCutoffIndex;
    private SupportModifierKind? _activeEditingBracingModifierKind;
    private Dictionary<Guid, bool>? _clusterToolSupportLayerVisibilitySnapshot;
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
        _lineSupportToolOptionsControl = new LineSupportToolOptionsControl();
        _contourSupportToolOptionsControl = new ContourSupportToolOptionsControl();
        _areaSupportToolOptionsControl = new AreaSupportToolOptionsControl();
        _supportClusterToolOptionsControl = new SupportClusterToolOptionsControl();
        _supportBracingToolOptionsControl = new SupportBracingToolOptionsControl();
        _directEditToolOptionsControl = new DirectEditToolOptionsControl();
        _scaleToolOptionsControl = new ScaleToolOptionsControl();
        _rotationToolOptionsControl = new RotationToolOptionsControl();
        _toolSessionOptionsControl = new ToolSessionOptionsControl();
        _toolSessionOverlayCoordinator = new ToolSessionOverlayCoordinator(
            WorkflowModePanelOverlay,
            ToolOptionsHostOverlay,
            SupportPresetPanelOverlay);

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        EffectsManager = new DefaultEffectsManager();
        Viewport.EffectsManager = EffectsManager; // Needed since EffectsManager is initialised AFTER Main window is initialised. Data binding needs to be updated.
        InitializeViewportPostProcessingOptions();

        _document = new CadDocument();
        BackgroundGridDefinition backgroundGridDefinition = ReadBackgroundGridDefinition();
        _printableVolumeDefinition = backgroundGridDefinition.PrintableVolume;
        _scene = new SceneManager(
            Viewport,
            _document,
            Properties.Settings.Default.SupportSides,
            ReadSelectionOutlineColor(),
            ReadSelectionOutlineSize(),
            backgroundGridDefinition,
            ReadDefaultModelMaterial());
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
            GetLineSupportSpacingOrDefault,
            GetLineSupportPlaceSupportsAtBendsOrDefault,
            GetContourSupportZHeightOrDefault,
            GetContourSupportCoplanarThresholdOrDefault,
            GetContourSupportSpacingOrDefault,
            GetContourSupportStartOffsetOrDefault,
            GetContourSupportFinalOffsetOrDefault,
            GetAreaSupportSpacingOrDefault,
            GetAreaSupportBoundaryOffsetOrDefault,
            GetAreaSupportBoundarySpacingOrDefault,
            GetAreaSupportConcaveCornerAngleOrDefault,
            GetAreaSupportThinRegions,
            GetAreaSupportMinimumThinRegionThicknessOrDefault,
            GetAreaSupportFillMode,
            GetAreaSupportAdditionalOffsetCountOrDefault,
            GetAreaSupportOffsetSpacingOrDefault,
            GetAreaSupportShowSpacing,
            SetContourSupportZHeight,
            SetContourSupportClosedState,
            StartAreaSupportFaceSelectionSession,
            GetSelectedSupportProfile);
        _directEditTool = new DirectEditTool(Viewport, _document, _scene, _projection, _selectTool);
        _stlImporter = new StlImporter();
        WireLayerPanel();
        InitializeModelClippingControls();
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
        InitializeScaledCursorControls();
        _selectTool.SelectionWindowChanged += _selectionWindowOverlay.Update;
        _manualSupportTool.SelectionWindowChanged += _selectionWindowOverlay.Update;
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
    /// Reads printer volume settings and adapts the shared grid definition used by rendering and camera framing.
    /// </summary>
    private static BackgroundGridDefinition ReadBackgroundGridDefinition()
    {
        PrintableVolumeDefinition defaultVolume = BackgroundGridDefinition.Default.PrintableVolume;
        float xDistance = ReadPositiveFloatSetting(Properties.Settings.Default.PrintableVolumeX, defaultVolume.XDistance);
        float yDistance = ReadPositiveFloatSetting(Properties.Settings.Default.PrintableVolumeY, defaultVolume.YDistance);
        float zDistance = ReadPositiveFloatSetting(Properties.Settings.Default.PrintableVolumeZ, defaultVolume.ZDistance);
        PrintableVolumeDefinition printableVolume = new PrintableVolumeDefinition(xDistance, yDistance, zDistance);

        return BackgroundGridDefinition.Default.WithPrintableVolume(printableVolume);
    }

    /// <summary>
    /// Converts one numeric setting to a positive float, falling back to the default when config values are invalid.
    /// </summary>
    private static float ReadPositiveFloatSetting(double configuredValue, float fallbackValue)
    {
        if (double.IsNaN(configuredValue) || double.IsInfinity(configuredValue) || configuredValue <= 0.0)
        {
            return fallbackValue;
        }

        if (configuredValue > float.MaxValue)
        {
            return fallbackValue;
        }

        return (float)configuredValue;
    }

    /// <summary>
    /// Disposes shell-owned camera helpers when the window closes.
    /// </summary>
    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        CloseFaceSetSelectionSession(false);
        SetPrecisionSelectCursor(false);
        _scene.HideScaledCursorPreview();
        _manualSupportTool.SelectionWindowChanged -= _selectionWindowOverlay.Update;
        _manualSupportTool.PrecisionSelectCursorRequested -= ManualSupportTool_PrecisionSelectCursorRequested;
        _manualSupportTool.PreviewCalculationStateChanged -= ManualSupportTool_PreviewCalculationStateChanged;
        ClipRangeSliderOverlay.ClipRangeChanged -= ClipRangeSliderOverlay_ClipRangeChanged;
        SetSelectedModelBoundsClipIndicatorMesh(null);
        DisposeViewportPostProcessingOptions();
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

        ApplyViewportPostProcessingOptions();
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
        _ringSupportToolOptionsControl.DeleteRequested += RingSupportToolOptionsControl_DeleteRequested;
        _lineSupportToolOptionsControl.OptionsChanged += LineSupportToolOptionsControl_OptionsChanged;
        _lineSupportToolOptionsControl.ApplyRequested += LineSupportToolOptionsControl_ApplyRequested;
        _lineSupportToolOptionsControl.CloseRequested += LineSupportToolOptionsControl_CloseRequested;
        _lineSupportToolOptionsControl.DeleteRequested += LineSupportToolOptionsControl_DeleteRequested;
        _contourSupportToolOptionsControl.OptionsChanged += ContourSupportToolOptionsControl_OptionsChanged;
        _contourSupportToolOptionsControl.PickZHeightRequested += ContourSupportToolOptionsControl_PickZHeightRequested;
        _contourSupportToolOptionsControl.ApplyRequested += ContourSupportToolOptionsControl_ApplyRequested;
        _contourSupportToolOptionsControl.CloseRequested += ContourSupportToolOptionsControl_CloseRequested;
        _contourSupportToolOptionsControl.DeleteRequested += ContourSupportToolOptionsControl_DeleteRequested;
        _areaSupportToolOptionsControl.OptionsChanged += AreaSupportToolOptionsControl_OptionsChanged;
        _areaSupportToolOptionsControl.SelectFacesRequested += AreaSupportToolOptionsControl_SelectFacesRequested;
        _areaSupportToolOptionsControl.ApplyRequested += AreaSupportToolOptionsControl_ApplyRequested;
        _areaSupportToolOptionsControl.CloseRequested += AreaSupportToolOptionsControl_CloseRequested;
        _areaSupportToolOptionsControl.DeleteRequested += AreaSupportToolOptionsControl_DeleteRequested;
        _supportClusterToolOptionsControl.OptionsChanged += SupportClusterToolOptionsControl_OptionsChanged;
        _supportClusterToolOptionsControl.ApplyToSelectedRequested += SupportClusterToolOptionsControl_ApplyToSelectedRequested;
        _supportClusterToolOptionsControl.ApplyToAllRequested += SupportClusterToolOptionsControl_ApplyToAllRequested;
        _supportClusterToolOptionsControl.RemoveAllRequested += SupportClusterToolOptionsControl_RemoveAllRequested;
        _supportClusterToolOptionsControl.UnclusterSelectedRequested += SupportClusterToolOptionsControl_UnclusterSelectedRequested;
        _supportClusterToolOptionsControl.CloseRequested += SupportClusterToolOptionsControl_CloseRequested;
        _supportBracingToolOptionsControl.OptionsChanged += SupportBracingToolOptionsControl_OptionsChanged;
        _supportBracingToolOptionsControl.BraceSelectedRequested += SupportBracingToolOptionsControl_BraceSelectedRequested;
        _supportBracingToolOptionsControl.BraceAllRequested += SupportBracingToolOptionsControl_BraceAllRequested;
        _supportBracingToolOptionsControl.RemoveBracingFromSelectedRequested += SupportBracingToolOptionsControl_RemoveBracingFromSelectedRequested;
        _supportBracingToolOptionsControl.ButtressSelectedRequested += SupportBracingToolOptionsControl_ButtressSelectedRequested;
        _supportBracingToolOptionsControl.ButtressAllRequested += SupportBracingToolOptionsControl_ButtressAllRequested;
        _supportBracingToolOptionsControl.RemoveButtressingFromSelectedRequested += SupportBracingToolOptionsControl_RemoveButtressingFromSelectedRequested;
        _supportBracingToolOptionsControl.RemoveAllBracingRequested += SupportBracingToolOptionsControl_RemoveAllBracingRequested;
        _supportBracingToolOptionsControl.RemoveAllButtressesRequested += SupportBracingToolOptionsControl_RemoveAllButtressesRequested;
        _supportBracingToolOptionsControl.CloseRequested += SupportBracingToolOptionsControl_CloseRequested;
        _directEditToolOptionsControl.HighlightAngleChanged += DirectEditToolOptionsControl_HighlightAngleChanged;
        _directEditToolOptionsControl.CloseRequested += DirectEditToolOptionsControl_CloseRequested;
        _directEditTool.EditCommitted += DirectEditTool_EditCommitted;
        _directEditTool.StatusMessageRequested += DirectEditTool_StatusMessageRequested;
        _scaleToolOptionsControl.OptionsChanged += ScaleToolOptionsControl_OptionsChanged;
        _scaleToolOptionsControl.FinishRequested += ScaleToolOptionsControl_FinishRequested;
        _rotationToolOptionsControl.OptionsChanged += RotationToolOptionsControl_OptionsChanged;
        _rotationToolOptionsControl.CoordinateSpaceChanged += RotationToolOptionsControl_CoordinateSpaceChanged;
        _rotationToolOptionsControl.ResetRequested += RotationToolOptionsControl_ResetRequested;
        _rotationToolOptionsControl.FinishRequested += RotationToolOptionsControl_FinishRequested;
        _rotationToolOptionsControl.CancelRequested += RotationToolOptionsControl_CancelRequested;
        _toolSessionOptionsControl.FinishRequested += ToolSessionOptionsControl_FinishRequested;
        SupportPresetPanelOverlay.SetPresets(_supportPresetService.Presets);
        SupportPresetPanelOverlay.SelectPreset(_supportPresetService.SelectedPreset);
        SupportPresetPanelOverlay.PresetSelected += SupportPresetPanelOverlay_PresetSelected;
        SupportPresetPanelOverlay.AdvancedRequested += SupportPresetPanelOverlay_AdvancedRequested;
        _supportPresetService.SelectedPresetChanged += SupportPresetService_SelectedPresetChanged;
        LayerPanelOverlay.ImportModelRequested += LayerPanel_ImportModelRequested;
        LayerPanelOverlay.RemoveRequested += LayerPanel_RemoveRequested;
        LayerPanelOverlay.ExportModelRequested += LayerPanel_ExportModelRequested;
        LayerPanelOverlay.ExportSupportGroupRequested += LayerPanel_ExportSupportGroupRequested;
        LayerPanelOverlay.RenameLayerRequested += LayerPanel_RenameLayerRequested;
        LayerPanelOverlay.ChangeLayerVisibilityRequested += LayerPanel_ChangeLayerVisibilityRequested;
        LayerPanelOverlay.ChangeSupportGroupColorRequested += LayerPanel_ChangeSupportGroupColorRequested;
        LayerPanelOverlay.EditSupportGroupRequested += LayerPanel_EditSupportGroupRequested;
        LayerPanelOverlay.EditSupportModifierRequested += LayerPanel_EditSupportModifierRequested;
    }

    /// <summary>
    /// Cancels active tool previews before document-level file commands change entities.
    /// </summary>
    private void CancelTransientToolState()
    {
        _selectTool.ResetSelectionFilter();
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
    /// Reads Line Support spacing from the active Line Support options panel while keeping WPF controls out of rendering tools.
    /// </summary>
    private float GetLineSupportSpacingOrDefault()
    {
        if (_lineSupportToolOptionsControl.TryGetLineSupportSpacing(out float spacing))
        {
            return spacing;
        }

        _viewModel.SetStatusText("Line support spacing is invalid; using 5.00 mm.");
        return LineSupportToolOptionsControl.DefaultLineSupportSpacing;
    }

    /// <summary>
    /// Reads the Line Support bend placement option while keeping WPF controls out of rendering tools.
    /// </summary>
    private bool GetLineSupportPlaceSupportsAtBendsOrDefault()
    {
        return _lineSupportToolOptionsControl.GetPlaceSupportsAtBends();
    }

    /// <summary>
    /// Reads Contour Support Z height from the active Contour Support options panel.
    /// </summary>
    private float GetContourSupportZHeightOrDefault()
    {
        if (_contourSupportToolOptionsControl.TryGetZHeight(out float zHeight))
        {
            return zHeight;
        }

        _viewModel.SetStatusText("Contour support Z height is invalid; using 0.00 mm.");
        return 0.0f;
    }

    /// <summary>
    /// Reads Contour Support coplanar threshold from the active Contour Support options panel.
    /// </summary>
    private float GetContourSupportCoplanarThresholdOrDefault()
    {
        if (_contourSupportToolOptionsControl.TryGetCoplanarThresholdDegrees(out float threshold))
        {
            return threshold;
        }

        _viewModel.SetStatusText("Contour support threshold is invalid; using 15 degrees.");
        return ContourSupportToolOptionsControl.DefaultCoplanarThresholdDegrees;
    }

    /// <summary>
    /// Reads Contour Support spacing from the active Contour Support options panel.
    /// </summary>
    private float GetContourSupportSpacingOrDefault()
    {
        if (_contourSupportToolOptionsControl.TryGetSpacing(out float spacing))
        {
            return spacing;
        }

        _viewModel.SetStatusText("Contour support spacing is invalid; using 5.00 mm.");
        return ContourSupportToolOptionsControl.DefaultContourSupportSpacing;
    }

    /// <summary>
    /// Reads Contour Support start offset from the active Contour Support options panel.
    /// </summary>
    private float GetContourSupportStartOffsetOrDefault()
    {
        if (_contourSupportToolOptionsControl.TryGetStartOffset(out float startOffset))
        {
            return startOffset;
        }

        _viewModel.SetStatusText("Contour support start offset is invalid; using 0.00 mm.");
        return ContourSupportToolOptionsControl.DefaultStartOffset;
    }

    /// <summary>
    /// Reads Contour Support final offset from the active Contour Support options panel.
    /// </summary>
    private float GetContourSupportFinalOffsetOrDefault()
    {
        if (_contourSupportToolOptionsControl.TryGetFinalOffset(out float finalOffset))
        {
            return finalOffset;
        }

        _viewModel.SetStatusText("Contour support final offset is invalid; using 0.00 mm.");
        return ContourSupportToolOptionsControl.DefaultFinalOffset;
    }

    /// <summary>
    /// Reads Area Support spacing from the active Area Support options panel.
    /// </summary>
    private float GetAreaSupportSpacingOrDefault()
    {
        if (_areaSupportToolOptionsControl.TryGetSpacing(out float spacing))
        {
            return spacing;
        }

        _viewModel.SetStatusText("Area support spacing is invalid; using 3.0 mm.");
        return AreaSupportToolOptionsControl.DefaultAreaSupportSpacing;
    }

    /// <summary>
    /// Reads Area Support boundary offset from the active Area Support options panel.
    /// </summary>
    private float GetAreaSupportBoundaryOffsetOrDefault()
    {
        if (_areaSupportToolOptionsControl.TryGetBoundaryOffset(out float boundaryOffset))
        {
            return boundaryOffset;
        }

        _viewModel.SetStatusText("Area support boundary offset is invalid; using 1.5 mm.");
        return AreaSupportToolOptionsControl.DefaultAreaSupportBoundaryOffset;
    }

    /// <summary>
    /// Reads Area Support boundary spacing from the active Area Support options panel.
    /// </summary>
    private float GetAreaSupportBoundarySpacingOrDefault()
    {
        if (_areaSupportToolOptionsControl.TryGetBoundarySpacing(out float boundarySpacing))
        {
            return boundarySpacing;
        }

        _viewModel.SetStatusText("Area support boundary spacing is invalid; using 2.4 mm.");
        return AreaSupportToolOptionsControl.DefaultAreaSupportBoundarySpacing;
    }

    /// <summary>
    /// Reads Area Support concave corner angle from the active Area Support options panel.
    /// </summary>
    private float GetAreaSupportConcaveCornerAngleOrDefault()
    {
        if (_areaSupportToolOptionsControl.TryGetConcaveCornerAngleDegrees(out float angleDegrees))
        {
            return angleDegrees;
        }

        _viewModel.SetStatusText("Area support concave corner angle is invalid; using 30 degrees.");
        return AreaSupportToolOptionsControl.DefaultConcaveCornerAngleDegrees;
    }

    /// <summary>
    /// Reads whether Area Support should add centreline fallback supports in thin regions.
    /// </summary>
    private bool GetAreaSupportThinRegions()
    {
        return _areaSupportToolOptionsControl.GetSupportThinRegions();
    }

    /// <summary>
    /// Reads the minimum local thickness required for Area Support thin-region fallback.
    /// </summary>
    private float GetAreaSupportMinimumThinRegionThicknessOrDefault()
    {
        if (_areaSupportToolOptionsControl.TryGetMinimumThinRegionThickness(out float minimumThickness))
        {
            return minimumThickness;
        }

        _viewModel.SetStatusText("Area support minimum thickness is invalid; using 1.0 mm.");
        return AreaSupportToolOptionsControl.DefaultMinimumThinRegionThickness;
    }

    /// <summary>
    /// Gets the selected Area Support interior distribution strategy.
    /// </summary>
    private AreaSupportFillMode GetAreaSupportFillMode()
    {
        return _areaSupportToolOptionsControl.GetFillMode();
    }

    /// <summary>
    /// Gets the requested number of Area Support rings after the original offset boundary.
    /// </summary>
    private int GetAreaSupportAdditionalOffsetCountOrDefault()
    {
        if (_areaSupportToolOptionsControl.TryGetAdditionalOffsetCount(out int additionalOffsetCount))
        {
            return additionalOffsetCount;
        }

        return AreaSupportToolOptionsControl.DefaultAdditionalOffsetCount;
    }

    /// <summary>
    /// Gets the spacing between successive Area Support Boundary Offsets contours.
    /// </summary>
    private float GetAreaSupportOffsetSpacingOrDefault()
    {
        if (_areaSupportToolOptionsControl.TryGetOffsetSpacing(out float offsetSpacing))
        {
            return offsetSpacing;
        }

        _viewModel.SetStatusText("Area support offset spacing is invalid; using 1.5 mm.");
        return AreaSupportToolOptionsControl.DefaultAreaSupportOffsetSpacing;
    }

    /// <summary>
    /// Reads whether Area Support should show spacing circles in the transient preview.
    /// </summary>
    private bool GetAreaSupportShowSpacing()
    {
        return _areaSupportToolOptionsControl.GetShowSupportSpacing();
    }

    /// <summary>
    /// Writes a picked Contour Support Z height back to the options panel without coupling the operation to WPF.
    /// </summary>
    private void SetContourSupportZHeight(float zHeight)
    {
        _contourSupportToolOptionsControl.SetZHeight(zHeight);
    }

    /// <summary>
    /// Writes the current contour closed/open state back to the options panel without coupling the operation to WPF.
    /// </summary>
    private void SetContourSupportClosedState(bool isClosed)
    {
        _contourSupportToolOptionsControl.SetContourClosed(isClosed);
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
