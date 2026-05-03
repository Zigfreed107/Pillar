// SceneManager.cs
// Orchestrates document-to-visual mapping and incremental rendering state updates for the viewport.
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
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

namespace Pillar.Rendering.Scene;

/// <summary>
/// Manages the 3D scene for the CAD application.
/// </summary>
public class SceneManager
{
    private const string MeshHighlightPostEffect = "highlight";
    private const float FullyOpaqueSupportOpacity = 1.0f;

    private readonly Viewport3DX _viewport;
    private readonly CadDocument _document;
    private readonly Dictionary<GroupModel3D, CadEntity> _visualToEntity = new Dictionary<GroupModel3D, CadEntity>();
    private readonly Dictionary<CadEntity, GroupModel3D> _entityToVisual = new Dictionary<CadEntity, GroupModel3D>();
    private readonly Dictionary<Element3D, GroupModel3D> _elementToVisual = new Dictionary<Element3D, GroupModel3D>();
    private readonly GroupModel3D _entityRoot = new GroupModel3D();
    private readonly GroupModel3D _backgroundGridRoot = new GroupModel3D();
    private readonly GroupModel3D _previewRoot = new GroupModel3D();
    private readonly BackgroundGridRenderer _backgroundGridRenderer;
    private readonly SnapMarkerRenderer _snapMarkerRenderer;
    private readonly PreviewLineRenderer _previewLineRenderer;
    private readonly CircleSupportPreviewRenderer _circleSupportPreviewRenderer;
    private readonly SelectionManager _selectionManager;
    private readonly PhongMaterial _defaultMeshMaterial = MeshRenderer.CreateDefaultMaterial();
    private readonly PhongMaterial _highlightMaterial = new PhongMaterial
    {
        DiffuseColor = new Color4(1.0f, 1.0f, 0.0f, 1.0f)
    };

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
    {
        _viewport = viewport;
        _document = document;
        _selectionManager = new SelectionManager(_document);

        _viewport.Items.Add(new PostEffectMeshBorderHighlight
        {
            EffectName = MeshHighlightPostEffect
        });

        _backgroundGridRenderer = new BackgroundGridRenderer(_backgroundGridRoot, BackgroundGridDefinition.Default);
        _viewport.Items.Add(_backgroundGridRoot);
        _viewport.Items.Add(_entityRoot);

        _previewLineRenderer = new PreviewLineRenderer(_previewRoot);
        _circleSupportPreviewRenderer = new CircleSupportPreviewRenderer(_previewRoot);
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
    /// Shows the transient Circle Support guide circle.
    /// </summary>
    public void ShowCircleSupportPreview(Pillar.Geometry.Primitives.Circle3D circle)
    {
        _circleSupportPreviewRenderer.ShowCircle(circle);
    }

    /// <summary>
    /// Shows transient projected support marker positions for the Circle Support tool.
    /// </summary>
    public void ShowCircleSupportMarkers(IReadOnlyList<Vector3> markerPositions)
    {
        _circleSupportPreviewRenderer.ShowMarkers(markerPositions);
    }

    /// <summary>
    /// Hides transient projected support marker positions while leaving the circle and diameter handles visible.
    /// </summary>
    public void HideCircleSupportMarkers()
    {
        _circleSupportPreviewRenderer.HideMarkers();
    }

    /// <summary>
    /// Shows transient diameter handles for the Circle Support tool.
    /// </summary>
    public void ShowCircleSupportDiameterHandles(
        Vector3 firstPoint,
        Vector3? secondPoint,
        float handleDiameter,
        CircleSupportDiameterHandleKind activeHandle)
    {
        _circleSupportPreviewRenderer.ShowDiameterHandles(firstPoint, secondPoint, handleDiameter, activeHandle);
    }

    /// <summary>
    /// Hit-tests only the transient Circle Support diameter handles.
    /// </summary>
    public bool TryHitCircleSupportDiameterHandle(Vector2 screenPosition, out CircleSupportDiameterHandleKind handleKind)
    {
        IList<HitTestResult> hits = _viewport.FindHits(new Point(screenPosition.X, screenPosition.Y));

        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i].ModelHit is Element3D hitModel
                && _circleSupportPreviewRenderer.TryGetDiameterHandleKind(hitModel, out handleKind))
            {
                return true;
            }
        }

        handleKind = CircleSupportDiameterHandleKind.None;
        return false;
    }

    /// <summary>
    /// Hides all transient Circle Support preview geometry.
    /// </summary>
    public void HideCircleSupportPreview()
    {
        _circleSupportPreviewRenderer.Hide();
    }

    /// <summary>
    /// Hides the generated Circle Support circle and support markers while keeping diameter handles visible.
    /// </summary>
    public void HideCircleSupportCircleAndMarkers()
    {
        _circleSupportPreviewRenderer.HideCircleAndMarkers();
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

        ApplySupportLayerGroupMaterial(supportLayerGroup, opacity);
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
    /// Creates one renderable visual for one supported entity type.
    /// </summary>
    private GroupModel3D? CreateVisual(CadEntity entity)
    {
        if (entity is LineEntity line)
        {
            return LineRenderer.Create(line);
        }

        if (entity is MeshEntity mesh)
        {
            return MeshRenderer.Create(mesh);
        }

        if (entity is SupportEntity support)
        {
            return SupportRenderer.Create(support, GetSupportLayerGroupColor(support.SupportLayerGroupId));
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

        MeshGeometryModel3D? meshModel = MeshRenderer.GetMeshModel(group);

        if (meshModel == null)
        {
            return;
        }

        meshModel.PostEffects = string.Empty;

        if (entity is SupportEntity supportEntity)
        {
            meshModel.Material = SupportRenderer.CreateMaterial(GetSupportLayerGroupColor(supportEntity.SupportLayerGroupId));
            return;
        }

        meshModel.Material = _defaultMeshMaterial;
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
            }

            return;
        }

        Element3D? lineOverlay = LineRenderer.GetSelectionOverlay(group);

        if (lineOverlay != null)
        {
            lineOverlay.Visibility = System.Windows.Visibility.Visible;
            return;
        }

        MeshGeometryModel3D? meshModel = MeshRenderer.GetMeshModel(group);

        if (meshModel != null)
        {
            meshModel.PostEffects = MeshHighlightPostEffect;
        }
    }

    /// <summary>
    /// Applies one support layer group's color to all supports that belong to it.
    /// </summary>
    private void ApplySupportLayerGroupColorToEntities(SupportLayerGroup supportLayerGroup)
    {
        ApplySupportLayerGroupMaterial(supportLayerGroup, FullyOpaqueSupportOpacity);
    }

    /// <summary>
    /// Applies one material opacity to all supports that belong to a support layer group.
    /// </summary>
    private void ApplySupportLayerGroupMaterial(SupportLayerGroup supportLayerGroup, float opacity)
    {
        IReadOnlyList<SupportEntity> supportEntities = _document.GetSupportEntitiesForGroup(supportLayerGroup.Id);

        foreach (SupportEntity supportEntity in supportEntities)
        {
            if (!_entityToVisual.TryGetValue(supportEntity, out GroupModel3D? visual))
            {
                continue;
            }

            MeshGeometryModel3D? meshModel = SupportRenderer.GetMeshModel(visual);

            if (meshModel != null)
            {
                meshModel.Material = SupportRenderer.CreateMaterial(supportLayerGroup.Color, opacity);
            }
        }
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
