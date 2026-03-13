using CadApp.Core.Document;
using CadApp.Core.Entities;
using CadApp.Rendering.EntityRenderers;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace CadApp.Rendering.Scene;

public class SceneManager
{
    private readonly Viewport3DX _viewport;
    private readonly CadDocument _document;
    private readonly Dictionary<Element3D, CadEntity> _visualToEntity = new();
    private readonly GroupModel3D _entityRoot = new();

    public SceneManager(Viewport3DX viewport, CadDocument document)
    {
        _viewport = viewport;
        _viewport.Items.Add(_entityRoot);
        _document = document;
        _document.Entities.CollectionChanged += OnEntitiesChanged;

        RenderAll();
    }

    private void OnEntitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderAll();
    }

    private void RenderAll()
    {
        _entityRoot.Children.Clear();
        _visualToEntity.Clear();

        //_viewport.Items.Add(new SunLight());

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

}
