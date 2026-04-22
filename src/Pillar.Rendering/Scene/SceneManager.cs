// SceneManager.cs
// Orchestrates document-to-visual mapping and incremental rendering state updates for the viewport.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Rendering.BackgroundGrid;
using Pillar.Rendering.EntityRenderers;
using Pillar.Rendering.Preview;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Numerics;

namespace Pillar.Rendering.Scene;

public class SceneManager
{
    private const string MeshHighlightPostEffect = "highlight";

    private readonly Viewport3DX _viewport;
    private readonly CadDocument _document;

    // Mapping Dictionaries.
    // visual to entity dicts maintain the relationship between CAD entities and their visual representation in the viewer.
    // visuals are stored as GroupModel3D to allow for composite visuals made of multiple entities (e.g. line + selection overlay).
    private readonly Dictionary<GroupModel3D, CadEntity> _visualToEntity = new Dictionary<GroupModel3D, CadEntity>();
    private readonly Dictionary<CadEntity, GroupModel3D> _entityToVisual = new Dictionary<CadEntity, GroupModel3D>();
    // element to visual dict allows us to find the parent visual for any given element in the scene. E.g. a line on the screen is an element,
    // but might be part of a larger visual that includes selection overlays, etc. This allows us to find the correct visual to update when an element is interacted with.
    private readonly Dictionary<Element3D, GroupModel3D> _elementToVisual = new Dictionary<Element3D, GroupModel3D>();

    private readonly GroupModel3D _entityRoot = new GroupModel3D();  //Permanent CAD geometry
    private readonly GroupModel3D _backgroundGridRoot = new GroupModel3D();
    private readonly GroupModel3D _previewRoot = new GroupModel3D(); //Preview Geometry
    private readonly BackgroundGridRenderer _BackgroundGridRenderer;
    private readonly SnapMarkerRenderer _snapMarkerRenderer;
    private PreviewLineRenderer _previewLineRenderer;
    private readonly SelectionManager _selectionManager;

    // Materials used for highlighting, selection, etc.
    private readonly PhongMaterial _defaultMaterial = new PhongMaterial
    {
        DiffuseColor = new Color4(0.7f, 0.7f, 0.7f, 1.0f)
    };

    private readonly PhongMaterial _highlightMaterial = new PhongMaterial
    {
        DiffuseColor = new Color4(1.0f, 1.0f, 0.0f, 1.0f)
    };

    /// <summary>
    /// Manages the 3D scene for the CAD application.
    /// 
    /// Responsibilities:
    /// - Maintains separation between permanent geometry and preview visuals
    /// - Converts domain entities into renderable visuals
    /// - Coordinates preview and snapping renderers
    /// 
    /// Important:
    /// - Avoids recreating scene objects during interaction
    /// - Uses incremental updates for performance
    /// </summary>
    /// 
    public SelectionManager SelectionManager
    {
        get { return _selectionManager; }
    }
    public SceneManager(Viewport3DX viewport, CadDocument document)
    {
        _viewport = viewport;
        _document = document;
        _selectionManager = new SelectionManager(_document);

        _viewport.Items.Add(new PostEffectMeshBorderHighlight
        {
            EffectName = MeshHighlightPostEffect
        });

        // Add background grid
        _BackgroundGridRenderer = new BackgroundGridRenderer(_backgroundGridRoot);
        _viewport.Items.Add(_backgroundGridRoot);

        //Add CAD Geometry Root - add before preview so previews render over CAD Geometry.
        _viewport.Items.Add(_entityRoot);

        // Add Preview Objects
        _previewLineRenderer = new PreviewLineRenderer(_previewRoot);
        _snapMarkerRenderer = new SnapMarkerRenderer(_previewRoot);
        _viewport.Items.Add(_previewRoot);

        //Selection
        _selectionManager.SelectionChanged += OnSelectionChanged;



        _document.EntitiesChanged += OnEntitiesChanged;

        RenderAll();
    }

    private void OnEntitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (CadEntity entity in e.NewItems)
            {
                InsertEntity(entity);
            }
        }

        if (e.OldItems != null)
        {
            foreach (CadEntity entity in e.OldItems)
            {
                RemoveEntity(entity);
            }
        }
    }

    // Updated to match SelectionManager.SelectionChanged: Action<IEnumerable<Guid>, IEnumerable<Guid>>
    private void OnSelectionChanged(IEnumerable<Guid> addedIds, IEnumerable<Guid> removedIds)
    {
        // Reset visuals for removed entity ids
        foreach (Guid id in removedIds)
        {
            CadEntity? entity = FindEntityById(id);
            GroupModel3D? visual;
            if (entity != null && _entityToVisual.TryGetValue(entity, out visual))
            {
                ApplyDefaultMaterial(visual);
            }
        }

        // Apply highlight for newly added entity ids
        foreach (Guid id in addedIds)
        {
            CadEntity? entity = FindEntityById(id);
            GroupModel3D? visual;
            if (entity != null && _entityToVisual.TryGetValue(entity, out visual))
            {
                ApplyHighlightMaterial(visual);
            }
        }
    }

    private void RenderAll()
    {
        _entityRoot.Children.Clear();

        _entityToVisual.Clear();
        _visualToEntity.Clear();
        _elementToVisual.Clear();

        foreach (CadEntity entity in _document.Entities)
        {
            GroupModel3D? visual = CreateVisual(entity);

            if (visual != null)
            {
                _entityToVisual[entity] = visual;
                _visualToEntity[visual] = entity;
                _entityRoot.Children.Add(visual);

                foreach (Element3D element in visual.Children)
                {
                    _elementToVisual[element] = visual;
                }
            }
        }
    }

    /// <summary>
    /// Finds the visual from a particular element in the scene.
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    public CadEntity? GetEntityFromVisual(Element3D element)
    {
        GroupModel3D? visual = null;
        CadEntity? entity = null;

        if (_elementToVisual.TryGetValue(element, out visual))
        {
            _visualToEntity.TryGetValue(visual, out entity);
        }

        return entity;

    }

    /// <summary>
    /// Finds entity by Id.
    /// TODO: This is O(n) now — will optimize later.
    /// </summary>
    private CadEntity? FindEntityById(Guid id)
    {
        foreach (CadEntity entity in _document.Entities)
        {
            if (entity.Id == id)
                return entity;
        }

        return null;
    }

    private GroupModel3D? CreateVisual(CadEntity entity)
    {
        if (entity is LineEntity line)
            return LineRenderer.Create(line);

        if (entity is MeshEntity mesh)
            return MeshRenderer.Create(mesh);

        if (entity is SupportEntity support)
            return SupportRenderer.Create(support);

        return null;
    }

    /// <summary>
    /// Updates the preview line positions without recreating the visual.
    /// This is called frequently during mouse move.
    /// </summary>
    public void ShowPreviewLine(Vector3 start, Vector3 end)
    {
        _previewLineRenderer.Show(start, end);
    }

    /// <summary>
    /// Hides the preview line when not in use.
    /// </summary>
    public void HidePreviewLine()
    {
        _previewLineRenderer.Hide();
    }

    /// <summary>
    /// Shows Snapping Marker at given position.
    /// </summary>
    /// <param name="position"></param>
    public void ShowSnappingPoint(Vector3 position)
    {
        _snapMarkerRenderer.Show(position);
    }

    /// <summary>
    /// Hides Snapping Marker.
    /// </summary>
    public void HideSnappingPoint()
    {
        _snapMarkerRenderer.HideSnappingPoint();
    }

    /// <summary>
    /// Adds a new entity into the scene
    /// </summary>
    /// <param name="entity"></param>
    private void InsertEntity(CadEntity entity)
    {

        // Update visual to entity mapping
        GroupModel3D? visual = CreateVisual(entity);


        if (visual != null)
        {

            foreach (Element3D element in visual.Children)
            {
                _elementToVisual.Add(element, visual);
            }

            _entityRoot.Children.Add(visual);
            _entityToVisual[entity] = visual;
            _visualToEntity[visual] = entity;
            ApplyDefaultMaterial(visual);

        }
    }

    /// <summary>
    /// Removes and entity from the scene.
    /// </summary>
    /// <param name="entity"></param>
    private void RemoveEntity(CadEntity entity)
    {
        // Update visual to entity mapping
        GroupModel3D? visualToRemove = null;

        if (_entityToVisual.TryGetValue(entity, out visualToRemove))
        {
            foreach (Element3D element in visualToRemove.Children)
            {
                _elementToVisual.Remove(element);
            }

            _entityRoot.Children.Remove(visualToRemove);
            _entityToVisual.Remove(entity);
            _visualToEntity.Remove(visualToRemove);
        }
    }

    /// <summary>
    /// Applies default material to a visual.
    /// </summary>
    private void ApplyDefaultMaterial(Element3D visual)
    {
        if (visual == null) return;

        if (visual is GroupModel3D group)
        {
            Element3D? lineOverlay = LineRenderer.GetSelectionOverlay(group);

            if (lineOverlay != null)
            {
                lineOverlay.Visibility = System.Windows.Visibility.Hidden;
                return;
            }

            MeshGeometryModel3D? meshModel = MeshRenderer.GetMeshModel(group);

            if (meshModel != null)
            {
                meshModel.PostEffects = string.Empty;
            }

            return;
        }

        if (visual is MeshGeometryModel3D mesh)
        {
            mesh.Material = _defaultMaterial;
        }
    }

    /// <summary>
    /// Applies highlight material to a visual.
    /// </summary>
    private void ApplyHighlightMaterial(Element3D visual)
    {
        if (visual == null) return;

        if (visual is GroupModel3D group)
        {
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

            return;
        }

        if (visual is MeshGeometryModel3D mesh)
        {
            mesh.Material = _highlightMaterial;
        }
    }
}
