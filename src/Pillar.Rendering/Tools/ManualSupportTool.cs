// ManualSupportTool.cs
// Handles interactive manual support creation while routing durable document changes through CAD commands.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Tools;
using Pillar.Rendering.Math;
using Pillar.Rendering.Scene;
using System;
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
    private readonly Func<float> _getCircleSupportSpacing;
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
        Func<float> getCircleSupportSpacing)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _getSelectedModelEntityId = getSelectedModelEntityId ?? throw new ArgumentNullException(nameof(getSelectedModelEntityId));
        _getCircleSupportSpacing = getCircleSupportSpacing ?? throw new ArgumentNullException(nameof(getCircleSupportSpacing));
    }

    /// <summary>
    /// Gets the operation currently selected by the Manual Support overlay.
    /// </summary>
    public ManualSupportOperationKind ActiveOperationKind { get; private set; } = ManualSupportOperationKind.None;

    /// <summary>
    /// Raised when a support operation wants the shell to show a status message.
    /// </summary>
    public event Action<string>? StatusMessageRequested;

    /// <summary>
    /// Selects the operation that should receive Manual Support viewport input.
    /// </summary>
    public void SetActiveOperation(ManualSupportOperationKind operationKind)
    {
        if (ActiveOperationKind == operationKind)
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
                RaiseStatusMessageRequested);

            return;
        }

        if (operationKind == ManualSupportOperationKind.Circle)
        {
            _activeOperation = new CircleSupportOperation(
                _document,
                _projectionService,
                _scene,
                _commandRunner,
                _getSelectedModelEntityId,
                _getCircleSupportSpacing,
                RaiseStatusMessageRequested);

            RaiseStatusMessageRequested("Click the first point on the selected model for circle supports.");
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
    /// Raises one shell status message request from the current operation.
    /// </summary>
    private void RaiseStatusMessageRequested(string statusMessage)
    {
        StatusMessageRequested?.Invoke(statusMessage);
    }
}
