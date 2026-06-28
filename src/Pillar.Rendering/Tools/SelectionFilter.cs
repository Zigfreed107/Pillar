// SelectionFilter.cs
// Defines reusable entity eligibility rules for viewport tools that need constrained selection.
using Pillar.Core.Entities;
using System;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Describes which domain entities are eligible for a tool-specific selection workflow.
/// </summary>
public sealed class SelectionFilter
{
    private readonly Func<CadEntity, bool> _predicate;

    /// <summary>
    /// Creates a reusable selection filter from a domain-entity predicate.
    /// </summary>
    private SelectionFilter(Func<CadEntity, bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <summary>
    /// Gets the unrestricted selection filter used by normal selection mode.
    /// </summary>
    public static SelectionFilter AllowAll { get; } = new SelectionFilter(entity => entity != null);

    /// <summary>
    /// Creates a filter that accepts only entities assignable to the requested domain type.
    /// </summary>
    public static SelectionFilter EntitiesOfType<TEntity>()
        where TEntity : CadEntity
    {
        return new SelectionFilter(entity => entity is TEntity);
    }

    /// <summary>
    /// Creates a filter that accepts only support entities.
    /// </summary>
    public static SelectionFilter SupportsOnly()
    {
        return EntitiesOfType<SupportEntity>();
    }

    /// <summary>
    /// Creates a filter that accepts only supports owned by one support layer group.
    /// </summary>
    public static SelectionFilter SupportsInLayer(Guid supportLayerGroupId)
    {
        if (supportLayerGroupId == Guid.Empty)
        {
            throw new ArgumentException("A support layer selection filter requires a non-empty layer id.", nameof(supportLayerGroupId));
        }

        return new SelectionFilter(entity =>
            entity is SupportEntity supportEntity
            && supportEntity.SupportLayerGroupId == supportLayerGroupId);
    }

    /// <summary>
    /// Creates a filter from a caller-supplied domain predicate for tool-specific workflows.
    /// </summary>
    public static SelectionFilter FromPredicate(Func<CadEntity, bool> predicate)
    {
        return new SelectionFilter(predicate);
    }

    /// <summary>
    /// Returns true when the supplied entity is selectable for the active workflow.
    /// </summary>
    public bool Allows(CadEntity entity)
    {
        return entity != null && _predicate(entity);
    }
}
