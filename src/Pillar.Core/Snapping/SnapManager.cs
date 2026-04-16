using Pillar.Core.Entities;
using Pillar.Core.Spatial;
using System;
using System.Collections.Generic;
using System.Numerics;


namespace Pillar.Core.Snapping
{
    public class SnapManager
    {
        // Reusable buffer → NO allocations per frame
        private readonly List<SnapPoint> _snapBuffer = new(64);
        private readonly SpatialGrid _grid;
        private readonly List<CadEntity> _queryBuffer = new(64);
        private readonly List<CadEntity> _entityBuffer = new(64);


        // Max snap distance (tune later)
        public float MaxSnapDistance { get; set; } = 0.5f;

        public SnapManager(SpatialGrid grid)
        {
            _grid = grid;
        }

        public bool TryGetSnap(
            Vector3 cursorWorldPosition,
            out SnapResult result)
        {
            _entityBuffer.Clear();
            _snapBuffer.Clear();

            float maxDistSq = MaxSnapDistance * MaxSnapDistance;
            float closestDistSq = maxDistSq;

            SnapPoint? bestPoint = null;

            _grid.Query(cursorWorldPosition, MaxSnapDistance, _entityBuffer);

            foreach (var entity in _entityBuffer)
            {
                if (entity is not ISnapProvider snapProvider)
                    continue;

                snapProvider.GetSnapPoints(_snapBuffer);
            }

            foreach (var snap in _snapBuffer)
            {
                float distSq = Vector3.DistanceSquared(cursorWorldPosition, snap.Position);

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    bestPoint = snap;
                }
            }

            if (bestPoint.HasValue)
            {
                result = new SnapResult(
                    bestPoint.Value.Position,
                    bestPoint.Value.Type,
                    MathF.Sqrt(closestDistSq));

                return true;
            }

            result = default;
            return false;
        }
    }
}