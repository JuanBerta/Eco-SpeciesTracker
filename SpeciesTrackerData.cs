using Eco.Core.Serialization;
using Eco.Core.Utils;
using Eco.Shared.Networking;
using Eco.Shared.Serialization;
using Eco.Shared.Utils;

namespace SpeciesTracker
{
    /// <summary>
    /// Persistent per-user tracking configuration, saved through Eco's storage system.
    /// </summary>
    [Serialized]
    public class SpeciesTrackerData : Singleton<SpeciesTrackerData>, IStorage
    {
        [Serialized] public ThreadSafeDictionary<int, UserConfig> UserConfiguration = new();

        public IPersistent? StorageHandle { get; set; }
    }

    [Serialized]
    public class UserConfig
    {
        [Serialized] public ThreadSafeList<string> TrackedSpecies { get; set; } = new();

        // Single user-level detection range in meters applied to every tracked species.
        // null means "use the default range" (SpeciesTrackerManager.DefaultRange).
        [Serialized] public float? CustomRange { get; set; }
    }
}
