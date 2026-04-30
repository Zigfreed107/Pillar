// CircleSupportOperation.cs
// Creates a ring of individual support entities from three model-surface picks while keeping preview state transient.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using Pillar.Core.Tools;
using Pillar.Geometry.Primitives;
using Pillar.Geometry.Supports;
using Pillar.Rendering.Math;
using Pillar.Rendering.Scene;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Places a new support layer group by distributing supports around a three-point circle and projecting them vertically onto the active mesh.
/// </summary>
public sealed class CircleSupportOperation : IToolOperation
{
    private const string CircleSupportLayerGroupBaseName = "Circle Supports";
    private const int MinimumSupportCount = 3;
    private const int MaximumSupportCount = 512;

    private readonly CadDocument _document;
    private readonly ProjectionService _projectionService;
    private readonly SceneManager _scene;
    private readonly CadCommandRunner _commandRunner;
    private readonly Func<Guid?> _getSelectedModelEntityId;
    private readonly Func<float> _getSpacing;
    private readonly Action<string> _statusReporter;
    private readonly List<Vector3> _projectedPreviewPoints = new List<Vector3>(MaximumSupportCount);

    private Vector3? _firstPoint;
    private Vector3? _secondPoint;

    /// <summary>
    /// Creates the circle support operation.
    /// </summary>
    public CircleSupportOperation(
        CadDocument document,
        ProjectionService projectionService,
        SceneManager scene,
        CadCommandRunner commandRunner,
        Func<Guid?> getSelectedModelEntityId,
        Func<float> getSpacing,
        Action<string> statusReporter)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _getSelectedModelEntityId = getSelectedModelEntityId ?? throw new ArgumentNullException(nameof(getSelectedModelEntityId));
        _getSpacing = getSpacing ?? throw new ArgumentNullException(nameof(getSpacing));
        _statusReporter = statusReporter ?? throw new ArgumentNullException(nameof(statusReporter));
    }

    /// <summary>
    /// Captures the next circle point or commits the completed three-point circle.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        MeshEntity? selectedMesh = ResolveSelectedMesh();

        if (selectedMesh == null)
        {
            _statusReporter("Select a model before placing circle supports.");
            return;
        }

        Vector3 hitPosition;

        if (!TryGetHitOnSelectedMesh(screenPosition, selectedMesh, out hitPosition))
        {
            _statusReporter("Circle support points must be picked on the selected model.");
            return;
        }

        if (!_firstPoint.HasValue)
        {
            _firstPoint = hitPosition;
            _scene.HideCircleSupportPreview();
            _statusReporter("Click the second point on the circle circumference.");
            return;
        }

        if (!_secondPoint.HasValue)
        {
            _secondPoint = hitPosition;
            _statusReporter("Click the third point to finish the circle supports.");
            return;
        }

        CommitCircle(selectedMesh, _firstPoint.Value, _secondPoint.Value, hitPosition);
    }

    /// <summary>
    /// Updates provisional or full circle preview while the user is choosing points.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        if (!_firstPoint.HasValue)
        {
            return;
        }

        MeshEntity? selectedMesh = ResolveSelectedMesh();

        if (selectedMesh == null)
        {
            return;
        }

        Vector3 hoverPoint;

        if (!TryGetHitOnSelectedMesh(screenPosition, selectedMesh, out hoverPoint))
        {
            return;
        }

        if (!_secondPoint.HasValue)
        {
            Circle3D provisionalCircle;

            if (Circle3D.TryCreateFromDiameter(_firstPoint.Value, hoverPoint, out provisionalCircle))
            {
                _scene.ShowCircleSupportPreview(provisionalCircle);
            }

            return;
        }

        Circle3D circle;

        if (!Circle3D.TryCreateFromThreePoints(_firstPoint.Value, _secondPoint.Value, hoverPoint, out circle))
        {
            _scene.HideCircleSupportPreview();
            return;
        }

        _scene.ShowCircleSupportPreview(circle);
        UpdateProjectedMarkerPreview(selectedMesh, circle);
    }

    /// <summary>
    /// Ignores mouse-up because circle creation is committed by three deliberate clicks.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
        _ = screenPosition;
    }

    /// <summary>
    /// Cancels the in-progress circle gesture and clears transient preview geometry.
    /// </summary>
    public void Cancel()
    {
        _firstPoint = null;
        _secondPoint = null;
        _projectedPreviewPoints.Clear();
        _scene.HideCircleSupportPreview();
    }

    /// <summary>
    /// Creates all circle supports and records them as one undoable command.
    /// </summary>
    private void CommitCircle(MeshEntity selectedMesh, Vector3 firstPoint, Vector3 secondPoint, Vector3 thirdPoint)
    {
        Circle3D circle;

        if (!Circle3D.TryCreateFromThreePoints(firstPoint, secondPoint, thirdPoint, out circle))
        {
            _statusReporter("Circle support points must not be duplicate or collinear.");
            return;
        }

        float spacing = _getSpacing();
        int requestedSupportCount = CalculateSupportCount(circle, spacing);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(selectedMesh.Id, CreateCircleSupportLayerGroupName(selectedMesh.Id));
        List<SupportEntity> supportEntities = new List<SupportEntity>(requestedSupportCount);
        int missedProjectionCount = 0;
        int invalidSupportCount = 0;

        for (int i = 0; i < requestedSupportCount; i++)
        {
            float angle = (float)(i * System.Math.PI * 2.0 / requestedSupportCount);
            Vector3 guidePoint = circle.GetPoint(angle);
            Vector3 projectedPoint;

            if (!MeshVerticalProjection.TryProjectToMesh(selectedMesh, guidePoint, out projectedPoint))
            {
                missedProjectionCount++;
                continue;
            }

            Vector3 basePosition = new Vector3(projectedPoint.X, projectedPoint.Y, 0.0f);

            try
            {
                supportEntities.Add(new SupportEntity(
                    supportLayerGroup.Id,
                    projectedPoint,
                    basePosition,
                    SupportDefaults.CreateProfile()));
            }
            catch (ArgumentException)
            {
                invalidSupportCount++;
            }
        }

        if (supportEntities.Count == 0)
        {
            _statusReporter("No circle supports could be projected onto the selected model.");
            return;
        }

        _commandRunner.Execute(new AddSupportsToNewGroupCommand(_document, supportLayerGroup, supportEntities, "Add Circle Supports"));
        _statusReporter(CreateCompletionMessage(supportEntities.Count, missedProjectionCount, invalidSupportCount));
        Cancel();
    }

    /// <summary>
    /// Updates lightweight projected support markers for the current circle preview.
    /// </summary>
    private void UpdateProjectedMarkerPreview(MeshEntity selectedMesh, Circle3D circle)
    {
        _projectedPreviewPoints.Clear();
        int supportCount = CalculateSupportCount(circle, _getSpacing());

        for (int i = 0; i < supportCount; i++)
        {
            float angle = (float)(i * System.Math.PI * 2.0 / supportCount);
            Vector3 guidePoint = circle.GetPoint(angle);
            Vector3 projectedPoint;

            if (MeshVerticalProjection.TryProjectToMesh(selectedMesh, guidePoint, out projectedPoint))
            {
                _projectedPreviewPoints.Add(projectedPoint);
            }
        }

        _scene.ShowCircleSupportMarkers(_projectedPreviewPoints);
    }

    /// <summary>
    /// Converts requested spacing into an even support count around the circle.
    /// </summary>
    private static int CalculateSupportCount(Circle3D circle, float spacing)
    {
        if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0.0f)
        {
            spacing = 5.0f;
        }

        int count = (int)MathF.Ceiling(circle.Circumference / spacing);

        if (count < MinimumSupportCount)
        {
            count = MinimumSupportCount;
        }

        if (count > MaximumSupportCount)
        {
            count = MaximumSupportCount;
        }

        return count;
    }

    /// <summary>
    /// Finds the selected mesh entity that owns the circle support placement.
    /// </summary>
    private MeshEntity? ResolveSelectedMesh()
    {
        Guid? selectedModelEntityId = _getSelectedModelEntityId();

        if (!selectedModelEntityId.HasValue)
        {
            return null;
        }

        foreach (CadEntity entity in _document.Entities)
        {
            if (entity is MeshEntity meshEntity && meshEntity.Id == selectedModelEntityId.Value)
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

        if (!_projectionService.TryGetMeshSurfaceHit(screenPosition, out meshSurfaceHit))
        {
            hitPosition = Vector3.Zero;
            return false;
        }

        CadEntity? hitEntity = _scene.GetEntityFromVisual(meshSurfaceHit.HitModel);

        if (!ReferenceEquals(hitEntity, selectedMesh))
        {
            hitPosition = Vector3.Zero;
            return false;
        }

        hitPosition = meshSurfaceHit.HitPosition;
        return true;
    }

    /// <summary>
    /// Creates a stable user-facing name for a newly created circle-support group under one model.
    /// </summary>
    private string CreateCircleSupportLayerGroupName(Guid modelEntityId)
    {
        int duplicateCount = 0;

        foreach (SupportLayerGroup existingSupportLayerGroup in _document.SupportLayerGroups)
        {
            if (existingSupportLayerGroup.ModelEntityId != modelEntityId)
            {
                continue;
            }

            if (string.Equals(existingSupportLayerGroup.Name, CircleSupportLayerGroupBaseName, StringComparison.OrdinalIgnoreCase)
                || existingSupportLayerGroup.Name.StartsWith($"{CircleSupportLayerGroupBaseName} ", StringComparison.OrdinalIgnoreCase))
            {
                duplicateCount++;
            }
        }

        if (duplicateCount == 0)
        {
            return CircleSupportLayerGroupBaseName;
        }

        return $"{CircleSupportLayerGroupBaseName} {duplicateCount + 1}";
    }

    /// <summary>
    /// Builds a concise completion message including skipped projections when needed.
    /// </summary>
    private static string CreateCompletionMessage(int createdCount, int missedProjectionCount, int invalidSupportCount)
    {
        if (missedProjectionCount == 0 && invalidSupportCount == 0)
        {
            return $"Added {createdCount} circle supports.";
        }

        return $"Added {createdCount} circle supports; skipped {missedProjectionCount + invalidSupportCount}.";
    }
}
