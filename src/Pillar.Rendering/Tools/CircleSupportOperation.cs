// CircleSupportOperation.cs
// Creates a ring of individual support entities from two model-surface diameter picks while keeping preview state transient.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using Pillar.Core.Tools;
using Pillar.Geometry.Primitives;
using Pillar.Geometry.Supports;
using Pillar.Rendering.Math;
using Pillar.Rendering.Preview;
using Pillar.Rendering.Scene;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Places a new support layer group by distributing supports around a diameter-defined circle and projecting them vertically onto the active mesh.
/// </summary>
public sealed class CircleSupportOperation : IToolOperation
{
    private const string CircleSupportLayerGroupBaseName = "Circle Supports";
    private const float EditingSupportGroupOpacity = 0.5f;
    private const float DefaultSupportGroupOpacity = 1.0f;

    private readonly CadDocument _document;
    private readonly ProjectionService _projectionService;
    private readonly SceneManager _scene;
    private readonly CadCommandRunner _commandRunner;
    private readonly Func<Guid?> _getSelectedModelEntityId;
    private readonly Func<float> _getSpacing;
    private readonly Action<string> _statusReporter;
    private readonly List<Vector3> _guidePreviewPoints = new List<Vector3>(CircleSupportPattern.MaximumSupportCount);
    private readonly List<Vector3> _projectedPreviewPoints = new List<Vector3>(CircleSupportPattern.MaximumSupportCount);

    private Guid? _targetModelEntityId;
    private Guid? _editingSupportLayerGroupId;
    private Vector3? _firstPoint;
    private Vector3? _secondPoint;
    private Vector3? _currentPreviewSecondPoint;
    private CircleSupportDiameterHandleKind _activeDiameterHandle = CircleSupportDiameterHandleKind.None;

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
    /// Captures the first diameter endpoint or accepts the second endpoint for the editable preview.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _statusReporter("Select a model before placing circle supports.");
            return;
        }

        if (!_firstPoint.HasValue)
        {
            Vector3 firstHitPosition;

            if (!TryGetHitOnSelectedMesh(screenPosition, selectedMesh, out firstHitPosition))
            {
                _statusReporter("Circle support points must be picked on the selected model.");
                return;
            }

            _targetModelEntityId = selectedMesh.Id;
            _firstPoint = firstHitPosition;
            _secondPoint = null;
            _currentPreviewSecondPoint = null;
            _scene.HideCircleSupportPreview();
            UpdateDiameterHandlePreview();
            _statusReporter("Move the cursor to preview the support ring, then click the second diameter point.");
            return;
        }

        if (_secondPoint.HasValue)
        {
            CircleSupportDiameterHandleKind hitHandle;

            if (_scene.TryHitCircleSupportDiameterHandle(screenPosition, out hitHandle))
            {
                _activeDiameterHandle = hitHandle;
                UpdateDiameterHandlePreview();
                _statusReporter("Drag the diameter handle to refine the circle, then click Apply.");
                return;
            }

            _statusReporter("Circle support preview is ready. Adjust spacing or click Apply.");
            return;
        }

        Vector3 hitPosition;

        if (!TryGetDiameterPoint(screenPosition, selectedMesh, _firstPoint.Value, out hitPosition))
        {
            _statusReporter("The second circle support point could not be resolved on the construction plane.");
            return;
        }

        Vector3 normalizedSecondPoint = NormalizeSecondPointToCirclePlane(_firstPoint.Value, hitPosition);
        _secondPoint = normalizedSecondPoint;
        _currentPreviewSecondPoint = normalizedSecondPoint;

        if (UpdateCirclePreview(selectedMesh, _firstPoint.Value, normalizedSecondPoint))
        {
            _statusReporter("Circle support preview is ready. Adjust spacing or click Apply.");
            return;
        }

        _secondPoint = null;
        _statusReporter("Circle support diameter points must not overlap in the XY plane.");
    }

    /// <summary>
    /// Updates the live diameter circle preview while the user chooses the second point.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        if (_activeDiameterHandle != CircleSupportDiameterHandleKind.None)
        {
            MeshEntity? dragMesh = ResolvePlacementMesh();

            if (dragMesh != null)
            {
                UpdateDraggedDiameterHandle(screenPosition, dragMesh);
            }

            return;
        }

        if (!_firstPoint.HasValue || _secondPoint.HasValue)
        {
            return;
        }

        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            return;
        }

        Vector3 hoverPoint;

        if (!TryGetPreviewDiameterPoint(screenPosition, _firstPoint.Value, out hoverPoint))
        {
            return;
        }

        Vector3 normalizedHoverPoint = NormalizeSecondPointToCirclePlane(_firstPoint.Value, hoverPoint);
        _currentPreviewSecondPoint = normalizedHoverPoint;
        UpdateCirclePreview(selectedMesh, _firstPoint.Value, normalizedHoverPoint, false);
    }

    /// <summary>
    /// Ends any active diameter-handle drag; commits are still controlled by the Apply button.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
        _ = screenPosition;
        bool wasDraggingDiameterHandle = _activeDiameterHandle != CircleSupportDiameterHandleKind.None;
        _activeDiameterHandle = CircleSupportDiameterHandleKind.None;
        UpdateDiameterHandlePreview();

        if (wasDraggingDiameterHandle)
        {
            RefreshPreview();
        }
    }

    /// <summary>
    /// Cancels the in-progress circle gesture and clears transient preview geometry.
    /// </summary>
    public void Cancel()
    {
        ClearEditingSupportGroupOpacity();
        _targetModelEntityId = null;
        _editingSupportLayerGroupId = null;
        _firstPoint = null;
        _secondPoint = null;
        _currentPreviewSecondPoint = null;
        _activeDiameterHandle = CircleSupportDiameterHandleKind.None;
        _guidePreviewPoints.Clear();
        _projectedPreviewPoints.Clear();
        _scene.HideCircleSupportPreview();
    }

    /// <summary>
    /// Loads an existing Circle Support group into the operation so its settings can be edited.
    /// </summary>
    public void EditExistingCircleSupportGroup(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        CircleSupportSettings? settings = supportLayerGroup.CircleSupportSettings;

        if (settings == null)
        {
            _statusReporter("The selected support group was not created with the Circle Support tool.");
            return;
        }

        if (_editingSupportLayerGroupId.HasValue && _editingSupportLayerGroupId.Value != supportLayerGroup.Id)
        {
            ClearEditingSupportGroupOpacity();
        }

        _editingSupportLayerGroupId = supportLayerGroup.Id;
        _targetModelEntityId = supportLayerGroup.ModelEntityId;
        _firstPoint = settings.FirstDiameterPoint;
        _secondPoint = NormalizeSecondPointToCirclePlane(settings.FirstDiameterPoint, settings.SecondDiameterPoint);
        _currentPreviewSecondPoint = _secondPoint;
        _activeDiameterHandle = CircleSupportDiameterHandleKind.None;
        _scene.SetSupportLayerGroupOpacity(supportLayerGroup.Id, EditingSupportGroupOpacity);
        RefreshPreview();
        _statusReporter("Circle support group loaded. Adjust spacing or click Apply.");
    }

    /// <summary>
    /// Rebuilds the transient circle preview from the current tool state and spacing settings.
    /// </summary>
    public void RefreshPreview()
    {
        if (!_firstPoint.HasValue)
        {
            return;
        }

        if (!_currentPreviewSecondPoint.HasValue)
        {
            UpdateDiameterHandlePreview();
            return;
        }

        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _scene.HideCircleSupportPreview();
            return;
        }

        UpdateCirclePreview(selectedMesh, _firstPoint.Value, _currentPreviewSecondPoint.Value, true);
    }

    /// <summary>
    /// Applies the previewed circle supports to either a new support group or the loaded generated support group.
    /// </summary>
    public bool Apply()
    {
        if (!_firstPoint.HasValue || !_secondPoint.HasValue)
        {
            _statusReporter("Pick two diameter points before applying circle supports.");
            return false;
        }

        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _statusReporter("The selected model could not be found.");
            return false;
        }

        CircleSupportSettings settings = new CircleSupportSettings(_firstPoint.Value, _secondPoint.Value, _getSpacing());

        if (_editingSupportLayerGroupId.HasValue)
        {
            return UpdateExistingCircleSupportGroup(selectedMesh, settings);
        }

        return CommitNewCircleSupportGroup(selectedMesh, settings);
    }

    /// <summary>
    /// Creates a new Circle Support group from the accepted preview and records it as one undoable command.
    /// </summary>
    private bool CommitNewCircleSupportGroup(MeshEntity selectedMesh, CircleSupportSettings settings)
    {
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(selectedMesh.Id, CreateCircleSupportLayerGroupName(selectedMesh.Id));
        supportLayerGroup.SetCircleSupportSettings(settings);

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
            _statusReporter("No circle supports could be projected onto the selected model.");
            return false;
        }

        _commandRunner.Execute(new AddSupportsToNewGroupCommand(_document, supportLayerGroup, supportEntities, "Add Circle Supports"));
        _statusReporter(CreateCompletionMessage(supportEntities.Count, missedProjectionCount, invalidSupportCount));
        return true;
    }

    /// <summary>
    /// Updates the loaded Circle Support group as one undoable parametric regeneration.
    /// </summary>
    private bool UpdateExistingCircleSupportGroup(MeshEntity selectedMesh, CircleSupportSettings newSettings)
    {
        if (!_editingSupportLayerGroupId.HasValue)
        {
            return false;
        }

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(_editingSupportLayerGroupId.Value);

        if (supportLayerGroup == null)
        {
            _statusReporter("The Circle Support group could not be found.");
            return false;
        }

        CircleSupportSettings? oldSettings = supportLayerGroup.CircleSupportSettings;

        if (oldSettings == null)
        {
            _statusReporter("The selected support group was not created with the Circle Support tool.");
            return false;
        }

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
            _statusReporter("No circle supports could be projected onto the selected model.");
            return false;
        }

        IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        _commandRunner.Execute(new UpdateCircleSupportGroupCommand(
            _document,
            supportLayerGroup,
            oldSettings,
            oldSupportEntities,
            newSettings,
            newSupportEntities));

        _firstPoint = newSettings.FirstDiameterPoint;
        _secondPoint = newSettings.SecondDiameterPoint;
        _currentPreviewSecondPoint = newSettings.SecondDiameterPoint;
        _statusReporter(CreateCompletionMessage(newSupportEntities.Count, missedProjectionCount, invalidSupportCount));
        return true;
    }

    /// <summary>
    /// Generates support entities for one Circle Support definition without mutating the document.
    /// </summary>
    private List<SupportEntity> CreateSupportEntities(
        MeshEntity selectedMesh,
        Guid supportLayerGroupId,
        CircleSupportSettings settings,
        out int missedProjectionCount,
        out int invalidSupportCount)
    {
        Circle3D circle;

        if (!Circle3D.TryCreateHorizontalFromDiameter(settings.FirstDiameterPoint, settings.SecondDiameterPoint, out circle))
        {
            missedProjectionCount = 0;
            invalidSupportCount = 0;
            return new List<SupportEntity>();
        }

        SupportProfile supportProfile = SupportDefaults.CreateProfile();
        int requestedSupportCount = CircleSupportPattern.CalculateSupportCount(circle, settings.Spacing);
        List<SupportEntity> supportEntities = new List<SupportEntity>(requestedSupportCount);
        missedProjectionCount = 0;
        invalidSupportCount = 0;

        CircleSupportPattern.FillGuidePoints(circle, settings.Spacing, _guidePreviewPoints);

        for (int i = 0; i < _guidePreviewPoints.Count; i++)
        {
            Vector3 guidePoint = _guidePreviewPoints[i];
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
                    supportLayerGroupId,
                    projectedPoint,
                    basePosition,
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
    /// Updates lightweight projected support markers for the current circle preview.
    /// </summary>
    private void UpdateProjectedMarkerPreview(MeshEntity selectedMesh, Circle3D circle)
    {
        _guidePreviewPoints.Clear();
        _projectedPreviewPoints.Clear();
        CircleSupportPattern.FillGuidePoints(circle, _getSpacing(), _guidePreviewPoints);

        for (int i = 0; i < _guidePreviewPoints.Count; i++)
        {
            Vector3 guidePoint = _guidePreviewPoints[i];
            Vector3 projectedPoint;

            if (MeshVerticalProjection.TryProjectToMesh(selectedMesh, guidePoint, out projectedPoint))
            {
                _projectedPreviewPoints.Add(projectedPoint);
            }
        }

        _scene.ShowCircleSupportMarkers(_projectedPreviewPoints);
    }

    /// <summary>
    /// Updates the horizontal circle and support marker preview for the current diameter endpoints.
    /// </summary>
    private bool UpdateCirclePreview(MeshEntity selectedMesh, Vector3 firstPoint, Vector3 secondPoint)
    {
        return UpdateCirclePreview(selectedMesh, firstPoint, secondPoint, true);
    }

    /// <summary>
    /// Updates the horizontal circle and optionally refreshes projected marker hits for stable interaction states.
    /// </summary>
    private bool UpdateCirclePreview(MeshEntity selectedMesh, Vector3 firstPoint, Vector3 secondPoint, bool showProjectedMarkers)
    {
        Circle3D circle;
        Vector3 normalizedSecondPoint = NormalizeSecondPointToCirclePlane(firstPoint, secondPoint);

        UpdateDiameterHandlePreview(firstPoint, normalizedSecondPoint);

        if (!Circle3D.TryCreateHorizontalFromDiameter(firstPoint, normalizedSecondPoint, out circle))
        {
            _scene.HideCircleSupportCircleAndMarkers();
            return false;
        }

        _scene.ShowCircleSupportPreview(circle);

        if (showProjectedMarkers)
        {
            UpdateProjectedMarkerPreview(selectedMesh, circle);
        }
        else
        {
            _scene.HideCircleSupportMarkers();
        }

        return true;
    }

    /// <summary>
    /// Updates handle positions from the operation's current point state.
    /// </summary>
    private void UpdateDiameterHandlePreview()
    {
        if (!_firstPoint.HasValue)
        {
            return;
        }

        UpdateDiameterHandlePreview(_firstPoint.Value, _currentPreviewSecondPoint);
    }

    /// <summary>
    /// Updates handle positions using caller-supplied endpoint values.
    /// </summary>
    private void UpdateDiameterHandlePreview(Vector3 firstPoint, Vector3? secondPoint)
    {
        Vector3? normalizedSecondPoint = secondPoint.HasValue
            ? NormalizeSecondPointToCirclePlane(firstPoint, secondPoint.Value)
            : null;

        _scene.ShowCircleSupportDiameterHandles(
            firstPoint,
            normalizedSecondPoint,
            _getSpacing(),
            _activeDiameterHandle);
    }

    /// <summary>
    /// Moves whichever diameter handle is active and refreshes the generated preview without committing document changes.
    /// </summary>
    private void UpdateDraggedDiameterHandle(Vector2 screenPosition, MeshEntity selectedMesh)
    {
        if (!_firstPoint.HasValue || !_secondPoint.HasValue)
        {
            return;
        }

        Vector3 dragPoint;

        if (!TryGetPreviewDiameterPoint(screenPosition, _firstPoint.Value, out dragPoint))
        {
            return;
        }

        Vector3 nextFirstPoint = _firstPoint.Value;
        Vector3 nextSecondPoint = _secondPoint.Value;

        if (_activeDiameterHandle == CircleSupportDiameterHandleKind.FirstPoint)
        {
            nextFirstPoint = dragPoint;
            nextSecondPoint = NormalizeSecondPointToCirclePlane(nextFirstPoint, nextSecondPoint);
        }
        else if (_activeDiameterHandle == CircleSupportDiameterHandleKind.SecondPoint)
        {
            nextSecondPoint = NormalizeSecondPointToCirclePlane(nextFirstPoint, dragPoint);
        }
        else
        {
            return;
        }

        _firstPoint = nextFirstPoint;
        _secondPoint = nextSecondPoint;
        _currentPreviewSecondPoint = nextSecondPoint;

        if (!UpdateCirclePreview(selectedMesh, nextFirstPoint, nextSecondPoint, false))
        {
            _statusReporter("Circle support diameter points must not overlap in the XY plane.");
            return;
        }
    }

    /// <summary>
    /// Finds the mesh entity that owns the current circle support placement.
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
    /// Resolves the moving diameter endpoint from the selected mesh when possible, otherwise from a horizontal construction plane.
    /// </summary>
    private bool TryGetDiameterPoint(Vector2 screenPosition, MeshEntity selectedMesh, Vector3 firstPoint, out Vector3 diameterPoint)
    {
        if (TryGetHitOnSelectedMesh(screenPosition, selectedMesh, out diameterPoint))
        {
            return true;
        }

        return _projectionService.TryGetWorldPointOnHorizontalPlane(screenPosition, firstPoint.Z, out diameterPoint);
    }

    /// <summary>
    /// Resolves live preview movement on the construction plane so dense model hit testing does not run every mouse event.
    /// </summary>
    private bool TryGetPreviewDiameterPoint(Vector2 screenPosition, Vector3 firstPoint, out Vector3 diameterPoint)
    {
        return _projectionService.TryGetWorldPointOnHorizontalPlane(screenPosition, firstPoint.Z, out diameterPoint);
    }

    /// <summary>
    /// Keeps the second endpoint on the horizontal circle plane because only its XY offset defines the circle.
    /// </summary>
    private static Vector3 NormalizeSecondPointToCirclePlane(Vector3 firstPoint, Vector3 secondPoint)
    {
        return new Vector3(secondPoint.X, secondPoint.Y, firstPoint.Z);
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
