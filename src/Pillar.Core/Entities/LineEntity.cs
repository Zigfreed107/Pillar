// LineEntity.cs
// Defines the domain representation of a line entity used by tools, snapping, selection, and rendering adapters.
using Pillar.Core.Entities;
using Pillar.Core.Snapping;
using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Represents a straight line entity defined by a start and end point in 3D space. Contains core CAD logic, but not rendering.
/// </summary>
public class LineEntity : CadEntity, ISnapProvider
{
    public Vector3 Start { get; }
    public Vector3 End { get; }

    /// <summary>
    /// Creates a line with the default user-visible name used for newly drawn lines.
    /// </summary>
    public LineEntity(Vector3 start, Vector3 end)
        : base("Line")
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Recreates a saved line while preserving the document identity and user-visible name.
    /// </summary>
    public static LineEntity CreateLoaded(Guid id, string name, Vector3 start, Vector3 end)
    {
        LineEntity line = new LineEntity(start, end);
        line.Id = id;
        line.Name = string.IsNullOrWhiteSpace(name) ? "Line" : name;
        return line;
    }

    /// <summary>
    /// Adds line-specific snap points used by drawing and editing tools.
    /// </summary>
    public void GetSnapPoints(List<SnapPoint> snapPoints)
    {
        // Endpoints
        snapPoints.Add(new SnapPoint(Start, SnapType.Endpoint));
        snapPoints.Add(new SnapPoint(End, SnapType.Endpoint));

        // Midpoint
        Vector3 mid = (Start + End) * 0.5f;
        snapPoints.Add(new SnapPoint(mid, SnapType.Midpoint));
    }

    /// <summary>
    /// Calculates the axis-aligned bounds for this line.
    /// </summary>
    public override (Vector3 Min, Vector3 Max) GetBounds()
    {
        Vector3 min = Vector3.Min(Start, End);
        Vector3 max = Vector3.Max(Start, End);
        return (min, max);
    }
}
