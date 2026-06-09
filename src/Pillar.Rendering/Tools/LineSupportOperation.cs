// LineSupportOperation.cs
// Creates line-pattern support entities from model-surface polyline picks while keeping preview state transient.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using Pillar.Core.Tools;
using Pillar.Geometry.Supports;
using Pillar.Rendering.Math;
using Pillar.Rendering.Preview;
using Pillar.Rendering.Scene;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Input;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Places a new support layer group by distributing supports along a picked model-surface polyline and projecting them vertically onto the active mesh.
/// </summary>
public sealed class LineSupportOperation : IToolOperation, IEditableSupportGroupOperation
{
    private const string LineSupportLayerGroupBaseName = "Line Supports";
    private const float EditingSupportGroupOpacity = 0.5f;
    private const float DefaultSupportGroupOpacity = 1.0f;
    private const float DragThresholdPixels = 4.0f;
    private const float DragThresholdSquared = DragThresholdPixels * DragThresholdPixels;
    private const float MinimumSegmentLength = 0.0001f;

    private enum WindowSelectionOperation
    {
        Replace,
        Add,
        Subtract
    }

    private enum ClickSelectionOperation
    {
        Replace,
        Add,
        Subtract
    }

    private readonly CadDocument _document;
    private readonly ProjectionService _projectionService;
    private readonly SceneManager _scene;
    private readonly CadCommandRunner _commandRunner;
    private readonly Func<Guid?> _getSelectedModelEntityId;
    private readonly Func<float> _getSpacing;
    private readonly Func<bool> _getPlaceSupportsAtBends;
    private readonly Func<SupportProfile> _createSupportProfile;
    private readonly Action<string> _statusReporter;
    private readonly Action<bool> _precisionSelectCursorRequester;
    private readonly Action<bool> _previewCalculationStateReporter;
    private readonly List<Vector3> _points = new List<Vector3>(32);
    private readonly List<Vector3> _guidePreviewPoints = new List<Vector3>(LineSupportPattern.MaximumSupportCount);
    private readonly List<Vector3> _projectedPreviewPoints = new List<Vector3>(LineSupportPattern.MaximumSupportCount);
    private readonly List<CadEntity> _windowSelectionBuffer = new List<CadEntity>(64);

    private Guid? _targetModelEntityId;
    private Guid? _editingSupportLayerGroupId;
    private Vector3? _currentPreviewPoint;
    private Vector2 _mouseDownPosition;
    private int _activePointHandleIndex = -1;
    private bool _isMouseDown;
    private bool _isDraggingSupportSelection;
    private bool _isFinalized;

    /// <summary>
    /// Creates the line support operation.
    /// </summary>
    public LineSupportOperation(
        CadDocument document,
        ProjectionService projectionService,
        SceneManager scene,
        CadCommandRunner commandRunner,
        Func<Guid?> getSelectedModelEntityId,
        Func<float> getSpacing,
        Func<bool> getPlaceSupportsAtBends,
        Func<SupportProfile> createSupportProfile,
        Action<string> statusReporter,
        Action<bool> precisionSelectCursorRequester,
        Action<bool> previewCalculationStateReporter)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _getSelectedModelEntityId = getSelectedModelEntityId ?? throw new ArgumentNullException(nameof(getSelectedModelEntityId));
        _getSpacing = getSpacing ?? throw new ArgumentNullException(nameof(getSpacing));
        _getPlaceSupportsAtBends = getPlaceSupportsAtBends ?? throw new ArgumentNullException(nameof(getPlaceSupportsAtBends));
        _createSupportProfile = createSupportProfile ?? throw new ArgumentNullException(nameof(createSupportProfile));
        _statusReporter = statusReporter ?? throw new ArgumentNullException(nameof(statusReporter));
        _precisionSelectCursorRequester = precisionSelectCursorRequester ?? throw new ArgumentNullException(nameof(precisionSelectCursorRequester));
        _previewCalculationStateReporter = previewCalculationStateReporter ?? throw new ArgumentNullException(nameof(previewCalculationStateReporter));
        _precisionSelectCursorRequester(true);
    }

    /// <summary>
    /// Gets the Line Support group currently loaded for editing, or null while creating a new line.
    /// </summary>
    public Guid? EditingSupportLayerGroupId
    {
        get { return _editingSupportLayerGroupId; }
    }

    /// <summary>
    /// Raised while the user drags a support selection rectangle in edit mode.
    /// </summary>
    public event Action<SelectionWindowOverlayState>? SelectionWindowChanged;

    /// <summary>
    /// Captures model-surface polyline points or starts support selection after the line has been applied.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _statusReporter("Select a model before placing line supports.");
            return;
        }

        if (_isFinalized)
        {
            int pointHandleIndex;

            if (_scene.TryHitLineSupportPointHandle(screenPosition, out pointHandleIndex))
            {
                _activePointHandleIndex = pointHandleIndex;
                _statusReporter("Drag the line point handle to reshape the support line, then click Apply.");
                return;
            }

            if (_editingSupportLayerGroupId.HasValue)
            {
                StartSupportSelectionGesture(screenPosition);
                return;
            }

            _statusReporter("Line support preview is ready. Adjust spacing or click Apply.");
            return;
        }

        CaptureLinePoint(screenPosition, selectedMesh);
    }

    /// <summary>
    /// Updates the live polyline preview and cursor spacing guide while placing points.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        if (_activePointHandleIndex >= 0)
        {
            MeshEntity? dragMesh = ResolvePlacementMesh();

            if (dragMesh != null)
            {
                UpdateDraggedPointHandle(screenPosition, dragMesh);
            }

            return;
        }

        if (_isMouseDown && _isFinalized && _editingSupportLayerGroupId.HasValue)
        {
            UpdateSupportSelectionWindow(screenPosition);
            return;
        }

        if (_isFinalized || _points.Count == 0)
        {
            return;
        }

        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            return;
        }

        Vector3 hoverPoint;

        if (!TryGetHitOnSelectedMesh(screenPosition, selectedMesh, out hoverPoint))
        {
            _scene.HideLineSupportSpacingGuide();
            return;
        }

        _currentPreviewPoint = hoverPoint;
        _scene.ShowLineSupportPreview(_points, _currentPreviewPoint);
        _scene.ShowLineSupportSpacingGuide(hoverPoint, _getSpacing());
    }

    /// <summary>
    /// Ends any active support selection gesture.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
        if (_activePointHandleIndex >= 0)
        {
            _activePointHandleIndex = -1;
            RefreshPreview();
            _statusReporter("Line point updated. Click Apply to regenerate supports.");
            return;
        }

        if (!_isMouseDown)
        {
            return;
        }

        if (_isDraggingSupportSelection)
        {
            Rect selectionRect = CreateScreenRect(_mouseDownPosition, screenPosition);
            bool selectsCrossingEntities = IsRightToLeftDrag(_mouseDownPosition, screenPosition);
            WindowSelectionOperation operation = GetWindowSelectionOperation();

            HideSelectionWindow();
            ApplySupportWindowSelection(selectionRect, selectsCrossingEntities, operation);
            ResetSupportSelectionGesture();
            return;
        }

        ApplySupportClickSelection(screenPosition);
        ResetSupportSelectionGesture();
    }

    /// <summary>
    /// Cancels the in-progress line gesture and clears transient preview geometry.
    /// </summary>
    public void Cancel()
    {
        ClearEditingSupportGroupOpacity();
        _targetModelEntityId = null;
        _editingSupportLayerGroupId = null;
        _points.Clear();
        _currentPreviewPoint = null;
        _activePointHandleIndex = -1;
        _isFinalized = false;
        ResetSupportSelectionGesture();
        HideSelectionWindow();
        _guidePreviewPoints.Clear();
        _projectedPreviewPoints.Clear();
        _scene.HideLineSupportPreview();
        _precisionSelectCursorRequester(false);
    }

    /// <summary>
    /// Completes the picked polyline and generates projected preview markers.
    /// </summary>
    public bool FinalizePolyline()
    {
        if (_isFinalized)
        {
            return true;
        }

        if (_points.Count < 2)
        {
            _statusReporter("Pick at least two points on the selected model before finishing the line.");
            return false;
        }

        _currentPreviewPoint = null;
        _isFinalized = true;
        _scene.HideLineSupportSpacingGuide();
        RefreshPreview();
        _precisionSelectCursorRequester(false);
        _statusReporter("Line support preview is ready. Adjust spacing or click Apply.");
        return true;
    }

    /// <summary>
    /// Loads an existing Line Support group into the operation so its settings can be edited.
    /// </summary>
    public void EditExistingLineSupportGroup(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        LineSupportSettings? settings = supportLayerGroup.LineSupportSettings;

        if (settings == null)
        {
            _statusReporter("The selected support group was not created with the Line Support tool.");
            return;
        }

        if (_editingSupportLayerGroupId.HasValue && _editingSupportLayerGroupId.Value != supportLayerGroup.Id)
        {
            ClearEditingSupportGroupOpacity();
        }

        _editingSupportLayerGroupId = supportLayerGroup.Id;
        _targetModelEntityId = supportLayerGroup.ModelEntityId;
        _points.Clear();
        _points.AddRange(settings.Points);
        _currentPreviewPoint = null;
        _isFinalized = true;
        _precisionSelectCursorRequester(false);
        _scene.SetSupportLayerGroupOpacity(supportLayerGroup.Id, EditingSupportGroupOpacity);
        RefreshPreview();
        _statusReporter("Line support group loaded. Adjust spacing or click Apply.");
    }

    /// <summary>
    /// Rebuilds the transient line preview from the current tool state and spacing settings.
    /// </summary>
    public void RefreshPreview()
    {
        RunWithPreviewCalculationFeedback(RefreshPreviewCore);
    }

    /// <summary>
    /// Applies the previewed line supports to either a new support group or the loaded generated support group.
    /// </summary>
    public bool Apply()
    {
        bool didApply = false;
        RunWithPreviewCalculationFeedback(() => didApply = ApplyCore());
        return didApply;
    }

    /// <summary>
    /// Rebuilds the transient line preview without controlling shell feedback.
    /// </summary>
    private void RefreshPreviewCore()
    {
        if (_points.Count == 0)
        {
            return;
        }

        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _scene.HideLineSupportPreview();
            return;
        }

        _scene.ShowLineSupportPreview(_points, _isFinalized ? null : _currentPreviewPoint);

        if (_isFinalized)
        {
            _scene.ShowLineSupportPointHandles(_points, _getSpacing());
        }
        else
        {
            _scene.HideLineSupportPointHandles();
        }

        if (_isFinalized && _points.Count >= 2)
        {
            UpdateProjectedMarkerPreview(selectedMesh);
        }
        else
        {
            _scene.HideLineSupportMarkers();
        }
    }

    /// <summary>
    /// Applies the current line preview without directly controlling shell feedback.
    /// </summary>
    private bool ApplyCore()
    {
        if (!_isFinalized || _points.Count < 2)
        {
            if (!FinalizePolyline())
            {
                return false;
            }
        }

        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _statusReporter("The selected model could not be found.");
            return false;
        }

        LineSupportSettings settings = new LineSupportSettings(_points, _getSpacing(), _getPlaceSupportsAtBends());

        if (_editingSupportLayerGroupId.HasValue)
        {
            return UpdateExistingLineSupportGroup(selectedMesh, settings);
        }

        return CommitNewLineSupportGroup(selectedMesh, settings);
    }

    /// <summary>
    /// Captures one model-surface point for the line definition.
    /// </summary>
    private void CaptureLinePoint(Vector2 screenPosition, MeshEntity selectedMesh)
    {
        Vector3 hitPosition;

        if (!TryGetHitOnSelectedMesh(screenPosition, selectedMesh, out hitPosition))
        {
            _statusReporter("Line support points must be picked on the selected model.");
            return;
        }

        if (_points.Count > 0 && Vector3.Distance(_points[_points.Count - 1], hitPosition) <= MinimumSegmentLength)
        {
            _statusReporter("Line support points must not overlap.");
            return;
        }

        _targetModelEntityId = selectedMesh.Id;
        _points.Add(hitPosition);
        _currentPreviewPoint = hitPosition;
        _scene.ShowLineSupportPreview(_points, null);
        _scene.ShowLineSupportSpacingGuide(hitPosition, _getSpacing());
        _scene.HideLineSupportMarkers();

        if (_points.Count == 1)
        {
            _statusReporter("Move the cursor to preview the line, then click the next point on the model.");
            return;
        }

        _statusReporter("Click another point to extend the line, or press Esc or Apply to preview line supports.");
    }

    /// <summary>
    /// Notifies the shell while synchronous preview projection or regeneration work is running.
    /// </summary>
    private void RunWithPreviewCalculationFeedback(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        _previewCalculationStateReporter(true);

        try
        {
            action();
        }
        finally
        {
            _previewCalculationStateReporter(false);
        }
    }

    /// <summary>
    /// Creates a new Line Support group from the accepted preview and records it as one undoable command.
    /// </summary>
    private bool CommitNewLineSupportGroup(MeshEntity selectedMesh, LineSupportSettings settings)
    {
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(selectedMesh.Id, CreateLineSupportLayerGroupName(selectedMesh.Id));
        supportLayerGroup.SetLineSupportSettings(settings);

        int missedProjectionCount;
        int invalidSupportCount;
        List<SupportEntity> supportEntities = CreateSupportEntities(
            selectedMesh,
            supportLayerGroup.Id,
            settings,
            out missedProjectionCount,
            out invalidSupportCount);

        if (supportEntities.Count == 0)
        {
            _statusReporter("No line supports could be projected onto the selected model.");
            return false;
        }

        _commandRunner.Execute(new AddSupportsToNewGroupCommand(_document, supportLayerGroup, supportEntities, "Add Line Supports"));
        EnterSupportGroupEditState(supportLayerGroup, settings);
        _statusReporter(CreateCompletionMessage(supportEntities.Count, missedProjectionCount, invalidSupportCount));
        return true;
    }

    /// <summary>
    /// Updates the loaded Line Support group as one undoable parametric regeneration.
    /// </summary>
    private bool UpdateExistingLineSupportGroup(MeshEntity selectedMesh, LineSupportSettings newSettings)
    {
        if (!_editingSupportLayerGroupId.HasValue)
        {
            return false;
        }

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(_editingSupportLayerGroupId.Value);

        if (supportLayerGroup == null)
        {
            _statusReporter("The Line Support group could not be found.");
            return false;
        }

        LineSupportSettings? oldSettings = supportLayerGroup.LineSupportSettings;

        if (oldSettings == null)
        {
            _statusReporter("The selected support group was not created with the Line Support tool.");
            return false;
        }

        IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        int missedProjectionCount;
        int invalidSupportCount;
        List<SupportEntity> newSupportEntities = CreateSupportEntities(
            selectedMesh,
            supportLayerGroup.Id,
            newSettings,
            out missedProjectionCount,
            out invalidSupportCount);

        if (newSupportEntities.Count == 0)
        {
            _statusReporter("No line supports could be projected onto the selected model.");
            return false;
        }

        _commandRunner.Execute(new UpdateLineSupportGroupCommand(
            _document,
            supportLayerGroup,
            oldSettings,
            oldSupportEntities,
            newSettings,
            newSupportEntities));

        EnterSupportGroupEditState(supportLayerGroup, newSettings);
        _statusReporter(CreateCompletionMessage(newSupportEntities.Count, missedProjectionCount, invalidSupportCount));
        return true;
    }

    /// <summary>
    /// Loads the accepted settings into edit mode after an Apply operation.
    /// </summary>
    private void EnterSupportGroupEditState(SupportLayerGroup supportLayerGroup, LineSupportSettings settings)
    {
        ClearEditingSupportGroupOpacity();
        _editingSupportLayerGroupId = supportLayerGroup.Id;
        _targetModelEntityId = supportLayerGroup.ModelEntityId;
        _points.Clear();
        _points.AddRange(settings.Points);
        _currentPreviewPoint = null;
        _isFinalized = true;
        _precisionSelectCursorRequester(false);
        _scene.SetSupportLayerGroupOpacity(supportLayerGroup.Id, EditingSupportGroupOpacity);
        RefreshPreview();
    }

    /// <summary>
    /// Moves the active editable point handle to a new selected-model surface hit.
    /// </summary>
    private void UpdateDraggedPointHandle(Vector2 screenPosition, MeshEntity selectedMesh)
    {
        if (_activePointHandleIndex < 0 || _activePointHandleIndex >= _points.Count)
        {
            return;
        }

        Vector3 hitPosition;

        if (!TryGetHitOnSelectedMesh(screenPosition, selectedMesh, out hitPosition))
        {
            return;
        }

        _points[_activePointHandleIndex] = hitPosition;
        _currentPreviewPoint = null;
        _scene.ShowLineSupportPreview(_points, null);
        _scene.ShowLineSupportPointHandles(_points, _getSpacing());
        _scene.HideLineSupportMarkers();
    }

    /// <summary>
    /// Starts either a support click-select or drag-select gesture in the active edit group.
    /// </summary>
    private void StartSupportSelectionGesture(Vector2 screenPosition)
    {
        _mouseDownPosition = screenPosition;
        _isMouseDown = true;
        _isDraggingSupportSelection = false;
    }

    /// <summary>
    /// Updates support selection rectangle state if the cursor has moved beyond the drag threshold.
    /// </summary>
    private void UpdateSupportSelectionWindow(Vector2 screenPosition)
    {
        Vector2 delta = screenPosition - _mouseDownPosition;

        if (!_isDraggingSupportSelection && delta.LengthSquared() < DragThresholdSquared)
        {
            return;
        }

        _isDraggingSupportSelection = true;
        PublishSelectionWindow(screenPosition);
    }

    /// <summary>
    /// Applies click selection to a support in the group currently being edited.
    /// </summary>
    private void ApplySupportClickSelection(Vector2 screenPosition)
    {
        if (!_editingSupportLayerGroupId.HasValue)
        {
            return;
        }

        SupportEntity supportEntity;
        ClickSelectionOperation operation = GetClickSelectionOperation();

        if (_scene.TryHitSupportEntity(screenPosition, _editingSupportLayerGroupId.Value, out supportEntity))
        {
            if (operation == ClickSelectionOperation.Subtract)
            {
                _scene.SelectionManager.RemoveFromSelection(supportEntity);
                return;
            }

            if (operation == ClickSelectionOperation.Add)
            {
                _scene.SelectionManager.AddToSelection(supportEntity);
                return;
            }

            _scene.SelectionManager.SelectSingle(supportEntity);
            _statusReporter("Selected support. Press Delete to remove it from this edit.");
            return;
        }

        if (operation == ClickSelectionOperation.Replace)
        {
            _scene.SelectionManager.ClearSelection();
        }

        _statusReporter("Line support edit is active. Drag-select supports or click Apply.");
    }

    /// <summary>
    /// Applies rectangular selection to supports in the group currently being edited.
    /// </summary>
    private void ApplySupportWindowSelection(
        Rect selectionRect,
        bool selectsCrossingEntities,
        WindowSelectionOperation operation)
    {
        if (!_editingSupportLayerGroupId.HasValue)
        {
            return;
        }

        _windowSelectionBuffer.Clear();
        _scene.FillSupportEntitiesSelectedByWindow(
            _editingSupportLayerGroupId.Value,
            selectionRect,
            selectsCrossingEntities,
            _windowSelectionBuffer);

        if (operation == WindowSelectionOperation.Subtract)
        {
            _scene.SelectionManager.RemoveRangeFromSelection(_windowSelectionBuffer);
            _statusReporter(CreateWindowSelectionMessage(_windowSelectionBuffer.Count));
            return;
        }

        if (operation == WindowSelectionOperation.Add)
        {
            _scene.SelectionManager.AddRangeToSelection(_windowSelectionBuffer);
            _statusReporter(CreateWindowSelectionMessage(_windowSelectionBuffer.Count));
            return;
        }

        _scene.SelectionManager.SelectMany(_windowSelectionBuffer);
        _statusReporter(CreateWindowSelectionMessage(_windowSelectionBuffer.Count));
    }

    /// <summary>
    /// Publishes overlay geometry and the preview outline style for support edit drag selection.
    /// </summary>
    private void PublishSelectionWindow(Vector2 screenPosition)
    {
        Rect selectionRect = CreateScreenRect(_mouseDownPosition, screenPosition);
        bool useSolidOutline = !IsRightToLeftDrag(_mouseDownPosition, screenPosition);

        SelectionWindowChanged?.Invoke(new SelectionWindowOverlayState(
            true,
            selectionRect.Left,
            selectionRect.Top,
            selectionRect.Width,
            selectionRect.Height,
            useSolidOutline));
    }

    /// <summary>
    /// Hides the transient support selection rectangle.
    /// </summary>
    private void HideSelectionWindow()
    {
        SelectionWindowChanged?.Invoke(new SelectionWindowOverlayState(false, 0.0, 0.0, 0.0, 0.0, false));
    }

    /// <summary>
    /// Clears support selection gesture flags after a click, drag, or cancel.
    /// </summary>
    private void ResetSupportSelectionGesture()
    {
        _isMouseDown = false;
        _isDraggingSupportSelection = false;
    }

    /// <summary>
    /// Creates a positive-size rectangle from two viewport pixel positions.
    /// </summary>
    private static Rect CreateScreenRect(Vector2 startPosition, Vector2 endPosition)
    {
        double left = global::System.Math.Min(startPosition.X, endPosition.X);
        double top = global::System.Math.Min(startPosition.Y, endPosition.Y);
        double width = global::System.Math.Abs(endPosition.X - startPosition.X);
        double height = global::System.Math.Abs(endPosition.Y - startPosition.Y);

        return new Rect(left, top, width, height);
    }

    /// <summary>
    /// Returns true when the drag direction should use crossing selection.
    /// </summary>
    private static bool IsRightToLeftDrag(Vector2 startPosition, Vector2 endPosition)
    {
        return endPosition.X < startPosition.X;
    }

    /// <summary>
    /// Chooses how a support window selection should modify the current selection.
    /// </summary>
    private static WindowSelectionOperation GetWindowSelectionOperation()
    {
        if (IsControlModifierDown())
        {
            return WindowSelectionOperation.Subtract;
        }

        if (IsShiftModifierDown())
        {
            return WindowSelectionOperation.Add;
        }

        return WindowSelectionOperation.Replace;
    }

    /// <summary>
    /// Chooses how a support click selection should modify the current selection.
    /// </summary>
    private static ClickSelectionOperation GetClickSelectionOperation()
    {
        if (IsControlModifierDown())
        {
            return ClickSelectionOperation.Subtract;
        }

        if (IsShiftModifierDown())
        {
            return ClickSelectionOperation.Add;
        }

        return ClickSelectionOperation.Replace;
    }

    /// <summary>
    /// Reads the current CTRL modifier state for subtractive selection.
    /// </summary>
    private static bool IsControlModifierDown()
    {
        return (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
    }

    /// <summary>
    /// Reads the current SHIFT modifier state for additive selection.
    /// </summary>
    private static bool IsShiftModifierDown()
    {
        return (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
    }

    /// <summary>
    /// Builds support window-selection feedback.
    /// </summary>
    private static string CreateWindowSelectionMessage(int selectedCount)
    {
        if (selectedCount == 1)
        {
            return "Selected 1 support. Press Delete to remove it from this edit.";
        }

        return $"Selected {selectedCount} supports. Press Delete to remove them from this edit.";
    }

    /// <summary>
    /// Generates support entities for one Line Support definition without mutating the document.
    /// </summary>
    private List<SupportEntity> CreateSupportEntities(
        MeshEntity selectedMesh,
        Guid supportLayerGroupId,
        LineSupportSettings settings,
        out int missedProjectionCount,
        out int invalidSupportCount)
    {
        SupportProfile supportProfile = _createSupportProfile();
        List<SupportEntity> supportEntities = new List<SupportEntity>();
        missedProjectionCount = 0;
        invalidSupportCount = 0;

        LineSupportPattern.FillGuidePoints(
            settings.Points,
            settings.Spacing,
            settings.PlaceSupportsAtBends,
            _guidePreviewPoints);

        for (int i = 0; i < _guidePreviewPoints.Count; i++)
        {
            Vector3 guidePoint = _guidePreviewPoints[i];
            MeshProjectionHit projectionHit;

            if (!MeshVerticalProjection.TryProjectToMesh(selectedMesh, guidePoint, out projectionHit))
            {
                missedProjectionCount++;
                continue;
            }

            Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(projectionHit.Normal, supportProfile);
            SupportBranchPlan branchPlan;

            if (!SupportBranchPlanner.TryCreateBranchPlan(selectedMesh, projectionHit.Point, headDirection, supportProfile, out branchPlan))
            {
                invalidSupportCount++;
                continue;
            }

            try
            {
                supportEntities.Add(new SupportEntity(
                    supportLayerGroupId,
                    projectionHit.Point,
                    branchPlan.BasePosition,
                    headDirection,
                    branchPlan.BranchLength,
                    branchPlan.BranchDirection,
                    supportProfile));
            }
            catch (ArgumentException)
            {
                invalidSupportCount++;
            }
        }

        return supportEntities;
    }

    /// <summary>
    /// Restores the support group being edited to normal opacity before the operation exits.
    /// </summary>
    private void ClearEditingSupportGroupOpacity()
    {
        if (_editingSupportLayerGroupId.HasValue)
        {
            _scene.SetSupportLayerGroupOpacity(_editingSupportLayerGroupId.Value, DefaultSupportGroupOpacity);
        }
    }

    /// <summary>
    /// Updates lightweight projected support markers for the current line preview.
    /// </summary>
    private void UpdateProjectedMarkerPreview(MeshEntity selectedMesh)
    {
        _guidePreviewPoints.Clear();
        _projectedPreviewPoints.Clear();
        LineSupportPattern.FillGuidePoints(_points, _getSpacing(), _getPlaceSupportsAtBends(), _guidePreviewPoints);

        for (int i = 0; i < _guidePreviewPoints.Count; i++)
        {
            Vector3 guidePoint = _guidePreviewPoints[i];
            Vector3 projectedPoint;

            if (MeshVerticalProjection.TryProjectToMesh(selectedMesh, guidePoint, out projectedPoint))
            {
                _projectedPreviewPoints.Add(projectedPoint);
            }
        }

        _scene.ShowLineSupportMarkers(_projectedPreviewPoints);
    }

    /// <summary>
    /// Finds the mesh entity that owns the current line support placement.
    /// </summary>
    private MeshEntity? ResolvePlacementMesh()
    {
        Guid? selectedModelEntityId = _targetModelEntityId ?? _getSelectedModelEntityId();

        if (!selectedModelEntityId.HasValue)
        {
            return null;
        }

        return FindMeshEntity(selectedModelEntityId.Value);
    }

    /// <summary>
    /// Finds one mesh entity by id from the current document.
    /// </summary>
    private MeshEntity? FindMeshEntity(Guid modelEntityId)
    {
        foreach (CadEntity entity in _document.Entities)
        {
            if (entity is MeshEntity meshEntity && meshEntity.Id == modelEntityId)
            {
                return meshEntity;
            }
        }

        return null;
    }

    /// <summary>
    /// Hit-tests the viewport and accepts only hits on the selected mesh visual.
    /// </summary>
    private bool TryGetHitOnSelectedMesh(Vector2 screenPosition, MeshEntity selectedMesh, out Vector3 hitPosition)
    {
        MeshSurfaceHit meshSurfaceHit;

        if (!_projectionService.TryGetMeshSurfaceHit(
            screenPosition,
            hitModel => ReferenceEquals(_scene.GetEntityFromVisual(hitModel), selectedMesh),
            out meshSurfaceHit))
        {
            hitPosition = Vector3.Zero;
            return false;
        }

        hitPosition = meshSurfaceHit.HitPosition;
        return true;
    }

    /// <summary>
    /// Creates a stable user-facing name for a newly created line-support group under one model.
    /// </summary>
    private string CreateLineSupportLayerGroupName(Guid modelEntityId)
    {
        int duplicateCount = 0;

        foreach (SupportLayerGroup existingSupportLayerGroup in _document.SupportLayerGroups)
        {
            if (existingSupportLayerGroup.ModelEntityId != modelEntityId)
            {
                continue;
            }

            if (string.Equals(existingSupportLayerGroup.Name, LineSupportLayerGroupBaseName, StringComparison.OrdinalIgnoreCase)
                || existingSupportLayerGroup.Name.StartsWith($"{LineSupportLayerGroupBaseName} ", StringComparison.OrdinalIgnoreCase))
            {
                duplicateCount++;
            }
        }

        if (duplicateCount == 0)
        {
            return LineSupportLayerGroupBaseName;
        }

        return $"{LineSupportLayerGroupBaseName} {duplicateCount + 1}";
    }

    /// <summary>
    /// Builds a concise completion message including skipped projections when needed.
    /// </summary>
    private static string CreateCompletionMessage(int createdCount, int missedProjectionCount, int invalidSupportCount)
    {
        if (missedProjectionCount == 0 && invalidSupportCount == 0)
        {
            return $"Added {createdCount} line supports.";
        }

        return $"Added {createdCount} line supports; skipped {missedProjectionCount + invalidSupportCount}.";
    }
}
