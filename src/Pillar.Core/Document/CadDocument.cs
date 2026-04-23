// CadDocument.cs
// Owns the current CAD entity collection and document-level mutation helpers used by tools, rendering, and file workflows.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Snapping;
using Pillar.Core.Spatial;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;

namespace Pillar.Core.Document;

/// <summary>
/// Represents the editable CAD document that tools modify and renderers observe.
/// </summary>
public class CadDocument
{
    //TODO Spatial grid size is a const value. Make it specified in a config file somewhere?
    private readonly ObservableCollection<CadEntity> _entities = new ObservableCollection<CadEntity>();
    private readonly ObservableCollection<SupportLayerGroup> _supportLayerGroups = new ObservableCollection<SupportLayerGroup>();

    public IReadOnlyList<CadEntity> Entities
    {
        get { return _entities; }
    }

    public SpatialGrid SpatialGrid { get; }

    public IReadOnlyList<SupportLayerGroup> SupportLayerGroups
    {
        get { return _supportLayerGroups; }
    }

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
    /// Raised when document-owned support layer groups change so UI trees can refresh without polling.
    /// </summary>
    public event NotifyCollectionChangedEventHandler? SupportLayerGroupsChanged
    {
        add { _supportLayerGroups.CollectionChanged += value; }
        remove { _supportLayerGroups.CollectionChanged -= value; }
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

        ValidateEntityOwnership(entity);

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

        RemoveSupportGroupsForEntity(entity);
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
        ReplaceDocumentData(entities, Array.Empty<SupportLayerGroup>());
    }

    /// <summary>
    /// Replaces entities and layer metadata while preserving collection notifications for observers.
    /// </summary>
    public void ReplaceDocumentData(IEnumerable<CadEntity> entities, IEnumerable<SupportLayerGroup> supportLayerGroups)
    {
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities));
        }

        if (supportLayerGroups == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroups));
        }

        List<CadEntity> replacementEntities = new List<CadEntity>(entities);
        List<SupportLayerGroup> replacementSupportLayerGroups = new List<SupportLayerGroup>(supportLayerGroups);

        ClearEntities();
        _supportLayerGroups.Clear();

        foreach (CadEntity entity in replacementEntities)
        {
            if (entity is not SupportEntity)
            {
                AddEntity(entity);
            }
        }

        foreach (SupportLayerGroup supportLayerGroup in replacementSupportLayerGroups)
        {
            AddSupportLayerGroup(supportLayerGroup);
        }

        foreach (CadEntity entity in replacementEntities)
        {
            if (entity is SupportEntity)
            {
                AddEntity(entity);
            }
        }
    }

    /// <summary>
    /// Adds one support group under an imported mesh layer.
    /// </summary>
    public void AddSupportLayerGroup(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        if (!ContainsMeshEntity(supportLayerGroup.ModelEntityId))
        {
            throw new InvalidOperationException("Support groups can only be added under imported mesh entities.");
        }

        if (FindSupportLayerGroupById(supportLayerGroup.Id) != null)
        {
            throw new InvalidOperationException("The document already contains this support group.");
        }

        _supportLayerGroups.Add(supportLayerGroup);
    }

    /// <summary>
    /// Removes one support group from the document.
    /// </summary>
    public bool RemoveSupportLayerGroup(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        if (!_supportLayerGroups.Contains(supportLayerGroup))
        {
            return false;
        }

        RemoveSupportEntitiesForGroup(supportLayerGroup.Id);
        return _supportLayerGroups.Remove(supportLayerGroup);
    }

    /// <summary>
    /// Renames one support group while preserving its identity and child support membership.
    /// </summary>
    public void RenameSupportLayerGroup(SupportLayerGroup supportLayerGroup, string newName)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        if (!_supportLayerGroups.Contains(supportLayerGroup))
        {
            throw new InvalidOperationException("The support group is not part of this document.");
        }

        supportLayerGroup.Rename(newName);
    }

    /// <summary>
    /// Updates one support group's display color while preserving its identity and child support membership.
    /// </summary>
    public void SetSupportLayerGroupColor(SupportLayerGroup supportLayerGroup, SupportLayerColor color)
    {
        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        if (!_supportLayerGroups.Contains(supportLayerGroup))
        {
            throw new InvalidOperationException("The support group is not part of this document.");
        }

        supportLayerGroup.SetColor(color);
    }

    /// <summary>
    /// Finds a support group by its stable identifier.
    /// </summary>
    public SupportLayerGroup? FindSupportLayerGroupById(Guid id)
    {
        foreach (SupportLayerGroup supportLayerGroup in _supportLayerGroups)
        {
            if (supportLayerGroup.Id == id)
            {
                return supportLayerGroup;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all support entities owned by one support layer group.
    /// </summary>
    public IReadOnlyList<SupportEntity> GetSupportEntitiesForGroup(Guid supportLayerGroupId)
    {
        List<SupportEntity> supportEntities = new List<SupportEntity>();

        foreach (CadEntity entity in _entities)
        {
            if (entity is SupportEntity supportEntity && supportEntity.SupportLayerGroupId == supportLayerGroupId)
            {
                supportEntities.Add(supportEntity);
            }
        }

        return supportEntities;
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

    /// <summary>
    /// Checks whether a model entity exists for layer ownership validation.
    /// </summary>
    private bool ContainsMeshEntity(Guid entityId)
    {
        foreach (CadEntity entity in _entities)
        {
            if (entity.Id == entityId && entity is MeshEntity)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates document ownership rules before accepting an entity.
    /// </summary>
    private void ValidateEntityOwnership(CadEntity entity)
    {
        if (entity is not SupportEntity supportEntity)
        {
            return;
        }

        if (FindSupportLayerGroupById(supportEntity.SupportLayerGroupId) == null)
        {
            throw new InvalidOperationException("Support entities can only be added to existing support layer groups.");
        }
    }

    /// <summary>
    /// Removes support metadata when its imported model leaves the document.
    /// </summary>
    private void RemoveSupportGroupsForEntity(CadEntity entity)
    {
        if (entity is not MeshEntity)
        {
            return;
        }

        for (int i = _supportLayerGroups.Count - 1; i >= 0; i--)
        {
            if (_supportLayerGroups[i].ModelEntityId == entity.Id)
            {
                RemoveSupportLayerGroup(_supportLayerGroups[i]);
            }
        }
    }

    /// <summary>
    /// Removes all supports that belong to one support layer group.
    /// </summary>
    private void RemoveSupportEntitiesForGroup(Guid supportLayerGroupId)
    {
        for (int i = _entities.Count - 1; i >= 0; i--)
        {
            if (_entities[i] is SupportEntity supportEntity && supportEntity.SupportLayerGroupId == supportLayerGroupId)
            {
                RemoveEntity(supportEntity);
            }
        }
    }
}
