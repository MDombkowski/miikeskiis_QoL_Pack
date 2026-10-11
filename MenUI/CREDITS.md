# Credits: MenUI

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Recorded on 2026-10-10.

## Code copied in

None. Every line of this mod was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| Giving the game's arrow-key list every button the main menu shows, in the order shown | [ConfigurationManager](https://github.com/shudnal/ConfigurationManager), by shudnal (a fork of BepInEx.ConfigurationManager) | LGPL-3.0 by its repository (its README says GPLv3), read 2026-10-10 | `src\MenuFit.cs` (`CheckArrowKeys`) does the same after its own check; no code taken. |
| How the main menu's buttons are kept: the list `m_menuList` and the arrow keys' `m_menuButtons` | Valheim's `FejdStartup`, by Iron Gate | proprietary | `src\GameHooks.cs` reads them by name; nothing copied. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (two field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |
| Other mods' menu buttons (RememberServerPassword by MagiCorp, ValheimPerformanceOverhaul by Skarif, ConfigurationManager by shudnal, and any other) | their authors | their own | Only measured and fitted on screen; never changed or called. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
