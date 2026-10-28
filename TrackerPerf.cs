using System;
using System.Collections.Generic;
using System.Numerics;

namespace SpeciesTracker
{
    /// <summary>
    /// Pure, Eco-free performance helpers extracted from TrackerMarkerService so they can be
    /// unit-tested without a running game (introduced with the 0.2.0 perf overhaul).
    /// </summary>
    internal static class TrackerPerf
    {
        /// <summary>Hard ceiling of live markers per player. Prevents 500m ranges from pushing
        /// thousands of network-synced markers and freezing the server/client.</summary>
        public const int MaxMarkersPerUser = 400;

        /// <summary>How many NEW markers a single tick may create per player. Spreads the
        /// initial creation burst over several ticks instead of one freeze frame.</summary>
        public const int MaxNewMarkersPerTick = 60;

        /// <summary>Squared-distance range test. Equivalent to dist &lt;= range without a sqrt.</summary>
        public static bool WithinRangeSq(float rangeSq, float distSq) => distSq <= rangeSq;

        /// <summary>Marker creation gate: only create when under the per-user ceiling AND under
        /// this tick's creation budget.</summary>
        public static bool CanCreateMarker(int perUserCount, int createdThisTick) =>
            perUserCount < MaxMarkersPerUser && createdThisTick < MaxNewMarkersPerTick;

        /// <summary>Animals move, so a marker position update is only pushed when the animal has
        /// actually travelled at least this far since the last push. Cuts per-tick SetPos network
        /// calls for herds inside a 500m range from "every tick" to "only when it matters".</summary>
        public const float AnimalMoveUpdateThresholdMeters = 2f;

        /// <summary>Squared-distance gate for animal marker position updates.</summary>
        public static bool ShouldUpdateAnimalMarker(float lastDistSq) =>
            lastDistSq > AnimalMoveUpdateThresholdMeters * AnimalMoveUpdateThresholdMeters;

        /// <summary>Marker key for an animal. MUST stay stable across versions so existing
        /// markers are reused instead of being removed and recreated (visible flicker).</summary>
        public static string AnimalKey(int id) => $"a{id}";

        /// <summary>Marker key for a plant (voxel position). MUST stay stable across versions.</summary>
        public static string PlantKey(Vector3 p) => $"p{p.X},{p.Y},{p.Z}";

        /// <summary>FNV-1a 64-bit fingerprint used to detect tracked-species list changes so the
        /// wanted hash-set cache is only rebuilt when necessary.</summary>
        public static ulong Fingerprint(IEnumerable<string> names)
        {
            ulong h = 1469598103934665603UL;
            foreach (var name in names)
            {
                foreach (var ch in name)
                {
                    h ^= ch;
                    h *= 1099511628211UL;
                }
                h ^= 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }
    }

    /// <summary>
    /// Minimal uniform-grid spatial hash used to turn the per-tick animal scan from
    /// O(all animals) into O(animals inside the query range). Pure and Eco-free so it
    /// can be unit-tested alongside the rest of <see cref="TrackerPerf"/>.
    /// <para>Cells wrap around the world edges when a positive cell count is provided
    /// (<paramref name="cellsX"/>/<paramref name="cellsZ"/>), matching Eco's wrapped world.</para>
    /// </summary>
    internal sealed class SpatialHashGrid<T>
    {
        private readonly float _cellSize;
        private readonly int _cellsX;
        private readonly int _cellsZ;
        private readonly Dictionary<long, List<T>> _cells = new();

        public SpatialHashGrid(float cellSize, int cellsX = 0, int cellsZ = 0)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            _cellSize = cellSize;
            _cellsX = cellsX;
            _cellsZ = cellsZ;
        }

        public int CellCount => _cells.Count;

        public void Clear() => _cells.Clear();

        public void Add(Vector3 pos, T item)
        {
            var key = Key(Wrap(ToCell(pos.X), _cellsX), Wrap(ToCell(pos.Z), _cellsZ));
            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<T>(4);
                _cells[key] = list;
            }
            list.Add(item);
        }

        /// <summary>Enumerates candidates whose cells could intersect the circle centered at
        /// <paramref name="center"/> with radius <paramref name="radius"/>. Callers must still
        /// apply an exact distance filter; this only prunes cells.</summary>
        public IEnumerable<T> Query(Vector3 center, float radius)
        {
            int span = Math.Max(0, (int)Math.Ceiling(radius / _cellSize));
            int cx = ToCell(center.X);
            int cz = ToCell(center.Z);
            for (int dz = -span; dz <= span; dz++)
            {
                for (int dx = -span; dx <= span; dx++)
                {
                    var key = Key(Wrap(cx + dx, _cellsX), Wrap(cz + dz, _cellsZ));
                    if (_cells.TryGetValue(key, out var list))
                    {
                        foreach (var item in list)
                            yield return item;
                    }
                }
            }
        }

        public static int ToCell(float coord, float cellSize) => (int)Math.Floor(coord / cellSize);

        private int ToCell(float coord) => ToCell(coord, _cellSize);

        private static int Wrap(int cell, int count) =>
            count <= 0 ? cell : ((cell % count) + count) % count;

        private static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;
    }
}
