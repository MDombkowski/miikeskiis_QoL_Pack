# Grounded

Lay rugs flat on the floor, overlapping each other, instead of piling them on top of one another.

## What it does

In the game, a rug lands on whatever you point at, so a rug placed over another sits on top of it, a little higher each time, and even the first one floats a little above a wooden floor. Hold <kbd>Left Alt</kbd> while you place a rug and it looks straight through the rugs already there and lies on the floor itself:

- **Overlap as many as you like.** A new rug lies just a hair above the rugs under it that are drawn at nearly its own height, so they don't flicker where they overlap. Rugs never pile up on each other's tops.
- **Down on the boards.** A rug comes down toward the planks or stones you see, lower than the game puts it, as far as it can while your hammer can still point at it to remove it. A setting lets it go all the way down.
- **Every floor piece counts:** wood, ashwood, stone, black marble, grausten, cage floors and the ice block, other mods' floors too. All eleven rugs count, Straw included, and other mods' rugs too.
- **Anything else stays as in the game.** Over a table, a shelf or the bare ground, the rug goes where the game puts it. Let go of the key and the game's own placement is back.

You can also make it work on a single press instead of a hold, or all the time, let any piece you build with the hammer (a table, a chair) look through rugs to the floor, add or remove pieces from its lists of rugs and floors, and fine-tune how low a rug lies.

## Its key

| Key | What it does |
|---|---|
| hold <kbd>Left Alt</kbd> | While building: rugs lie on the floor under them |

The game uses <kbd>Left Alt</kbd> for nothing. You can change the key in the settings, or have no key at all.

## Good to know

- **Only you need it.** A rug you place this way is an ordinary rug for everyone: friends see it where you put it without the mod, and the server needs nothing.
- **Changes work at once.** Change a setting in game and the next rug follows it.
- **Other mods:** a mod that moves the build ghost up or down itself (Infinity Hammer's offsets) or gives rugs snap points (Extra Snap Points Made Easy) may add to or undo what Grounded does. Switch Grounded off, or empty its key, and the game's own placement is back at once.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-Grounded-<version>.zip`. Keep the author `drumcowski` and the name `Grounded` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `Grounded.dll` into a folder named `drumcowski-Grounded` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1); each one says what it does there. They are kept in `BepInEx\config\modprojects.grounded.cfg`, which appears after the first start; in the bundle, in the section "Grounded" of its own file.

"Rugs" and "Floors" have a **Pick** button beside their text box there, which opens a window of everything the list can hold, each with its picture: a click puts a thing on the list or takes it off. The text box stays for typing.

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Grounded". If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and rugs are placed as without the mod. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\Grounded.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and the changelog agree on the version, builds, and writes `dist\drumcowski-Grounded-<version>.zip`. Until Grounded is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: its code was written by Claude agents (Anthropic), directed by miikeskii.
