// SelectionManager.cs
// Domain-level selection state service used by tools and consumed by rendering via events.
using System;
using System.Collections.Generic;

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
    public void AddToSelection(ISelectable entity)
    {
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
}
