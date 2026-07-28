// TagTool.cs
// Owns pointer-driven closest-edge placement and transparent raft-tag preview state.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Tags;
using Pillar.Core.Tools;
using Pillar.Geometry.Tags;
using Pillar.Rendering.Math;
using Pillar.Rendering.Scene;
using System;
using System.Numerics;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Routes one tag placement gesture without mutating durable document state.
/// </summary>
public sealed class TagTool : ITool
{
    private const float MovingPreviewOpacity = 0.45f;
    private readonly ProjectionService _projection;
    private readonly SceneManager _scene;
    private TagPlacementPlanner? _placementPlanner;
    private TagSettings? _settings;
    private TagTextMeshData? _textMesh;
    private SupportLayerColor _color;
    private TagPlacement? _currentPlacement;

    /// <summary>
    /// Creates the viewport controller used only while the options panel is in placement mode.
    /// </summary>
    public TagTool(ProjectionService projection, SceneManager scene)
    {
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    /// <summary>
    /// Raised when the primary click locks the current closest-edge placement.
    /// </summary>
    public event Action<TagPlacement>? PlacementAccepted;

    /// <summary>
    /// Starts or restarts moving placement with one immutable settings snapshot.
    /// </summary>
    public void BeginPlacement(
        RaftEntity raft,
        TagSettings settings,
        TagTextMeshData textMesh,
        SupportLayerColor color)
    {
        if (raft == null)
        {
            throw new ArgumentNullException(nameof(raft));
        }

        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (textMesh == null)
        {
            throw new ArgumentNullException(nameof(textMesh));
        }

        Cancel();
        _placementPlanner = new TagPlacementPlanner(raft);
        _settings = settings;
        _textMesh = textMesh;
        _color = color;
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

        TagPlacement acceptedPlacement = _currentPlacement.Value;
        Cancel();
        PlacementAccepted?.Invoke(acceptedPlacement);
    }

    /// <summary>
    /// Slides the transparent preview to the raft boundary point closest to the pointer.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        if (_placementPlanner == null || _settings == null || _textMesh == null)
        {
            return;
        }

        if (!_projection.TryGetWorldPointOnHorizontalPlane(
                screenPosition,
                _placementPlanner.PlacementZ,
                out Vector3 worldPoint)
            || !_placementPlanner.TryFindClosestPlacement(
                new Vector2(worldPoint.X, worldPoint.Y),
                out TagPlacement placement))
        {
            return;
        }

        _currentPlacement = placement;
        TagMeshData mesh = TagMeshBuilder.Build(_settings, _textMesh, placement);
        _scene.ShowTagPreview(mesh, _color, MovingPreviewOpacity);
    }

    /// <summary>
    /// Tag placement commits on mouse-down, so mouse-up has no additional behavior.
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
        _settings = null;
        _textMesh = null;
        _currentPlacement = null;
        _scene.HideTagPreview();
    }
}
