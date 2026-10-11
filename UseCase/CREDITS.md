# Credits: Use Case

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-29 (the Use Case research in the vault).

## Code copied in

None from anyone else. The settings window's fold-outs (`src\Folds.cs`, `src\FoldDrawer.cs`, `src\ManagerLink.cs`) are adapted from Map View Rules, another mod of this pack.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| Every use of an item, and how many: recipes, the tools' pieces, fuel, processing, ammo, altars, doors, taming, bait, the traders; the upgrade cost per level | Valheim's own classes (`Recipe`, `Piece.Requirement.GetAmount`, `Fireplace`, `Smelter`, `CookingStation`, `Fermenter`, `Incinerator`, `ShieldGenerator`, `Turret`, `Catapult`, `OfferingBowl`, `Door`, `Tameable`, `MonsterAI`, `Fish`, `Trader`, `BossStone`), by Iron Gate | proprietary | `src\Index\GameReader.cs` reads them at run time; the game's own `GetAmount` gives the upgrade costs; the catapult's loading rule is followed in its own code. Nothing copied. |
| What the player knows | Valheim's `Player` known lists and seasons, by Iron Gate | proprietary | `src\Knowledge.cs` asks the game (`IsRecipeKnown`, `IsKnownMaterial`, the global keys). |
| The idea of listing what an item is used in | [VNEI](https://github.com/MSchmoecker/VNEI), by MSchmoecker | MIT | The idea only (it shows uses in a window of its own); its code was not copied. This mod shows them beside the tooltip. |
| Columns for a long tooltip | [SeneaL UI](https://thunderstore.io/c/valheim/p/seneaL/SeneaL_UI/), by seneaL | not stated | The idea only; its code was not read. |
| Reading TimedTorchesStayLit's settings | [TimedTorchesStayLit](https://thunderstore.io/c/valheim/p/Tastychickenlegs/TimedTorchesStayLit/), by Tastychickenlegs | not stated | Its settings' names were read from its code, to know which pieces it keeps lit; nothing copied. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute; its ModRegistry, to name the mod that added a thing; its ConfigManagerUtils, for the fold-outs. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Postfixes on the game's tooltip (placing it), the inventory's, the trader's and the cost icons' tooltips (noting their item), and the altars' and doors' start (seen). |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. At run time the panel borrows the look of the game's item tooltip (its frame and its text). |

## Assets

- No pictures of its own. The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
