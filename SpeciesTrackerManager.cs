using Eco.Gameplay.Players;
using Eco.Simulation.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeciesTracker
{
    /// <summary>
    /// Core logic for the species tracking lists. Works with the strongly typed
    /// <see cref="Species"/> class so both animals (AnimalSpecies) and plants
    /// (PlantSpecies, including TreeSpecies) can be tracked.
    /// </summary>
    public static class SpeciesTrackerManager
    {
        public const float DefaultRange = 150f;
        public const float MinRange = 1f;
        public const float MaxRange = 500f;

        private static UserConfig GetConfig(User user)
        {
            var configs = SpeciesTrackerData.Obj.UserConfiguration;
            if (!configs.TryGetValue(user.Id, out var config))
            {
                config = new UserConfig();
                configs[user.Id] = config;
            }
            return config;
        }

        public static bool AddTracking(User user, Species species)
        {
            if (species == null || string.IsNullOrWhiteSpace(species.Name)) return false;
            var config = GetConfig(user);
            if (config.TrackedSpecies.Any(n => string.Equals(n, species.Name, StringComparison.OrdinalIgnoreCase)))
                return false;
            config.TrackedSpecies.Add(species.Name);
            return true;
        }

        public static bool RemoveTracking(User user, Species species) =>
            RemoveTracking(user, species?.Name ?? string.Empty);

        public static bool RemoveTracking(User user, string speciesName)
        {
            if (string.IsNullOrWhiteSpace(speciesName)) return false;
            var config = GetConfig(user);
            var match = config.TrackedSpecies.FirstOrDefault(n => string.Equals(n, speciesName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match == null) return false;
            config.TrackedSpecies.Remove(match);
            return true;
        }

        public static bool IsTracking(User user, string speciesName)
        {
            if (string.IsNullOrWhiteSpace(speciesName)) return false;
            var config = GetConfig(user);
            return config.TrackedSpecies.Any(n => string.Equals(n, speciesName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static bool SetRange(User user, float range)
        {
            if (range < MinRange || range > MaxRange) return false;
            var config = GetConfig(user);
            config.CustomRange = range;
            return true;
        }

        public static float GetRange(User user)
        {
            var config = GetConfig(user);
            if (config.CustomRange is float range)
                return Math.Clamp(range, MinRange, MaxRange);
            return DefaultRange;
        }

        public static List<string> GetTrackedSpeciesNames(User user)
        {
            if (!SpeciesTrackerData.Obj.UserConfiguration.TryGetValue(user.Id, out var config))
                return new List<string>();
            return config.TrackedSpecies.ToList();
        }

        public static void Clear(User user)
        {
            var config = GetConfig(user);
            config.TrackedSpecies.Clear();
        }
    }
}
