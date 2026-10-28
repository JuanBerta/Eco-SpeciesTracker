using Eco.Gameplay.Players;
using Eco.Gameplay.UI.WorldMarker;
using Eco.Shared.Logging;
using Eco.Shared.Math;
using Eco.Shared.Utils;
using Eco.Shared.Voxel;
using Eco.Simulation;
using Eco.Simulation.Animals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SpeciesTracker
{
    /// <summary>
    /// Maintains temporary dynamic world map markers (visible on the M-key map and the
    /// minimap) for every instance of a tracked species inside the player's detection range.
    ///
    /// Performance notes (0.2.0):
    ///  - Plants are enumerated through PlantSim.PlantsWithinRange (spatial index), so the
    ///    cost scales with the population INSIDE the range instead of the whole map. The old
    ///    EcoSim.PlantSim.All scan made 150-500m ranges freeze: every 0.5s tick walked tens of
    ///    thousands of plants for every online user.
    ///  - Animals have no public spatial index (AnimalsManager exposes only the full
    ///    ThreadSafeList), so they are still scanned globally, but with squared-distance tests
    ///    (World.WrappedDistanceSq) and a single per-user range lookup instead of per-animal.
    ///  - Marker positions are cached: static plants (and stationary animals) no longer emit a
    ///    SetPos network call every tick.
    ///  - Defensive caps (MaxMarkersPerUser / MaxNewMarkersPerTick) flatten marker-creation
    ///    storms at large ranges, which prevents server/client hitches.
    ///
    /// Lessons from HunterTracking 0.1.7 - 0.1.8:
    ///  - A freshly created marker is INVISIBLE until SetVisiblity(WorldMarkerVisibility.ShowInTheWorldAndMinimap)
    ///    is called, otherwise the client never registers it for rendering.
    ///  - WorldMarker.IconNameOverride is get-only; the server cannot pick a custom icon.
    ///    The client resolves the icon from its own built-in icon library (icons_assets_all_*.bundle)
    ///    based on the marker object, so no icon name is set here (verified against the
    ///    Eco 0.14.2.1 reference assemblies).
    ///  - Animals are not WorldObjects, so they can only be surfaced to clients through WorldMarkers.
    ///  - WorldMarkerUtils.MakeMarkerPersonal(user, marker) is required for per-player visibility.
    /// </summary>
    public static class TrackerMarkerService
    {
        private const double TickIntervalSeconds = 0.5;
        private static DateTime lastTick = DateTime.MinValue;

        // user.Id -> (marker key -> marker + last known voxel position)
        private sealed class MarkerEntry
        {
            public WorldMarker Marker = null!;
            public Vector3i LastPos;
        }

        private static readonly Dictionary<int, Dictionary<string, MarkerEntry>> ActiveMarkers = new Dictionary<int, Dictionary<string, MarkerEntry>>();
        private static readonly object MarkersLock = new object();

        // Per-user "tracked species" hash-set cache. Rebuilt only when the fingerprint of the
        // tracked-species list changes, instead of every tick.
        private static readonly Dictionary<int, HashSet<string>> WantedCache = new Dictionary<int, HashSet<string>>();
        private static readonly Dictionary<int, ulong> WantedFingerprints = new Dictionary<int, ulong>();
        private static readonly object CacheLock = new object();

        public static void Tick()
        {
            var now = DateTime.UtcNow;
            if ((now - lastTick).TotalSeconds < TickIntervalSeconds)
                return;
            lastTick = now;

            foreach (var user in UserManager.Users)
            {
                if (user == null || !user.IsActive || user.Markers == null || user.Player == null)
                    continue;

                if (!SpeciesTrackerData.Obj.UserConfiguration.TryGetValue(user.Id, out var config) || config.TrackedSpecies.Count == 0)
                {
                    ClearUser(user);
                    continue;
                }

                var wanted = GetWantedSet(user.Id, config);
                if (wanted.Count == 0)
                {
                    ClearUser(user);
                    continue;
                }

                var playerPos = user.Position;
                var range = SpeciesTrackerManager.GetRange(user); // one lookup per user per tick
                var rangeSq = range * range;
                var toKeep = new HashSet<string>();
                var createdThisTick = 0;

                // Animals (land + aquatic): no public spatial index, scan the full list.
                // The wanted-set check + squared distance keep each iteration extremely cheap.
                foreach (var animal in AnimalsManager.Obj.Animals)
                {
                    if (animal == null || !animal.Active || animal.Dead || animal.Species == null)
                        continue;
                    if (!wanted.Contains(animal.Species.Name))
                        continue;
                    if (World.WrappedDistanceSq(animal.Position, playerPos) > rangeSq)
                        continue;

                    var key = TrackerPerf.AnimalKey(animal.ID);
                    toKeep.Add(key);
                    UpdateMarker(user, key, animal.GroundPosition, animal.Species.Name, range, ref createdThisTick);
                }

                // Plants (including trees): spatial index lookup. Only the in-range population is
                // visited; the species filter runs inside the query predicate.
                foreach (var plant in EcoSim.PlantSim.PlantsWithinRange(playerPos, range,
                             p => p != null && p.Species != null && !p.Dead && wanted.Contains(p.Species.Name)))
                {
                    var key = TrackerPerf.PlantKey(plant.Position);
                    toKeep.Add(key);
                    UpdateMarker(user, key, plant.Position, plant.Species.Name, range, ref createdThisTick);
                }

                RemoveStale(user, toKeep);
            }
        }

        private static HashSet<string> GetWantedSet(int userId, UserConfig config)
        {
            // FNV-1a fingerprint of the tracked-species list; rebuild the hash set only when it changes.
            var fingerprint = TrackerPerf.Fingerprint(config.TrackedSpecies);

            lock (CacheLock)
            {
                if (WantedFingerprints.TryGetValue(userId, out var previous) && previous == fingerprint)
                    return WantedCache[userId];

                var set = new HashSet<string>(config.TrackedSpecies, StringComparer.OrdinalIgnoreCase);
                WantedFingerprints[userId] = fingerprint;
                WantedCache[userId] = set;
                return set;
            }
        }

        private static void UpdateMarker(User user, string key, Vector3 position, string speciesName, float range, ref int createdThisTick)
        {
            var player = user.Player;
            if (player == null || user.Markers == null)
                return;

            var posi = ToVector3i(position);
            lock (MarkersLock)
            {
                if (!ActiveMarkers.TryGetValue(user.Id, out var perUser))
                {
                    perUser = new Dictionary<string, MarkerEntry>();
                    ActiveMarkers[user.Id] = perUser;
                }

                if (perUser.TryGetValue(key, out var entry))
                {
                    // Only push a network update when the marker actually moved. Plants are static,
                    // so this skips the vast majority of per-tick SetPos calls at large ranges.
                    if (entry.LastPos != posi)
                    {
                        try
                        {
                            entry.Marker.SetPos(player, posi);
                            entry.LastPos = posi;
                        }
                        catch (Exception ex)
                        {
                            Log.Debug($"SpeciesTracker: failed to move marker {key}: {ex.Message}");
                        }
                    }
                    return;
                }

                // Defensive caps: skip creation when at the per-user ceiling or over this tick's budget.
                // In-range entities stay in toKeep, so they are not removed as stale and are retried
                // on later ticks once budget/ceiling frees up.
                if (!TrackerPerf.CanCreateMarker(perUser.Count, createdThisTick))
                    return;

                WorldMarker? marker = null;
                try
                {
                    marker = user.Markers.Add(user, posi, speciesName, false, false, MarkerFolderName.None);
                    if (marker == null)
                        return;

                    // Critical: without SetVisiblity the marker is created but never rendered.
                    marker.SetVisiblity(WorldMarkerVisibility.ShowInTheWorldAndMinimap);
                    marker.SetColor(player, Color.NeonGreen);
                    marker.SetDisplayName(player, speciesName);
                    marker.SetMaxDistanceToShow(player, range);

                    // Icons come from the client's built-in icon library; IconNameOverride is read-only.
                    // Add() already placed the marker into this user's personal Markers list
                    // (WorldMarkerManager.Add -> List.Add), so MakeMarkerPersonal is not needed.
                    //WorldMarkerUtils.MakeMarkerPersonal(user, marker);

                    perUser[key] = new MarkerEntry { Marker = marker, LastPos = posi };
                    createdThisTick++;
                }
                catch (Exception ex)
                {
                    Log.Debug($"SpeciesTracker: failed to create marker for {speciesName}: {ex.Message}");
                    if (marker != null)
                    {
                        try { marker.Remove(player); } catch { }
                    }
                }
            }
        }

        private static void RemoveStale(User user, HashSet<string> toKeep)
        {
            Dictionary<string, MarkerEntry>? perUser;
            lock (MarkersLock)
            {
                if (!ActiveMarkers.TryGetValue(user.Id, out perUser) || perUser == null)
                    return;
            }

            var player = user.Player;
            if (player == null)
                return;

            List<string> stale;
            lock (MarkersLock)
            {
                stale = perUser.Keys.Where(k => !toKeep.Contains(k)).ToList();
            }

            foreach (var key in stale)
            {
                MarkerEntry? entry;
                lock (MarkersLock)
                {
                    if (!perUser.TryGetValue(key, out entry) || entry == null)
                        continue;
                }

                try
                {
                    entry.Marker.Remove(player);
                }
                catch (Exception ex)
                {
                    Log.Debug($"SpeciesTracker: failed to remove marker {key}: {ex.Message}");
                }

                lock (MarkersLock)
                {
                    perUser.Remove(key);
                    if (perUser.Count == 0)
                        ActiveMarkers.Remove(user.Id);
                }
            }
        }

        /// <summary>Removes every marker currently shown to a player (on untrack, clear, or disconnect).</summary>
        public static void ClearUser(User user)
        {
            Dictionary<string, MarkerEntry>? perUser;
            lock (MarkersLock)
            {
                if (!ActiveMarkers.TryGetValue(user.Id, out perUser) || perUser == null)
                    return;
                ActiveMarkers.Remove(user.Id);
            }

            lock (CacheLock)
            {
                WantedCache.Remove(user.Id);
                WantedFingerprints.Remove(user.Id);
            }

            var player = user.Player;
            if (player == null)
                return;

            foreach (var entry in perUser.Values)
            {
                try
                {
                    entry.Marker.Remove(player);
                }
                catch (Exception ex)
                {
                    Log.Debug($"SpeciesTracker: failed to clear marker: {ex.Message}");
                }
            }
        }

        private static Vector3i ToVector3i(Vector3 v) =>
            new Vector3i((int)Math.Floor(v.X), (int)Math.Floor(v.Y), (int)Math.Floor(v.Z));
    }
}
