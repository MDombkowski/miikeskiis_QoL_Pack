# Credits: Craft from Chests

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-23 (the installed-mods study) and 2026-09-27.

## Code copied in

None. Every line of this mod, and of the chest service it carries (`parts\Chests`), was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| The whole idea and what it does: crafting, building and filling stations from nearby chests, "available/required" amounts in the requirement lists, the buildable count after the piece's name, a key to fill a station, a key to switch it off, leaving one item in each chest, the charcoal kiln taking only Wood (its default rules file), personal chests left out | [AzuCraftyBoxes](https://thunderstore.io/c/valheim/p/Azumatt/AzuCraftyBoxes/) 1.8.19, by Azumatt (source: the [Canabalooza fork](https://github.com/Canabalooza/AzuCraftyBoxes)) | MIT No Attribution | The whole mod, written anew; nothing copied. Its settings' defaults, keys and colours are kept so it feels the same. Unlike it, every take asks the chest's owner first (the chest service), and the check before a craft counts only chests already handed over. |
| Finding the chests near the player, and taking from them only once their owner has handed them over | The QoL Pack's chest service (`parts\Chests`), written for Add to Cart; see Add to Cart's credits for what that borrows | GPL-3.0 (the pack) | `parts\Chests`, a copy kept identical by `..\QoLMods\tools\sync-kit.ps1`. |
| Taking from a chest someone has open by asking the game of the player who has it open, which changes the chest and answers with the items | [MultiUserChest](https://github.com/MSchmoecker/No-Chest-Block) 0.6.2, by MSchmoecker | MIT | `parts\Chests\ChestShare.cs`, written anew with its own messages (a hello between games on the pack, a take by item and amount, the reply in the game's own item format, timeouts); nothing copied. Studied 2026-09-28. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [AzuCraftyBoxes](https://thunderstore.io/c/valheim/p/Azumatt/AzuCraftyBoxes/), and CraftFromContainers (plugin ID `aedenthorn.CraftFromContainers`, as AzuCraftyBoxes names it) | Azumatt, aedenthorn | MIT No Attribution, not checked | Looked for at start by plugin ID: while either is installed, this mod stays off. |
| [MultiUserChest](https://github.com/MSchmoecker/No-Chest-Block) | MSchmoecker | MIT | Looked for by plugin ID once every mod has started: if it is installed, a chest someone has open isn't claimed, and its two prefixes on the game's open and stack requests are skipped while the chest service holds a chest after a take (they don't check whether an earlier prefix stopped the request). |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's crafting, building, stations and chest requests. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
