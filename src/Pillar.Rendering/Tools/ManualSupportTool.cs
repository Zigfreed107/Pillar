// ManualSupportTool.cs
// Handles interactive manual support creation while routing durable document changes through CAD commands.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Selection;
using Pillar.Core.Supports;
using Pillar.Core.Tools;
using Pillar.Rendering.Math;
using Pillar.Rendering.Scene;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Routes Manual Support mode viewport input to the currently selected support operation.
/// </summary>
public class ManualSupportTool : ITool
{
    private readonly CadDocument _document;
    private readonly ProjectionService _projectionService;
    private readonly SceneManager _scene;
    private readonly CadCommandRunner _commandRunner;
    private readonly Func<Guid?> _getSelectedModelEntityId;
    private readonly Func<float> _getRingSupportSpacing;
    private readonly Func<float> _getLineSupportSpacing;
    private readonly Func<bool> _getLineSupportPlaceSupportsAtBends;
    private readonly Func<float> _getContourSupportZHeight;
    private readonly Func<float> _getContourSupportCoplanarThresholdDegrees;
    private readonly Func<float> _getContourSupportSpacing;
    private readonly Func<float> _getContourSupportStartOffset;
    private readonly Func<float> _getContourSupportFinalOffset;
    private readonly Func<float> _getAreaSupportSpacing;
    private readonly Func<float> _getAreaSupportBoundarySpacing;
    private readonly Func<float> _getAreaSupportConcaveCornerAngleDegrees;
    private readonly Func<bool> _getAreaSupportShowSpacing;
    private readonly Func<SupportProfile> _createSupportProfile;
    private readonly Action<float> _contourSupportZHeightSelectedReporter;
    private readonly Action<bool> _contourSupportClosedStateReporter;
    private readonly Action<IReadOnlyCollection<FaceSelectionKey>, Action<IReadOnlyCollection<FaceSelectionKey>>> _faceSelectionSessionStarter;
    private IToolOperation? _activeOperation;

    /// <summary>
    /// Creates the manual support tool with the services needed to instantiate support operations.
    /// </summary>
    public ManualSupportTool(
        CadDocument document,
        ProjectionService projectionService,
        SceneManager scene,
        CadCommandRunner commandRunner,
        Func<Guid?> getSelectedModelEntityId,
        Func<float> getRingSupportSpacing,
        Func<float> getLineSupportSpacing,
        Func<bool> getLineSupportPlaceSupportsAtBends,
        Func<float> getContourSupportZHeight,
        Func<float> getContourSupportCoplanarThresholdDegrees,
        Func<float> getContourSupportSpacing,
        Func<float> getContourSupportStartOffset,
        Func<float> getContourSupportFinalOffset,
        Func<float> getAreaSupportSpacing,
        Func<float> getAreaSupportBoundarySpacing,
        Func<float> getAreaSupportConcaveCornerAngleDegrees,
        Func<bool> getAreaSupportShowSpacing,
        Action<float> contourSupportZHeightSelectedReporter,
        Action<bool> contourSupportClosedStateReporter,
        Action<IReadOnlyCollection<FaceSelectionKey>, Action<IReadOnlyCollection<FaceSelectionKey>>> faceSelectionSessionStarter,
        Func<SupportProfile> createSupportProfile)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _getSelectedModelEntityId = getSelectedModelEntityId ?? throw new ArgumentNullException(nameof(getSelectedModelEntityId));
        _getRingSupportSpacing = getRingSupportSpacing ?? throw new ArgumentNullException(nameof(getRingSupportSpacing));
        _getLineSupportSpacing = getLineSupportSpacing ?? throw new ArgumentNullException(nameof(getLineSupportSpacing));
        _getLineSupportPlaceSupportsAtBends = getLineSupportPlaceSupportsAtBends ?? throw new ArgumentNullException(nameof(getLineSupportPlaceSupportsAtBends));
        _getContourSupportZHeight = getContourSupportZHeight ?? throw new ArgumentNullException(nameof(getContourSupportZHeight));
        _getContourSupportCoplanarThresholdDegrees = getContourSupportCoplanarThresholdDegrees ?? throw new ArgumentNullException(nameof(getContourSupportCoplanarThresholdDegrees));
        _getContourSupportSpacing = getContourSupportSpacing ?? throw new ArgumentNullException(nameof(getContourSupportSpacing));
        _getContourSupportStartOffset = getContourSupportStartOffset ?? throw new ArgumentNullException(nameof(getContourSupportStartOffset));
        _getContourSupportFinalOffset = getContourSupportFinalOffset ?? throw new ArgumentNullException(nameof(getContourSupportFinalOffset));
        _getAreaSupportSpacing = getAreaSupportSpacing ?? throw new ArgumentNullException(nameof(getAreaSupportSpacing));
        _getAreaSupportBoundarySpacing = getAreaSupportBoundarySpacing ?? throw new ArgumentNullException(nameof(getAreaSupportBoundarySpacing));
        _getAreaSupportConcaveCornerAngleDegrees = getAreaSupportConcaveCornerAngleDegrees ?? throw new ArgumentNullException(nameof(getAreaSupportConcaveCornerAngleDegrees));
        _getAreaSupportShowSpacing = getAreaSupportShowSpacing ?? throw new ArgumentNullException(nameof(getAreaSupportShowSpacing));
        _contourSupportZHeightSelectedReporter = contourSupportZHeightSelectedReporter ?? throw new ArgumentNullException(nameof(contourSupportZHeightSelectedReporter));
        _contourSupportClosedStateReporter = contourSupportClosedStateReporter ?? throw new ArgumentNullException(nameof(contourSupportClosedStateReporter));
        _faceSelectionSessionStarter = faceSelectionSessionStarter ?? throw new ArgumentNullException(nameof(faceSelectionSessionStarter));
        _createSupportProfile = createSupportProfile ?? throw new ArgumentNullException(nameof(createSupportProfile));
    }

    /// <summary>
    /// Gets the operation currently selected by the Manual Support overlay.
    /// </summary>
    public ManualSupportOperationKind ActiveOperationKind { get; private set; } = ManualSupportOperationKind.None;

    /// <summary>
    /// Gets the support group currently being edited by the active support operation.
    /// </summary>
    public Guid? ActiveEditingSupportLayerGroupId
    {
        get
        {
            if (_activeOperation is IEditableSupportGroupOperation editableSupportGroupOperation)
            {
                return editableSupportGroupOperation.EditingSupportLayerGroupId;
            }

            return null;
        }
    }

    /// <summary>
    /// Raised when a support operation wants the shell to show a status message.
    /// </summary>
    public event Action<string>? StatusMessageRequested;

    /// <summary>
    /// Raised when an operation wants the shell to show or restore the precision-selection cursor.
    /// </summary>
    public event Action<bool>? PrecisionSelectCursorRequested;

    /// <summary>
    /// Raised when an operation starts or finishes synchronous preview work that may block viewport feedback.
    /// </summary>
    public event Action<bool>? PreviewCalculationStateChanged;

    /// <summary>
    /// Raised while a support edit operation wants the shell to draw a selection rectangle.
    /// </summary>
    public event Action<SelectionWindowOverlayState>? SelectionWindowChanged;

    /// <summary>
    /// Selects the operation that should receive Manual Support viewport input.
    /// </summary>
    public void SetActiveOperation(ManualSupportOperationKind operationKind, bool restartExistingOperation = false)
    {
        if (ActiveOperationKind == operationKind && !restartExistingOperation)
        {
            return;
        }

        _activeOperation?.Cancel();
        ActiveOperationKind = operationKind;

        if (operationKind == ManualSupportOperationKind.Point)
        {
            _activeOperation = new PointSupportOperation(
                _document,
                _projectionService,
                _scene,
                _commandRunner,
                _getSelectedModelEntityId,
                _createSupportProfile,
                RaiseStatusMessageRequested);

            return;
        }

        if (operationKind == ManualSupportOperationKind.Line)
        {
            LineSupportOperation lineSupportOperation = new LineSupportOperation(
                _document,
                _projectionService,
                _scene,
                _commandRunner,
                _getSelectedModelEntityId,
                _getLineSupportSpacing,
                _getLineSupportPlaceSupportsAtBends,
                _createSupportProfile,
                RaiseStatusMessageRequested,
                RaisePrecisionSelectCursorRequested,
                RaisePreviewCalculationStateChanged);

            lineSupportOperation.SelectionWindowChanged += RaiseSelectionWindowChanged;
            _activeOperation = lineSupportOperation;
            RaiseStatusMessageRequested("Click the first point on the selected model for line supports.");
            return;
        }

        if (operationKind == ManualSupportOperationKind.Ring)
        {
            RingSupportOperation ringSupportOperation = new RingSupportOperation(
                _document,
                _projectionService,
                _scene,
                _commandRunner,
                _getSelectedModelEntityId,
                _getRingSupportSpacing,
                _createSupportProfile,
                RaiseStatusMessageRequested,
                RaisePrecisionSelectCursorRequested,
                RaisePreviewCalculationStateChanged);

            ringSupportOperation.SelectionWindowChanged += RaiseSelectionWindowChanged;
            _activeOperation = ringSupportOperation;
            RaiseStatusMessageRequested("Click the first point on the selected model for ring supports.");
            return;
        }

        if (operationKind == ManualSupportOperationKind.Contour)
        {
            ContourSupportOperation contourSupportOperation = new ContourSupportOperation(
                _document,
                _projectionService,
                _scene,
                _commandRunner,
                _getSelectedModelEntityId,
                _getContourSupportZHeight,
                _getContourSupportCoplanarThresholdDegrees,
                _getContourSupportSpacing,
                _getContourSupportStartOffset,
                _getContourSupportFinalOffset,
                _createSupportProfile,
                _contourSupportZHeightSelectedReporter,
                _contourSupportClosedStateReporter,
                RaiseStatusMessageRequested,
                RaisePrecisionSelectCursorRequested,
                RaisePreviewCalculationStateChanged);

            contourSupportOperation.SelectionWindowChanged += RaiseSelectionWindowChanged;
            _activeOperation = contourSupportOperation;
            RaiseStatusMessageRequested("Click the selected model to seed contour supports.");
            return;
        }

        if (operationKind == ManualSupportOperationKind.Area)
        {
            AreaSupportOperation areaSupportOperation = new AreaSupportOperation(
                _document,
                _scene,
                _commandRunner,
                _getSelectedModelEntityId,
                _getAreaSupportSpacing,
                _getAreaSupportBoundarySpacing,
                _getAreaSupportConcaveCornerAngleDegrees,
                _getAreaSupportShowSpacing,
                _createSupportProfile,
                _faceSelectionSessionStarter,
                RaiseStatusMessageRequested,
                RaisePreviewCalculationStateChanged);

            areaSupportOperation.SelectionWindowChanged += RaiseSelectionWindowChanged;
            _activeOperation = areaSupportOperation;
            RaiseStatusMessageRequested("Use Select faces to choose an area for supports.");
            return;
        }

        _activeOperation = null;
    }

    /// <summary>
    /// Routes mouse-down input to the active operation when one has concrete behavior.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        _activeOperation?.OnMouseDown(screenPosition);
    }

    /// <summary>
    /// Routes mouse-move input to the active operation when one has concrete behavior.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        _activeOperation?.OnMouseMove(screenPosition);
    }

    /// <summary>
    /// Routes mouse-up input to the active operation when one has concrete behavior.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
        _activeOperation?.OnMouseUp(screenPosition);
    }

    /// <summary>
    /// Cancels transient operation state without clearing the selected operation kind.
    /// </summary>
    public void Cancel()
    {
        _activeOperation?.Cancel();
    }

    /// <summary>
    /// Refreshes live option-driven previews for operations that expose editable preview state.
    /// </summary>
    public void RefreshActiveOperationPreview()
    {
        if (_activeOperation is LineSupportOperation lineSupportOperation)
        {
            lineSupportOperation.RefreshPreview();
            return;
        }

        if (_activeOperation is RingSupportOperation ringSupportOperation)
        {
            ringSupportOperation.RefreshPreview();
            return;
        }

        if (_activeOperation is ContourSupportOperation contourSupportOperation)
        {
            contourSupportOperation.RefreshPreview();
            return;
        }

        if (_activeOperation is AreaSupportOperation areaSupportOperation)
        {
            areaSupportOperation.RefreshPreview();
        }
    }

    /// <summary>
    /// Applies an operation preview when the active operation supports an explicit apply step.
    /// </summary>
    public bool ApplyActiveOperation()
    {
        if (_activeOperation is LineSupportOperation lineSupportOperation)
        {
            return lineSupportOperation.Apply();
        }

        if (_activeOperation is RingSupportOperation ringSupportOperation)
        {
            return ringSupportOperation.Apply();
        }

        if (_activeOperation is ContourSupportOperation contourSupportOperation)
        {
            return contourSupportOperation.Apply();
        }

        if (_activeOperation is AreaSupportOperation areaSupportOperation)
        {
            return areaSupportOperation.Apply();
        }

        RaiseStatusMessageRequested("Choose a generated support tool before applying supports.");
        return false;
    }

    /// <summary>
    /// Requests that the active Contour Support operation capture a new Z height from the next model click.
    /// </summary>
    public void BeginPickContourSupportZHeight()
    {
        if (_activeOperation is ContourSupportOperation contourSupportOperation)
        {
            contourSupportOperation.BeginPickZHeight();
            return;
        }

        RaiseStatusMessageRequested("Choose the Contour Support tool before picking a contour Z height.");
    }

    /// <summary>
    /// Requests that the active Area Support operation launch the reusable face-selection helper.
    /// </summary>
    public void BeginAreaSupportFaceSelection()
    {
        if (_activeOperation is AreaSupportOperation areaSupportOperation)
        {
            areaSupportOperation.BeginFaceSelection();
            return;
        }

        RaiseStatusMessageRequested("Choose the Area Support tool before selecting faces.");
    }

    /// <summary>
    /// Completes the active Line Support polyline when the shell receives the finishing keyboard or Apply request.
    /// </summary>
    public bool FinalizeActiveLineSupportPolyline()
    {
        if (_activeOperation is LineSupportOperation lineSupportOperation)
        {
            return lineSupportOperation.FinalizePolyline();
        }

        return false;
    }

    /// <summary>
    /// Deletes selected support entities that belong to the support group currently being edited.
    /// </summary>
    public bool DeleteSelectedSupportsInActiveEditGroup()
    {
        if (_activeOperation is not IEditableSupportGroupOperation editableSupportGroupOperation
            || !editableSupportGroupOperation.EditingSupportLayerGroupId.HasValue)
        {
            return false;
        }

        Guid editingSupportLayerGroupId = editableSupportGroupOperation.EditingSupportLayerGroupId.Value;
        List<SupportEntity> selectedSupportEntities = new List<SupportEntity>();

        foreach (Guid selectedEntityId in _scene.SelectionManager.SelectedEntityIds)
        {
            if (FindSupportEntityById(selectedEntityId) is SupportEntity supportEntity
                && supportEntity.SupportLayerGroupId == editingSupportLayerGroupId)
            {
                selectedSupportEntities.Add(supportEntity);
            }
        }

        if (selectedSupportEntities.Count == 0)
        {
            RaiseStatusMessageRequested("Select a support in the active edit group before pressing Delete.");
            return true;
        }

        string displayName = selectedSupportEntities.Count == 1
            ? "Delete Support"
            : "Delete Supports";

        _commandRunner.Execute(new RemoveSupportEntitiesCommand(_document, selectedSupportEntities, displayName));
        RaiseStatusMessageRequested(CreateSupportDeletionMessage(selectedSupportEntities.Count));
        return true;
    }

    /// <summary>
    /// Gets whether current selection contains at least one support from the active edit group.
    /// </summary>
    public bool HasSelectedSupportsInActiveEditGroup()
    {
        if (_activeOperation is not IEditableSupportGroupOperation editableSupportGroupOperation
            || !editableSupportGroupOperation.EditingSupportLayerGroupId.HasValue)
        {
            return false;
        }

        Guid editingSupportLayerGroupId = editableSupportGroupOperation.EditingSupportLayerGroupId.Value;

        foreach (Guid selectedEntityId in _scene.SelectionManager.SelectedEntityIds)
        {
            if (FindSupportEntityById(selectedEntityId) is SupportEntity supportEntity
                && supportEntity.SupportLayerGroupId == editingSupportLayerGroupId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Loads an existing Ring Support-generated group into the Ring Support operation.
    /// </summary>
    public void EditRingSupportGroup(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        if (ActiveOperationKind != ManualSupportOperationKind.Ring)
        {
            SetActiveOperation(ManualSupportOperationKind.Ring);
        }

        if (_activeOperation is RingSupportOperation ringSupportOperation)
        {
            ringSupportOperation.EditExistingRingSupportGroup(supportLayerGroup);
        }
    }

    /// <summary>
    /// Loads an existing Line Support-generated group into the Line Support operation.
    /// </summary>
    public void EditLineSupportGroup(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        if (ActiveOperationKind != ManualSupportOperationKind.Line)
        {
            SetActiveOperation(ManualSupportOperationKind.Line);
        }

        if (_activeOperation is LineSupportOperation lineSupportOperation)
        {
            lineSupportOperation.EditExistingLineSupportGroup(supportLayerGroup);
        }
    }

    /// <summary>
    /// Loads an existing Contour Support-generated group into the Contour Support operation.
    /// </summary>
    public void EditContourSupportGroup(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        if (ActiveOperationKind != ManualSupportOperationKind.Contour)
        {
            SetActiveOperation(ManualSupportOperationKind.Contour);
        }

        if (_activeOperation is ContourSupportOperation contourSupportOperation)
        {
            contourSupportOperation.EditExistingContourSupportGroup(supportLayerGroup);
        }
    }

    /// <summary>
    /// Loads an existing Area Support-generated group into the Area Support operation.
    /// </summary>
    public void EditAreaSupportGroup(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        if (ActiveOperationKind != ManualSupportOperationKind.Area)
        {
            SetActiveOperation(ManualSupportOperationKind.Area);
        }

        if (_activeOperation is AreaSupportOperation areaSupportOperation)
        {
            areaSupportOperation.EditExistingAreaSupportGroup(supportLayerGroup);
        }
    }

    /// <summary>
    /// Finds one support entity in the current document by its stable identifier.
    /// </summary>
    private SupportEntity? FindSupportEntityById(Guid supportEntityId)
    {
        foreach (CadEntity entity in _document.Entities)
        {
            if (entity is SupportEntity supportEntity && supportEntity.Id == supportEntityId)
            {
                return supportEntity;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds user-facing feedback for one support deletion command.
    /// </summary>
    private static string CreateSupportDeletionMessage(int deletedSupportCount)
    {
        if (deletedSupportCount == 1)
        {
            return "Deleted support from the active edit group.";
        }

        return $"Deleted {deletedSupportCount} supports from the active edit group.";
    }

    /// <summary>
    /// Raises one shell status message request from the current operation.
    /// </summary>
    private void RaiseStatusMessageRequested(string statusMessage)
    {
        StatusMessageRequested?.Invoke(statusMessage);
    }

    /// <summary>
    /// Raises one shell cursor request from the current operation.
    /// </summary>
    private void RaisePrecisionSelectCursorRequested(bool isPrecisionSelectCursorRequested)
    {
        PrecisionSelectCursorRequested?.Invoke(isPrecisionSelectCursorRequested);
    }

    /// <summary>
    /// Raises one abstract preview-calculation state change from the current operation.
    /// </summary>
    private void RaisePreviewCalculationStateChanged(bool isCalculating)
    {
        PreviewCalculationStateChanged?.Invoke(isCalculating);
    }

    /// <summary>
    /// Raises one selection-window overlay update from the current operation.
    /// </summary>
    private void RaiseSelectionWindowChanged(SelectionWindowOverlayState state)
    {
        SelectionWindowChanged?.Invoke(state);
    }
}
