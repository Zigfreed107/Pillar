// RingSupportOperation.cs
// Creates a ring of individual support entities from three circumference picks while keeping preview state transient.
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
/// Places a new support layer group by distributing supports around a three-point ring and projecting them vertically onto the active mesh.
/// </summary>
public sealed class RingSupportOperation : IToolOperation
{
    private const string RingSupportLayerGroupBaseName = "Ring Supports";
    private const float EditingSupportGroupOpacity = 0.5f;
    private const float DefaultSupportGroupOpacity = 1.0f;

    private readonly CadDocument _document;
    private readonly ProjectionService _projectionService;
    private readonly SceneManager _scene;
    private readonly CadCommandRunner _commandRunner;
    private readonly Func<Guid?> _getSelectedModelEntityId;
    private readonly Func<float> _getSpacing;
    private readonly Action<string> _statusReporter;
    private readonly Action<bool> _precisionSelectCursorRequester;
    private readonly List<Vector3> _guidePreviewPoints = new List<Vector3>(RingSupportPattern.MaximumSupportCount);
    private readonly List<Vector3> _projectedPreviewPoints = new List<Vector3>(RingSupportPattern.MaximumSupportCount);

    private Guid? _targetModelEntityId;
    private Guid? _editingSupportLayerGroupId;
    private Vector3? _firstPoint;
    private Vector3? _secondPoint;
    private Vector3? _thirdPoint;
    private Vector3? _currentPreviewPoint;
    private RingSupportPointHandleKind _activePointHandle = RingSupportPointHandleKind.None;

    /// <summary>
    /// Creates the ring support operation.
    /// </summary>
    public RingSupportOperation(
        CadDocument document,
        ProjectionService projectionService,
        SceneManager scene,
        CadCommandRunner commandRunner,
        Func<Guid?> getSelectedModelEntityId,
        Func<float> getSpacing,
        Action<string> statusReporter,
        Action<bool> precisionSelectCursorRequester)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _getSelectedModelEntityId = getSelectedModelEntityId ?? throw new ArgumentNullException(nameof(getSelectedModelEntityId));
        _getSpacing = getSpacing ?? throw new ArgumentNullException(nameof(getSpacing));
        _statusReporter = statusReporter ?? throw new ArgumentNullException(nameof(statusReporter));
        _precisionSelectCursorRequester = precisionSelectCursorRequester ?? throw new ArgumentNullException(nameof(precisionSelectCursorRequester));
        _precisionSelectCursorRequester(true);
    }

    /// <summary>
    /// Captures circumference points or starts an edit drag after all three points exist.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _statusReporter("Select a model before placing ring supports.");
            return;
        }

        if (!_firstPoint.HasValue)
        {
            CaptureFirstPoint(screenPosition, selectedMesh);
            return;
        }

        if (_thirdPoint.HasValue)
        {
            RingSupportPointHandleKind hitHandle;

            if (_scene.TryHitRingSupportPointHandle(screenPosition, out hitHandle))
            {
                _activePointHandle = hitHandle;
                UpdatePointHandlePreview();
                _statusReporter("Drag the ring point handle to refine the ring, then click Apply.");
                return;
            }

            _statusReporter("Ring support preview is ready. Adjust spacing or click Apply.");
            return;
        }

        if (!_secondPoint.HasValue)
        {
            CaptureSecondPoint(screenPosition, selectedMesh);
            return;
        }

        CaptureThirdPoint(screenPosition, selectedMesh);
    }

    /// <summary>
    /// Updates the live ring preview while the user chooses point two or point three.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        if (_activePointHandle != RingSupportPointHandleKind.None)
        {
            MeshEntity? dragMesh = ResolvePlacementMesh();

            if (dragMesh != null)
            {
                UpdateDraggedPointHandle(screenPosition, dragMesh);
            }

            return;
        }

        if (!_firstPoint.HasValue || _thirdPoint.HasValue)
        {
            return;
        }

        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            return;
        }

        Vector3 hoverPoint;

        if (!TryGetPreviewRingPoint(screenPosition, _firstPoint.Value, out hoverPoint))
        {
            return;
        }

        Vector3 normalizedHoverPoint = NormalizePointToRingPlane(_firstPoint.Value, hoverPoint);
        _currentPreviewPoint = normalizedHoverPoint;

        if (!_secondPoint.HasValue)
        {
            UpdateTwoPointPreview(_firstPoint.Value, normalizedHoverPoint);
            return;
        }

        UpdateThreePointPreview(selectedMesh, _firstPoint.Value, _secondPoint.Value, normalizedHoverPoint, false);
    }

    /// <summary>
    /// Ends any active point-handle drag; commits are still controlled by the Apply button.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
        _ = screenPosition;
        bool wasDraggingPointHandle = _activePointHandle != RingSupportPointHandleKind.None;
        _activePointHandle = RingSupportPointHandleKind.None;
        UpdatePointHandlePreview();

        if (wasDraggingPointHandle)
        {
            RefreshPreview();
        }
    }

    /// <summary>
    /// Cancels the in-progress ring gesture and clears transient preview geometry.
    /// </summary>
    public void Cancel()
    {
        ClearEditingSupportGroupOpacity();
        _targetModelEntityId = null;
        _editingSupportLayerGroupId = null;
        _firstPoint = null;
        _secondPoint = null;
        _thirdPoint = null;
        _currentPreviewPoint = null;
        _activePointHandle = RingSupportPointHandleKind.None;
        _guidePreviewPoints.Clear();
        _projectedPreviewPoints.Clear();
        _scene.HideRingSupportPreview();
        _precisionSelectCursorRequester(false);
    }

    /// <summary>
    /// Loads an existing Ring Support group into the operation so its settings can be edited.
    /// </summary>
    public void EditExistingRingSupportGroup(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        RingSupportSettings? settings = supportLayerGroup.RingSupportSettings;

        if (settings == null)
        {
            _statusReporter("The selected support group was not created with the Ring Support tool.");
            return;
        }

        if (_editingSupportLayerGroupId.HasValue && _editingSupportLayerGroupId.Value != supportLayerGroup.Id)
        {
            ClearEditingSupportGroupOpacity();
        }

        _editingSupportLayerGroupId = supportLayerGroup.Id;
        _targetModelEntityId = supportLayerGroup.ModelEntityId;
        _firstPoint = settings.FirstPoint;
        _secondPoint = NormalizePointToRingPlane(settings.FirstPoint, settings.SecondPoint);
        _thirdPoint = NormalizePointToRingPlane(settings.FirstPoint, settings.ThirdPoint);
        _currentPreviewPoint = _thirdPoint;
        _activePointHandle = RingSupportPointHandleKind.None;
        _precisionSelectCursorRequester(false);
        _scene.SetSupportLayerGroupOpacity(supportLayerGroup.Id, EditingSupportGroupOpacity);
        RefreshPreview();
        _statusReporter("Ring support group loaded. Adjust spacing or click Apply.");
    }

    /// <summary>
    /// Rebuilds the transient ring preview from the current tool state and spacing settings.
    /// </summary>
    public void RefreshPreview()
    {
        if (!_firstPoint.HasValue)
        {
            return;
        }

        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _scene.HideRingSupportPreview();
            return;
        }

        if (_thirdPoint.HasValue && _secondPoint.HasValue)
        {
            UpdateThreePointPreview(selectedMesh, _firstPoint.Value, _secondPoint.Value, _thirdPoint.Value, true);
            return;
        }

        if (_secondPoint.HasValue && _currentPreviewPoint.HasValue)
        {
            UpdateThreePointPreview(selectedMesh, _firstPoint.Value, _secondPoint.Value, _currentPreviewPoint.Value, false);
            return;
        }

        if (_secondPoint.HasValue)
        {
            UpdatePointHandlePreview();
            return;
        }

        if (_currentPreviewPoint.HasValue)
        {
            UpdateTwoPointPreview(_firstPoint.Value, _currentPreviewPoint.Value);
            return;
        }

        UpdatePointHandlePreview();
    }

    /// <summary>
    /// Applies the previewed ring supports to either a new support group or the loaded generated support group.
    /// </summary>
    public bool Apply()
    {
        if (!_firstPoint.HasValue || !_secondPoint.HasValue || !_thirdPoint.HasValue)
        {
            _statusReporter("Pick three ring points before applying ring supports.");
            return false;
        }

        MeshEntity? selectedMesh = ResolvePlacementMesh();

        if (selectedMesh == null)
        {
            _statusReporter("The selected model could not be found.");
            return false;
        }

        RingSupportSettings settings = new RingSupportSettings(
            _firstPoint.Value,
            _secondPoint.Value,
            _thirdPoint.Value,
            _getSpacing());

        if (_editingSupportLayerGroupId.HasValue)
        {
            return UpdateExistingRingSupportGroup(selectedMesh, settings);
        }

        return CommitNewRingSupportGroup(selectedMesh, settings);
    }

    /// <summary>
    /// Captures the first circumference point from a selected mesh hit.
    /// </summary>
    private void CaptureFirstPoint(Vector2 screenPosition, MeshEntity selectedMesh)
    {
        Vector3 firstHitPosition;

        if (!TryGetHitOnSelectedMesh(screenPosition, selectedMesh, out firstHitPosition))
        {
            _statusReporter("Ring support points must be picked on the selected model.");
            return;
        }

        _targetModelEntityId = selectedMesh.Id;
        _firstPoint = firstHitPosition;
        _secondPoint = null;
        _thirdPoint = null;
        _currentPreviewPoint = null;
        _scene.HideRingSupportPreview();
        UpdatePointHandlePreview();
        _statusReporter("Move the cursor to preview the support ring, then click the second ring point.");
    }

    /// <summary>
    /// Captures the second circumference point on the locked construction plane.
    /// </summary>
    private void CaptureSecondPoint(Vector2 screenPosition, MeshEntity selectedMesh)
    {
        if (!_firstPoint.HasValue)
        {
            return;
        }

        Vector3 hitPosition;

        if (!TryGetRingPoint(screenPosition, selectedMesh, _firstPoint.Value, out hitPosition))
        {
            _statusReporter("The second ring support point could not be resolved on the construction plane.");
            return;
        }

        Vector3 normalizedSecondPoint = NormalizePointToRingPlane(_firstPoint.Value, hitPosition);
        _secondPoint = normalizedSecondPoint;
        _currentPreviewPoint = normalizedSecondPoint;

        if (UpdateTwoPointPreview(_firstPoint.Value, normalizedSecondPoint))
        {
            _statusReporter("Move the cursor to preview the support ring, then click the third ring point.");
            return;
        }

        _secondPoint = null;
        _currentPreviewPoint = null;
        _statusReporter("Ring support points must not overlap on the construction plane.");
    }

    /// <summary>
    /// Captures the third circumference point and enables projected support markers.
    /// </summary>
    private void CaptureThirdPoint(Vector2 screenPosition, MeshEntity selectedMesh)
    {
        if (!_firstPoint.HasValue || !_secondPoint.HasValue)
        {
            return;
        }

        Vector3 hitPosition;

        if (!TryGetRingPoint(screenPosition, selectedMesh, _firstPoint.Value, out hitPosition))
        {
            _statusReporter("The third ring support point could not be resolved on the construction plane.");
            return;
        }

        Vector3 normalizedThirdPoint = NormalizePointToRingPlane(_firstPoint.Value, hitPosition);
        _thirdPoint = normalizedThirdPoint;
        _currentPreviewPoint = normalizedThirdPoint;

        if (UpdateThreePointPreview(selectedMesh, _firstPoint.Value, _secondPoint.Value, normalizedThirdPoint, true))
        {
            _precisionSelectCursorRequester(false);
            _statusReporter("Ring support preview is ready. Adjust spacing or click Apply.");
            return;
        }

        _thirdPoint = null;
        _statusReporter("Ring support points must be distinct and not collinear.");
    }

    /// <summary>
    /// Creates a new Ring Support group from the accepted preview and records it as one undoable command.
    /// </summary>
    private bool CommitNewRingSupportGroup(MeshEntity selectedMesh, RingSupportSettings settings)
    {
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(selectedMesh.Id, CreateRingSupportLayerGroupName(selectedMesh.Id));
        supportLayerGroup.SetRingSupportSettings(settings);

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
            _statusReporter("No ring supports could be projected onto the selected model.");
            return false;
        }

        _commandRunner.Execute(new AddSupportsToNewGroupCommand(_document, supportLayerGroup, supportEntities, "Add Ring Supports"));
        _statusReporter(CreateCompletionMessage(supportEntities.Count, missedProjectionCount, invalidSupportCount));
        return true;
    }

    /// <summary>
    /// Updates the loaded Ring Support group as one undoable parametric regeneration.
    /// </summary>
    private bool UpdateExistingRingSupportGroup(MeshEntity selectedMesh, RingSupportSettings newSettings)
    {
        if (!_editingSupportLayerGroupId.HasValue)
        {
            return false;
        }

        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(_editingSupportLayerGroupId.Value);

        if (supportLayerGroup == null)
        {
            _statusReporter("The Ring Support group could not be found.");
            return false;
        }

        RingSupportSettings? oldSettings = supportLayerGroup.RingSupportSettings;

        if (oldSettings == null)
        {
            _statusReporter("The selected support group was not created with the Ring Support tool.");
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
            _statusReporter("No ring supports could be projected onto the selected model.");
            return false;
        }

        IReadOnlyList<SupportEntity> oldSupportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        _commandRunner.Execute(new UpdateRingSupportGroupCommand(
            _document,
            supportLayerGroup,
            oldSettings,
            oldSupportEntities,
            newSettings,
            newSupportEntities));

        _firstPoint = newSettings.FirstPoint;
        _secondPoint = newSettings.SecondPoint;
        _thirdPoint = newSettings.ThirdPoint;
        _currentPreviewPoint = newSettings.ThirdPoint;
        _statusReporter(CreateCompletionMessage(newSupportEntities.Count, missedProjectionCount, invalidSupportCount));
        return true;
    }

    /// <summary>
    /// Generates support entities for one Ring Support definition without mutating the document.
    /// </summary>
    private List<SupportEntity> CreateSupportEntities(
        MeshEntity selectedMesh,
        Guid supportLayerGroupId,
        RingSupportSettings settings,
        out int missedProjectionCount,
        out int invalidSupportCount)
    {
        Circle3D circle;

        if (!Circle3D.TryCreateFromThreePoints(settings.FirstPoint, settings.SecondPoint, settings.ThirdPoint, out circle))
        {
            missedProjectionCount = 0;
            invalidSupportCount = 0;
            return new List<SupportEntity>();
        }

        SupportProfile supportProfile = SupportDefaults.CreateProfile();
        int requestedSupportCount = RingSupportPattern.CalculateSupportCount(circle, settings.Spacing);
        List<SupportEntity> supportEntities = new List<SupportEntity>(requestedSupportCount);
        missedProjectionCount = 0;
        invalidSupportCount = 0;

        RingSupportPattern.FillGuidePoints(circle, settings.Spacing, _guidePreviewPoints);

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
    /// Updates lightweight projected support markers for the current ring preview.
    /// </summary>
    private void UpdateProjectedMarkerPreview(MeshEntity selectedMesh, Circle3D circle)
    {
        _guidePreviewPoints.Clear();
        _projectedPreviewPoints.Clear();
        RingSupportPattern.FillGuidePoints(circle, _getSpacing(), _guidePreviewPoints);

        for (int i = 0; i < _guidePreviewPoints.Count; i++)
        {
            Vector3 guidePoint = _guidePreviewPoints[i];
            Vector3 projectedPoint;

            if (MeshVerticalProjection.TryProjectToMesh(selectedMesh, guidePoint, out projectedPoint))
            {
                _projectedPreviewPoints.Add(projectedPoint);
            }
        }

        _scene.ShowRingSupportMarkers(_projectedPreviewPoints);
    }

    /// <summary>
    /// Updates the provisional two-point ring without projecting support markers.
    /// </summary>
    private bool UpdateTwoPointPreview(Vector3 firstPoint, Vector3 secondPoint)
    {
        Circle3D circle;
        Vector3 normalizedSecondPoint = NormalizePointToRingPlane(firstPoint, secondPoint);

        UpdatePointHandlePreview(firstPoint, normalizedSecondPoint, null);

        if (!Circle3D.TryCreateFromDiameter(firstPoint, normalizedSecondPoint, out circle))
        {
            _scene.HideRingSupportCircleAndMarkers();
            return false;
        }

        _scene.ShowRingSupportPreview(circle);
        _scene.HideRingSupportMarkers();
        return true;
    }

    /// <summary>
    /// Updates the three-point ring and optionally refreshes projected marker hits for stable interaction states.
    /// </summary>
    private bool UpdateThreePointPreview(
        MeshEntity selectedMesh,
        Vector3 firstPoint,
        Vector3 secondPoint,
        Vector3 thirdPoint,
        bool showProjectedMarkers)
    {
        Circle3D circle;
        Vector3 normalizedSecondPoint = NormalizePointToRingPlane(firstPoint, secondPoint);
        Vector3 normalizedThirdPoint = NormalizePointToRingPlane(firstPoint, thirdPoint);

        UpdatePointHandlePreview(firstPoint, normalizedSecondPoint, normalizedThirdPoint);

        if (!Circle3D.TryCreateFromThreePoints(firstPoint, normalizedSecondPoint, normalizedThirdPoint, out circle))
        {
            _scene.HideRingSupportCircleAndMarkers();
            return false;
        }

        _scene.ShowRingSupportPreview(circle);

        if (showProjectedMarkers)
        {
            UpdateProjectedMarkerPreview(selectedMesh, circle);
        }
        else
        {
            _scene.HideRingSupportMarkers();
        }

        return true;
    }

    /// <summary>
    /// Updates handle positions from the operation's current point state.
    /// </summary>
    private void UpdatePointHandlePreview()
    {
        if (!_firstPoint.HasValue)
        {
            return;
        }

        Vector3? secondPoint = _secondPoint ?? _currentPreviewPoint;
        Vector3? thirdPoint = _thirdPoint;

        if (_secondPoint.HasValue && !_thirdPoint.HasValue)
        {
            thirdPoint = _currentPreviewPoint;
        }

        UpdatePointHandlePreview(_firstPoint.Value, secondPoint, thirdPoint);
    }

    /// <summary>
    /// Updates handle positions using caller-supplied point values.
    /// </summary>
    private void UpdatePointHandlePreview(Vector3 firstPoint, Vector3? secondPoint, Vector3? thirdPoint)
    {
        Vector3? normalizedSecondPoint = secondPoint.HasValue
            ? NormalizePointToRingPlane(firstPoint, secondPoint.Value)
            : null;
        Vector3? normalizedThirdPoint = thirdPoint.HasValue
            ? NormalizePointToRingPlane(firstPoint, thirdPoint.Value)
            : null;

        _scene.ShowRingSupportPointHandles(
            firstPoint,
            normalizedSecondPoint,
            normalizedThirdPoint,
            _getSpacing());
    }

    /// <summary>
    /// Moves whichever point handle is active and refreshes the generated preview without committing document changes.
    /// </summary>
    private void UpdateDraggedPointHandle(Vector2 screenPosition, MeshEntity selectedMesh)
    {
        if (!_firstPoint.HasValue || !_secondPoint.HasValue || !_thirdPoint.HasValue)
        {
            return;
        }

        Vector3 dragPoint;

        if (!TryGetPreviewRingPoint(screenPosition, _firstPoint.Value, out dragPoint))
        {
            return;
        }

        Vector3 nextFirstPoint = _firstPoint.Value;
        Vector3 nextSecondPoint = _secondPoint.Value;
        Vector3 nextThirdPoint = _thirdPoint.Value;

        if (_activePointHandle == RingSupportPointHandleKind.FirstPoint)
        {
            nextFirstPoint = dragPoint;
            nextSecondPoint = NormalizePointToRingPlane(nextFirstPoint, nextSecondPoint);
            nextThirdPoint = NormalizePointToRingPlane(nextFirstPoint, nextThirdPoint);
        }
        else if (_activePointHandle == RingSupportPointHandleKind.SecondPoint)
        {
            nextSecondPoint = NormalizePointToRingPlane(nextFirstPoint, dragPoint);
        }
        else if (_activePointHandle == RingSupportPointHandleKind.ThirdPoint)
        {
            nextThirdPoint = NormalizePointToRingPlane(nextFirstPoint, dragPoint);
        }
        else
        {
            return;
        }

        _firstPoint = nextFirstPoint;
        _secondPoint = nextSecondPoint;
        _thirdPoint = nextThirdPoint;
        _currentPreviewPoint = nextThirdPoint;

        if (!UpdateThreePointPreview(selectedMesh, nextFirstPoint, nextSecondPoint, nextThirdPoint, false))
        {
            _statusReporter("Ring support points must be distinct and not collinear.");
            return;
        }
    }

    /// <summary>
    /// Finds the mesh entity that owns the current ring support placement.
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
    /// Resolves a clicked ring point from the selected mesh when possible, otherwise from the locked construction plane.
    /// </summary>
    private bool TryGetRingPoint(Vector2 screenPosition, MeshEntity selectedMesh, Vector3 firstPoint, out Vector3 ringPoint)
    {
        if (TryGetHitOnSelectedMesh(screenPosition, selectedMesh, out ringPoint))
        {
            ringPoint = NormalizePointToRingPlane(firstPoint, ringPoint);
            return true;
        }

        if (_projectionService.TryGetWorldPointOnHorizontalPlane(screenPosition, firstPoint.Z, out ringPoint))
        {
            ringPoint = NormalizePointToRingPlane(firstPoint, ringPoint);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves live preview movement on the construction plane so dense model hit testing does not run every mouse event.
    /// </summary>
    private bool TryGetPreviewRingPoint(Vector2 screenPosition, Vector3 firstPoint, out Vector3 ringPoint)
    {
        return _projectionService.TryGetWorldPointOnHorizontalPlane(screenPosition, firstPoint.Z, out ringPoint);
    }

    /// <summary>
    /// Keeps circumference points on the horizontal construction plane locked by the first pick.
    /// </summary>
    private static Vector3 NormalizePointToRingPlane(Vector3 firstPoint, Vector3 point)
    {
        return new Vector3(point.X, point.Y, firstPoint.Z);
    }

    /// <summary>
    /// Creates a stable user-facing name for a newly created ring-support group under one model.
    /// </summary>
    private string CreateRingSupportLayerGroupName(Guid modelEntityId)
    {
        int duplicateCount = 0;

        foreach (SupportLayerGroup existingSupportLayerGroup in _document.SupportLayerGroups)
        {
            if (existingSupportLayerGroup.ModelEntityId != modelEntityId)
            {
                continue;
            }

            if (string.Equals(existingSupportLayerGroup.Name, RingSupportLayerGroupBaseName, StringComparison.OrdinalIgnoreCase)
                || existingSupportLayerGroup.Name.StartsWith($"{RingSupportLayerGroupBaseName} ", StringComparison.OrdinalIgnoreCase))
            {
                duplicateCount++;
            }
        }

        if (duplicateCount == 0)
        {
            return RingSupportLayerGroupBaseName;
        }

        return $"{RingSupportLayerGroupBaseName} {duplicateCount + 1}";
    }

    /// <summary>
    /// Builds a concise completion message including skipped projections when needed.
    /// </summary>
    private static string CreateCompletionMessage(int createdCount, int missedProjectionCount, int invalidSupportCount)
    {
        if (missedProjectionCount == 0 && invalidSupportCount == 0)
        {
            return $"Added {createdCount} ring supports.";
        }

        return $"Added {createdCount} ring supports; skipped {missedProjectionCount + invalidSupportCount}.";
    }
}
