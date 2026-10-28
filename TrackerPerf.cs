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
}
