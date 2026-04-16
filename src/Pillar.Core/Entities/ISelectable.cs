using System;

/// <summary>
/// Represents an entity that can be selected in the CAD scene.
/// This is part of the domain layer and does NOT contain rendering logic.
/// </summary>
public interface ISelectable
{
    /// <summary>
    /// Unique identifier for the entity.
    /// Used by selection systems to track entities.
    /// </summary>
    Guid Id { get; }
}