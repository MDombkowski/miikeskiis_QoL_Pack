# Credits: Item Stand Icons

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-29 (the item stands research in the vault), and from Map Zoom's credits for the shared parts.

## Code copied in

None. Every line of this mod was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| Which items fit an item stand: a model to hang, not refused, listed or of an allowed type | Valheim's `ItemStand.CanAttach`, by Iron Gate | proprietary | The mod calls the game's own check (`parts\ItemStands\StandFit.cs`); its setting "StandListsOnly" works the same four steps out in its own code, reading the stands' lists; nothing copied. |
| Where item icons are drawn: the inventory grids, the hotbar, the crafting menu, the trader | Valheim's `InventoryGrid`, `HotkeyBar`, `InventoryGui`, `Hud` and `StoreGui`, by Iron Gate | proprietary | `src\Places.cs` marks each after the game draws it; nothing copied. |
| A mark hung on an inventory cell, never catching a click | HexQuickStackStorage's favourite border (`InventoryBorderRenderer`), by Hex | none found (all rights reserved) | The idea only, as read for the research; the part's mark is its own code. |
| Look settings for a mark on an icon (text, colour, size, position) | MyLittleUI, by shudnal | Unlicense | The idea of which settings a mark has; no code taken. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (two field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the drawing of the inventory grids, the hotbar, the crafting menu, the build panel's costs and the trader's list, to add the marks after the game. |
| [Item Stand All Items](https://thunderstore.io/c/valheim/p/JereKuusela/Item_Stand_All_Items/) | JereKuusela | Unlicense | Not needed. When installed, the marks follow what it lets onto stands, since the mod asks the game's own check, which it changes. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. At run time the mod asks the game's two item stands which items they take, and borrows the inventory's number font for its text looks. |

## Assets

- The arrows are drawn in code (`parts\ItemStands\StandArrows.cs`). The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
