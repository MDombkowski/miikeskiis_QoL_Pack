# Credits: Reserve Stock

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-23 (the installed-mods study), 2026-09-24 (the Reserve Stock research) and 2026-09-28.

## Code copied in

None. Every line of this mod, and of the chest service it carries (`parts\Chests`), was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| A per-chest mark saved in the chest's own network record, under the mod's own name, so the server keeps it with the world without knowing the mod | The way [MyLittleUI](https://thunderstore.io/c/valheim/p/shudnal/MyLittleUI/) (chest names) and [AutoStash](https://thunderstore.io/c/valheim/p/dbendu/AutoStash/) (filters) store per-chest settings | MyLittleUI: not checked; AutoStash: MIT | The idea only; `src\Mark.cs` and the chest service's `ChestReserve.cs` are written anew. |
| A per-chest "leave this chest alone" choice for automatic storage and crafting | [Hearthkeeper](https://thunderstore.io/c/valheim/p/MaddCatter365/Hearthkeeper/) (a manual reserve lock), [SmartContainersIgnore](https://thunderstore.io/c/valheim/p/Botaniclas/SmartContainersIgnore/) (an on/off control in the open chest) | Not stated | The idea only, found while researching (2026-09-24); nothing copied. |
| Square buttons copied from the chest window's own Place stacks button, placed beside it | [HexQuickStackStorage](https://thunderstore.io/c/valheim/p/Hex_Viking/HexQuickStackStorage/) 1.4.3, by Hex_Viking (its Sort button) | No licence | The idea of the copy and its 36-pixel size (`src\WindowButton.cs`); nothing copied. |
| Finding the chests near the player, and claiming one from its owner through the game's own open request | The QoL Pack's chest service (`parts\Chests`), written for Add to Cart; see Add to Cart's credits for what that borrows | GPL-3.0 (the pack) | `parts\Chests`, a copy kept identical by `..\QoLMods\tools\sync-kit.ps1`. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [HexQuickStackStorage](https://thunderstore.io/c/valheim/p/Hex_Viking/HexQuickStackStorage/) 1.4.3 | Hex_Viking | No licence | Optional. Found at start by plugin ID (`com.hex.quickstackstorage`); two of its internal methods are patched: `ContainerService.CanUseContainer`, so its quick stack skips reserved chests, and, with a setting, `QuickStackService.QuickStack(Player, Container)`, its Place stacks. |
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [MultiUserChest](https://github.com/MSchmoecker/No-Chest-Block) | MSchmoecker | MIT | Looked for by plugin ID once every mod has started (by the chest service this mod carries): if it is installed, its two prefixes on the game's open and stack requests are skipped while the chest service holds a chest after a take (they don't check whether an earlier prefix stopped the request). |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's chest hover text, chest press, Place stacks, and HexQuickStackStorage. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. The button is a copy, made at run time, of the game's own Place stacks button, and uses the game's own tooltip. |

## Assets

- The padlock is drawn by the mod's code at run time (the chest service's `ReserveLock.cs`); no picture file ships.
- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
