// ManualSupportTool.cs
// Handles interactive manual support creation while routing durable document changes through CAD commands.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
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
    private readonly Func<SupportProfile> _createSupportProfile;
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
        Func<SupportProfile> createSupportProfile)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _getSelectedModelEntityId = getSelectedModelEntityId ?? throw new ArgumentNullException(nameof(getSelectedModelEntityId));
        _getRingSupportSpacing = getRingSupportSpacing ?? throw new ArgumentNullException(nameof(getRingSupportSpacing));
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
        if (_activeOperation is RingSupportOperation ringSupportOperation)
        {
            ringSupportOperation.RefreshPreview();
        }
    }

    /// <summary>
    /// Applies an operation preview when the active operation supports an explicit apply step.
    /// </summary>
    public bool ApplyActiveOperation()
    {
        if (_activeOperation is RingSupportOperation ringSupportOperation)
        {
            return ringSupportOperation.Apply();
        }

        RaiseStatusMessageRequested("Choose the Ring Support tool before applying ring supports.");
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
