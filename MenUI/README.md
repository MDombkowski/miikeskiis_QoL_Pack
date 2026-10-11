# MenUI

Made with AI. Keeps every button of Valheim's main menu on screen. Mods such as RememberServerPassword, ValheimPerformanceOverhaul and ConfigurationManager each add a button to the main menu, and the list only grows downward, until Quit falls below the window. MenUI fits the list back on screen, whatever buttons it holds and whichever mod added them.

## What it does

- **Fits the main menu.** When the last button reaches past the bottom of the window, the buttons shrink just enough, their top staying where it is. If they would get smaller than 70% of their size, the list also moves up, no higher than about the bottom of the game's logo.
- **Any mod's buttons.** It measures whatever the menu shows, so a button any mod adds, at any time, is fitted too.
- **Leaves a menu that fits alone.** With few buttons, or a window tall enough, the menu looks exactly as the game made it.
- **The in-game menu too.** The menu Esc opens in game is fitted the same way when other mods' buttons make it too tall, shrunk around its middle.
- **The arrow keys reach every button.** The game only knows the buttons it had when it started; MenUI adds the others, in the order they are shown, so the up and down arrow keys reach them. (A gamepad moves between buttons its own way, which MenUI leaves alone.)

## Good to know

- **Only you need it.** It's client-side: neither the server nor the other players need it.
- **Changes work at once.** Change a setting, or the window's size, and the menu is fitted again; no restart.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-MenUI-<version>.zip`. Keep the author `drumcowski` and the name `MenUI` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `MenUI.dll` into a folder named `drumcowski-MenUI` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.menui.cfg`, which appears after the first start; in the bundle, in the section "MenUI" of its own file.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off puts the menus back as the game and other mods made them, at once. |
| Also fit the in-game menu (Esc) | on | Off: only the main menu is fitted. |
| How to fit | Shrink, then move up | Or "Move up, then shrink", or "Shrink only". Whatever is chosen, every button ends up on screen. |
| Smallest size (%) | 70 | How small the buttons may get before the list moves up instead. 30 to 100. |
| Space below the last button (% of the window) | 2 | The gap kept under the last button. 0 to 20. |
| Highest the main menu may reach (% of the window) | 60 | How far up the main menu may move, from the window's bottom. 30 to 95. |
| Arrow keys reach every button | on | Off: the up and down arrow keys reach only the game's own buttons. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "MenUI": it says when it fits a menu, and when the menu fits again by itself. If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and the menus look as they do without it. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\MenUI.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-MenUI-<version>.zip`. Until MenUI is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx and Jötunn.
