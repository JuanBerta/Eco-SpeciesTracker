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

        // Key: exact species name as stored in TrackedSpecies. Value: custom detection range in meters.
        [Serialized] public ThreadSafeDictionary<string, float> CustomRanges { get; set; } = new();
    }
}
