// ContourSupportOperation.cs
// Creates contour-pattern support entities from a connected model face patch while keeping preview state transient.
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
/// Places a new support layer group by slicing a connected face patch at a horizontal Z height.
/// </summary>
public sealed class ContourSupportOperation : IToolOperation, IEditableSupportGroupOperation
{
    private const string ContourSupportLayerGroupBaseName = "Contour Supports";
    private const float EditingSupportGroupOpacity = 0.5f;
    private const float DefaultSupportGroupOpacity = 1.0f;
    private const float DragThresholdPixels = 4.0f;
    private const float DragThresholdSquared = DragThresholdPixels * DragThresholdPixels;

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
    private readonly Func<float> _getZHeight;
    private readonly Func<float> _getCoplanarThresholdDegrees;
    private readonly Func<float> _getSpacing;
    private readonly Func<float> _getStartOffset;
    private readonly Func<float> _getFinalOffset;
    private readonly Func<SupportProfile> _createSupportProfile;
    private readonly Action<float> _zHeightSelectedReporter;
    private readonly Action<bool> _contourClosedStateReporter;
    private readonly Action<string> _statusReporter;
    private readonly Action<bool> _precisionSelectCursorRequester;
    private readonly Action<bool> _previewCalculationStateReporter;
    private readonly List<CadEntity> _windowSelectionBuffer = new List<CadEntity>(64);

    private Guid? _targetModelEntityId;
    private Guid? _editingSupportLayerGroupId;
    private Vector3? _seedPoint;
    private int _seedTriangleIndex = -1;
    private Vector2 _mouseDownPosition;
    private bool _isMouseDown;
    private bool _isDraggingSupportSelection;
    private bool _isPickingZHeight;

    /// <summary>
    /// Creates the contour support operation.
    /// </summary>
    public ContourSupportOperation(
        CadDocument document,
        ProjectionService projectionService,
        SceneManager scene,
        CadCommandRunner commandRunner,
        Func<Guid?> getSelectedModelEntityId,
        Func<float> getZHeight,
        Func<float> getCoplanarThresholdDegrees,
        Func<float> getSpacing,
        Func<float> getStartOffset,
        Func<float> getFinalOffset,
        Func<SupportProfile> createSupportProfile,
        Action<float> zHeightSelectedReporter,
        Action<bool> contourClosedStateReporter,
        Action<string> statusReporter,
        Action<bool> precisionSelectCursorRequester,
        Action<bool> previewCalculationStateReporter)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _getSelectedModelEntityId = getSelectedModelEntityId ?? throw new ArgumentNullException(nameof(getSelectedModelEntityId));
        _getZHeight = getZHeight ?? throw new ArgumentNullException(nameof(getZHeight));
        _getCoplanarThresholdDegrees = getCoplanarThresholdDegrees ?? throw new ArgumentNullException(nameof(getCoplanarThresholdDegrees));
        _getSpacing = getSpacing ?? throw new ArgumentNullException(nameof(getSpacing));
        _getStartOffset = getStartOffset ?? throw new ArgumentNullException(nameof(getStartOffset));
        _getFinalOffset = getFinalOffset ?? throw new ArgumentNullException(nameof(getFinalOffset));
        _createSupportProfile = createSupportProfile ?? throw new ArgumentNullException(nameof(createSupportProfile));
        _zHeightSelectedReporter = zHeightSelectedReporter ?? throw new ArgumentNullException(nameof(zHeightSelectedReporter));
        _contourClosedStateReporter = contourClosedStateReporter ?? throw new ArgumentNullException(nameof(contourClosedStateReporter));
        _statusReporter = statusReporter ?? throw new ArgumentNullException(nameof(statusReporter));
        _precisionSelectCursorRequester = precisionSelectCursorRequester ?? throw new ArgumentNullException(nameof(precisionSelectCursorRequester));
        _previewCalculationStateReporter = previewCalculationStateReporter ?? throw new ArgumentNullException(nameof(previewCalculationStateReporter));
        _precisionSelectCursorRequester(true);
    }

    /// <summary>
    /// Gets the Contour Support group currently loaded for editing, or null while creating a new contour.
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
    /// Captures the contour seed point or starts support selection in edit mode.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _statusReporter("Select a model before placing contour supports.");
            return;
        }

        if (!_seedPoint.HasValue || _isPickingZHeight)
        {
            CaptureContourSeed(screenPosition, selectedMesh);
            return;
        }

        if (_editingSupportLayerGroupId.HasValue)
        {
            StartSupportSelectionGesture(screenPosition);
            return;
        }

        _statusReporter("Contour support preview is ready. Adjust settings or click Apply.");
    }

    /// <summary>
    /// Updates support edit selection rectangles.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        if (_isMouseDown && _editingSupportLayerGroupId.HasValue)
        {
            UpdateSupportSelectionWindow(screenPosition);
        }
    }

    /// <summary>
    /// Ends any active support selection gesture.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
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
    /// Cancels the in-progress contour gesture and clears transient preview geometry.
    /// </summary>
    public void Cancel()
    {
        ClearEditingSupportGroupOpacity();
        _targetModelEntityId = null;
        _editingSupportLayerGroupId = null;
        _seedPoint = null;
        _seedTriangleIndex = -1;
        _isPickingZHeight = false;
        ResetSupportSelectionGesture();
        HideSelectionWindow();
        _contourClosedStateReporter(false);
        _scene.HideContourSupportPreview();
        _precisionSelectCursorRequester(false);
    }

    /// <summary>
    /// Requests that the next model click replace the contour seed and Z height.
    /// </summary>
    public void BeginPickZHeight()
    {
        _isPickingZHeight = true;
        _precisionSelectCursorRequester(true);
        _statusReporter("Click the model to choose a new contour Z height.");
    }

    /// <summary>
    /// Loads an existing Contour Support group into the operation so its settings can be edited.
    /// </summary>
    public void EditExistingContourSupportGroup(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        ContourSupportSettings? settings = supportLayerGroup.ContourSupportSettings;

        if (settings == null)
        {
            _statusReporter("The selected support group was not created with the Contour Support tool.");
            return;
        }

        if (_editingSupportLayerGroupId.HasValue && _editingSupportLayerGroupId.Value != supportLayerGroup.Id)
        {
            ClearEditingSupportGroupOpacity();
        }

        _editingSupportLayerGroupId = supportLayerGroup.Id;
        _targetModelEntityId = supportLayerGroup.ModelEntityId;
        _seedPoint = settings.SeedPoint;
        _seedTriangleIndex = settings.SeedTriangleIndex;
        _isPickingZHeight = false;
        _precisionSelectCursorRequester(false);
        _scene.SetSupportLayerGroupOpacity(supportLayerGroup.Id, EditingSupportGroupOpacity);
        RefreshPreview();
        _statusReporter("Contour support group loaded. Adjust settings or click Apply.");
    }

    /// <summary>
    /// Rebuilds the transient contour preview from the current tool state and settings.
    /// </summary>
    public void RefreshPreview()
    {
        RunWithPreviewCalculationFeedback(RefreshPreviewCore);
    }

    /// <summary>
    /// Applies the previewed contour supports to either a new support group or the loaded generated support group.
    /// </summary>
    public bool Apply()
    {
        bool didApply = false;
        RunWithPreviewCalculationFeedback(() => didApply = ApplyCore());
        return didApply;
    }

    /// <summary>
    /// Rebuilds the transient contour preview without controlling shell feedback.
    /// </summary>
    private void RefreshPreviewCore()
    {
        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (!_seedPoint.HasValue || _seedTriangleIndex < 0 || selectedMesh == null)
        {
            _scene.HideContourSupportPreview();
            _contourClosedStateReporter(false);
            return;
        }

        ContourSupportSettings settings = CreateCurrentSettings();
        ContourSupportResult contourResult;

        if (!ContourSupportPattern.TryCreate(selectedMesh, settings, out contourResult))
        {
            _scene.HideContourSupportPreview();
            _contourClosedStateReporter(false);
            _statusReporter("No contour could be found from the selected face at this Z height.");
            return;
        }

        _scene.ShowContourSupportPreview(contourResult);
        _contourClosedStateReporter(contourResult.IsClosed);
        _statusReporter(CreateContourPreviewStatusMessage(contourResult.Diagnostics));
    }

    /// <summary>
    /// Applies the current contour preview without directly controlling shell feedback.
    /// </summary>
    private bool ApplyCore()
    {
        if (!_seedPoint.HasValue || _seedTriangleIndex < 0)
        {
            _statusReporter("Click the selected model before applying contour supports.");
            return false;
        }

        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _statusReporter("The selected model could not be found.");
            return false;
        }

        ContourSupportSettings settings = CreateCurrentSettings();

        if (_editingSupportLayerGroupId.HasValue)
        {
            return UpdateExistingContourSupportGroup(selectedMesh, settings);
        }

        return CommitNewContourSupportGroup(selectedMesh, settings);
    }

    /// <summary>
    /// Captures the seed face, seed point, and Z height from a selected model hit.
    /// </summary>
    private void CaptureContourSeed(Vector2 screenPosition, MeshEntity selectedMesh)
    {
        MeshSurfaceHit meshSurfaceHit;

        if (!_projectionService.TryGetMeshSurfaceHit(
            screenPosition,
            hitModel => ReferenceEquals(_scene.GetEntityFromVisual(hitModel), selectedMesh),
            out meshSurfaceHit))
        {
            _statusReporter("Contour Support must start from a point picked on the selected model.");
            return;
        }

        int triangleIndex;

        if (!ContourSupportPattern.TryFindContainingTriangleIndex(selectedMesh, meshSurfaceHit.HitPosition, out triangleIndex))
        {
            _statusReporter("The clicked model face could not be resolved.");
            return;
        }

        _targetModelEntityId = selectedMesh.Id;
        _seedPoint = meshSurfaceHit.HitPosition;
        _seedTriangleIndex = triangleIndex;
        _isPickingZHeight = false;
        _zHeightSelectedReporter(meshSurfaceHit.HitPosition.Z);
        _precisionSelectCursorRequester(false);
        RefreshPreview();
        _statusReporter("Contour preview is ready. Adjust settings or click Apply.");
    }

    /// <summary>
    /// Creates a new Contour Support group from the accepted preview and records it as one undoable command.
    /// </summary>
    private bool CommitNewContourSupportGroup(MeshEntity selectedMesh, ContourSupportSettings settings)
    {
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(selectedMesh.Id, CreateContourSupportLayerGroupName(selectedMesh.Id));
        supportLayerGroup.SetContourSupportSettings(settings);

        int invalidSupportCount;
        List<SupportEntity> supportEntities = CreateSupportEntities(selectedMesh, supportLayerGroup.Id, settings, out invalidSupportCount);

        if (supportEntities.Count == 0)
        {
            _statusReporter("No contour supports could be created from this contour.");
            return false;
        }

        _commandRunner.Execute(new AddSupportsToNewGroupCommand(_document, supportLayerGroup, supportEntities, "Add Contour Supports"));
        EnterSupportGroupEditState(supportLayerGroup, settings);
        _statusReporter(CreateCompletionMessage(supportEntities.Count, invalidSupportCount));
        return true;
    }

    /// <summary>
    /// Updates the loaded Contour Support group as one undoable parametric regeneration.
    /// </summary>
    private bool UpdateExistingContourSupportGroup(MeshEntity selectedMesh, ContourSupportSettings newSettings)
    {
        if (!_editingSupportLayerGroupId.HasValue)
        {
            return false;
        }

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(_editingSupportLayerGroupId.Value);

        if (supportLayerGroup == null)
        {
            _statusReporter("The Contour Support group could not be found.");
            return false;
        }

        ContourSupportSettings? oldSettings = supportLayerGroup.ContourSupportSettings;

        if (oldSettings == null)
        {
            _statusReporter("The selected support group was not created with the Contour Support tool.");
            return false;
        }

        IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        int invalidSupportCount;
        List<SupportEntity> newSupportEntities = CreateSupportEntities(selectedMesh, supportLayerGroup.Id, newSettings, out invalidSupportCount);

        if (newSupportEntities.Count == 0)
        {
            _statusReporter("No contour supports could be created from this contour.");
            return false;
        }

        _commandRunner.Execute(new UpdateContourSupportGroupCommand(
            _document,
            supportLayerGroup,
            oldSettings,
            oldSupportEntities,
            newSettings,
            newSupportEntities));

        EnterSupportGroupEditState(supportLayerGroup, newSettings);
        _statusReporter(CreateCompletionMessage(newSupportEntities.Count, invalidSupportCount));
        return true;
    }

    /// <summary>
    /// Loads the accepted settings into edit mode after an Apply operation.
    /// </summary>
    private void EnterSupportGroupEditState(SupportLayerGroup supportLayerGroup, ContourSupportSettings settings)
    {
        ClearEditingSupportGroupOpacity();
        _editingSupportLayerGroupId = supportLayerGroup.Id;
        _targetModelEntityId = supportLayerGroup.ModelEntityId;
        _seedPoint = settings.SeedPoint;
        _seedTriangleIndex = settings.SeedTriangleIndex;
        _isPickingZHeight = false;
        _precisionSelectCursorRequester(false);
        _scene.SetSupportLayerGroupOpacity(supportLayerGroup.Id, EditingSupportGroupOpacity);
        RefreshPreview();
    }

    /// <summary>
    /// Generates support entities for one Contour Support definition without mutating the document.
    /// </summary>
    private List<SupportEntity> CreateSupportEntities(
        MeshEntity selectedMesh,
        Guid supportLayerGroupId,
        ContourSupportSettings settings,
        out int invalidSupportCount)
    {
        SupportProfile supportProfile = _createSupportProfile();
        ContourSupportResult contourResult;
        invalidSupportCount = 0;

        if (!ContourSupportPattern.TryCreate(selectedMesh, settings, out contourResult))
        {
            return new List<SupportEntity>();
        }

        List<SupportEntity> supportEntities = new List<SupportEntity>(contourResult.SupportSamples.Count);

        for (int i = 0; i < contourResult.SupportSamples.Count; i++)
        {
            ContourSupportSample sample = contourResult.SupportSamples[i];
            Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(sample.Normal, supportProfile);
            SupportBranchPlan branchPlan;

            if (!SupportBranchPlanner.TryCreateBranchPlan(selectedMesh, sample.Position, headDirection, supportProfile, out branchPlan))
            {
                invalidSupportCount++;
                continue;
            }

            try
            {
                supportEntities.Add(new SupportEntity(
                    supportLayerGroupId,
                    sample.Position,
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
    /// Creates validated contour settings from the current UI-backed option callbacks.
    /// </summary>
    private ContourSupportSettings CreateCurrentSettings()
    {
        return new ContourSupportSettings(
            _seedPoint!.Value,
            _seedTriangleIndex,
            _getZHeight(),
            _getCoplanarThresholdDegrees(),
            _getSpacing(),
            _getStartOffset(),
            _getFinalOffset());
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

        _statusReporter("Contour support edit is active. Drag-select supports or click Apply.");
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
    /// Finds the mesh entity that owns the current contour support placement.
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
    /// Creates a stable user-facing name for a newly created contour-support group under one model.
    /// </summary>
    private string CreateContourSupportLayerGroupName(Guid modelEntityId)
    {
        int duplicateCount = 0;

        foreach (SupportLayerGroup existingSupportLayerGroup in _document.SupportLayerGroups)
        {
            if (existingSupportLayerGroup.ModelEntityId != modelEntityId)
            {
                continue;
            }

            if (string.Equals(existingSupportLayerGroup.Name, ContourSupportLayerGroupBaseName, StringComparison.OrdinalIgnoreCase)
                || existingSupportLayerGroup.Name.StartsWith($"{ContourSupportLayerGroupBaseName} ", StringComparison.OrdinalIgnoreCase))
            {
                duplicateCount++;
            }
        }

        if (duplicateCount == 0)
        {
            return ContourSupportLayerGroupBaseName;
        }

        return $"{ContourSupportLayerGroupBaseName} {duplicateCount + 1}";
    }

    /// <summary>
    /// Builds a concise completion message including skipped supports when needed.
    /// </summary>
    private static string CreateCompletionMessage(int createdCount, int invalidSupportCount)
    {
        if (invalidSupportCount == 0)
        {
            return $"Added {createdCount} contour supports.";
        }

        return $"Added {createdCount} contour supports; skipped {invalidSupportCount}.";
    }

    /// <summary>
    /// Builds a preview status message from renderer-agnostic contour extraction diagnostics.
    /// </summary>
    private static string CreateContourPreviewStatusMessage(ContourSupportDiagnostics diagnostics)
    {
        if (diagnostics.ThresholdBlockedAdjacencyCount > 0)
        {
            return "Contour preview is ready. Some adjacent faces were blocked by the Coplanar threshold.";
        }

        if (diagnostics.UsedNearestLongerPath)
        {
            return "Contour preview is ready. A nearby longer contour was selected from the seeded face patch.";
        }

        if (diagnostics.EndpointDegreeIssueCount > 0 || diagnostics.AssembledPathCount > 1)
        {
            return "Contour preview is ready. Multiple contour candidates were found in the seeded face patch.";
        }

        return "Contour preview is ready. Adjust settings or click Apply.";
    }
}
