// RaftTextTool.cs
// Owns pointer-driven interior raft text placement and transparent preview state.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.RaftTexts;
using Pillar.Core.Tools;
using Pillar.Geometry.RaftTexts;
using Pillar.Rendering.Math;
using Pillar.Rendering.Scene;
using System;
using System.Numerics;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Routes one raft text placement gesture without mutating durable document state.
/// </summary>
public sealed class RaftTextTool : ITool
{
    private const float MovingPreviewOpacity = 0.45f;
    private readonly ProjectionService _projection;
    private readonly SceneManager _scene;
    private RaftTextPlacementPlanner? _placementPlanner;
    private RaftTextMeshData? _localMesh;
    private Vector3? _currentPlacement;

    /// <summary>
    /// Creates the viewport controller used while the options panel is in placement mode.
    /// </summary>
    public RaftTextTool(ProjectionService projection, SceneManager scene)
    {
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    /// <summary>
    /// Raised when the primary click locks the current placement.
    /// </summary>
    public event Action<Vector3>? PlacementAccepted;

    /// <summary>
    /// Starts or restarts moving placement with one immutable settings snapshot.
    /// </summary>
    public void BeginPlacement(
        RaftEntity raft,
        RaftTextSettings settings,
        RaftTextMeshData localMesh,
        SupportLayerColor color)
    {
        if (raft == null) throw new ArgumentNullException(nameof(raft));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (localMesh == null) throw new ArgumentNullException(nameof(localMesh));

        Cancel();
        _placementPlanner = new RaftTextPlacementPlanner(raft, localMesh, settings.BorderOffset);
        _localMesh = localMesh;
        _scene.PrepareMovingRaftTextPreview(localMesh, color, MovingPreviewOpacity);
    }

    /// <summary>
    /// Locks the current preview on primary mouse press.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        _ = screenPosition;

        if (!_currentPlacement.HasValue)
        {
            return;
        }

        Vector3 acceptedPlacement = _currentPlacement.Value;
        Cancel();
        PlacementAccepted?.Invoke(acceptedPlacement);
    }

    /// <summary>
    /// Slides the transparent preview to the closest valid interior point beneath the pointer.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        if (_placementPlanner == null || _localMesh == null)
        {
            return;
        }

        if (!_projection.TryGetWorldPointOnHorizontalPlane(
                screenPosition,
                _placementPlanner.ProjectionPlaneZ,
                out Vector3 worldPoint)
            || !_placementPlanner.TryFindPlacement(
                new Vector2(worldPoint.X, worldPoint.Y),
                out Vector3 placement))
        {
            _currentPlacement = null;
            _scene.HidePreparedRaftTextPreview();
            return;
        }

        _currentPlacement = placement;
        _scene.MovePreparedRaftTextPreview(placement);
    }

    /// <summary>
    /// Raft text placement commits on mouse-down, so mouse-up has no additional behavior.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
        _ = screenPosition;
    }

    /// <summary>
    /// Drops all transient placement state and preview geometry.
    /// </summary>
    public void Cancel()
    {
        _placementPlanner = null;
        _localMesh = null;
        _currentPlacement = null;
        _scene.HideRaftTextPreview();
    }
}
