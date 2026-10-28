# Species Tracker (Eco Mod)

Track **animals and plants** in Eco. Every instance of a tracked species inside your **global detection range** shows a **temporary personal marker** on the M-key world map and the minimap. Markers follow moving animals, refresh every 0.5 s, and disappear when the entity dies, leaves the range, or tracking is stopped.

- **Author:** Beinded (JuanBerta)
- **GitHub:** https://github.com/JuanBerta/Eco-SpeciesTracker.git
- **Game version:** Eco 0.14.2.1
- **Mod version:** 0.3.0

## Changelog

- **0.3.0** — Animal scan performance: animals now go through a shared wrap-aware uniform-grid spatial index (`SpatialHashGrid`) rebuilt every 5 s, so each 0.5 s tick only visits animals whose cells intersect your range instead of scanning the whole `AnimalsManager` list per user. Animal markers are only repositioned once the animal has actually moved ≥ 2 m (was: every tick). Together this removes the multi-second freezes seen at 500 m ranges tracking herds (e.g. Deer), and makes `/trackspecies clear` responsive again.
- **0.2.0** — Plant scan performance: plants now use `PlantSim.PlantsWithinRange` (spatial index) instead of walking the full plant list; squared-distance range checks, marker position caching (static entities stop pushing per-tick network updates), creation caps (400 markers/user, 60 new/tick), pure logic extracted into `TrackerPerf.cs` with a unit-test project. Custom range switched to a float with -1 sentinel.
- **0.1.3** — Detection range is now user-global: one per-player range applies to every tracked species at once (`/trackspecies range <meters>`), replacing the old per-species ranges. Saves from 0.1.2 (per-species ranges) are superseded by the new global range (default 50 m).
- **0.1.2** — Fix map markers never rendering: `IThreadedPlugin.Run()` is invoked exactly once on a dedicated background thread by PluginManager, so the plugin now keeps the marker refresh loop alive for the whole server lifetime (previously the refresh pass only ran once at startup, before any species was tracked, and no WorldMarker was ever created).
- **0.1.1** — Rename the chat command from `/track` to `/trackspecies` to avoid a server startup conflict with another mod's command.

## Features

- Track land animals, aquatic animals, **and plants/trees** (any species in the simulation database).
- One global per-player detection range (1 - 500 m, default 50 m) applied to every tracked species.
- Personal world map + minimap markers (neon green), refreshed at 0.5 s.
- Markers are cleaned up when the entity dies / is removed, leaves range, or tracking stops.
- Per-player configuration persisted across restarts.

## Commands

| Command | Description |
|---|---|
| `/trackspecies help` | Show this help |
| `/trackspecies species` | List every trackable species (animals and plants) |
| `/trackspecies add <species>` | Start tracking a species |
| `/trackspecies remove <species>` | Stop tracking a species |
| `/trackspecies range <meters>` | Set your global detection range for every tracked species (1-500, default 50) |
| `/trackspecies list` | Show your tracked species and your global detection range |
| `/trackspecies clear` | Stop tracking every species |

Examples:

```
/trackspecies species
/trackspecies add Bison
/trackspecies add Pine
/trackspecies range 120
/trackspecies list
/trackspecies remove Bison
```

## Installation

1. Download the release zip (contains `SpeciesTracker.dll`, `modinfo.json`, `Locales/`).
2. Extract it into `Eco_Data/Server/Mods/SpeciesTracker/` of your Eco server installation.
3. Restart the server (or use `/reloadmods` if available).

## Building

Requires .NET 10 SDK. The project references `Eco.ReferenceAssemblies` 0.14.2.1-beta-release-1106 (NuGet).

```
dotnet build SpeciesTracker.csproj -c Release
```

## License

Source code released under the MIT License. Species names come from the vanilla game database. This mod is not affiliated with Strange Loop Games.
