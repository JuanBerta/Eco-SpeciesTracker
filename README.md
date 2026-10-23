# Species Tracker (Eco Mod)

Track **animals and plants** in Eco. Every instance of a tracked species inside your detection range shows a **temporary personal marker** on the M-key world map and the minimap. Markers follow moving animals, refresh every 0.5 s, and disappear when the entity dies, leaves the range, or tracking is stopped.

- **Author:** Beinded (JuanBerta)
- **GitHub:** https://github.com/JuanBerta/SpeciesTracker
- **Game version:** Eco 0.14.2.1
- **Mod version:** 0.1.0

## Features

- Track land animals, aquatic animals, **and plants/trees** (any species in the simulation database).
- Per-species, per-player detection range (1 - 500 m, default 50 m).
- Personal world map + minimap markers (neon green), refreshed at 0.5 s.
- Markers are cleaned up when the entity dies / is removed, leaves range, or tracking stops.
- Per-player configuration persisted across restarts.

## Commands

| Command | Description |
|---|---|
| `/track help` | Show this help |
| `/track species` | List every trackable species (animals and plants) |
| `/track add <species>` | Start tracking a species |
| `/track remove <species>` | Stop tracking a species |
| `/track range <species> <meters>` | Set detection range for a species (1-500, default 50) |
| `/track list` | Show your tracked species and their ranges |
| `/track clear` | Stop tracking every species |

Examples:

```
/track species
/track add Bison
/track add Pine
/track range Bison 120
/track list
/track remove Bison
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
