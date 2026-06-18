// AreaSupportOperation.cs
// Creates area-pattern support entities from selected model faces while keeping preview state transient.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Selection;
using Pillar.Core.Supports;
using Pillar.Core.Tools;
using Pillar.Geometry.Supports;
using Pillar.Rendering.Preview;
using Pillar.Rendering.Scene;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Input;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Places a new support layer group across selected model faces using top-down area coverage.
/// </summary>
public sealed class AreaSupportOperation : IToolOperation, IEditableSupportGroupOperation
{
    private const string AreaSupportLayerGroupBaseName = "Area Support";
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
    private readonly SceneManager _scene;
    private readonly CadCommandRunner _commandRunner;
    private readonly Func<Guid?> _getSelectedModelEntityId;
    private readonly Func<float> _getSpacing;
    private readonly Func<float> _getBoundaryOffset;
    private readonly Func<float> _getBoundarySpacing;
    private readonly Func<float> _getConcaveCornerAngleDegrees;
    private readonly Func<bool> _getSupportThinRegions;
    private readonly Func<float> _getMinimumThinRegionThickness;
    private readonly Func<AreaSupportFillMode> _getFillMode;
    private readonly Func<int> _getAdditionalOffsetCount;
    private readonly Func<bool> _getShowSupportSpacing;
    private readonly Func<SupportProfile> _createSupportProfile;
    private readonly Action<IReadOnlyCollection<FaceSelectionKey>, Action<IReadOnlyCollection<FaceSelectionKey>>> _faceSelectionSessionStarter;
    private readonly Action<string> _statusReporter;
    private readonly Action<bool> _previewCalculationStateReporter;
    private readonly HashSet<FaceSelectionKey> _selectedFaces = new HashSet<FaceSelectionKey>();
    private readonly List<CadEntity> _windowSelectionBuffer = new List<CadEntity>(64);

    private Guid? _targetModelEntityId;
    private Guid? _editingSupportLayerGroupId;
    private Vector2 _mouseDownPosition;
    private bool _isMouseDown;
    private bool _isDraggingSupportSelection;

    /// <summary>
    /// Creates the area support operation.
    /// </summary>
    public AreaSupportOperation(
        CadDocument document,
        SceneManager scene,
        CadCommandRunner commandRunner,
        Func<Guid?> getSelectedModelEntityId,
        Func<float> getSpacing,
        Func<float> getBoundaryOffset,
        Func<float> getBoundarySpacing,
        Func<float> getConcaveCornerAngleDegrees,
        Func<bool> getSupportThinRegions,
        Func<float> getMinimumThinRegionThickness,
        Func<AreaSupportFillMode> getFillMode,
        Func<int> getAdditionalOffsetCount,
        Func<bool> getShowSupportSpacing,
        Func<SupportProfile> createSupportProfile,
        Action<IReadOnlyCollection<FaceSelectionKey>, Action<IReadOnlyCollection<FaceSelectionKey>>> faceSelectionSessionStarter,
        Action<string> statusReporter,
        Action<bool> previewCalculationStateReporter)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _getSelectedModelEntityId = getSelectedModelEntityId ?? throw new ArgumentNullException(nameof(getSelectedModelEntityId));
        _getSpacing = getSpacing ?? throw new ArgumentNullException(nameof(getSpacing));
        _getBoundaryOffset = getBoundaryOffset ?? throw new ArgumentNullException(nameof(getBoundaryOffset));
        _getBoundarySpacing = getBoundarySpacing ?? throw new ArgumentNullException(nameof(getBoundarySpacing));
        _getConcaveCornerAngleDegrees = getConcaveCornerAngleDegrees ?? throw new ArgumentNullException(nameof(getConcaveCornerAngleDegrees));
        _getSupportThinRegions = getSupportThinRegions ?? throw new ArgumentNullException(nameof(getSupportThinRegions));
        _getMinimumThinRegionThickness = getMinimumThinRegionThickness ?? throw new ArgumentNullException(nameof(getMinimumThinRegionThickness));
        _getFillMode = getFillMode ?? throw new ArgumentNullException(nameof(getFillMode));
        _getAdditionalOffsetCount = getAdditionalOffsetCount ?? throw new ArgumentNullException(nameof(getAdditionalOffsetCount));
        _getShowSupportSpacing = getShowSupportSpacing ?? throw new ArgumentNullException(nameof(getShowSupportSpacing));
        _createSupportProfile = createSupportProfile ?? throw new ArgumentNullException(nameof(createSupportProfile));
        _faceSelectionSessionStarter = faceSelectionSessionStarter ?? throw new ArgumentNullException(nameof(faceSelectionSessionStarter));
        _statusReporter = statusReporter ?? throw new ArgumentNullException(nameof(statusReporter));
        _previewCalculationStateReporter = previewCalculationStateReporter ?? throw new ArgumentNullException(nameof(previewCalculationStateReporter));
    }

    /// <summary>
    /// Gets the Area Support group currently loaded for editing, or null while creating a new area.
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
    /// Starts support selection in edit mode; face selection is launched from the options panel.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        if (_editingSupportLayerGroupId.HasValue)
        {
            StartSupportSelectionGesture(screenPosition);
            return;
        }

        _statusReporter("Use Select faces to choose the area before applying Area Supports.");
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
    /// Cancels the in-progress area support gesture and clears transient preview geometry.
    /// </summary>
    public void Cancel()
    {
        ClearEditingSupportGroupOpacity();
        _targetModelEntityId = null;
        _editingSupportLayerGroupId = null;
        _selectedFaces.Clear();
        ResetSupportSelectionGesture();
        HideSelectionWindow();
        _scene.HideAreaSupportPreview();
    }

    /// <summary>
    /// Launches the reusable face-set selection helper and accepts its result back into this operation.
    /// </summary>
    public void BeginFaceSelection()
    {
        _faceSelectionSessionStarter(_selectedFaces, AcceptFaceSelection);
        _statusReporter("Select model faces for Area Support, then accept the face selection.");
    }

    /// <summary>
    /// Loads an existing Area Support group into the operation so its settings can be edited.
    /// </summary>
    public void EditExistingAreaSupportGroup(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        AreaSupportSettings? settings = supportLayerGroup.AreaSupportSettings;

        if (settings == null)
        {
            _statusReporter("The selected support group was not created with the Area Support tool.");
            return;
        }

        if (_editingSupportLayerGroupId.HasValue && _editingSupportLayerGroupId.Value != supportLayerGroup.Id)
        {
            ClearEditingSupportGroupOpacity();
        }

        _selectedFaces.Clear();

        for (int i = 0; i < settings.SelectedFaces.Count; i++)
        {
            _selectedFaces.Add(settings.SelectedFaces[i]);
        }

        _editingSupportLayerGroupId = supportLayerGroup.Id;
        _targetModelEntityId = supportLayerGroup.ModelEntityId;
        _scene.SetSupportLayerGroupOpacity(supportLayerGroup.Id, EditingSupportGroupOpacity);
        RefreshPreview();
        _statusReporter("Area support group loaded. Adjust settings or click Apply.");
    }

    /// <summary>
    /// Rebuilds the transient Area Support preview from the current tool state and settings.
    /// </summary>
    public void RefreshPreview()
    {
        RunWithPreviewCalculationFeedback(RefreshPreviewCore);
    }

    /// <summary>
    /// Applies the previewed Area Supports to either a new support group or the loaded generated support group.
    /// </summary>
    public bool Apply()
    {
        bool didApply = false;
        RunWithPreviewCalculationFeedback(() => didApply = ApplyCore());
        return didApply;
    }

    /// <summary>
    /// Accepts faces returned by the reusable Face Set Selection helper.
    /// </summary>
    private void AcceptFaceSelection(IReadOnlyCollection<FaceSelectionKey> acceptedSelection)
    {
        _selectedFaces.Clear();

        foreach (FaceSelectionKey selectedFace in acceptedSelection)
        {
            _selectedFaces.Add(selectedFace);
        }

        _targetModelEntityId = ResolveSelectionModelEntityId();
        RefreshPreview();
        _statusReporter($"Area Support selected {_selectedFaces.Count} faces. Adjust settings or click Apply.");
    }

    /// <summary>
    /// Rebuilds the transient area preview without controlling shell feedback.
    /// </summary>
    private void RefreshPreviewCore()
    {
        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (_selectedFaces.Count == 0 || selectedMesh == null)
        {
            _scene.HideAreaSupportPreview();
            return;
        }

        AreaSupportSettings settings = CreateCurrentSettings();
        AreaSupportResult areaSupportResult;

        if (!AreaSupportPattern.TryCreate(selectedMesh, settings, out areaSupportResult))
        {
            _scene.HideAreaSupportPreview();
            _statusReporter("No Area Support preview could be generated from the selected faces.");
            return;
        }

        _scene.ShowAreaSupportPreview(areaSupportResult, settings.Spacing, _getShowSupportSpacing());
        _statusReporter(CreateAreaPreviewStatusMessage(areaSupportResult.Diagnostics));
    }

    /// <summary>
    /// Applies the current area preview without directly controlling shell feedback.
    /// </summary>
    private bool ApplyCore()
    {
        if (_selectedFaces.Count == 0)
        {
            _statusReporter("Select faces before applying Area Supports.");
            return false;
        }

        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _statusReporter("The selected model could not be found.");
            return false;
        }

        AreaSupportSettings settings = CreateCurrentSettings();

        if (_editingSupportLayerGroupId.HasValue)
        {
            return UpdateExistingAreaSupportGroup(selectedMesh, settings);
        }

        return CommitNewAreaSupportGroup(selectedMesh, settings);
    }

    /// <summary>
    /// Creates a new Area Support group from the accepted preview and records it as one undoable command.
    /// </summary>
    private bool CommitNewAreaSupportGroup(MeshEntity selectedMesh, AreaSupportSettings settings)
    {
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(selectedMesh.Id, CreateAreaSupportLayerGroupName(selectedMesh.Id));
        supportLayerGroup.SetAreaSupportSettings(settings);

        int invalidSupportCount;
        List<SupportEntity> supportEntities = CreateSupportEntities(selectedMesh, supportLayerGroup.Id, settings, out invalidSupportCount);

        if (supportEntities.Count == 0)
        {
            _statusReporter("No Area Supports could be created from the selected faces.");
            return false;
        }

        _commandRunner.Execute(new AddSupportsToNewGroupCommand(_document, supportLayerGroup, supportEntities, "Add Area Supports"));
        EnterSupportGroupEditState(supportLayerGroup, settings);
        _statusReporter(CreateCompletionMessage(supportEntities.Count, invalidSupportCount));
        return true;
    }

    /// <summary>
    /// Updates the loaded Area Support group as one undoable parametric regeneration.
    /// </summary>
    private bool UpdateExistingAreaSupportGroup(MeshEntity selectedMesh, AreaSupportSettings newSettings)
    {
        if (!_editingSupportLayerGroupId.HasValue)
        {
            return false;
        }

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(_editingSupportLayerGroupId.Value);

        if (supportLayerGroup == null)
        {
            _statusReporter("The Area Support group could not be found.");
            return false;
        }

        AreaSupportSettings? oldSettings = supportLayerGroup.AreaSupportSettings;

        if (oldSettings == null)
        {
            _statusReporter("The selected support group was not created with the Area Support tool.");
            return false;
        }

        IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        int invalidSupportCount;
        List<SupportEntity> newSupportEntities = CreateSupportEntities(selectedMesh, supportLayerGroup.Id, newSettings, out invalidSupportCount);

        if (newSupportEntities.Count == 0)
        {
            _statusReporter("No Area Supports could be created from the selected faces.");
            return false;
        }

        _commandRunner.Execute(new UpdateAreaSupportGroupCommand(
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
    private void EnterSupportGroupEditState(SupportLayerGroup supportLayerGroup, AreaSupportSettings settings)
    {
        ClearEditingSupportGroupOpacity();
        _editingSupportLayerGroupId = supportLayerGroup.Id;
        _targetModelEntityId = supportLayerGroup.ModelEntityId;
        _selectedFaces.Clear();

        for (int i = 0; i < settings.SelectedFaces.Count; i++)
        {
            _selectedFaces.Add(settings.SelectedFaces[i]);
        }

        _scene.SetSupportLayerGroupOpacity(supportLayerGroup.Id, EditingSupportGroupOpacity);
        RefreshPreview();
    }

    /// <summary>
    /// Generates support entities for one Area Support definition without mutating the document.
    /// </summary>
    private List<SupportEntity> CreateSupportEntities(
        MeshEntity selectedMesh,
        Guid supportLayerGroupId,
        AreaSupportSettings settings,
        out int invalidSupportCount)
    {
        SupportProfile supportProfile = _createSupportProfile();
        AreaSupportResult areaSupportResult;
        invalidSupportCount = 0;

        if (!AreaSupportPattern.TryCreate(selectedMesh, settings, out areaSupportResult))
        {
            return new List<SupportEntity>();
        }

        List<SupportEntity> supportEntities = new List<SupportEntity>(areaSupportResult.SupportSamples.Count);

        for (int i = 0; i < areaSupportResult.SupportSamples.Count; i++)
        {
            AreaSupportSample sample = areaSupportResult.SupportSamples[i];
            SupportPlacementPlan placementPlan;

            if (!SupportPlacementPlanner.TryCreatePlacement(selectedMesh, sample.Position, sample.Normal, supportProfile, out placementPlan))
            {
                invalidSupportCount++;
                continue;
            }

            try
            {
                supportEntities.Add(new SupportEntity(
                    supportLayerGroupId,
                    sample.Position,
                    placementPlan.BasePosition,
                    placementPlan.HeadDirection,
                    placementPlan.BranchLength,
                    placementPlan.BranchDirection,
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
    /// Creates validated area settings from the current UI-backed option callbacks.
    /// </summary>
    private AreaSupportSettings CreateCurrentSettings()
    {
        Guid? modelEntityId = ResolveSelectionModelEntityId();

        if (modelEntityId.HasValue)
        {
            _targetModelEntityId = modelEntityId;
        }

        return new AreaSupportSettings(
            _selectedFaces,
            _getSpacing(),
            _getBoundaryOffset(),
            _getBoundarySpacing(),
            _getConcaveCornerAngleDegrees(),
            _getSupportThinRegions(),
            _getMinimumThinRegionThickness(),
            _getFillMode(),
            _getAdditionalOffsetCount());
    }

    /// <summary>
    /// Notifies the shell while synchronous preview projection or regeneration work is running.
    /// </summary>
    private void RunWithPreviewCalculationFeedback(Action action)
    {
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
    }

    /// <summary>
    /// Applies rectangular selection to supports in the group currently being edited.
    /// </summary>
    private void ApplySupportWindowSelection(Rect selectionRect, bool selectsCrossingEntities, WindowSelectionOperation operation)
    {
        if (!_editingSupportLayerGroupId.HasValue)
        {
            return;
        }

        _windowSelectionBuffer.Clear();
        _scene.FillSupportEntitiesSelectedByWindow(_editingSupportLayerGroupId.Value, selectionRect, selectsCrossingEntities, _windowSelectionBuffer);

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

        SelectionWindowChanged?.Invoke(new SelectionWindowOverlayState(true, selectionRect.Left, selectionRect.Top, selectionRect.Width, selectionRect.Height, useSolidOutline));
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
    /// Finds the mesh entity that owns the current area support placement.
    /// </summary>
    private MeshEntity? ResolvePlacementMesh()
    {
        Guid? selectedModelEntityId = _targetModelEntityId ?? ResolveSelectionModelEntityId() ?? _getSelectedModelEntityId();

        if (!selectedModelEntityId.HasValue)
        {
            return null;
        }

        return FindMeshEntity(selectedModelEntityId.Value);
    }

    /// <summary>
    /// Resolves a single mesh id from the selected faces.
    /// </summary>
    private Guid? ResolveSelectionModelEntityId()
    {
        Guid? modelEntityId = null;

        foreach (FaceSelectionKey selectedFace in _selectedFaces)
        {
            if (!modelEntityId.HasValue)
            {
                modelEntityId = selectedFace.MeshEntityId;
                continue;
            }

            if (modelEntityId.Value != selectedFace.MeshEntityId)
            {
                return _getSelectedModelEntityId();
            }
        }

        return modelEntityId;
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
    /// Creates a stable user-facing name for a newly created Area Support group under one model.
    /// </summary>
    private string CreateAreaSupportLayerGroupName(Guid modelEntityId)
    {
        int duplicateCount = 0;

        foreach (SupportLayerGroup existingSupportLayerGroup in _document.SupportLayerGroups)
        {
            if (existingSupportLayerGroup.ModelEntityId != modelEntityId)
            {
                continue;
            }

            if (string.Equals(existingSupportLayerGroup.Name, AreaSupportLayerGroupBaseName, StringComparison.OrdinalIgnoreCase)
                || existingSupportLayerGroup.Name.StartsWith($"{AreaSupportLayerGroupBaseName} ", StringComparison.OrdinalIgnoreCase))
            {
                duplicateCount++;
            }
        }

        if (duplicateCount == 0)
        {
            return AreaSupportLayerGroupBaseName;
        }

        return $"{AreaSupportLayerGroupBaseName} {duplicateCount + 1}";
    }

    /// <summary>
    /// Builds a concise completion message including skipped supports when needed.
    /// </summary>
    private static string CreateCompletionMessage(int createdCount, int invalidSupportCount)
    {
        if (invalidSupportCount == 0)
        {
            return $"Added {createdCount} area supports.";
        }

        return $"Added {createdCount} area supports; skipped {invalidSupportCount}.";
    }

    /// <summary>
    /// Builds a preview status message from renderer-agnostic area generation diagnostics.
    /// </summary>
    private static string CreateAreaPreviewStatusMessage(AreaSupportDiagnostics diagnostics)
    {
        if (diagnostics.IslandCount > 1)
        {
            return $"Area preview is ready across {diagnostics.IslandCount} selected face islands.";
        }

        return "Area preview is ready. Adjust settings or click Apply.";
    }
}
