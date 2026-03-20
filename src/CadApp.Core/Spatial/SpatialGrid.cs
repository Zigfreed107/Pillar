using CadApp.Core.Entities;
using System.Collections.Generic;
using System.Numerics;

namespace CadApp.Core.Spatial
{
    public class SpatialGrid
    {
        private readonly Dictionary<GridKey, List<CadEntity>> _cells = new();

        public float CellSize { get; }

        public SpatialGrid(float cellSize)
        {
            CellSize = cellSize;
        }

        // -----------------------------
        // INSERT
        // -----------------------------
        public void Insert(CadEntity entity, Vector3 position)
        {
            GridKey key = ToKey(position);

            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<CadEntity>(4);
                _cells[key] = list;
            }

            list.Add(entity);
        }

        // -----------------------------
        // REMOVE
        // -----------------------------
        public void Remove(CadEntity entity, Vector3 position)
        {
            GridKey key = ToKey(position);

            if (_cells.TryGetValue(key, out var list))
            {
                list.Remove(entity);

                if (list.Count == 0)
                {
                    _cells.Remove(key);
                }
            }
        }

        // -----------------------------
        // QUERY (NO ALLOCATIONS)
        // -----------------------------
        public void Query(
            Vector3 position,
            float radius,
            List<CadEntity> results)
        {
            int range = (int)(radius / CellSize) + 1;

            var center = ToKey(position);

            for (int x = -range; x <= range; x++)
                for (int y = -range; y <= range; y++)
                    for (int z = -range; z <= range; z++)
                    {
                        var key = new GridKey(
                            center.X + x,
                            center.Y + y,
                            center.Z + z);

                        if (_cells.TryGetValue(key, out var list))
                        {
                            // NO allocations, just copy references
                            results.AddRange(list);
                        }
                    }
        }

        // -----------------------------
        // HELPERS
        // -----------------------------
        private GridKey ToKey(Vector3 position)
        {
            int x = (int)(position.X / CellSize);
            int y = (int)(position.Y / CellSize);
            int z = (int)(position.Z / CellSize);

            return new GridKey(x, y, z);
        }
    }
}