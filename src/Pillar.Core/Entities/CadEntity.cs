// CadEntity.cs
// Defines the shared domain data every CAD entity carries, independent of rendering and UI concerns.
using Pillar.Core.Snapping;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Core.Entities;

/// <summary>
/// Provides the shared identity, naming, bounds, and snap-point contract for all CAD entities.
/// </summary>
public abstract class CadEntity: ISelectable
{
    public Guid Id { get; protected set; }
    public string Name { get; set; }

    public abstract (Vector3 Min, Vector3 Max) GetBounds();

    /// <summary>
    /// Creates a CAD entity with a stable identifier and user-visible name.
    /// </summary>
    protected CadEntity(string name)
    {
        Id = Guid.NewGuid();
        Name = string.IsNullOrWhiteSpace(name) ? "Entity" : name;
    }

    /// <summary>
    /// Returns entity snap points for tools that support snapping.
    /// </summary>
    public virtual IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield break;
    }
}
