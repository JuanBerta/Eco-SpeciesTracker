using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.Messaging.Chat.Commands;
using Eco.Shared.Localization;
using Eco.Shared.Services;
using Eco.Simulation;
using Eco.Simulation.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpeciesTracker
{
    /// <summary>
    /// Chat commands for managing tracked species. Every instance of a tracked species
    /// inside the configured range shows a temporary personal map marker (M-key map + minimap).
    /// </summary>
    [ChatCommandHandler]
    public static class TrackerCommands
    {
        [ChatCommand("Track animals and plants: see every instance of a tracked species as a personal map marker on the M-key map and minimap within a configurable range.", "track")]
        public static void TrackSpecies() { }

        [ChatSubCommand(nameof(TrackSpecies), "help", ChatAuthorizationLevel.User)]
        public static void Help(User user)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Localizer.DoStr("/trackspecies help - this help"));
            sb.AppendLine(Localizer.DoStr("/trackspecies species - list every trackable species (animals and plants)"));
            sb.AppendLine(Localizer.DoStr("/trackspecies add <species> - start tracking a species"));
            sb.AppendLine(Localizer.DoStr("/trackspecies remove <species> - stop tracking a species"));
            sb.AppendLine(Localizer.DoStr("/trackspecies range <species> <meters> - set the detection range for a species (1-500, default 50)"));
            sb.AppendLine(Localizer.DoStr("/trackspecies list - show your tracked species and their ranges"));
            sb.AppendLine(Localizer.DoStr("/trackspecies clear - stop tracking every species"));
            user.Player.MsgLoc($"{sb.ToStringLoc()}", NotificationStyle.Chat);
        }

        [ChatSubCommand(nameof(TrackSpecies), "species", ChatAuthorizationLevel.User)]
        public static void Species(User user)
        {
            var all = EcoSim.AllSpecies
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Name))
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (all.Count == 0)
            {
                user.Player.ErrorLoc($"No species are available in the simulation database.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(Localizer.DoStr("Trackable species:"));
            foreach (var s in all)
            {
                sb.AppendLineLoc($" - {s.Name} [{Describe(s)}]");
            }
            user.Player.MsgLoc($"{sb.ToStringLoc()}", NotificationStyle.Chat);
        }

        [ChatSubCommand(nameof(TrackSpecies), "add", ChatAuthorizationLevel.User)]
        public static void Add(User user, string speciesName)
        {
            var species = FindSpecies(speciesName);
            if (species == null)
            {
                user.Player.ErrorLoc($"No species matching '{speciesName}' was found. Run /trackspecies species to see what is available.");
                return;
            }

            if (SpeciesTrackerManager.AddTracking(user, species))
                user.Player.MsgLoc($"Now tracking {species.Name}.", NotificationStyle.Chat);
            else
                user.Player.ErrorLoc($"You are already tracking {species.Name}.");
        }

        [ChatSubCommand(nameof(TrackSpecies), "remove", ChatAuthorizationLevel.User)]
        public static void Remove(User user, string speciesName)
        {
            if (SpeciesTrackerManager.RemoveTracking(user, speciesName))
                user.Player.MsgLoc($"Stopped tracking {speciesName.Trim()}.", NotificationStyle.Chat);
            else
                user.Player.ErrorLoc($"'{speciesName}' is not in your tracked list. Run /trackspecies list to see what you track.");
        }

        [ChatSubCommand(nameof(TrackSpecies), "range", ChatAuthorizationLevel.User)]
        public static void Range(User user, string speciesName, float meters)
        {
            if (meters < SpeciesTrackerManager.MinRange || meters > SpeciesTrackerManager.MaxRange)
            {
                user.Player.ErrorLoc($"Range must be between {SpeciesTrackerManager.MinRange:0} and {SpeciesTrackerManager.MaxRange:0} meters.");
                return;
            }

            if (!SpeciesTrackerManager.IsTracking(user, speciesName))
            {
                user.Player.ErrorLoc($"'{speciesName}' is not in your tracked list. Add it first with /trackspecies add <species>.");
                return;
            }

            if (SpeciesTrackerManager.SetRange(user, speciesName, meters))
                user.Player.MsgLoc($"Detection range for {speciesName.Trim()} set to {meters:0} m.", NotificationStyle.Chat);
            else
                user.Player.ErrorLoc($"Could not update the range. Please provide a valid species name.");
        }

        [ChatSubCommand(nameof(TrackSpecies), "list", ChatAuthorizationLevel.User)]
        public static void List(User user)
        {
            var names = SpeciesTrackerManager.GetTrackedSpeciesNames(user);
            if (names.Count == 0)
            {
                user.Player.ErrorLoc($"You are not tracking any species. Add one with /trackspecies add <species>.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(Localizer.DoStr("Tracked species:"));
            foreach (var name in names)
            {
                sb.AppendLineLoc($" - {name}: {SpeciesTrackerManager.GetRange(user, name):0} m");
            }
            user.Player.MsgLoc($"{sb.ToStringLoc()}", NotificationStyle.Chat);
        }

        [ChatSubCommand(nameof(TrackSpecies), "clear", ChatAuthorizationLevel.User)]
        public static void Clear(User user)
        {
            SpeciesTrackerManager.Clear(user);
            TrackerMarkerService.ClearUser(user);
            user.Player.MsgLoc($"Tracking list cleared.", NotificationStyle.Chat);
        }

        private static string Describe(Species s)
        {
            if (s is AnimalSpecies a) return a.Aquatic ? "aquatic animal" : "land animal";
            if (s is PlantSpecies p)
            {
                if (p is TreeSpecies) return "tree";
                return p.Water ? "aquatic plant" : "plant";
            }
            return "species";
        }

        private static Species? FindSpecies(string input)
        {
            var wanted = input.Trim();
            if (wanted.Length == 0) return null;

            return EcoSim.AllSpecies.FirstOrDefault(s =>
                s != null &&
                (s.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase) ||
                 s.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase) ||
                 wanted.Contains(s.Name, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
