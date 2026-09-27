# Map Zoom

Zoom the big map with fewer turns of the mouse wheel, toward where the mouse points. Without it, Valheim takes about 44 notches of the wheel to go from fully zoomed in to fully zoomed out; with it, 18 by default, or as many as you like.

## What it does

- **Fewer notches.** "Scroll notches, fully in to fully out" sets how many notches of the wheel take the big map from fully zoomed in to fully zoomed out. The same number takes it back in. Every notch zooms by the same step, so it feels the same at every zoom.
- **Toward the pointer.** The place under the mouse pointer stays under it as you zoom, as on web maps. Turn "Zoom toward the mouse pointer" off to zoom around the middle of the view, as the game does.
- **Nothing else changes.** The comma and period keys, a gamepad, dragging the map and the small map in the corner work as they always do.

## Good to know

- **Only you need it.** It's client-side: neither the server nor the other players need it.
- **Changes work at once.** Change a setting in game and the next notch uses it; no restart.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-MapZoom-<version>.zip`. Keep the author `drumcowski` and the name `MapZoom` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `MapZoom.dll` into a folder named `drumcowski-MapZoom` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.mapzoom.cfg`, which appears after the first start; in the bundle, in the section "Map Zoom" of its own file.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off gives the mouse wheel back to the game's own zoom at once; on brings this mod's zoom back. |
| Scroll notches, fully in to fully out | 18 | How many notches of the wheel go from fully zoomed in to fully zoomed out, and back. 4 to 60; fewer zooms faster. The game's own is about 44 out and 40 in. |
| Zoom toward the mouse pointer | on | On: the place under the pointer stays under it. Off: zooms around the middle of the view. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Map Zoom". If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and the map zooms as it does without the mod. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\MapZoom.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-MapZoom-<version>.zip`. Until Map Zoom is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn.
