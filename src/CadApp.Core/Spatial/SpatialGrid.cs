using CadApp.Core.Entities;
using System.Collections.Generic;
using System.Numerics;

namespace CadApp.Core.Spatial
{
    //TODO applies an XYZ axis aligned grid, but we could also consider an adaptive grid that follows the geometry better (only grids cells that the object passes through...)

    /// <summary>
    /// Divides the document up into smaller spatial regions so that we can quickly query for nearby entities.
    /// </summary>
    public class SpatialGrid
    {
        private readonly Dictionary<GridKey, List<CadEntity>> _cells = new();

        public float CellSize { get; }

        public SpatialGrid(float cellSize)
        {
            CellSize = cellSize;
        }

        /// <summary>
        /// Inserts an entity into all grid cells overlapped by its bounding box.
        /// </summary>
        public void Insert(CadEntity entity)
        {
            (Vector3 min, Vector3 max) = entity.GetBounds();

            GridKey minKey = ToKey(min);
            GridKey maxKey = ToKey(max);

            for (int x = minKey.X; x <= maxKey.X; x++)
                for (int y = minKey.Y; y <= maxKey.Y; y++)
                    for (int z = minKey.Z; z <= maxKey.Z; z++)
                    {
                        GridKey key = new GridKey(x, y, z);

                        if (!_cells.TryGetValue(key, out List<CadEntity>? list))
                        {
                            list = new List<CadEntity>(4);
                            _cells[key] = list;
                        }

                        list.Add(entity);
                    }
        }

        /// <summary>
        /// Removes an entity into all grid cells overlapped by its bounding box.
        /// </summary>
        /// <param name="entity"></param>
        public void Remove(CadEntity entity)
        {
            (Vector3 min, Vector3 max) = entity.GetBounds();

            GridKey minKey = ToKey(min);
            GridKey maxKey = ToKey(max);

            for (int x = minKey.X; x <= maxKey.X; x++)
                for (int y = minKey.Y; y <= maxKey.Y; y++)
                    for (int z = minKey.Z; z <= maxKey.Z; z++)
                    {
                        GridKey key = new GridKey(x, y, z);

                        if (_cells.TryGetValue(key, out List<CadEntity>? list))
                        {
                            list.Remove(entity);

                            if (list.Count == 0)
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