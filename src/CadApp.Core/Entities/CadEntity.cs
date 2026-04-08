using CadApp.Core.Snapping;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CadApp.Core.Entities;

public abstract class CadEntity
{
    public Guid Id { get; } = Guid.NewGuid();
    public abstract (Vector3 Min, Vector3 Max) GetBounds();

    public virtual IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield break;
    }
}