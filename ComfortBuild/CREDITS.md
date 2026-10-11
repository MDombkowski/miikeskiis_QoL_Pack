# Credits: Comfort Build

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-24 (the Comfort Build research in the vault), and from Map Zoom's credits for the shared parts.

## Code copied in

None. Every line of this mod was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| The comfort rule: 2 when sheltered, plus the best piece of each group within 10 m, a piece without a group once per kind | Valheim's `SE_Rested.CalculateComfortLevel`, by Iron Gate | proprietary | `src\ComfortMath.cs` works out the same count in its own code (the game's own search finds the pieces); nothing copied. |
| A new tab in the build menu, and a list the game's own code fills | Valheim's `BuildUi` and `IPieceList`, by Iron Gate | proprietary | `src\ComfortTab.cs` is one more `IPieceList`; `src\Menu.cs` adds its tab button, copied at run time from the game's own. |
| The idea of a comfort number on each piece in the build menu | [ComfortMe](https://thunderstore.io/c/valheim/p/Hardwire99/ComfortMe/), by Hardwire99 | none found (all rights reserved) | The idea only; its code was not read. This mod adds its own tab, the grey, the switch and the groups' values. |
| Showing which pieces count, and what a piece would add | [ComfortAudit](https://thunderstore.io/c/valheim/p/Jumpingmushroom/ComfortAudit/), by Jumpingmushroom | MIT | The idea only; no code taken. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (two field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the build menu (its waking, opening, list and piece buttons) and the build panel's piece description (to show its comfort line beside it). |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. At run time the mod borrows the game's own looks: the build menu's tab button, list row and text, the favourites dropdown's checkbox and tick, and the inventory's level-number font. |

## Assets

- No pictures of its own: the tick and the checkbox are the game's, found at run time. The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
