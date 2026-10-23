using Eco.Core.Plugins;
using Eco.Core.Plugins.Interfaces;
using Eco.Shared.Localization;
using Eco.Shared.Logging;
using Eco.Shared.Utils;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
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
        private const int PollSleepMilliseconds = 50;
        private volatile bool shuttingDown;

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
            // IThreadedPlugin.Run is invoked EXACTLY ONCE on a dedicated background thread by
            // PluginManager (EcoServer.StartPlugins: new Thread(threadedPlugin.Run), verified
            // against the Eco 0.14.2.1 reference assemblies). A single returning call would
            // kill the thread: the marker pass would only run once at startup (before any
            // player has configured tracking) and no WorldMarker would ever be created or
            // refreshed. The loop below is therefore mandatory for the periodic refresh.
            while (!shuttingDown)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    if ((now - lastMarkerPass).TotalSeconds >= MarkerPassIntervalSeconds)
                    {
                        lastMarkerPass = now;
                        TrackerMarkerService.Tick();
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteErrorLineLoc($"SpeciesTracker marker pass failed: {ex.Message}");
                }
                Thread.Sleep(PollSleepMilliseconds);
            }
        }

        public Task ShutdownAsync()
        {
            shuttingDown = true;
            return Task.CompletedTask;
        }

        public override string ToString() => Localizer.DoStr("Species Tracker");
    }
}
