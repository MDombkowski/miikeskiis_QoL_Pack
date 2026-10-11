# Credits: Topper-Upper

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as found on 2026-10-09 by the research for card A77.

## Code copied in

None. Every line of this mod was written for it. The fold-outs in the settings window (`src\Folds.cs`, `src\FoldDrawer.cs`, `src\ManagerLink.cs`) are the pack's own, as House Trained carries them.

## Code or ideas adapted

Prior art was studied for its settings and its approach, from its pages, sources and settings files; no code was taken from any of them.

| What | From | Licence | Where it shows here |
|---|---|---|---|
| A key that switches one station off, saved on it and shown in its hover text | [AutomaticFuel](https://thunderstore.io/c/valheim/p/TastyChickenLegs/AutomaticFuel/), by TastyChickenLegs; [Hearthkeeper](https://thunderstore.io/c/valheim/p/MaddCatter365/Hearthkeeper/), by MaddCatter365 | none stated | The idea of Left Alt + E at a light or station; written its own way (`src\Marks.cs`). |
| Taking only from chests the player's own game already holds | [LazyVikings](https://thunderstore.io/c/valheim/p/blacks7ar/LazyVikings/), by blacks7ar | not stated | The same rule; the pack's chest service does it (`MayChange`). |
| Chests linked to a station, input and output, saved by something other than the network ID | [NishinkusSmelterLink](https://thunderstore.io/c/valheim/p/UllrHeim/NishinkusSmelterLink/), by UllrHeim; [StoreAndCraft](https://thunderstore.io/c/valheim/p/Morda/StoreAndCraft/), by Morda; [RunicProduction](https://thunderstore.io/c/valheim/p/Chazman/RunicProduction/), by Chazman | not stated | Links saved on the station by each chest's place (`src\Logic\LinkText.cs`). |
| A top-up threshold, a reserve per item, a product maximum (the kiln's coal), a minimum age before a product is touched, per-cycle budgets | ServersideQoL's AutoProcess and AutoStore, by ArgusMagnus; [SmartCraftStorage](https://github.com/Zellds/SmartCraft-Storage), by Zellds; [McAutoStore](https://thunderstore.io/c/valheim/p/McLovin/McAutoStore/), by McLovin (MIT); Valheim Plus (AGPL-3.0) | as listed | Settings of this mod's own, each worked out here. |
| A list of woods the charcoal kiln may take | AzuCraftyBoxes, by Azumatt; the pack's own Craft from Chests | not stated / the pack's | "Charcoal kiln: the woods it may take" (Wood alone). |
| Picking loose items up into nearby chests that already hold them: the reach from the item to the chest (64 m), leaving items within 4 m of a player, and leaving food lying near a tame that eats it | [ServersideQoL AutoStore](https://thunderstore.io/c/valheim/p/ArgusMagnus/ServersideQoL_AutoStore/), by ArgusMagnus, studied from its source (research 2026-10-09) | none found | The loose-item pickup's defaults and its fodder rule (`src\Loose\`), written its own way: only items this game owns, in batches; nothing copied. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute, and asking a configuration manager to rebuild its list. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Notes the game's lights and stations as they load, adds hover lines, keeps the game's E from also acting while one of this mod's keys is held, and skips the game's answer to an ownership request for an item on the ground (`ItemDrop.RPC_RequestOwn`) only where it would write log lines and nothing else. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
