using UnityEngine;
using System.Collections.Generic;

namespace HairSalonSim.Core
{
    public struct SpatialHashEntry
    {
        public int strandIndex;
        public int particleIndex;
    }

    public class SpatialHashGrid
    {
        private readonly float cellSize;
        private readonly float invCellSize;
        private readonly Dictionary<long, List<SpatialHashEntry>> cells;

        public SpatialHashGrid(float cellSize)
        {
            this.cellSize = cellSize;
            this.invCellSize = 1f / cellSize;
            this.cells = new Dictionary<long, List<SpatialHashEntry>>();
        }

        public void Clear()
        {
            foreach (var list in cells.Values)
            {
                list.Clear();
            }
        }

        public void Insert(Vector3 position, int strandIndex, int particleIndex)
        {
            long key = HashPosition(position);
            if (!cells.TryGetValue(key, out var list))
            {
                list = new List<SpatialHashEntry>();
                cells[key] = list;
            }
            list.Add(new SpatialHashEntry { strandIndex = strandIndex, particleIndex = particleIndex });
        }

        public List<SpatialHashEntry> Query(Vector3 position, float radius)
        {
            var results = new List<SpatialHashEntry>();
            int cellRadius = Mathf.CeilToInt(radius * invCellSize);

            int cx = Mathf.FloorToInt(position.x * invCellSize);
            int cy = Mathf.FloorToInt(position.y * invCellSize);
            int cz = Mathf.FloorToInt(position.z * invCellSize);

            for (int x = cx - cellRadius; x <= cx + cellRadius; x++)
            {
                for (int y = cy - cellRadius; y <= cy + cellRadius; y++)
                {
                    for (int z = cz - cellRadius; z <= cz + cellRadius; z++)
                    {
                        long key = HashCoords(x, y, z);
                        if (cells.TryGetValue(key, out var list))
                        {
                            results.AddRange(list);
                        }
                    }
                }
            }

            return results;
        }

        private long HashPosition(Vector3 position)
        {
            int x = Mathf.FloorToInt(position.x * invCellSize);
            int y = Mathf.FloorToInt(position.y * invCellSize);
            int z = Mathf.FloorToInt(position.z * invCellSize);
            return HashCoords(x, y, z);
        }

        private static long HashCoords(int x, int y, int z)
        {
            unchecked
            {
                long hash = (long)x * 73856093L ^ (long)y * 19349663L ^ (long)z * 83492791L;
                return hash;
            }
        }
    }
}
