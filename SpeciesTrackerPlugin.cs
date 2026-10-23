using Eco.Core.Plugins;
using Eco.Core.Plugins.Interfaces;
using Eco.Shared.Localization;
using Eco.Shared.Logging;
using Eco.Shared.Utils;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace SpeciesTracker
{
    /// <summary>
    /// Plugin entry point. Loads persisted data on startup, saves it on shutdown,
    /// and drives the periodic marker refresh pass.
    /// </summary>
    public class SpeciesTrackerPlugin : Singleton<SpeciesTrackerPlugin>, IModKitPlugin, ISaveablePlugin, IThreadedPlugin
    {
        [NotNull] private readonly SpeciesTrackerData data;
        private DateTime lastMarkerPass = DateTime.MinValue;
        private const double MarkerPassIntervalSeconds = 0.5;

        public SpeciesTrackerPlugin()
        {
            data = StorageManager.LoadOrCreate<SpeciesTrackerData>("SpeciesTracker");
        }

        public string GetCategory() => Localizer.DoStr("Mods");
        public string GetStatus() => Localizer.DoStr("Species Tracker active");

        public void SaveAll()
        {
            StorageManager.Obj.MarkDirty(data);
        }

        public void Run()
        {
            var now = DateTime.UtcNow;
            if ((now - lastMarkerPass).TotalSeconds < MarkerPassIntervalSeconds)
                return;
            lastMarkerPass = now;
            try
            {
                TrackerMarkerService.Tick();
            }
            catch (Exception ex)
            {
                Log.WriteErrorLineLoc($"SpeciesTracker marker pass failed: {ex.Message}");
            }
        }

        public Task ShutdownAsync() => Task.CompletedTask;

        public override string ToString() => Localizer.DoStr("Species Tracker");
    }
}
