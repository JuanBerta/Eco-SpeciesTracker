# Species Tracker (Eco Mod)

Track **animals and plants** in Eco. Every instance of a tracked species inside your detection range shows a **temporary personal marker** on the M-key world map and the minimap. Markers follow moving animals, refresh every 0.5 s, and disappear when the entity dies, leaves the range, or tracking is stopped.

- **Author:** Beinded (JuanBerta)
- **GitHub:** https://github.com/JuanBerta/SpeciesTracker
- **Game version:** Eco 0.14.2.1
- **Mod version:** 0.1.2

## Changelog

- **0.1.2** — Fix map markers never rendering: `IThreadedPlugin.Run()` is invoked exactly once on a dedicated background thread by PluginManager, so the plugin now keeps the marker refresh loop alive for the whole server lifetime (previously the refresh pass only ran once at startup, before any species was tracked, and no WorldMarker was ever created).
- **0.1.1** — Rename the chat command from `/track` to `/trackspecies` to avoid a server startup conflict with another mod's command.

## Features

- Track land animals, aquatic animals, **and plants/trees** (any species in the simulation database).
- Per-species, per-player detection range (1 - 500 m, default 50 m).
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
| `/trackspecies range <species> <meters>` | Set detection range for a species (1-500, default 50) |
| `/trackspecies list` | Show your tracked species and their ranges |
| `/trackspecies clear` | Stop tracking every species |

Examples:

```
/trackspecies species
/trackspecies add Bison
/trackspecies add Pine
/trackspecies range Bison 120
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
