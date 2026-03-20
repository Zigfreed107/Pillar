using CadApp.Core.Entities;
using CadApp.Core.Snapping;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Principal;

public class LineEntity : CadEntity, ISnapProvider
{
    public Vector3 Start { get; }
    public Vector3 End { get; }

    public LineEntity(Vector3 start, Vector3 end)
    {
        Start = start;
        End = end;
    }

    public void GetSnapPoints(List<SnapPoint> snapPoints)
    {
        // Endpoints
        snapPoints.Add(new SnapPoint(Start, SnapType.Endpoint));
        snapPoints.Add(new SnapPoint(End, SnapType.Endpoint));

        // Midpoint
        var mid = (Start + End) * 0.5f;
        snapPoints.Add(new SnapPoint(mid, SnapType.Midpoint));
    }
}