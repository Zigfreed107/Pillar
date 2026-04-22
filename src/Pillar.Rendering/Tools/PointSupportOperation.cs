// PointSupportOperation.cs
// Creates one point support per click by converting a viewport mesh hit into a support entity and command.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using Pillar.Core.Tools;
using Pillar.Rendering.Math;
using Pillar.Rendering.Scene;
using System;
using System.Numerics;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Creates one support entity from a single click onto the selected model surface.
/// </summary>
public sealed class PointSupportOperation : IToolOperation
{
    private readonly CadDocument _document;
    private readonly ProjectionService _projectionService;
    private readonly SceneManager _scene;
    private readonly CadCommandRunner _commandRunner;
    private readonly Func<Guid?> _getSelectedSupportLayerGroupId;
    private readonly Action<string> _statusReporter;

    /// <summary>
    /// Creates the point support operation.
    /// </summary>
    public PointSupportOperation(
        CadDocument document,
        ProjectionService projectionService,
        SceneManager scene,
        CadCommandRunner commandRunner,
        Func<Guid?> getSelectedSupportLayerGroupId,
        Action<string> statusReporter)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _getSelectedSupportLayerGroupId = getSelectedSupportLayerGroupId ?? throw new ArgumentNullException(nameof(getSelectedSupportLayerGroupId));
        _statusReporter = statusReporter ?? throw new ArgumentNullException(nameof(statusReporter));
    }

    /// <summary>
    /// Creates one support when the click lands on the selected support group's owning mesh.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        Guid? selectedSupportLayerGroupId = _getSelectedSupportLayerGroupId();

        if (!selectedSupportLayerGroupId.HasValue)
        {
            _statusReporter("Select a support group before placing supports.");
            return;
        }

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(selectedSupportLayerGroupId.Value);

        if (supportLayerGroup == null)
        {
            _statusReporter("The selected support group is no longer available.");
            return;
        }

        MeshEntity? targetMesh = FindMeshEntity(supportLayerGroup.ModelEntityId);

        if (targetMesh == null)
        {
            _statusReporter("The support group's owning model could not be found.");
            return;
        }

        MeshSurfaceHit meshSurfaceHit;

        if (!_projectionService.TryGetMeshSurfaceHit(screenPosition, out meshSurfaceHit))
        {
            _statusReporter("Click on the selected model to place a support.");
            return;
        }

        CadEntity? hitEntity = _scene.GetEntityFromVisual(meshSurfaceHit.HitModel);

        if (!ReferenceEquals(hitEntity, targetMesh))
        {
            _statusReporter("Supports can only be placed on the model owned by the selected support group.");
            return;
        }

        Vector3 tipPosition = meshSurfaceHit.HitPosition;
        Vector3 basePosition = new Vector3(tipPosition.X, tipPosition.Y, 0.0f);
        SupportEntity supportEntity = new SupportEntity(
            supportLayerGroup.Id,
            tipPosition,
            basePosition,
            SupportDefaults.CreateProfile());

        _commandRunner.Execute(new AddEntityCommand(_document, supportEntity, "Add Support"));
        _statusReporter("Added support");
    }

    /// <summary>
    /// Ignores mouse move because point support creation is click-based in v1.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        _ = screenPosition;
    }

    /// <summary>
    /// Ignores mouse up because point support creation is committed on mouse down.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
        _ = screenPosition;
    }

    /// <summary>
    /// Cancels transient state owned by the operation.
    /// </summary>
    public void Cancel()
    {
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
}
