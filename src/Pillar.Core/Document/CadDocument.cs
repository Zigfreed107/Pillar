// CadDocument.cs
// Owns the current CAD entity collection and document-level mutation helpers used by tools, rendering, and file workflows.
using Pillar.Core.Entities;
using Pillar.Core.Snapping;
using Pillar.Core.Spatial;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Pillar.Core.Document;

/// <summary>
/// Represents the editable CAD document that tools modify and renderers observe.
/// </summary>
public class CadDocument
{
    //TODO Spatial grid size is a const value. Make it specified in a config file somewhere?
    private readonly ObservableCollection<CadEntity> _entities = new ObservableCollection<CadEntity>();

    public IReadOnlyList<CadEntity> Entities
    {
        get { return _entities; }
    }

    public SpatialGrid SpatialGrid { get; }

    public CadDocument()
    {
        SpatialGrid = new SpatialGrid(1.0f);
    }

    /// <summary>
    /// Raised when document entities change so observers can synchronize render state without mutating the collection.
    /// </summary>
    public event NotifyCollectionChangedEventHandler? EntitiesChanged
    {
        add { _entities.CollectionChanged += value; }
        remove { _entities.CollectionChanged -= value; }
    }

    /// <summary>
    /// Adds one entity to the document and updates document-owned acceleration structures.
    /// </summary>
    public void AddEntity(CadEntity entity)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        AddEntityToSpatialIndex(entity);
        _entities.Add(entity);
    }

    /// <summary>
    /// Removes one entity from the document and updates document-owned acceleration structures.
    /// </summary>
    public bool RemoveEntity(CadEntity entity)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        if (!_entities.Contains(entity))
        {
            return false;
        }

        RemoveEntityFromSpatialIndex(entity);
        _entities.Remove(entity);

        return true;
    }

    /// <summary>
    /// Removes all entities while preserving per-entity collection notifications for renderers.
    /// </summary>
    public void ClearEntities()
    {
        for (int i = _entities.Count - 1; i >= 0; i--)
        {
            RemoveEntity(_entities[i]);
        }
    }

    /// <summary>
    /// Replaces the document contents while preserving collection notifications for scene synchronization.
    /// </summary>
    public void ReplaceEntities(IEnumerable<CadEntity> entities)
    {
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities));
        }

        List<CadEntity> replacementEntities = new List<CadEntity>(entities);

        ClearEntities();

        foreach (CadEntity entity in replacementEntities)
        {
            AddEntity(entity);
        }
    }

    /// <summary>
    /// Adds entities that provide snap points to the document-owned spatial index.
    /// </summary>
    private void AddEntityToSpatialIndex(CadEntity entity)
    {
        if (entity is ISnapProvider)
        {
            SpatialGrid.Insert(entity);
        }
    }

    /// <summary>
    /// Removes entities that provide snap points from the document-owned spatial index.
    /// </summary>
    private void RemoveEntityFromSpatialIndex(CadEntity entity)
    {
        if (entity is ISnapProvider)
        {
            SpatialGrid.Remove(entity);
        }
    }
}
