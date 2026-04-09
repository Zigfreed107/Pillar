// SceneManager.cs
// Orchestrates document-to-visual mapping and incremental rendering state updates for the viewport.
using CadApp.Core.Document;
using CadApp.Core.Entities;
using CadApp.Rendering.BackgroundGrid;
using CadApp.Rendering.EntityRenderers;
using CadApp.Rendering.Preview;
using HelixToolkit.Maths;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Numerics;

namespace CadApp.Rendering.Scene;

public class SceneManager
{
    private readonly Viewport3DX _viewport;
    private readonly CadDocument _document;
    private readonly Dictionary<Element3D, CadEntity> _visualToEntity = new Dictionary<Element3D, CadEntity>();
    private readonly Dictionary<CadEntity, Element3D> _entityToVisual = new Dictionary<CadEntity, Element3D>();

    private readonly GroupModel3D _entityRoot = new GroupModel3D();  //Permanent CAD geometry
    private readonly GroupModel3D _backgroundGridRoot = new GroupModel3D();
    private readonly GroupModel3D _previewRoot = new GroupModel3D(); //Preview Geometry
    private readonly BackgroundGridRenderer _BackgroundGridRenderer;
    private readonly SnapMarkerRenderer _snapMarkerRenderer;
    private PreviewLineRenderer _previewLineRenderer;
    private readonly SelectionManager _selectionManager = new SelectionManager();

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



        _document.Entities.CollectionChanged += OnEntitiesChanged;

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
            Element3D? visual;
            if (entity != null && _entityToVisual.TryGetValue(entity, out visual))
            {
                ApplyDefaultMaterial(visual);
            }
        }

        // Apply highlight for newly added entity ids
        foreach (Guid id in addedIds)
        {
            CadEntity? entity = FindEntityById(id);
            Element3D? visual;
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

        foreach (CadEntity entity in _document.Entities)
        {
            Element3D? visual = CreateVisual(entity);

            if (visual != null)
            {
                _entityRoot.Children.Add(visual);
                _visualToEntity[visual] = entity;
                _entityToVisual[entity] = visual;

                ApplyDefaultMaterial(visual);
            }
        }
    }

    public CadEntity? GetEntityFromVisual(Element3D visual)
    {
        CadEntity? entity;
        if (_visualToEntity.TryGetValue(visual, out entity))
            return entity;

        return null;
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

    private Element3D? CreateVisual(CadEntity entity)
    {
        if (entity is LineEntity line)
            return LineRenderer.Create(line);

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

    private void InsertEntity(CadEntity entity)
    {
        Element3D? visual = CreateVisual(entity);

        if (visual != null)
        {
            _entityRoot.Children.Add(visual);
            _entityToVisual[entity] = visual;
            _visualToEntity[visual] = entity;
            ApplyDefaultMaterial(visual);
        }

        if (entity is LineEntity line)
        {
            _document.SpatialGrid.Insert(line);
        }
    }

    private void RemoveEntity(CadEntity entity)
    {
        Element3D? visualToRemove = null;

        foreach (KeyValuePair<Element3D, CadEntity> pair in _visualToEntity)
        {
            if (pair.Value == entity)
            {
                visualToRemove = pair.Key;
                break;
            }
        }

        if (visualToRemove != null)
        {
            _entityRoot.Children.Remove(visualToRemove);
            _entityToVisual.Remove(entity);
            _visualToEntity.Remove(visualToRemove);
        }

        if (entity is LineEntity line)
        {
            _document.SpatialGrid.Remove(line);
        }
    }

    /// <summary>
    /// Applies default material to a visual.
    /// </summary>
    private void ApplyDefaultMaterial(Element3D visual)
    {
        if (visual is GroupModel3D group)
        {
            foreach (Element3D child in group.Children)
            {
                if (LineRenderer.IsSelectionOverlay(child))
                {
                    child.Visibility = System.Windows.Visibility.Hidden;
                }
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
        if (visual is GroupModel3D group)
        {
            foreach (Element3D child in group.Children)
            {
                if (LineRenderer.IsSelectionOverlay(child))
                {
                    child.Visibility = System.Windows.Visibility.Visible;
                }
            }

            return;
        }

        if (visual is MeshGeometryModel3D mesh)
        {
            mesh.Material = _highlightMaterial;
        }
    }
}
