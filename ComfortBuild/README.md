# Comfort Build

A Comfort tab in Valheim's build menu: only the pieces that give comfort, with their comfort groups down the left, each piece's comfort in a corner of its picture, and the pieces that wouldn't raise your comfort where you stand greyed out, saying why.

## What it does

- **A Comfort tab,** after Favorites. It lists the pieces of the tool in hand that give comfort and that you can build, in the order you unlock them. The groups down the left (Fires, Beds, Chairs, Tables …) show only what you can build something in; "Show All" lists them all, group by group. The tab shows only for a tool with comfort pieces, and Q and E (or a gamepad's shoulder buttons) reach it like the game's own tabs.
- **Each piece's comfort** in the top-left corner of its picture, in light blue, the way gear shows its level.
- **What counts where you stand.** The game counts only the best piece of each group within 10 metres of you (a second chair adds nothing beside a better one), and a piece without a group once per kind. So beside each group the list shows what it gives where you stand and the most you can get from it with what you can build: "Chairs 2/3". At the top, "Comfort here: 9" is your comfort where you stand, as the game counts it under a roof: the "Comfort: N" the game shows on its Resting icon while you rest there (unless "Count unlit fires as lit" is on).
- **Grey Out Unneeded,** a switch at the top of the list. On, a piece that wouldn't raise your comfort where you stand shows in grey with a small tick ("you have this covered"). Beside a comfort-2 chair, the comfort-1 and comfort-2 chairs are greyed, and the comfort-3 thrones aren't.
- **Why, in a box beside the build panel** (the piece's picture, name and materials at the bottom of the screen), on its right (a setting: left, or off), for the piece under the pointer or the one in hand: "Comfort 2 (Chairs). Adds nothing here: the Raven Throne near you already gives 3." It also says when a fire near you is out and would give at least as much once lit (the hot tub: once heated), and when you aren't under shelter. The game's own description is left as it is.
- **Measured where you stand.** Comfort counts only under a roof; if you aren't sheltered, it is counted as if you were, and it says so.

Nothing else in the menu changes: the game's own four tabs, their pictures and their order look as they do without the mod, unless "Comfort on every tab" is on.

## Good to know

- **Only you need it.** It's client-side: neither the server nor the other players need it. Every comfort piece near you counts, whoever built it.
- **Changes work at once.** Change a setting in game and the menu follows; no restart.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-ComfortBuild-<version>.zip`. Keep the author `drumcowski` and the name `ComfortBuild` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `ComfortBuild.dll` into a folder named `drumcowski-ComfortBuild` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.comfortbuild.cfg`, which appears after the first start; in the bundle, in the section "Comfort Build" of its own file.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off takes the Comfort tab, the numbers and the grey out of the build menu at once; on brings them back. |
| Comfort tab | on | The Comfort tab in the build menu. |
| Grey out unneeded | on | Pieces that wouldn't raise your comfort where you stand are greyed, with a tick, and the comfort line says why. The switch "Grey Out Unneeded" at the top of the Comfort tab's list sets this too. |
| Look of the unneeded | GreyWithTick | GreyWithTick: the picture in grey, with the tick. DimmedWithTick: the picture darkened, in colour, with the tick. TickOnly: only the tick. |
| Comfort on every tab | off | On: the numbers, the grey and the comfort line on the game's own tabs too. |
| Comfort line | Right | The box beside the build panel that says what a piece would add where you stand, and why: Right, Left (where some mods put a bar of their own, such as EquipmentAndQuickSlots' quick slots) or Off. |
| What counts, beside each group | on | "Chairs 2/3" in the Comfort tab's list: what the group gives where you stand, and the most you can build. |
| Count unlit fires as lit | off | Off: a fire that is out gives nothing, as the game counts it. On: counted as if lit (the hot tub as if heated). |
| Order | Progression | The order of the pieces in each group: Progression (as you unlock them), ComfortHighestFirst, or Name. |
| Order of the groups | Fires, Beds, … Other | The groups in the list, separated by commas; groups left out follow in the default order. |
| Corner of the number | TopLeft | Which corner of a piece's picture shows its comfort. |
| Colour of the number | light blue | The colour of the comfort number, the tick, the values beside each group, "Comfort here" and the comfort line. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Comfort Build". If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and the build menu is the game's own. The same goes for an error while playing. If "Comfort here" differs from the game's own count (the "Comfort: N" on its Resting icon) for more than a few seconds while you're sheltered, the log says so once: another mod may change how comfort is counted.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\ComfortBuild.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-ComfortBuild-<version>.zip`. Until Comfort Build is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, `kit\` is a copy of the QoL Mods kit that runs it, and `parts\` holds copies of the two parts it uses (the catalogue, for the order of pieces and the groups' names; pictures, for the grey copies); how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn.
