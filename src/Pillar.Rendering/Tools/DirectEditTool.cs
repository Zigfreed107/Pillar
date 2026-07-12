// DirectEditTool.cs
// Owns selection-driven Direct Edit gizmos, constrained dragging, and transient support previews.
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using Pillar.Core.Tools;
using Pillar.Rendering.Math;
using Pillar.Rendering.Preview;
using Pillar.Rendering.Scene;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Carries one per-stem Direct Edit action produced by a multi-selection gesture.
/// </summary>
public sealed class DirectEditCommitAction
{
    private readonly List<Guid> _targetSupportIds;

    /// <summary>
    /// Creates one immutable commit action for a shared or individual stem.
    /// </summary>
    public DirectEditCommitAction(IReadOnlyList<Guid> targetSupportIds, SupportDirectEditSettings settings)
    {
        if (targetSupportIds == null || targetSupportIds.Count == 0)
        {
            throw new ArgumentException("A Direct Edit action requires at least one target support.", nameof(targetSupportIds));
        }

        _targetSupportIds = new List<Guid>(targetSupportIds);
        Settings = settings?.Clone() ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Gets support identities sharing this edited stem.
    /// </summary>
    public IReadOnlyList<Guid> TargetSupportIds
    {
        get { return _targetSupportIds; }
    }

    /// <summary>
    /// Gets the original and edited geometry for this stem.
    /// </summary>
    public SupportDirectEditSettings Settings { get; }
}

/// <summary>
/// Routes selection-driven support editing without mutating durable document state during a drag.
/// </summary>
public sealed class DirectEditTool : ITool
{
    private const float PositionTolerance = 0.001f;
    private const float DraggedLayerOpacity = 0.25f;
    private readonly Viewport3DX _viewport;
    private readonly CadDocument _document;
    private readonly SceneManager _scene;
    private readonly ProjectionService _projection;
    private readonly SelectionManager _selection;
    private readonly SelectTool _windowSelectionTool;
    private readonly List<SelectedStemGroup> _selectedStemGroups = new List<SelectedStemGroup>();
    private readonly List<Guid> _selectedEditableSupportIds = new List<Guid>();
    private readonly List<SupportEntity> _previewSupports = new List<SupportEntity>();
    private readonly List<DirectEditCommitAction> _dragStartActions = new List<DirectEditCommitAction>();
    private readonly List<DirectEditCommitAction> _previewActions = new List<DirectEditCommitAction>();
    private Guid? _activeSupportLayerGroupId;
    private SupportEntity? _gizmoSupport;
    private DirectEditGizmoHandleKind _dragHandle;
    private Vector2 _dragStartScreenPosition;
    private Vector3 _dragStartPlanePoint;
    private float _xyGizmoScale = 1.5f;
    private float _zGizmoScale = 3.0f;
    private bool _isCommitting;
    private bool _isWindowSelectionGesture;

    /// <summary>
    /// Creates the viewport controller and observes document selection for persistent gizmos.
    /// </summary>
    public DirectEditTool(
        Viewport3DX viewport,
        CadDocument document,
        SceneManager scene,
        ProjectionService projection,
        SelectTool windowSelectionTool)
    {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        _windowSelectionTool = windowSelectionTool ?? throw new ArgumentNullException(nameof(windowSelectionTool));
        _selection = scene.SelectionManager;
        _selection.SelectionChanged += Selection_SelectionChanged;
    }

    /// <summary>
    /// Raised when a completed gesture is ready to become one undoable set of modifier actions.
    /// </summary>
    public event Action<IReadOnlyList<DirectEditCommitAction>>? EditCommitted;

    /// <summary>
    /// Raised when selection or drag state changes user-facing guidance.
    /// </summary>
    public event Action<string>? StatusMessageRequested;

    /// <summary>
    /// Starts a Direct Edit session and immediately displays gizmos for the first selected support.
    /// </summary>
    public void Begin(Guid supportLayerGroupId, float xyGizmoScale, float zGizmoScale)
    {
        Cancel();
        _activeSupportLayerGroupId = supportLayerGroupId;
        _xyGizmoScale = IsPositiveFinite(xyGizmoScale) ? xyGizmoScale : 1.5f;
        _zGizmoScale = IsPositiveFinite(zGizmoScale) ? zGizmoScale : 3.0f;
        _windowSelectionTool.SetSelectionFilter(SelectionFilter.FromPredicate(
            (CadEntity entity) => IsWindowSelectableSupport(entity, supportLayerGroupId)));
        _windowSelectionTool.PruneSelectionToActiveFilter();
        RefreshSelectionState();
    }

    /// <summary>
    /// Starts a gizmo drag or updates the normal selection from a support stem click.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        if (!_activeSupportLayerGroupId.HasValue)
        {
            return;
        }

        if (_gizmoSupport != null
            && _scene.TryHitDirectEditGizmo(screenPosition, out DirectEditGizmoHandleKind handleKind))
        {
            BeginDrag(screenPosition, handleKind);
            return;
        }

        if (!_scene.TryHitSupportEntity(
                screenPosition,
                _activeSupportLayerGroupId.Value,
                out SupportEntity hitSupport,
                out Vector3 hitPosition))
        {
            _isWindowSelectionGesture = true;
            _windowSelectionTool.OnMouseDown(screenPosition);
            return;
        }

        if (hitSupport.Style.Kind != SupportStyleKind.Buttress && !IsStemHit(hitSupport, hitPosition))
        {
            StatusMessageRequested?.Invoke("Direct Edit: click the support stem to select it.");
            return;
        }

        SupportEntity? editableSupport = ResolveEditableSupport(hitSupport);

        if (editableSupport == null)
        {
            StatusMessageRequested?.Invoke("Direct Edit: brace members cannot be edited directly.");
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            _selection.ToggleSelection(editableSupport);
        }
        else
        {
            _selection.SelectSingle(editableSupport);
        }

        RefreshSelectionState();
    }

    /// <summary>
    /// Updates every selected stem preview from the first gizmo's current drag delta.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        if (_isWindowSelectionGesture)
        {
            _windowSelectionTool.OnMouseMove(screenPosition);
            return;
        }

        if (_dragHandle == DirectEditGizmoHandleKind.None)
        {
            return;
        }

        if (!TryBuildDraggedActions(screenPosition, _previewActions))
        {
            return;
        }

        RefreshPreview(_previewActions);
    }

    /// <summary>
    /// Commits all selected stems as one undoable gesture.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
        if (_isWindowSelectionGesture)
        {
            _isWindowSelectionGesture = false;
            _windowSelectionTool.OnMouseUp(screenPosition);
            return;
        }

        if (_dragHandle == DirectEditGizmoHandleKind.None || _previewActions.Count == 0)
        {
            return;
        }

        List<DirectEditCommitAction> committedActions = CloneActions(_previewActions);

        CommitEdit(committedActions);
    }

    /// <summary>
    /// Cancels the active gesture and removes all transient Direct Edit state without changing selection.
    /// </summary>
    public void Cancel()
    {
        if (_activeSupportLayerGroupId.HasValue)
        {
            _scene.SetSupportLayerGroupOpacity(_activeSupportLayerGroupId.Value, 1.0f);
        }

        _windowSelectionTool.Cancel();
        _windowSelectionTool.ResetSelectionFilter();
        _isWindowSelectionGesture = false;
        _activeSupportLayerGroupId = null;
        _gizmoSupport = null;
        _selectedStemGroups.Clear();
        _selectedEditableSupportIds.Clear();
        _dragStartActions.Clear();
        _previewActions.Clear();
        _previewSupports.Clear();
        _dragHandle = DirectEditGizmoHandleKind.None;
        _scene.HideDirectEditPreview();
    }

    /// <summary>
    /// Rebuilds selected stem groups whenever normal viewport selection changes.
    /// </summary>
    private void Selection_SelectionChanged(IEnumerable<Guid> addedIds, IEnumerable<Guid> removedIds)
    {
        _ = addedIds;
        _ = removedIds;

        if (!_activeSupportLayerGroupId.HasValue
            || _dragHandle != DirectEditGizmoHandleKind.None
            || _isCommitting)
        {
            return;
        }

        RefreshSelectionState();
    }

    /// <summary>
    /// Creates editable stem groups in document order and shows the first group's gizmo.
    /// </summary>
    private void RefreshSelectionState()
    {
        _selectedStemGroups.Clear();
        _selectedEditableSupportIds.Clear();
        _gizmoSupport = null;
        _scene.HideDirectEditPreview();

        if (!_activeSupportLayerGroupId.HasValue)
        {
            return;
        }

        IReadOnlyList<SupportEntity> supports = _document.GetSupportEntitiesForGroup(_activeSupportLayerGroupId.Value);
        HashSet<Guid> addedEditableIds = new HashSet<Guid>();

        for (int i = 0; i < supports.Count; i++)
        {
            SupportEntity selectedSupport = supports[i];

            if (!_selection.IsSelected(selectedSupport.Id))
            {
                continue;
            }

            SupportEntity? editableSupport = ResolveEditableSupport(selectedSupport);

            if (editableSupport == null || !addedEditableIds.Add(editableSupport.Id))
            {
                continue;
            }

            _selectedEditableSupportIds.Add(editableSupport.Id);
            AddSelectedStemGroup(editableSupport, supports);
        }

        if (_selectedStemGroups.Count == 0)
        {
            StatusMessageRequested?.Invoke("Direct Edit: click stems or drag a selection window.");
            return;
        }

        _gizmoSupport = _selectedStemGroups[0].Anchor;
        Vector3 stemTop = SupportDirectEditPlanner.CalculateStemTop(_gizmoSupport);
        ShowGizmo(_gizmoSupport.BasePosition, stemTop);
        int supportCount = _selectedEditableSupportIds.Count;
        StatusMessageRequested?.Invoke(
            supportCount == 1
                ? "Direct Edit: Ctrl-click more stems, or drag X, Y, XY, or Z."
                : $"Direct Edit: {supportCount} selected supports will move together.");
    }

    /// <summary>
    /// Adds one individual stem or one complete clustered shared stem, avoiding duplicates.
    /// </summary>
    private void AddSelectedStemGroup(SupportEntity support, IReadOnlyList<SupportEntity> allSupports)
    {
        if (support.Style.Kind == SupportStyleKind.Clustered)
        {
            for (int i = 0; i < _selectedStemGroups.Count; i++)
            {
                if (_selectedStemGroups[i].Anchor.Style.Kind == SupportStyleKind.Clustered
                    && SharesStem(_selectedStemGroups[i].Anchor, support))
                {
                    return;
                }
            }

            List<SupportEntity> sharedSupports = new List<SupportEntity>();

            for (int i = 0; i < allSupports.Count; i++)
            {
                if (allSupports[i].Style.Kind == SupportStyleKind.Clustered && SharesStem(allSupports[i], support))
                {
                    sharedSupports.Add(allSupports[i]);
                }
            }

            _selectedStemGroups.Add(new SelectedStemGroup(support, sharedSupports));
            return;
        }

        _selectedStemGroups.Add(new SelectedStemGroup(support, new List<SupportEntity> { support }));
    }

    /// <summary>
    /// Captures each selected stem's original geometry and starts the constrained gesture.
    /// </summary>
    private void BeginDrag(Vector2 screenPosition, DirectEditGizmoHandleKind handleKind)
    {
        if (_gizmoSupport == null || _selectedStemGroups.Count == 0)
        {
            return;
        }

        _dragStartActions.Clear();

        for (int i = 0; i < _selectedStemGroups.Count; i++)
        {
            SelectedStemGroup group = _selectedStemGroups[i];
            Vector3 stemTop = SupportDirectEditPlanner.CalculateStemTop(group.Anchor);
            SupportDirectEditSettings settings = new SupportDirectEditSettings(group.Anchor.BasePosition, stemTop.Z);
            _dragStartActions.Add(new DirectEditCommitAction(CreateTargetIds(group.Supports), settings));
        }

        _dragHandle = handleKind;
        _dragStartScreenPosition = screenPosition;
        _dragStartPlanePoint = _gizmoSupport.BasePosition;

        if (handleKind != DirectEditGizmoHandleKind.ZAxis)
        {
            _projection.TryGetWorldPointOnHorizontalPlane(
                screenPosition,
                _gizmoSupport.BasePosition.Z,
                out _dragStartPlanePoint);
        }

        _scene.SetSupportLayerGroupOpacity(_activeSupportLayerGroupId!.Value, DraggedLayerOpacity);
        _previewActions.Clear();
        _previewActions.AddRange(CloneActions(_dragStartActions));
        RefreshPreview(_previewActions);
    }

    /// <summary>
    /// Converts pointer displacement into one relative edit for every selected stem.
    /// </summary>
    private bool TryBuildDraggedActions(
        Vector2 screenPosition,
        List<DirectEditCommitAction> destination)
    {
        if (_gizmoSupport == null || _dragStartActions.Count == 0)
        {
            return false;
        }

        Vector3 xyDelta = Vector3.Zero;
        float zDelta = 0.0f;
        SupportDirectEditSettings firstSettings = _dragStartActions[0].Settings;

        if (_dragHandle == DirectEditGizmoHandleKind.ZAxis)
        {
            Vector3 stemTop = new Vector3(
                firstSettings.BasePosition.X,
                firstSettings.BasePosition.Y,
                firstSettings.StemTopZ);
            float gizmoLength = GetZGizmoLength(_gizmoSupport);
            Point originScreen = _viewport.Project(new Point3D(stemTop.X, stemTop.Y, stemTop.Z));
            Point endScreen = _viewport.Project(new Point3D(stemTop.X, stemTop.Y, stemTop.Z + gizmoLength));
            Vector2 screenAxis = new Vector2(
                (float)(endScreen.X - originScreen.X),
                (float)(endScreen.Y - originScreen.Y));
            float screenLength = screenAxis.Length();

            if (screenLength <= PositionTolerance)
            {
                return false;
            }

            float pixelOffset = Vector2.Dot(
                screenPosition - _dragStartScreenPosition,
                screenAxis / screenLength);
            zDelta = pixelOffset * gizmoLength / screenLength;
        }
        else
        {
            if (!_projection.TryGetWorldPointOnHorizontalPlane(
                    screenPosition,
                    firstSettings.BasePosition.Z,
                    out Vector3 dragPoint))
            {
                return false;
            }

            xyDelta = dragPoint - _dragStartPlanePoint;
            xyDelta.Z = 0.0f;

            if (_dragHandle == DirectEditGizmoHandleKind.XAxis)
            {
                xyDelta.Y = 0.0f;
            }
            else if (_dragHandle == DirectEditGizmoHandleKind.YAxis)
            {
                xyDelta.X = 0.0f;
            }
        }

        destination.Clear();

        for (int i = 0; i < _dragStartActions.Count; i++)
        {
            DirectEditCommitAction startAction = _dragStartActions[i];
            SupportDirectEditSettings start = startAction.Settings;
            Vector3 basePosition = start.BasePosition + xyDelta;
            float stemTopZ = MathF.Max(
                basePosition.Z + PositionTolerance,
                start.StemTopZ + zDelta);
            SupportDirectEditSettings edited = new SupportDirectEditSettings(
                basePosition,
                stemTopZ,
                start.OriginalBasePosition,
                start.OriginalStemTopZ);
            destination.Add(new DirectEditCommitAction(startAction.TargetSupportIds, edited));
        }

        return true;
    }

    /// <summary>
    /// Rebuilds every selected support and keeps the visible gizmo on the first stem.
    /// </summary>
    private void RefreshPreview(IReadOnlyList<DirectEditCommitAction> actions)
    {
        _previewSupports.Clear();

        for (int groupIndex = 0; groupIndex < actions.Count; groupIndex++)
        {
            SelectedStemGroup group = _selectedStemGroups[groupIndex];
            SupportDirectEditSettings settings = actions[groupIndex].Settings;

            for (int supportIndex = 0; supportIndex < group.Supports.Count; supportIndex++)
            {
                _previewSupports.Add(SupportDirectEditPlanner.RebuildSupport(
                    group.Supports[supportIndex],
                    settings));
            }
        }

        SupportLayerGroup? layerGroup = _document.FindSupportLayerGroupById(_activeSupportLayerGroupId!.Value);

        if (layerGroup != null)
        {
            _scene.ShowDirectEditSupportPreview(_previewSupports, layerGroup.Color);
        }

        SupportDirectEditSettings first = actions[0].Settings;
        ShowGizmo(
            first.BasePosition,
            new Vector3(first.BasePosition.X, first.BasePosition.Y, first.StemTopZ));
    }

    /// <summary>
    /// Applies a completed multi-stem edit and regenerates downstream modifiers immediately.
    /// </summary>
    private void CommitEdit(IReadOnlyList<DirectEditCommitAction> actions)
    {
        _isCommitting = true;

        try
        {
            EditCommitted?.Invoke(actions);
            EndDragPreview();
            RestoreEditableSelectionAfterCommit();
        }
        finally
        {
            _isCommitting = false;
        }

        RefreshSelectionState();
    }

    /// <summary>
    /// Restores normal rendering after a completed drag.
    /// </summary>
    private void EndDragPreview()
    {
        if (_activeSupportLayerGroupId.HasValue)
        {
            _scene.SetSupportLayerGroupOpacity(_activeSupportLayerGroupId.Value, 1.0f);
        }

        _dragHandle = DirectEditGizmoHandleKind.None;
        _dragStartActions.Clear();
        _previewActions.Clear();
        _previewSupports.Clear();
        _scene.HideDirectEditPreview();
    }

    /// <summary>
    /// Restores stable selected support identities after command output replacement.
    /// </summary>
    private void RestoreEditableSelectionAfterCommit()
    {
        if (!_activeSupportLayerGroupId.HasValue)
        {
            return;
        }

        HashSet<Guid> selectedIds = new HashSet<Guid>(_selectedEditableSupportIds);
        IReadOnlyList<SupportEntity> supports = _document.GetSupportEntitiesForGroup(_activeSupportLayerGroupId.Value);
        List<CadEntity> restoredSelection = new List<CadEntity>();

        for (int i = 0; i < supports.Count; i++)
        {
            if (selectedIds.Contains(supports[i].Id))
            {
                restoredSelection.Add(supports[i]);
            }
        }

        _selection.SelectMany(restoredSelection);
    }

    /// <summary>
    /// Maps a generated buttress back to its original support and rejects brace members.
    /// </summary>
    private SupportEntity? ResolveEditableSupport(SupportEntity hitSupport)
    {
        if (hitSupport.Style.Kind == SupportStyleKind.BraceMember)
        {
            return null;
        }

        if (hitSupport.Style.Kind != SupportStyleKind.Buttress)
        {
            return hitSupport;
        }

        IReadOnlyList<SupportEntity> supports = _document.GetSupportEntitiesForGroup(hitSupport.SupportLayerGroupId);

        for (int i = 0; i < supports.Count; i++)
        {
            SupportEntity candidate = supports[i];

            if ((candidate.Style.Kind == SupportStyleKind.Individual
                    || candidate.Style.Kind == SupportStyleKind.Clustered)
                && Vector3.DistanceSquared(
                    SupportDirectEditPlanner.CalculateStemTop(candidate),
                    hitSupport.TipPosition) <= PositionTolerance * PositionTolerance)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Restricts delegated window selection to editable supports in the active support layer.
    /// </summary>
    private static bool IsWindowSelectableSupport(CadEntity entity, Guid supportLayerGroupId)
    {
        return entity is SupportEntity support
            && support.SupportLayerGroupId == supportLayerGroupId
            && support.Style.Kind != SupportStyleKind.BraceMember;
    }
    /// <summary>
    /// Tests whether a support-body hit lies on its vertical stem.
    /// </summary>
    private static bool IsStemHit(SupportEntity support, Vector3 hitPosition)
    {
        Vector3 stemTop = SupportDirectEditPlanner.CalculateStemTop(support);
        SupportPartDimensions dimensions = SupportDimensionResolver.Resolve(support.Profile, support.Style);
        float maximumStemRadius = MathF.Max(
            dimensions.StemBottomDiameter,
            dimensions.StemTopDiameter) * 0.65f;
        float dx = hitPosition.X - support.BasePosition.X;
        float dy = hitPosition.Y - support.BasePosition.Y;
        float radialDistanceSquared = (dx * dx) + (dy * dy);
        float minimumStemZ = support.BasePosition.Z + (support.Profile.BaseHeight * 0.5f);
        return radialDistanceSquared <= maximumStemRadius * maximumStemRadius
            && hitPosition.Z >= minimumStemZ
            && hitPosition.Z <= stemTop.Z + maximumStemRadius;
    }

    /// <summary>
    /// Positions handles using the first selected support's resolved dimensions.
    /// </summary>
    private void ShowGizmo(Vector3 basePosition, Vector3 stemTop)
    {
        if (_gizmoSupport == null)
        {
            return;
        }

        SupportPartDimensions dimensions = SupportDimensionResolver.Resolve(
            _gizmoSupport.Profile,
            _gizmoSupport.Style);
        float baseDiameter = _gizmoSupport.Profile.BaseBottomRadius * 2.0f;
        _scene.ShowDirectEditGizmo(
            basePosition,
            stemTop,
            baseDiameter * _xyGizmoScale,
            dimensions.StemTopDiameter * _zGizmoScale);
    }

    /// <summary>
    /// Gets the configured Z arrow world-space length.
    /// </summary>
    private float GetZGizmoLength(SupportEntity support)
    {
        return SupportDimensionResolver.Resolve(
            support.Profile,
            support.Style).StemTopDiameter * _zGizmoScale;
    }

    /// <summary>
    /// Creates stable target identities for one selected stem group.
    /// </summary>
    private static IReadOnlyList<Guid> CreateTargetIds(IReadOnlyList<SupportEntity> supports)
    {
        List<Guid> ids = new List<Guid>(supports.Count);

        for (int i = 0; i < supports.Count; i++)
        {
            ids.Add(supports[i].Id);
        }

        return ids;
    }

    /// <summary>
    /// Clones transient action payloads before external ownership.
    /// </summary>
    private static List<DirectEditCommitAction> CloneActions(IReadOnlyList<DirectEditCommitAction> actions)
    {
        List<DirectEditCommitAction> clones = new List<DirectEditCommitAction>(actions.Count);

        for (int i = 0; i < actions.Count; i++)
        {
            clones.Add(new DirectEditCommitAction(actions[i].TargetSupportIds, actions[i].Settings));
        }

        return clones;
    }

    /// <summary>
    /// Tests whether two clustered support heads use the same shared stem.
    /// </summary>
    private static bool SharesStem(SupportEntity left, SupportEntity right)
    {
        return Vector3.DistanceSquared(left.BasePosition, right.BasePosition)
            <= PositionTolerance * PositionTolerance;
    }

    /// <summary>
    /// Checks a configured world-space handle length.
    /// </summary>
    private static bool IsPositiveFinite(float value)
    {
        return float.IsFinite(value) && value > 0.0f;
    }

    /// <summary>
    /// Stores one selected individual or shared clustered stem and its output supports.
    /// </summary>
    private sealed class SelectedStemGroup
    {
        /// <summary>
        /// Creates one selected stem group.
        /// </summary>
        public SelectedStemGroup(SupportEntity anchor, IReadOnlyList<SupportEntity> supports)
        {
            Anchor = anchor;
            Supports = supports;
        }

        /// <summary>
        /// Gets the support used to calculate this stem's geometry.
        /// </summary>
        public SupportEntity Anchor { get; }

        /// <summary>
        /// Gets every support head connected to this stem.
        /// </summary>
        public IReadOnlyList<SupportEntity> Supports { get; }
    }
}
