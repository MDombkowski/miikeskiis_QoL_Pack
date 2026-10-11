# Handyman

R picks the hammer's Repair instead of putting the hammer away. With the hammer in hand, a tap of R picks Repair, a tap again goes back to the piece you had, turned as it was, and holding R puts the hammer away as before.

**On out of the box** since 0.1.1 (miikeskii's own setting): switch off "R picks Repair" and R puts the hammer away as in the game. An older settings file still at an old default moves to the new one once; a setting you changed stays as you set it.

## What it does

- **Tap R: Repair.** With the hammer in hand, a tap of R picks its Repair, the same as the little hammer button beside the build menu's filter box.
- **Tap R again: back to your piece.** The piece you had before Repair comes back, turned as it was, even a piece the game turns at random each time it's picked. It works however Repair was picked, by R or by the menu's button. Each build tool remembers its own piece.
- **Hold R: put the hammer away,** as R does in the game. A hold is a third of a second by default, the game's own limit for a tap of its gamepad's hide button.
- **With the build menu open,** a tap of R picks Repair and closes the menu, as the menu's hammer button does.
- **It follows the game's key.** R is the game's "Hide/show weapon" key; if you rebind that in the game's controls, the new key does all of the above. A key of its own is a setting.
- **Only build tools with a Repair.** The hammer, and another mod's build tool with a Repair. With the hoe, the cultivator, the serving tray or any other item in hand, and with empty hands, R works as in the game.
- **Everything else stays the game's:** swimming, sitting, eating, crafting stations and the barber put the hammer away as they always do, and the game's piece copy (Shift + middle mouse) works while Repair is picked.

## Good to know

- **Only you need it.** It's client-side: neither the server nor the other players need it, and nothing is saved in the world.
- **Repairing works as in the game:** it still needs the piece's crafting station in reach. A mod that widens or removes that (LetMeBuild, for one) works with it.
- **Changes work at once.** Change a setting in game and the next press uses it; no restart.
- **Keyboard and mouse only.** A gamepad's hide button works as in the game.
- **With "hold the key", switching away from the game (Alt + Tab) while holding R** does nothing: the press is dropped.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Mods that do the same job

If you prefer another mod for this job, switch this one off ("Enabled" off, or "R picks Repair" off) and use the other; switched off, it leaves nothing behind:

- **ZenConstruction's hammer key** (B by default; press and hold for Repair) does a similar job on another key. The two should not clash; if you use it instead, switch Handyman off.
- **Any other mod that reads the game's Hide key (R)** would clash with this one: switch Handyman off, or give Handyman its own key ("Key" at OwnKey).

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-Handyman-<version>.zip`. Keep the author `drumcowski` and the name `Handyman` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `Handyman.dll` into a folder named `drumcowski-Handyman` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.handyman.cfg`, which appears after the first start; in the bundle, in the section "Handyman" of its own file.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off gives R back to the game at once. |
| R picks Repair | **on** | On: a tap of R picks Repair, as above. Off: R puts the hammer away, as in the game. |
| Key | GameHideKey | GameHideKey: the game's "Hide/show weapon" key (R, or your rebind). OwnKey: the key in "Own key"; R then works as in the game. |
| Own key | H | The key used when "Key" is OwnKey. A mouse button works too. |
| Tap again while Repair is picked | BackToMyPiece | BackToMyPiece: the piece you had, turned as it was. PutHammerAway. Nothing. |
| With no piece to go back to | Nothing | When the tool hasn't had another piece picked since the game started (the game starts the hammer on Repair), or that piece can't be picked now (out of season): Nothing (with "Message" on, a message says so), PutHammerAway, or OpenBuildMenu. |
| Putting the hammer away | HoldTheKey | HoldTheKey: hold R. ShiftAndTheKey: Shift + R, and R alone picks Repair at once. HotbarKeyOnly: only the hammer's hotbar key. |
| How long is a hold (seconds) | 0.33 | 0.2 to 1.5. |
| Going back restores the piece's turn | on | Off: the game turns the piece as it does when you pick it in the build menu. |
| Tools | EveryToolWithRepair | Or HammerOnly: the game's hammer only. (Another mod's tool whose Repair isn't first in its pieces would get another piece from the game's own pick: the mod puts your piece back, says so once in the log, and leaves R to the game with that tool.) |
| Serving tray picks its Remove | off | On: the serving tray's Remove (or that of any tool with a Remove and no Repair) is picked the same way. |
| With the build menu open | PickRepairAndClose | Or AsInTheGame (R puts the hammer away), or Nothing (Shift + R included). |
| Message | on | A short message at the top left: "Repair", then your piece's name. |
| Sound | on | The build menu's own click. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Handyman". If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and R works as in the game. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\Handyman.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-Handyman-<version>.zip`. Until Handyman is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: its code was written by Claude agents (Anthropic), directed by miikeskii.
