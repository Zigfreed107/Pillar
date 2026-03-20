using CadApp.Core.Document;
using CadApp.Core.Snapping;
using System.Collections.Generic;
using System.Numerics;

namespace CadApp.Rendering.Snapping;

public class SnapManager
{
    private readonly CadDocument _document;

    private readonly List<SnapPoint> _snapPoints = new();

    public SnapManager(CadDocument document)
    {
        _document = document;

        Rebuild();
        _document.Entities.CollectionChanged += (_, __) => Rebuild();
    }

    private void Rebuild()
    {
        _snapPoints.Clear();

        foreach (var entity in _document.Entities)
        {
            _snapPoints.AddRange(entity.GetSnapPoints());
        }
    }

    public bool TrySnap(Vector3 worldPos, float threshold, out SnapPoint snap)
    {
        snap = default;
        float bestDist = float.MaxValue;
        bool found = false;

        foreach (var sp in _snapPoints)
        {
            float dist = Vector3.Distance(sp.Position, worldPos);

            if (dist < threshold && dist < bestDist)
            {
                bestDist = dist;
                snap = sp;
                found = true;
            }
        }

        return found;
    }
}