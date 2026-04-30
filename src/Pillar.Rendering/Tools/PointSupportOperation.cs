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
    private const string PointSupportLayerGroupBaseName = "Point Supports";

    private readonly CadDocument _document;
    private readonly ProjectionService _projectionService;
    private readonly SceneManager _scene;
    private readonly CadCommandRunner _commandRunner;
    private readonly Func<Guid?> _getSelectedModelEntityId;
    private readonly Action<string> _statusReporter;
    private Guid? _activeSupportLayerGroupId;

    /// <summary>
    /// Creates the point support operation.
    /// </summary>
    public PointSupportOperation(
        CadDocument document,
        ProjectionService projectionService,
        SceneManager scene,
        CadCommandRunner commandRunner,
        Func<Guid?> getSelectedModelEntityId,
        Action<string> statusReporter)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _getSelectedModelEntityId = getSelectedModelEntityId ?? throw new ArgumentNullException(nameof(getSelectedModelEntityId));
        _statusReporter = statusReporter ?? throw new ArgumentNullException(nameof(statusReporter));
    }

    /// <summary>
    /// Creates one support when the click lands on the active model surface.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        ResolveActivePlacementTarget(out Guid? targetModelEntityId, out SupportLayerGroup? supportLayerGroup);

        if (!targetModelEntityId.HasValue)
        {
            _statusReporter("Select a model before placing point supports.");
            return;
        }

        MeshEntity? targetMesh = FindMeshEntity(targetModelEntityId.Value);

        if (targetMesh == null)
        {
            _statusReporter("The selected model could not be found.");
            return;
        }

        MeshSurfaceHit meshSurfaceHit;

        if (!_projectionService.TryGetMeshSurfaceHit(screenPosition, out meshSurfaceHit))
        {
            _statusReporter("Click on the active model to place a point support.");
            return;
        }

        CadEntity? hitEntity = _scene.GetEntityFromVisual(meshSurfaceHit.HitModel);

        if (!ReferenceEquals(hitEntity, targetMesh))
        {
            _statusReporter("Point supports can only be placed on the active model.");
            return;
        }

        SupportLayerGroup resolvedSupportLayerGroup;
        Vector3 tipPosition = meshSurfaceHit.HitPosition;
        Vector3 basePosition = new Vector3(tipPosition.X, tipPosition.Y, 0.0f);

        if (supportLayerGroup == null)
        {
            resolvedSupportLayerGroup = new SupportLayerGroup(targetModelEntityId.Value, CreatePointSupportLayerGroupName(targetModelEntityId.Value));
            SupportEntity firstSupportEntity = new SupportEntity(
                resolvedSupportLayerGroup.Id,
                tipPosition,
                basePosition,
                SupportDefaults.CreateProfile());

            _commandRunner.Execute(new AddSupportToNewGroupCommand(_document, resolvedSupportLayerGroup, firstSupportEntity));
            _activeSupportLayerGroupId = resolvedSupportLayerGroup.Id;
        }
        else
        {
            resolvedSupportLayerGroup = supportLayerGroup;
            SupportEntity supportEntity = new SupportEntity(
                resolvedSupportLayerGroup.Id,
                tipPosition,
                basePosition,
                SupportDefaults.CreateProfile());

            _commandRunner.Execute(new AddEntityCommand(_document, supportEntity, "Add Support"));
        }

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
    /// Resolves the model and support group that the current point-support operation should use.
    /// </summary>
    private void ResolveActivePlacementTarget(out Guid? modelEntityId, out SupportLayerGroup? supportLayerGroup)
    {
        supportLayerGroup = null;
        modelEntityId = null;

        if (_activeSupportLayerGroupId.HasValue)
        {
            SupportLayerGroup? activeSupportLayerGroup = _document.FindSupportLayerGroupById(_activeSupportLayerGroupId.Value);

            if (activeSupportLayerGroup != null)
            {
                supportLayerGroup = activeSupportLayerGroup;
                modelEntityId = activeSupportLayerGroup.ModelEntityId;
                return;
            }

            _activeSupportLayerGroupId = null;
        }

        Guid? selectedModelEntityId = _getSelectedModelEntityId();

        if (selectedModelEntityId.HasValue)
        {
            modelEntityId = selectedModelEntityId.Value;
            return;
        }
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
    /// Creates a stable user-facing name for a newly created point-support group under one model.
    /// </summary>
    private string CreatePointSupportLayerGroupName(Guid modelEntityId)
    {
        int duplicateCount = 0;

        foreach (SupportLayerGroup existingSupportLayerGroup in _document.SupportLayerGroups)
        {
            if (existingSupportLayerGroup.ModelEntityId != modelEntityId)
            {
                continue;
            }

            if (string.Equals(existingSupportLayerGroup.Name, PointSupportLayerGroupBaseName, StringComparison.OrdinalIgnoreCase)
                || existingSupportLayerGroup.Name.StartsWith($"{PointSupportLayerGroupBaseName} ", StringComparison.OrdinalIgnoreCase))
            {
                duplicateCount++;
            }
        }

        if (duplicateCount == 0)
        {
            return PointSupportLayerGroupBaseName;
        }

        return $"{PointSupportLayerGroupBaseName} {duplicateCount + 1}";
    }
}
