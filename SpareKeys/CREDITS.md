# Credits: Spare Keys

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-10-09 (the free keys research in the vault, `research/2026-10-09_free-keys-every-key-in-use.md`).

## Code copied in

None. Every line of this mod was written for it. Its window's helpers follow the patterns of Add to Cart's and Outfitter's windows, other mods of this pack.

## Code or ideas adapted (studied, nothing copied)

| What | From | Licence | Where it shows here |
|---|---|---|---|
| Listing every key of the game and the mods, clashes split into "same keys" and "a plain key under a combination", free keys suggested near a used one | [Bindrune](https://github.com/isimp/Bindrune), by isimp | MIT | The idea, read in its README and `docs/technical.md`; its code was not copied. `src\Model\KeyTable.cs` works the rules out its own way. |
| A picture of the keyboard and mouse coloured by assigned and unused keys, the users on hover | [Keybind Atlas](https://modrinth.com/mod/keybind-atlas) (Minecraft) | All Rights Reserved | The idea only; nothing of it read or copied. `src\Keys\Layouts.cs` and `src\Ui\KeyboardView.cs`. |
| Help in the game's own controls screen while rebinding | [Controlling](https://www.curseforge.com/minecraft/mc-mods/controlling) (Minecraft) | not recorded | The idea only. `src\Ui\PickPanels.cs`. |
| When a key setting in F1 waits for a key, and where its window is | [shudnal's ConfigurationManager](https://github.com/shudnal/ConfigurationManager) 1.1.25, by shudnal | LGPL-3.0 (its repository's LICENSE.txt; its README says GPLv3) | Its members' names (`SettingFieldDrawer.SettingKeyboardShortcut`, `DisplayingWindow`, `currentWindowRect`, `ScaleFactor`) were read in its decompiled code and are read by reflection at run time (`src\Reach.cs`); nothing copied. |
| How a KeyboardShortcut setting is written and read, and BepInEx's own check (no extra key held) | [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23's `KeyboardShortcut` | LGPL-2.1 | `src\Keys\Combo.cs` reads the setting's text the way it does; the rule "A plain key counts as used under any modifier" offers its check as a choice. Nothing copied. |
| Jötunn's buttons named `button!mod id` in the game's table | [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2's `InputManager` | MIT | `src\Model\ModKeys.cs` reads them; nothing copied. |
| The game's buttons, their binds and rebinding, its fixed keys, its console binds, its Controls menu | Valheim 1.0.17 (`ZInput`, `Terminal`, `KeyboardMouseSettings`), by Iron Gate | proprietary | Read at run time (`src\Reach.cs`); the fixed keys are a table in `src\Model\GameKeys.cs` written from the decompiled code. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute, its GUI manager (the window in the game's look, holding the game's input while it's open). |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader; every mod's settings are read through it. |
| [Mono.Cecil](https://github.com/jbevain/cecil) | Jb Evain | MIT | Ships with BepInEx: reads other mods' code (never runs it) for the keys they read. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Not used to patch anything: the mod only reads. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |

## Assets

- No pictures of its own: the keys are drawn in code. The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
