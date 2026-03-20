using CadApp.Core.Entities;
using System;
using System.Collections.Generic;
using System.Numerics;
using CadApp.Core.Spatial;

namespace CadApp.Core.Snapping
{
    public class SnapManager
    {
        // Reusable buffer → NO allocations per frame
        private readonly List<SnapPoint> _snapBuffer = new(64);
        private readonly SpatialGrid _grid;
        private readonly List<CadEntity> _queryBuffer = new(64);

        // Max snap distance (tune later)
        public float MaxSnapDistance { get; set; } = 0.5f;

        public SnapManager(SpatialGrid grid)
        {
            _grid = grid;
        }

        public bool TryGetSnap(
            Vector3 cursorWorldPosition,
            IEnumerable<CadEntity> entities,
            out SnapResult result)
        {
            _snapBuffer.Clear();

            _grid.Query(cursorWorldPosition, MaxSnapDistance, _queryBuffer);

            float closestDistSq = MaxSnapDistance * MaxSnapDistance;
            SnapPoint? bestPoint = null;

            foreach (var entity in _queryBuffer)
            {
                if (entity is not ISnapProvider snapProvider)
                    continue;

                snapProvider.GetSnapPoints(_snapBuffer);
            }

            result = default;
            return false;
        }
    }
}