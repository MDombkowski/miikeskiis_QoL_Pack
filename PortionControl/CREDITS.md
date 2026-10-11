# Credits: Portion Control

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-24 and 2026-09-29 in the research behind cards A17 and A49.

## Code copied in

None. Every line of this mod was written for it. BetterSplitDialog has no licence, so none of its code could be used, and none was: this mod was written from scratch, from what the game's own split window does (its decompiled code, read to learn how it works) and from the ideas below.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| The idea of an amount box, one-click percentage buttons labelled with their amounts (hiding repeats), and slider buttons (Min, -1, 50%, +1, Max) in the split window; hiding the one-click buttons while CurrencyPocket is installed | [BetterSplitDialog](https://thunderstore.io/c/valheim/p/turbero/BetterSplitDialog/), by turbero | none published | The whole mod, which replaces it; ideas only, no code taken. |
| The idea of fixed one-click amounts, and running a one-click amount through the game's own OK | [Abacus](https://thunderstore.io/c/valheim/p/ComfyMods/Abacus/), by bruce (ComfyMods) | GPL-3.0 | `src\Window.cs` (the one-click amounts); ideas only, no code taken. |
| The idea of "just enough to stop being encumbered" | [DropJustEnough](https://thunderstore.io/c/valheim/p/Gimrall/DropJustEnough/), by Gimrall (a fork of DeekyJay's 2021 mod) | not found | `src\Weights.cs` (`JustEnough`); idea only, no code taken. |
| The settings' groups that open and close in F1 (their title rows and how they hide their settings) | Map View Rules' `Folds` and `FoldDrawer`, a module of this same pack | the pack's GPL-3.0 | `src\Folds.cs`; written again for this mod after the pack's own code. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (the field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute, and the amount box (`GUIManager.CreateInputField`). |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's split window (opening it, and reading its keys). |
| BetterSplitDialog, Abacus | turbero; bruce | none; GPL-3.0 | Looked for at start by their plugin IDs: while either is installed, this mod stands aside. Never changed. |
| CurrencyPocket | Azumatt | not checked | Looked for at start by its plugin ID: the one-click buttons' guard. Never changed. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. The buttons are copies of the windows' own buttons (the split window's OK, the chest window's Take all and Place stacks), their tooltips the game's own, and the chest window's Max weight picture the game's own encumbered icon, all taken while the game runs. |
| The pack's catalogue part | this pack | the pack's GPL-3.0 | `parts\Catalogue\`, a copy kept identical by `..\QoLMods\tools\sync-kit.ps1`: the categories, the items' names in your language, for the amounts by category and the item list. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
