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

        // user.Id -> (marker key -> marker)
        private static readonly Dictionary<int, Dictionary<string, WorldMarker>> ActiveMarkers = new Dictionary<int, Dictionary<string, WorldMarker>>();
        private static readonly object MarkersLock = new object();

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

                if (!SpeciesTrackerData.Obj.UserConfiguration.TryGetValue(user.Id, out var config))
                {
                    ClearUser(user);
                    continue;
                }
                if (config.TrackedSpecies.Count == 0)
                {
                    ClearUser(user);
                    continue;
                }

                var wanted = new HashSet<string>(config.TrackedSpecies, StringComparer.OrdinalIgnoreCase);
                var playerPos = user.Position;
                var toKeep = new HashSet<string>();

                // Animals (land + aquatic), strongly typed via AnimalsManager.
                foreach (var animal in AnimalsManager.Obj.Animals)
                {
                    if (animal == null || !animal.Active || animal.Dead || animal.Species == null)
                        continue;
                    if (!wanted.Contains(animal.Species.Name))
                        continue;

                    var range = SpeciesTrackerManager.GetRange(user, animal.Species.Name);
                    if (World.WrappedDistance(animal.Position, playerPos) > range)
                        continue;

                    var key = "a" + animal.ID;
                    toKeep.Add(key);
                    UpdateMarker(user, key, animal.GroundPosition, animal.Species.Name, range);
                }

                // Plants (including trees). Plant positions are static voxel positions; the
                // marker follows the plant's own position.
                foreach (var plant in EcoSim.PlantSim.All)
                {
                    if (plant == null || plant.Species == null || plant.Dead)
                        continue;
                    if (!wanted.Contains(plant.Species.Name))
                        continue;

                    var range = SpeciesTrackerManager.GetRange(user, plant.Species.Name);
                    if (World.WrappedDistance(plant.Position, playerPos) > range)
                        continue;

                    var key = "p" + plant.Position.X + "," + plant.Position.Y + "," + plant.Position.Z;
                    toKeep.Add(key);
                    UpdateMarker(user, key, plant.Position, plant.Species.Name, range);
                }

                RemoveStale(user, toKeep);
            }
        }

        private static void UpdateMarker(User user, string key, Vector3 position, string speciesName, float range)
        {
            var player = user.Player;
            if (player == null || user.Markers == null)
                return;

            var posi = ToVector3i(position);
            lock (MarkersLock)
            {
                if (!ActiveMarkers.TryGetValue(user.Id, out var perUser))
                {
                    perUser = new Dictionary<string, WorldMarker>();
                    ActiveMarkers[user.Id] = perUser;
                }

                if (perUser.TryGetValue(key, out var existing))
                {
                    try
                    {
                        existing.SetPos(player, posi);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"SpeciesTracker: failed to move marker {key}: {ex.Message}");
                    }
                    return;
                }

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
                    //WorldMarkerUtils.MakeMarkerPersonal(user, marker);

                    perUser[key] = marker;
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
            Dictionary<string, WorldMarker>? perUser;
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
                WorldMarker? marker;
                lock (MarkersLock)
                {
                    if (!perUser.TryGetValue(key, out marker) || marker == null)
                        continue;
                }

                try
                {
                    marker.Remove(player);
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
            Dictionary<string, WorldMarker>? perUser;
            lock (MarkersLock)
            {
                if (!ActiveMarkers.TryGetValue(user.Id, out perUser) || perUser == null)
                    return;
                ActiveMarkers.Remove(user.Id);
            }

            var player = user.Player;
            if (player == null)
                return;

            foreach (var marker in perUser.Values)
            {
                try
                {
                    marker.Remove(player);
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
