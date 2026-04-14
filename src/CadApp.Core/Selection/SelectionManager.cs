// SelectionManager.cs
// Domain-level selection state service used by tools and consumed by rendering via events.
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using CadApp.Core.Document;
using CadApp.Core.Entities;

/// <summary>
/// Manages selection state for all selectable entities.
/// This class is part of the domain layer and contains no rendering logic.
/// </summary>
public class SelectionManager
{
    private static readonly Guid[] EmptyIds = Array.Empty<Guid>();

    /// <summary>
    /// Currently selected entities.
    /// </summary>
    private readonly HashSet<Guid> _selectedEntityIds = new HashSet<Guid>();

    /// <summary>
    /// Creates document-scoped selection state that automatically removes deleted entities from selection.
    /// </summary>
    public SelectionManager(CadDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        document.EntitiesChanged += OnDocumentEntitiesChanged;
    }

    /// <summary>
    /// Gets the identifiers currently selected by the active tool.
    /// </summary>
    public IReadOnlyCollection<Guid> SelectedEntityIds
    {
        get { return _selectedEntityIds; }
    }

    /// <summary>
    /// Gets the number of currently selected entities.
    /// </summary>
    public int SelectedCount
    {
        get { return _selectedEntityIds.Count; }
    }

    /// <summary>
    /// Fired when selection changes.
    /// Provides delta changes for efficient rendering updates.
    /// </summary>
    public event Action<IEnumerable<Guid>, IEnumerable<Guid>>? SelectionChanged;

    /// <summary>
    /// Select a single entity (clears previous selection).
    /// </summary>
    public void SelectSingle(ISelectable entity)
    {
        if (_selectedEntityIds.Count == 1 && _selectedEntityIds.Contains(entity.Id))
        {
            return;
        }

        List<Guid> removed = new List<Guid>(_selectedEntityIds);
        List<Guid> added = new List<Guid>();

        _selectedEntityIds.Clear();

        if (!_selectedEntityIds.Contains(entity.Id))
        {
            _selectedEntityIds.Add(entity.Id);
            added.Add(entity.Id);
        }

        SelectionChanged?.Invoke(added, removed);
    }

    /// <summary>
    /// Add entity to selection (for multi-select later).
    /// </summary>
    public void AddToSelection(CadEntity entity)
    {
        if (_selectedEntityIds.Add(entity.Id))
        {
            SelectionChanged?.Invoke(new Guid[] { entity.Id }, EmptyIds);
        }
    }

    /// <summary>
    /// Adds many entities to the current selection and emits one batched change event.
    /// </summary>
    public void AddRangeToSelection(IEnumerable<CadEntity> entities)
    {
        List<Guid> added = new List<Guid>();

        foreach (CadEntity entity in entities)
        {
            if (_selectedEntityIds.Add(entity.Id))
            {
                added.Add(entity.Id);
            }
        }

        if (added.Count == 0)
        {
            return;
        }

        SelectionChanged?.Invoke(added, EmptyIds);
    }

    /// <summary>
    /// Replaces the current selection with many entities and emits one batched change event.
    /// </summary>
    public void SelectMany(IEnumerable<CadEntity> entities)
    {
        HashSet<Guid> requestedIds = new HashSet<Guid>();

        foreach (CadEntity entity in entities)
        {
            requestedIds.Add(entity.Id);
        }

        List<Guid> removed = new List<Guid>();
        List<Guid> added = new List<Guid>();

        foreach (Guid selectedId in _selectedEntityIds)
        {
            if (!requestedIds.Contains(selectedId))
            {
                removed.Add(selectedId);
            }
        }

        foreach (Guid requestedId in requestedIds)
        {
            if (!_selectedEntityIds.Contains(requestedId))
            {
                added.Add(requestedId);
            }
        }

        if (removed.Count == 0 && added.Count == 0)
        {
            return;
        }

        _selectedEntityIds.Clear();

        foreach (Guid requestedId in requestedIds)
        {
            _selectedEntityIds.Add(requestedId);
        }

        SelectionChanged?.Invoke(added, removed);
    }

    /// <summary>
    /// Removes one entity from the current selection while leaving other selected entities intact.
    /// </summary>
    public void RemoveFromSelection(ISelectable entity)
    {
        if (_selectedEntityIds.Remove(entity.Id))
        {
            SelectionChanged?.Invoke(EmptyIds, new Guid[] { entity.Id });
        }
    }

    /// <summary>
    /// Removes many entities from the current selection and emits one batched change event.
    /// </summary>
    public void RemoveRangeFromSelection(IEnumerable<CadEntity> entities)
    {
        List<Guid> removed = new List<Guid>();

        foreach (CadEntity entity in entities)
        {
            if (_selectedEntityIds.Remove(entity.Id))
            {
                removed.Add(entity.Id);
            }
        }

        if (removed.Count == 0)
        {
            return;
        }

        SelectionChanged?.Invoke(EmptyIds, removed);
    }

    /// <summary>
    /// Selects an unselected entity or deselects a selected entity.
    /// </summary>
    public void ToggleSelection(ISelectable entity)
    {
        if (_selectedEntityIds.Contains(entity.Id))
        {
            RemoveFromSelection(entity);
            return;
        }

        if (_selectedEntityIds.Add(entity.Id))
        {
            SelectionChanged?.Invoke(new Guid[] { entity.Id }, EmptyIds);
        }
    }

    /// <summary>
    /// Deselect all entities.
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedEntityIds.Count == 0)
            return;

        List<Guid> removed = new List<Guid>(_selectedEntityIds);

        _selectedEntityIds.Clear();

        SelectionChanged?.Invoke(EmptyIds, removed);
    }

    /// <summary>
    /// Check if an entity is selected.
    /// </summary>
    public bool IsSelected(Guid entityId)
    {
        return _selectedEntityIds.Contains(entityId);
    }

    /// <summary>
    /// Removes entities from selection as soon as they leave the document.
    /// </summary>
    private void OnDocumentEntitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems == null)
        {
            return;
        }

        List<CadEntity> removedEntities = new List<CadEntity>();

        foreach (object? oldItem in e.OldItems)
        {
            if (oldItem is CadEntity entity)
            {
                removedEntities.Add(entity);
            }
        }

        RemoveRangeFromSelection(removedEntities);
    }
}
