// SceneManager.cs
// Orchestrates document-to-visual mapping and incremental rendering state updates for the viewport.
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Selection;
using Pillar.Geometry.Analysis;
using Pillar.Geometry.Supports;
using Pillar.Rendering.BackgroundGrid;
using Pillar.Rendering.EntityRenderers;
using Pillar.Rendering.Preview;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Numerics;
using System.Windows;
using System.Windows.Media.Media3D;
using MediaColor = System.Windows.Media.Color;

namespace Pillar.Rendering.Scene;

/// <summary>
/// Manages the 3D scene for the CAD application.
/// </summary>
public class SceneManager
{
    private const string MeshHighlightPostEffect = "highlight";
    private const float FullyOpaqueSupportOpacity = 1.0f;
    private const float FaceLineSelectionSampleSpacingPixels = 2.0f;
    private static readonly MediaColor DefaultSelectionOutlineColor = MediaColor.FromRgb(255, 215, 0);

    private readonly Viewport3DX _viewport;
    private readonly CadDocument _document;
    private readonly Dictionary<GroupModel3D, CadEntity> _visualToEntity = new Dictionary<GroupModel3D, CadEntity>();
    private readonly Dictionary<CadEntity, GroupModel3D> _entityToVisual = new Dictionary<CadEntity, GroupModel3D>();
    private readonly Dictionary<Element3D, GroupModel3D> _elementToVisual = new Dictionary<Element3D, GroupModel3D>();
    private readonly Dictionary<Guid, float> _supportLayerGroupOpacityOverrides = new Dictionary<Guid, float>();
    private readonly List<SupportEntity> _supportGroupQueryBuffer = new List<SupportEntity>(256);
    private readonly GroupModel3D _entityRoot = new GroupModel3D();
    private readonly GroupModel3D _backgroundGridRoot = new GroupModel3D();
    private readonly GroupModel3D _previewRoot = new GroupModel3D();
    private readonly BackgroundGridRenderer _backgroundGridRenderer;
    private readonly SnapMarkerRenderer _snapMarkerRenderer;
    private readonly PreviewLineRenderer _previewLineRenderer;
    private readonly RingSupportPreviewRenderer _ringSupportPreviewRenderer;
    private readonly LineSupportPreviewRenderer _lineSupportPreviewRenderer;
    private readonly ContourSupportPreviewRenderer _contourSupportPreviewRenderer;
    private readonly AreaSupportPreviewRenderer _areaSupportPreviewRenderer;
    private readonly ScaleOriginPreviewRenderer _scaleOriginPreviewRenderer;
    private readonly ScaledCursorPreviewRenderer _scaledCursorPreviewRenderer;
    private readonly SelectionManager _selectionManager;
    private readonly int _supportSides;
    private readonly PrintableVolumeDefinition _printableVolumeDefinition;
    private readonly PhongMaterial _defaultMeshMaterial = MeshRenderer.CreateDefaultMaterial();
    private readonly MediaColor _selectionOutlineColor;
    private readonly PhongMaterial _highlightMaterial;
    private readonly Dictionary<Guid, List<int>> _faceSelectionByMeshId = new Dictionary<Guid, List<int>>();
    private bool _isFaceAngleHighlightEnabled;
    private double _faceAngleThresholdDegrees = 45.0;
    private Color4 _faceAngleHighlightColor = new Color4(1.0f, 0.0f, 0.0f, 0.65f);
    private bool _isModelClipRangeConfigured;
    private float _modelClipLowerZ;
    private float _modelClipUpperZ;
    private const float ModelClipRangeTolerance = 0.001f;

    /// <summary>
    /// Gets the domain selection manager used by selection tools.
    /// </summary>
    public SelectionManager SelectionManager
    {
        get { return _selectionManager; }
    }

    /// <summary>
    /// Gets the rendered background-grid bounds used for startup camera framing.
    /// </summary>
    public Rect3D BackgroundGridBounds
    {
        get { return _backgroundGridRenderer.RenderBounds; }
    }

    /// <summary>
    /// Creates the scene manager and subscribes to document changes.
    /// </summary>
    public SceneManager(Viewport3DX viewport, CadDocument document)
        : this(viewport, document, 16, DefaultSelectionOutlineColor)
    {
    }

    /// <summary>
    /// Creates the scene manager with an explicit support side count and subscribes to document changes.
    /// </summary>
    public SceneManager(Viewport3DX viewport, CadDocument document, int supportSides)
        : this(viewport, document, supportSides, DefaultSelectionOutlineColor)
    {
    }

    /// <summary>
    /// Creates the scene manager with explicit rendering settings and subscribes to document changes.
    /// </summary>
    public SceneManager(Viewport3DX viewport, CadDocument document, int supportSides, MediaColor selectionOutlineColor)
        : this(viewport, document, supportSides, selectionOutlineColor, BackgroundGridDefinition.Default)
    {
    }

    /// <summary>
    /// Creates the scene manager with explicit rendering settings and subscribes to document changes.
    /// </summary>
    public SceneManager(
        Viewport3DX viewport,
        CadDocument document,
        int supportSides,
        MediaColor selectionOutlineColor,
        BackgroundGridDefinition backgroundGridDefinition)
        : this(viewport, document, supportSides, selectionOutlineColor, backgroundGridDefinition, MeshRenderer.CreateDefaultMaterial())
    {
    }

    /// <summary>
    /// Creates the scene manager with explicit rendering settings and subscribes to document changes.
    /// </summary>
    public SceneManager(
        Viewport3DX viewport,
        CadDocument document,
        int supportSides,
        MediaColor selectionOutlineColor,
        BackgroundGridDefinition backgroundGridDefinition,
        PhongMaterial defaultMeshMaterial)
    {
        _viewport = viewport;
        _document = document;
        _supportSides = supportSides;
        _selectionOutlineColor = selectionOutlineColor;
        _defaultMeshMaterial = defaultMeshMaterial ?? throw new ArgumentNullException(nameof(defaultMeshMaterial));
        _highlightMaterial = CreateSelectionMaterial(_selectionOutlineColor);
        _selectionManager = new SelectionManager(_document);
        BackgroundGridDefinition resolvedBackgroundGridDefinition = backgroundGridDefinition ?? throw new ArgumentNullException(nameof(backgroundGridDefinition));
        _printableVolumeDefinition = resolvedBackgroundGridDefinition.PrintableVolume;

        _viewport.Items.Add(new PostEffectMeshBorderHighlight
        {
            EffectName = MeshHighlightPostEffect,
            Color = _selectionOutlineColor
        });

        _backgroundGridRenderer = new BackgroundGridRenderer(_backgroundGridRoot, resolvedBackgroundGridDefinition);
        _viewport.Items.Add(_backgroundGridRoot);
        _viewport.Items.Add(_entityRoot);

        _previewLineRenderer = new PreviewLineRenderer(_previewRoot);
        _ringSupportPreviewRenderer = new RingSupportPreviewRenderer(_previewRoot);
        _lineSupportPreviewRenderer = new LineSupportPreviewRenderer(_previewRoot);
        _contourSupportPreviewRenderer = new ContourSupportPreviewRenderer(_previewRoot);
        _areaSupportPreviewRenderer = new AreaSupportPreviewRenderer(_previewRoot);
        _scaleOriginPreviewRenderer = new ScaleOriginPreviewRenderer(_previewRoot);
        _scaledCursorPreviewRenderer = new ScaledCursorPreviewRenderer(_previewRoot);
        _snapMarkerRenderer = new SnapMarkerRenderer(_previewRoot);
        _viewport.Items.Add(_previewRoot);

        _selectionManager.SelectionChanged += OnSelectionChanged;
        _document.EntitiesChanged += OnEntitiesChanged;
        _document.SupportLayerGroupsChanged += OnSupportLayerGroupsChanged;

        SubscribeToExistingEntities();
        SubscribeToExistingSupportLayerGroups();
        RenderAll();
    }

    /// <summary>
    /// Updates scene visuals when entities are added or removed from the document.
    /// </summary>
    private void OnEntitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (CadEntity entity in e.NewItems)
            {
                entity.PropertyChanged += Entity_PropertyChanged;
                InsertEntity(entity);
            }
        }

        if (e.OldItems != null)
        {
            foreach (CadEntity entity in e.OldItems)
            {
                entity.PropertyChanged -= Entity_PropertyChanged;
                RemoveEntity(entity);
            }
        }
    }

    /// <summary>
    /// Subscribes and unsubscribes when support groups are added or removed.
    /// </summary>
    private void OnSupportLayerGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (SupportLayerGroup supportLayerGroup in e.NewItems)
            {
                supportLayerGroup.PropertyChanged += SupportLayerGroup_PropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (SupportLayerGroup supportLayerGroup in e.OldItems)
            {
                supportLayerGroup.PropertyChanged -= SupportLayerGroup_PropertyChanged;
                _supportLayerGroupOpacityOverrides.Remove(supportLayerGroup.Id);
            }
        }
    }

    /// <summary>
    /// Applies live support recoloring when one support layer group's properties change.
    /// </summary>
    private void SupportLayerGroup_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SupportLayerGroup supportLayerGroup)
        {
            return;
        }

        if (!string.Equals(e.PropertyName, nameof(SupportLayerGroup.Color), StringComparison.Ordinal))
        {
            return;
        }

        ApplySupportLayerGroupColorToEntities(supportLayerGroup);
    }

    /// <summary>
    /// Updates selection highlight state for entities whose selection changed.
    /// </summary>
    private void OnSelectionChanged(IEnumerable<Guid> addedIds, IEnumerable<Guid> removedIds)
    {
        foreach (Guid id in removedIds)
        {
            CadEntity? entity = FindEntityById(id);

            if (entity != null && _entityToVisual.TryGetValue(entity, out GroupModel3D? visual))
            {
                ApplyDefaultMaterial(visual);
            }
        }

        foreach (Guid id in addedIds)
        {
            CadEntity? entity = FindEntityById(id);

            if (entity != null && _entityToVisual.TryGetValue(entity, out GroupModel3D? visual))
            {
                ApplyHighlightMaterial(visual);
            }
        }
    }

    /// <summary>
    /// Applies incremental visual updates when an entity changes without leaving the document.
    /// </summary>
    private void Entity_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MeshEntity meshEntity)
        {
            return;
        }

        if (!_entityToVisual.TryGetValue(meshEntity, out GroupModel3D? visual))
        {
            return;
        }

        if (string.Equals(e.PropertyName, nameof(MeshEntity.ImportPlacementTransform), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(MeshEntity.UserTransform), StringComparison.Ordinal))
        {
            MeshRenderer.ApplyTransform(visual, meshEntity);
            ApplyFaceAngleHighlight(visual, meshEntity);
        }
    }

    /// <summary>
    /// Rebuilds the scene from the current document contents.
    /// </summary>
    private void RenderAll()
    {
        _entityRoot.Children.Clear();
        _entityToVisual.Clear();
        _visualToEntity.Clear();
        _elementToVisual.Clear();

        foreach (CadEntity entity in _document.Entities)
        {
            GroupModel3D? visual = CreateVisual(entity);

            if (visual == null)
            {
                continue;
            }

            _entityToVisual[entity] = visual;
            _visualToEntity[visual] = entity;
            _entityRoot.Children.Add(visual);

            foreach (Element3D element in visual.Children)
            {
                _elementToVisual[element] = visual;
            }

            ApplyDefaultMaterial(visual);
            ApplyModelClipRangeIfConfigured(visual);
            if (entity is MeshEntity mesh)
            {
                ApplyFaceAngleHighlight(visual, mesh);
            }
        }
    }

    /// <summary>
    /// Finds the domain entity that owns one viewport element.
    /// </summary>
    public CadEntity? GetEntityFromVisual(Element3D element)
    {
        if (_elementToVisual.TryGetValue(element, out GroupModel3D? visual))
        {
            _visualToEntity.TryGetValue(visual, out CadEntity? entity);
            return entity;
        }

        return null;
    }

    /// <summary>
    /// Shows the transient preview line while an interaction is in progress.
    /// </summary>
    public void ShowPreviewLine(Vector3 start, Vector3 end)
    {
        _previewLineRenderer.Show(start, end);
    }

    /// <summary>
    /// Hides the transient preview line.
    /// </summary>
    public void HidePreviewLine()
    {
        _previewLineRenderer.Hide();
    }

    /// <summary>
    /// Shows the transient Ring Support guide circle.
    /// </summary>
    public void ShowRingSupportPreview(Pillar.Geometry.Primitives.Circle3D circle)
    {
        _ringSupportPreviewRenderer.ShowCircle(circle);
    }

    /// <summary>
    /// Shows transient projected support marker positions for the Ring Support tool.
    /// </summary>
    public void ShowRingSupportMarkers(IReadOnlyList<Vector3> markerPositions)
    {
        _ringSupportPreviewRenderer.ShowMarkers(markerPositions);
    }

    /// <summary>
    /// Hides transient projected support marker positions while leaving the circle and point handles visible.
    /// </summary>
    public void HideRingSupportMarkers()
    {
        _ringSupportPreviewRenderer.HideMarkers();
    }

    /// <summary>
    /// Shows transient point handles for the Ring Support tool.
    /// </summary>
    public void ShowRingSupportPointHandles(
        Vector3 firstPoint,
        Vector3? secondPoint,
        Vector3? thirdPoint,
        float handleDiameter)
    {
        _ringSupportPreviewRenderer.ShowPointHandles(firstPoint, secondPoint, thirdPoint, handleDiameter);
    }

    /// <summary>
    /// Hit-tests only the transient Ring Support point handles.
    /// </summary>
    public bool TryHitRingSupportPointHandle(Vector2 screenPosition, out RingSupportPointHandleKind handleKind)
    {
        IList<HitTestResult> hits = _viewport.FindHits(new Point(screenPosition.X, screenPosition.Y));

        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i].ModelHit is Element3D hitModel
                && _ringSupportPreviewRenderer.TryGetPointHandleKind(hitModel, out handleKind))
            {
                return true;
            }
        }

        handleKind = RingSupportPointHandleKind.None;
        return false;
    }

    /// <summary>
    /// Hit-tests support visuals and accepts only supports in the requested support layer group.
    /// </summary>
    public bool TryHitSupportEntity(Vector2 screenPosition, Guid supportLayerGroupId, out SupportEntity supportEntity)
    {
        IList<HitTestResult> hits = _viewport.FindHits(new Point(screenPosition.X, screenPosition.Y));

        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i].ModelHit is Element3D hitModel
                && GetEntityFromVisual(hitModel) is SupportEntity hitSupportEntity
                && hitSupportEntity.SupportLayerGroupId == supportLayerGroupId)
            {
                supportEntity = hitSupportEntity;
                return true;
            }
        }

        supportEntity = null!;
        return false;
    }

    /// <summary>
    /// Adds support entities from one support group that satisfy the supplied screen-space selection rectangle.
    /// </summary>
    public void FillSupportEntitiesSelectedByWindow(
        Guid supportLayerGroupId,
        Rect selectionRect,
        bool selectsCrossingEntities,
        List<CadEntity> selectedEntities)
    {
        if (selectedEntities == null)
        {
            throw new ArgumentNullException(nameof(selectedEntities));
        }

        _supportGroupQueryBuffer.Clear();
        _document.FillSupportEntitiesForGroup(supportLayerGroupId, _supportGroupQueryBuffer);

        try
        {
            for (int i = 0; i < _supportGroupQueryBuffer.Count; i++)
            {
                SupportEntity supportEntity = _supportGroupQueryBuffer[i];
                Rect projectedBounds;

                if (!TryGetProjectedBounds(supportEntity, out projectedBounds))
                {
                    continue;
                }

                if (selectsCrossingEntities && selectionRect.IntersectsWith(projectedBounds))
                {
                    selectedEntities.Add(supportEntity);
                    continue;
                }

                if (!selectsCrossingEntities && selectionRect.Contains(projectedBounds))
                {
                    selectedEntities.Add(supportEntity);
                }
            }
        }
        finally
        {
            _supportGroupQueryBuffer.Clear();
        }
    }

    /// <summary>
    /// Hides all transient Ring Support preview geometry.
    /// </summary>
    public void HideRingSupportPreview()
    {
        _ringSupportPreviewRenderer.Hide();
    }

    /// <summary>
    /// Shows transient Area Support boundary, marker, and optional spacing previews.
    /// </summary>
    public void ShowAreaSupportPreview(AreaSupportResult areaSupportResult, float spacing, bool showSupportSpacing)
    {
        _areaSupportPreviewRenderer.Show(areaSupportResult, spacing, showSupportSpacing);
    }

    /// <summary>
    /// Hides all transient Area Support preview geometry.
    /// </summary>
    public void HideAreaSupportPreview()
    {
        _areaSupportPreviewRenderer.Hide();
    }

    /// <summary>
    /// Shows the visual-only Transform Scale origin marker at the supplied world-space position.
    /// </summary>
    public void ShowScaleOriginPreview(Vector3 origin, float radius)
    {
        _scaleOriginPreviewRenderer.Show(origin, radius);
    }

    /// <summary>
    /// Hides the Transform Scale origin marker.
    /// </summary>
    public void HideScaleOriginPreview()
    {
        _scaleOriginPreviewRenderer.Hide();
    }

    /// <summary>
    /// Shows the visual-only scaled cursor circle at the supplied build-plate position.
    /// </summary>
    public void ShowScaledCursorPreview(Vector3 center, float diameter, MediaColor color)
    {
        _scaledCursorPreviewRenderer.Show(center, diameter, color);
    }

    /// <summary>
    /// Hides the scaled cursor guide when the toolbar toggle is off or the cursor leaves the viewport.
    /// </summary>
    public void HideScaledCursorPreview()
    {
        _scaledCursorPreviewRenderer.Hide();
    }

    /// <summary>
    /// Hides the generated Ring Support circle and support markers while keeping point handles visible.
    /// </summary>
    public void HideRingSupportCircleAndMarkers()
    {
        _ringSupportPreviewRenderer.HideCircleAndMarkers();
    }

    /// <summary>
    /// Shows the transient Line Support polyline preview.
    /// </summary>
    public void ShowLineSupportPreview(IReadOnlyList<Vector3> points, Vector3? hoverPoint)
    {
        _lineSupportPreviewRenderer.ShowPolyline(points, hoverPoint);
    }

    /// <summary>
    /// Shows transient projected support marker positions for the Line Support tool.
    /// </summary>
    public void ShowLineSupportMarkers(IReadOnlyList<Vector3> markerPositions)
    {
        _lineSupportPreviewRenderer.ShowMarkers(markerPositions);
    }

    /// <summary>
    /// Hides transient projected Line Support marker positions while leaving the polyline visible.
    /// </summary>
    public void HideLineSupportMarkers()
    {
        _lineSupportPreviewRenderer.HideMarkers();
    }

    /// <summary>
    /// Shows the Line Support cursor spacing guide.
    /// </summary>
    public void ShowLineSupportSpacingGuide(Vector3 center, float diameter)
    {
        _lineSupportPreviewRenderer.ShowSpacingGuide(center, diameter);
    }

    /// <summary>
    /// Hides the Line Support cursor spacing guide.
    /// </summary>
    public void HideLineSupportSpacingGuide()
    {
        _lineSupportPreviewRenderer.HideSpacingGuide();
    }

    /// <summary>
    /// Shows editable Line Support polyline point handles.
    /// </summary>
    public void ShowLineSupportPointHandles(IReadOnlyList<Vector3> points, float handleDiameter)
    {
        _lineSupportPreviewRenderer.ShowPointHandles(points, handleDiameter);
    }

    /// <summary>
    /// Hides editable Line Support polyline point handles.
    /// </summary>
    public void HideLineSupportPointHandles()
    {
        _lineSupportPreviewRenderer.HidePointHandles();
    }

    /// <summary>
    /// Hit-tests only the transient Line Support point handles.
    /// </summary>
    public bool TryHitLineSupportPointHandle(Vector2 screenPosition, out int pointIndex)
    {
        IList<HitTestResult> hits = _viewport.FindHits(new Point(screenPosition.X, screenPosition.Y));

        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i].ModelHit is Element3D hitModel
                && _lineSupportPreviewRenderer.TryGetPointHandleIndex(hitModel, out pointIndex))
            {
                return true;
            }
        }

        pointIndex = -1;
        return false;
    }

    /// <summary>
    /// Hides all transient Line Support preview geometry.
    /// </summary>
    public void HideLineSupportPreview()
    {
        _lineSupportPreviewRenderer.Hide();
    }

    /// <summary>
    /// Shows the transient Contour Support contour and marker preview.
    /// </summary>
    public void ShowContourSupportPreview(ContourSupportResult contourResult)
    {
        _contourSupportPreviewRenderer.Show(contourResult);
    }

    /// <summary>
    /// Hides all transient Contour Support preview geometry.
    /// </summary>
    public void HideContourSupportPreview()
    {
        _contourSupportPreviewRenderer.Hide();
    }

    /// <summary>
    /// Applies a temporary opacity override to all support visuals in one support group.
    /// </summary>
    public void SetSupportLayerGroupOpacity(Guid supportLayerGroupId, float opacity)
    {
        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(supportLayerGroupId);

        if (supportLayerGroup == null)
        {
            return;
        }

        float normalizedOpacity = global::System.Math.Clamp(opacity, 0.0f, 1.0f);

        if (normalizedOpacity >= FullyOpaqueSupportOpacity)
        {
            _supportLayerGroupOpacityOverrides.Remove(supportLayerGroupId);
        }
        else
        {
            _supportLayerGroupOpacityOverrides[supportLayerGroupId] = normalizedOpacity;
        }

        ApplySupportLayerGroupMaterial(supportLayerGroup, normalizedOpacity);
    }

    /// <summary>
    /// Shows the snapping marker at the supplied world position.
    /// </summary>
    public void ShowSnappingPoint(Vector3 position)
    {
        _snapMarkerRenderer.Show(position);
    }

    /// <summary>
    /// Hides the snapping marker.
    /// </summary>
    public void HideSnappingPoint()
    {
        _snapMarkerRenderer.HideSnappingPoint();
    }

    /// <summary>
    /// Applies the horizontal-face highlight settings to all rendered mesh visuals.
    /// </summary>
    public void ConfigureFaceAngleHighlight(bool isEnabled, double thresholdDegrees, Color4 highlightColor)
    {
        _isFaceAngleHighlightEnabled = isEnabled;
        _faceAngleThresholdDegrees = thresholdDegrees;
        _faceAngleHighlightColor = highlightColor;

        foreach (KeyValuePair<CadEntity, GroupModel3D> visualPair in _entityToVisual)
        {
            if (visualPair.Key is MeshEntity mesh)
            {
                ApplyFaceAngleHighlight(visualPair.Value, mesh);
            }
        }
    }

    /// <summary>
    /// Updates the visual-only selected-face overlay from a temporary face selection set.
    /// </summary>
    public void ConfigureFaceSelection(IReadOnlyCollection<FaceSelectionKey> selectedFaces, Color4 selectionColor)
    {
        if (selectedFaces == null)
        {
            throw new ArgumentNullException(nameof(selectedFaces));
        }

        _faceSelectionByMeshId.Clear();

        foreach (FaceSelectionKey selectedFace in selectedFaces)
        {
            if (!_faceSelectionByMeshId.TryGetValue(selectedFace.MeshEntityId, out List<int>? triangleIndices))
            {
                triangleIndices = new List<int>();
                _faceSelectionByMeshId.Add(selectedFace.MeshEntityId, triangleIndices);
            }

            triangleIndices.Add(selectedFace.TriangleIndex);
        }

        foreach (KeyValuePair<CadEntity, GroupModel3D> visualPair in _entityToVisual)
        {
            if (visualPair.Key is not MeshEntity mesh)
            {
                continue;
            }

            if (!_faceSelectionByMeshId.TryGetValue(mesh.Id, out List<int>? triangleIndices))
            {
                triangleIndices = new List<int>();
            }

            MeshRenderer.ApplyFaceSelection(visualPair.Value, mesh, triangleIndices, selectionColor);
            MeshGeometryModel3D? faceSelectionModel = MeshRenderer.GetFaceSelectionModel(visualPair.Value);

            if (faceSelectionModel != null)
            {
                _elementToVisual[faceSelectionModel] = visualPair.Value;
            }

            ApplyModelClipRangeIfConfigured(visualPair.Value);
        }
    }

    /// <summary>
    /// Clears the visual-only selected-face overlay from every mesh.
    /// </summary>
    public void ClearFaceSelection()
    {
        ConfigureFaceSelection(Array.Empty<FaceSelectionKey>(), new Color4(1.0f, 1.0f, 0.0f, 0.65f));
    }

    /// <summary>
    /// Hit-tests the viewport and returns the mesh face under the screen position.
    /// </summary>
    public bool TryHitMeshFace(Vector2 screenPosition, out MeshEntity mesh, out int triangleIndex)
    {
        IList<HitTestResult> hits = _viewport.FindHits(new Point(screenPosition.X, screenPosition.Y));

        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i].ModelHit is not Element3D hitModel || GetEntityFromVisual(hitModel) is not MeshEntity hitMesh)
            {
                continue;
            }

            Vector3 hitPosition = new Vector3(hits[i].PointHit.X, hits[i].PointHit.Y, hits[i].PointHit.Z);

            if (FaceSetSelectionAnalyzer.TryFindContainingTriangleIndex(hitMesh, hitPosition, out triangleIndex))
            {
                mesh = hitMesh;
                return true;
            }
        }

        mesh = null!;
        triangleIndex = -1;
        return false;
    }

    /// <summary>
    /// Samples a screen-space line and fills only the visible mesh faces hit from the current camera viewpoint.
    /// </summary>
    public void FillVisibleMeshFacesCrossedByScreenLine(
        Vector2 screenStart,
        Vector2 screenEnd,
        ICollection<FaceSelectionKey> selectedFaces)
    {
        if (selectedFaces == null)
        {
            throw new ArgumentNullException(nameof(selectedFaces));
        }

        float segmentLength = Vector2.Distance(screenStart, screenEnd);

        if (segmentLength <= float.Epsilon)
        {
            AddVisibleMeshFaceAtScreenPoint(screenStart, selectedFaces);
            return;
        }

        int segmentCount = global::System.Math.Max(1, (int)MathF.Ceiling(segmentLength / FaceLineSelectionSampleSpacingPixels));

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            Vector2 samplePoint = Vector2.Lerp(screenStart, screenEnd, t);
            AddVisibleMeshFaceAtScreenPoint(samplePoint, selectedFaces);
        }
    }

    /// <summary>
    /// Projects a world-space point into viewport pixel coordinates for screen-space face selection tools.
    /// </summary>
    public bool TryProjectWorldPointToScreen(Vector3 worldPoint, out Point screenPoint)
    {
        return TryProjectWorldPoint(worldPoint, out screenPoint);
    }

    /// <summary>
    /// Adds the front-most visible mesh face under one screen point to the supplied collection.
    /// </summary>
    private void AddVisibleMeshFaceAtScreenPoint(Vector2 screenPosition, ICollection<FaceSelectionKey> selectedFaces)
    {
        if (!TryHitMeshFace(screenPosition, out MeshEntity mesh, out int triangleIndex))
        {
            return;
        }

        selectedFaces.Add(new FaceSelectionKey(mesh.Id, triangleIndex));
    }

    /// <summary>
    /// Applies render-only horizontal clipping planes to mesh-based entity visuals.
    /// </summary>
    public void ConfigureModelClipRange(float lowerZ, float upperZ)
    {
        _modelClipLowerZ = global::System.Math.Min(lowerZ, upperZ);
        _modelClipUpperZ = global::System.Math.Max(lowerZ, upperZ);
        _isModelClipRangeConfigured = true;
        bool isClippingActive = IsModelClipRangeActive();

        foreach (GroupModel3D visual in _entityToVisual.Values)
        {
            MeshRenderer.ApplyClipRange(visual, _modelClipLowerZ, _modelClipUpperZ, isClippingActive);
        }
    }

    /// <summary>
    /// Creates one renderable visual for one supported entity type.
    /// </summary>
    private GroupModel3D? CreateVisual(CadEntity entity)
    {
        if (entity is LineEntity line)
        {
            return LineRenderer.Create(line, _selectionOutlineColor);
        }

        if (entity is MeshEntity mesh)
        {
            return MeshRenderer.Create(mesh, _defaultMeshMaterial);
        }

        if (entity is SupportEntity support)
        {
            return SupportRenderer.Create(support, GetSupportLayerGroupColor(support.SupportLayerGroupId), _supportSides);
        }

        return null;
    }

    /// <summary>
    /// Inserts one new entity visual into the scene incrementally.
    /// </summary>
    private void InsertEntity(CadEntity entity)
    {
        GroupModel3D? visual = CreateVisual(entity);

        if (visual == null)
        {
            return;
        }

        foreach (Element3D element in visual.Children)
        {
            _elementToVisual.Add(element, visual);
        }

        _entityRoot.Children.Add(visual);
        _entityToVisual[entity] = visual;
        _visualToEntity[visual] = entity;
        ApplyDefaultMaterial(visual);
        ApplyModelClipRangeIfConfigured(visual);
        if (entity is MeshEntity mesh)
        {
            ApplyFaceAngleHighlight(visual, mesh);
        }
    }

    /// <summary>
    /// Removes one entity visual from the scene incrementally.
    /// </summary>
    private void RemoveEntity(CadEntity entity)
    {
        if (!_entityToVisual.TryGetValue(entity, out GroupModel3D? visualToRemove))
        {
            return;
        }

        foreach (Element3D element in visualToRemove.Children)
        {
            _elementToVisual.Remove(element);
        }

        _entityRoot.Children.Remove(visualToRemove);
        _entityToVisual.Remove(entity);
        _visualToEntity.Remove(visualToRemove);
    }

    /// <summary>
    /// Applies the entity's non-highlighted appearance.
    /// </summary>
    private void ApplyDefaultMaterial(Element3D visual)
    {
        if (visual is not GroupModel3D group)
        {
            if (visual is MeshGeometryModel3D mesh)
            {
                mesh.Material = _defaultMeshMaterial;
                mesh.IsSelected = false;
                mesh.PostEffects = string.Empty;
            }

            return;
        }

        CadEntity? entity = GetEntityFromGroup(group);
        Element3D? lineOverlay = LineRenderer.GetSelectionOverlay(group);

        if (lineOverlay != null)
        {
            lineOverlay.Visibility = System.Windows.Visibility.Hidden;
            return;
        }

        if (entity is SupportEntity supportEntity)
        {
            PhongMaterial supportMaterial = SupportRenderer.CreateMaterial(
                GetSupportLayerGroupColor(supportEntity.SupportLayerGroupId),
                GetSupportLayerGroupOpacity(supportEntity.SupportLayerGroupId));
            MeshRenderer.ApplyToSelectableMeshModels(group, (MeshGeometryModel3D meshModel) =>
            {
                meshModel.PostEffects = string.Empty;
                meshModel.IsSelected = false;
                meshModel.Material = supportMaterial;
            });
            return;
        }

        MeshRenderer.ApplyToSelectableMeshModels(group, (MeshGeometryModel3D meshModel) =>
        {
            meshModel.PostEffects = string.Empty;
            meshModel.IsSelected = false;
            meshModel.Material = _defaultMeshMaterial;
        });
    }

    /// <summary>
    /// Applies the highlighted appearance for a selected entity.
    /// </summary>
    private void ApplyHighlightMaterial(Element3D visual)
    {
        if (visual is not GroupModel3D group)
        {
            if (visual is MeshGeometryModel3D mesh)
            {
                mesh.Material = _highlightMaterial;
                mesh.IsSelected = true;
                mesh.PostEffects = MeshHighlightPostEffect;
            }

            return;
        }

        Element3D? lineOverlay = LineRenderer.GetSelectionOverlay(group);

        if (lineOverlay != null)
        {
            lineOverlay.Visibility = System.Windows.Visibility.Visible;
            return;
        }

        MeshRenderer.ApplyToSelectableMeshModels(group, (MeshGeometryModel3D meshModel) =>
        {
            meshModel.IsSelected = true;
            meshModel.PostEffects = MeshHighlightPostEffect;
        });
    }

    /// <summary>
    /// Applies one support layer group's color to all supports that belong to it.
    /// </summary>
    private void ApplySupportLayerGroupColorToEntities(SupportLayerGroup supportLayerGroup)
    {
        ApplySupportLayerGroupMaterial(supportLayerGroup, GetSupportLayerGroupOpacity(supportLayerGroup.Id));
    }

    /// <summary>
    /// Applies one material opacity to all supports that belong to a support layer group.
    /// </summary>
    private void ApplySupportLayerGroupMaterial(SupportLayerGroup supportLayerGroup, float opacity)
    {
        _supportGroupQueryBuffer.Clear();
        _document.FillSupportEntitiesForGroup(supportLayerGroup.Id, _supportGroupQueryBuffer);
        PhongMaterial supportMaterial = SupportRenderer.CreateMaterial(supportLayerGroup.Color, opacity);

        try
        {
            for (int i = 0; i < _supportGroupQueryBuffer.Count; i++)
            {
                SupportEntity supportEntity = _supportGroupQueryBuffer[i];

                if (!_entityToVisual.TryGetValue(supportEntity, out GroupModel3D? visual))
                {
                    continue;
                }

                MeshRenderer.ApplyToSelectableMeshModels(visual, (MeshGeometryModel3D meshModel) =>
                {
                    meshModel.Material = supportMaterial;
                });
            }
        }
        finally
        {
            _supportGroupQueryBuffer.Clear();
        }
    }

    /// <summary>
    /// Updates the face-angle overlay for one mesh visual using the current scene-level settings.
    /// </summary>
    private void ApplyFaceAngleHighlight(GroupModel3D visual, MeshEntity mesh)
    {
        MeshRenderer.ApplyFaceAngleHighlight(
            visual,
            mesh,
            _isFaceAngleHighlightEnabled,
            _faceAngleThresholdDegrees,
            _faceAngleHighlightColor);

        MeshGeometryModel3D? faceHighlightModel = MeshRenderer.GetFaceHighlightModel(visual);

        if (faceHighlightModel != null)
        {
            _elementToVisual[faceHighlightModel] = visual;
        }

        ApplyModelClipRangeIfConfigured(visual);
    }

    /// <summary>
    /// Applies the current clipping range to one visual when the clipping overlay has already initialized.
    /// </summary>
    private void ApplyModelClipRangeIfConfigured(GroupModel3D visual)
    {
        if (!_isModelClipRangeConfigured)
        {
            return;
        }

        MeshRenderer.ApplyClipRange(visual, _modelClipLowerZ, _modelClipUpperZ, IsModelClipRangeActive());
    }

    /// <summary>
    /// Returns true only when the requested range excludes part of the configured printable Z volume.
    /// </summary>
    private bool IsModelClipRangeActive()
    {
        if (!_isModelClipRangeConfigured)
        {
            return false;
        }

        return _modelClipLowerZ > ModelClipRangeTolerance
            || _modelClipUpperZ < _printableVolumeDefinition.ZDistance - ModelClipRangeTolerance;
    }

    /// <summary>
    /// Gets the color currently assigned to one support layer group.
    /// </summary>
    private SupportLayerColor GetSupportLayerGroupColor(Guid supportLayerGroupId)
    {
        SupportLayerGroup? supportLayerGroup = _document.FindSupportLayerGroupById(supportLayerGroupId);

        if (supportLayerGroup != null)
        {
            return supportLayerGroup.Color;
        }

        return SupportLayerColorGenerator.CreateFromStableSeed(supportLayerGroupId);
    }

    /// <summary>
    /// Projects an entity's 3D axis-aligned bounds to a conservative screen-space rectangle.
    /// </summary>
    private bool TryGetProjectedBounds(CadEntity entity, out Rect projectedBounds)
    {
        (Vector3 Min, Vector3 Max) bounds = entity.GetBounds();
        bool hasProjectedPoint = false;
        projectedBounds = Rect.Empty;

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Min.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Max.X, bounds.Min.Y, bounds.Min.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Min.X, bounds.Max.Y, bounds.Min.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Min.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Max.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Max.X, bounds.Min.Y, bounds.Max.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Min.X, bounds.Max.Y, bounds.Max.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        if (!TryAppendProjectedBoundsPoint(new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Max.Z), ref projectedBounds, ref hasProjectedPoint))
        {
            return false;
        }

        return hasProjectedPoint;
    }

    /// <summary>
    /// Adds one projected bounds corner to the accumulated screen-space rectangle.
    /// </summary>
    private bool TryAppendProjectedBoundsPoint(Vector3 worldPoint, ref Rect projectedBounds, ref bool hasProjectedPoint)
    {
        Point screenPoint;

        if (!TryProjectWorldPoint(worldPoint, out screenPoint))
        {
            return false;
        }

        if (!hasProjectedPoint)
        {
            projectedBounds = new Rect(screenPoint, screenPoint);
            hasProjectedPoint = true;
            return true;
        }

        projectedBounds.Union(screenPoint);
        return true;
    }

    /// <summary>
    /// Projects one world-space point into viewport pixel coordinates and rejects invalid results.
    /// </summary>
    private bool TryProjectWorldPoint(Vector3 worldPoint, out Point screenPoint)
    {
        screenPoint = _viewport.Project(new Point3D(worldPoint.X, worldPoint.Y, worldPoint.Z));

        return IsFinite(screenPoint.X) && IsFinite(screenPoint.Y);
    }

    /// <summary>
    /// Rejects NaN and infinity from projection math.
    /// </summary>
    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Gets the active opacity for one support layer group, including transient edit-mode overrides.
    /// </summary>
    private float GetSupportLayerGroupOpacity(Guid supportLayerGroupId)
    {
        if (_supportLayerGroupOpacityOverrides.TryGetValue(supportLayerGroupId, out float opacity))
        {
            return opacity;
        }

        return FullyOpaqueSupportOpacity;
    }

    /// <summary>
    /// Finds one document entity by its stable identifier.
    /// </summary>
    private CadEntity? FindEntityById(Guid id)
    {
        foreach (CadEntity entity in _document.Entities)
        {
            if (entity.Id == id)
            {
                return entity;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds one entity from its root visual.
    /// </summary>
    private CadEntity? GetEntityFromGroup(GroupModel3D group)
    {
        if (_visualToEntity.TryGetValue(group, out CadEntity? entity))
        {
            return entity;
        }

        return null;
    }

    /// <summary>
    /// Creates the fallback material used when a selected mesh visual is not inside a grouped entity visual.
    /// </summary>
    private static PhongMaterial CreateSelectionMaterial(MediaColor selectionOutlineColor)
    {
        Color4 color = new Color4(
            selectionOutlineColor.R / 255.0f,
            selectionOutlineColor.G / 255.0f,
            selectionOutlineColor.B / 255.0f,
            selectionOutlineColor.A / 255.0f);

        return new PhongMaterial
        {
            AmbientColor = color,
            DiffuseColor = color,
            SpecularColor = new Color4(0.18f, 0.18f, 0.18f, color.Alpha),
            SpecularShininess = 24.0f
        };
    }

    /// <summary>
    /// Subscribes to any support groups that already exist when the scene manager starts.
    /// </summary>
    private void SubscribeToExistingSupportLayerGroups()
    {
        foreach (SupportLayerGroup supportLayerGroup in _document.SupportLayerGroups)
        {
            supportLayerGroup.PropertyChanged += SupportLayerGroup_PropertyChanged;
        }
    }

    /// <summary>
    /// Subscribes to entities that already exist when the scene manager starts.
    /// </summary>
    private void SubscribeToExistingEntities()
    {
        foreach (CadEntity entity in _document.Entities)
        {
            entity.PropertyChanged += Entity_PropertyChanged;
        }
    }
}
