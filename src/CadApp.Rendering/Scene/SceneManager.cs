using CadApp.Core.Document;
using CadApp.Core.Entities;
using CadApp.Core.Spatial;
using CadApp.Rendering.EntityRenderers;
using CadApp.Rendering.Preview;
using HelixToolkit;
using HelixToolkit.Geometry;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CadApp.Rendering.Scene;

public class SceneManager
{
    private readonly Viewport3DX _viewport;
    private readonly CadDocument _document;
    private readonly Dictionary<Element3D, CadEntity> _visualToEntity = new();
    private readonly GroupModel3D _entityRoot = new();  //Permanent CAD geometry
    private readonly GroupModel3D _previewRoot = new(); //Preview Geometry
    private readonly SnapMarkerRenderer _snapMarkerRenderer;
    private PreviewLineRenderer _previewLineRenderer;

    public SceneManager(Viewport3DX viewport, CadDocument document)
    {
        _viewport = viewport;
        _document = document;

        // Add Preview Objects
        _previewLineRenderer = new PreviewLineRenderer(_previewRoot);
        _snapMarkerRenderer = new SnapMarkerRenderer(_previewRoot);
        _viewport.Items.Add(_previewRoot);

        // Add background grid
        var _grid = CreateGrid();
        
        
        
        _viewport.Items.Add(_grid);
        _viewport.Items.Add(_entityRoot);



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

        RenderAll();
    }

    private void RenderAll()
    {
        _entityRoot.Children.Clear();
        _visualToEntity.Clear();

        foreach (var entity in _document.Entities)
        {
            var visual = CreateVisual(entity);

            if (visual != null)
            {
                _entityRoot.Children.Add(visual);
                _visualToEntity[visual] = entity;
            }
        }
    }

    public CadEntity? GetEntityFromVisual(Element3D visual)
    {
        if (_visualToEntity.TryGetValue(visual, out var entity))
            return entity;

        return null;
    }

    private Element3D? CreateVisual(CadEntity entity)
    {
        if (entity is LineEntity line)
            return LineRenderer.Create(line);

        return null;
    }

    private Element3D CreateGrid()
    {
        var builder = new LineBuilder();

        int size = 50;
        int step = 1;

        for (int i = -size; i <= size; i += step)
        {
            // vertical lines
            builder.AddLine(
                new Vector3(i, -size, 0),
                new Vector3(i, size, 0));

            // horizontal lines
            builder.AddLine(
                new Vector3(-size, i, 0),
                new Vector3(size, i, 0));
        }

        return new LineGeometryModel3D
        {
            Geometry = builder.ToLineGeometry3D(),
            Color = System.Windows.Media.Color.FromRgb(150,150,150),
            Thickness = 0.5f
        };
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
        if (entity is LineEntity line)
        {
            _document.SpatialGrid.Insert(line, line.Start); // temporary
        }

        // future: handle other entity types
    }

    private void RemoveEntity(CadEntity entity)
    {
        if (entity is LineEntity line)
        {
            _document.SpatialGrid.Remove(line, line.Start);
        }
    }
}
