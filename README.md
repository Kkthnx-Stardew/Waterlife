# Waterlife

Shows the fish, junk and treasure that can actually be caught in the water around you, so you can
see what a cast would give you instead of guessing.

A from-scratch rewrite of [Visible Fish](https://www.nexusmods.com/stardewvalley/mods/8897) by
shekurika, targeting Stardew Valley 1.6 and SMAPI 4.

## What it does

- Asks the game what a cast at that tile would give you, so season, weather, time of day, bait,
  fishing level lookups, boss fish and catch limits all behave exactly like the real thing.
- Uses the real fishing depth of each water tile, so shallow-only fish stay near the shore and
  deep-water fish appear in open water.
- Places the mod's fixed spawns (Mutant Carp in the Sewer, the Legendary Family variants, the
  statue basket in SVE's woods) on their configured tiles.
- Adds cave jelly to the mines and respects the vanilla spawn caps there.
- Draws behind the water normally, and in front of it for locations flagged `Underwater`.
- Parses each species' sprite data once and shares it across every fish, instead of re-parsing it for each spawn.

## Install

1. Install SMAPI 4.0 or later.
2. Drop the `Waterlife` folder into `Stardew Valley/Mods`.

Do not run this alongside Visible Fish — both would draw the same fish.

## Config

Everything is editable in `config.json` or through
[Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional).

| Option | Default | What it does |
| --- | --- | --- |
| `MaximumFish` | 500 | Hard cap on how many fish and items exist in the water at once. Lower it for weaker hardware. |
| `FishDensity` | 0.2 | Fish per water tile, so 0.1 means roughly every tenth tile gets one. Still limited by `MaximumFish`. |
| `TrashOutside` | true | Show junk (trash, driftwood, algae...) in locations outside the farm. |
| `TrashFarm` | true | Same, for farm water. |
| `DisableTrashLvl10` | true | Stop showing junk at fishing level 10. Seaweed and algae still appear. |
| `ShowTreasure` | true | Show non-fish catches like furniture and paintings. |
| `DrawFishShadows` | true | Shadow under fish. |
| `DrawItemShadows` | true | Shadow under items. |
| `RotateAlgae` | false | Let seaweed and algae spin. |
| `FilterEels` | false | Hide eel-type fish. |

## Compatibility

Stardew Valley Expanded, Ridgeside Village and Sunberry Village patch this mod's data asset
(`Mods/shekurika.VisibleFish/LocationData`) by name. That asset name is intentionally kept even
though the mod is now Waterlife, so those patches keep applying.

## Profiling

Each location logs one line when it finishes populating, which is the honest answer to "what does
this cost":

```
Beach: 148 fish + 9 debris on 1512 water tiles (target 151) in 264 ms, 168 tile picks / 151 fish rolls; grid 0.61 ms, setup 1.12 ms
```

`grid` and `setup` happen inside the warp frame; the fill time is spread over ticks at 12 entities
per tick. Nothing is logged once a location is full.

## Building

Needs the .NET 6 SDK and a local game install. No NuGet packages: the game assemblies are referenced
by path.

```
dotnet build Waterlife/Waterlife.csproj -c Release -p:GamePath="C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley"
```

Add `-p:DeployToMods=true` to also copy the build into `Mods\Waterlife`.

## Credits

- Original mod, bundled art and location data: shekurika.
- Rewrite, 1.6 support and the spawner: Kkthnx.
