# Credits: StoreStash

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-23 (the installed-mods study) and 2026-09-29 (the chest research).

## Code copied in

None. Every line of this mod, and of the chest service, item marks and catalogue it carries (`parts\Chests`, `parts\ItemMarks`, `parts\Catalogue`), was written for the pack. HexQuickStackStorage's code was never opened while it was built (miikeskii's words: "so that my mod stands on it's own merit").

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| What the mod does, as a player sees it: a quick stack key and button into chests that already hold an item, trophies to a trophy chest, Place stacks and holding E by the same rules, favorites by kind with a gold border | [HexQuickStackStorage](https://thunderstore.io/c/valheim/p/Hex_Viking/HexQuickStackStorage/) 1.4.3, by Hex_Viking, itself inspired by Goldenrevolver's Quick Stack Store Sort Trash | No licence | The behaviour only, from its description and the pack's study of what it does for a player; its default key P and miikeskii's settings as the defaults. Nothing copied. |
| Reading its favorites file (one item name per line) to take miikeskii's list in once | HexQuickStackStorage's `HexQuickStackStorage.favorites.txt` | No licence | The file is only read (`parts\ItemMarks\Favorites.cs`); nothing of its code. |
| Sorting and the trash can as a player sees them (0.3.0): a sort key and button that leave the hotbar, worn items and favorites in place and merge part-stacks, a Sort button for an open chest and sorting chests as they open, trash marks by kind with a red border, a trash can that asks first ("don't ask again"), deleting a carried amount | HexQuickStackStorage 1.4.3, by Hex_Viking | No licence | The behaviour only, from the pack's study of what it does for a player; its S key and miikeskii's settings as the defaults. Nothing copied (`src\Sorting`, `src\Trash`). |
| Reading its trash list (one item name per line) to take miikeskii's marks in once | HexQuickStackStorage's `HexQuickStackStorage.junk.txt` | No licence | The file is only read (`src\Trash\TrashMarks.cs`, through the item marks part's `MarkList.Import`). |
| The pack's order, as Add to Cart lists things (families, their groups, progression), and its families, members and kinds as a chest's setup names them, browsed as Add to Cart's Take tab browses them (0.4.0) | The QoL Pack's catalogue (`parts\Catalogue`), written for Add to Cart | GPL-3.0 (the pack) | `parts\Catalogue`, a copy kept identical by `..\QoLMods\tools\sync-kit.ps1`; `src\Sorting\SortOrder.cs` reads it; `src\SetupWindow.cs` browses it, and `parts\Catalogue\ItemPicks.cs` (a setup's picks) was written for chest setups. |
| A quick stack on the client alone that asks each chest's owner first, through the game's own open request | [AutoStash](https://thunderstore.io/c/valheim/p/dbendu/AutoStash/), by dbendu | MIT | The idea only; the pack's chest service (`parts\Chests`) does it its own way. |
| Finding the chests near the player, claiming one from its owner, and holding it after a change | The QoL Pack's chest service (`parts\Chests`), written for Add to Cart; see Add to Cart's credits for what that borrows | GPL-3.0 (the pack) | `parts\Chests`, a copy kept identical by `..\QoLMods\tools\sync-kit.ps1`. |
| A square button copied from the chest window's own Place stacks button | Reserve Stock's padlock button (the pack) | GPL-3.0 (the pack) | `src\SquareButton.cs` (the quick stack's and the personal quick stack's buttons), `src\Sorting\SortTrashButton.cs` (the sort buttons and the trash can) |
| A quick stack into his personal chests only, and a "mine" mark on a chest | miikeskii's own idea (card A43, "Personal Stacker"); the nearest prior art, [SeidrChest](https://thunderstore.io/c/valheim/p/Neobotics/SeidrChest/)'s one bound Personal Chest, was only read about | — | `src\Personal.cs`, `src\Mine.cs` |
| Chests set up to take only the kinds of things picked (families, members, kinds, single things, left-outs, biomes), and quick stack sending those there first (0.4.0) | miikeskii's own idea (card A41, "Stack Allow List"); the nearest prior art, AutoStash's filters, RunicStorage, QuickStackPlus' Smart Storage and BestAutoSort, was only read about (the chest research, 2026-09-29) | — | `src\Setup*.cs`, `src\Setups.cs`, `parts\Chests\ChestSetup.cs`, `parts\Chests\StackRouting.cs` |
| A mark of the pack's in a chest's own record, written only as the chest's owner (claimed first) and sent at once | Reserve Stock's reserve mark (the pack) | GPL-3.0 (the pack) | `src\Mine.cs`, `parts\Chests\ChestMine.cs`, and a chest's setup (`parts\Chests\ChestSetup.cs`) |
| Chests set up alike acting as one chest, tidied by a key so each kind sits in as few of them as possible, moving other things out of the way (0.6.0, "Stack Overflow") | miikeskii's own idea (card A42); the nearest prior art, [ChestButler](https://thunderstore.io/c/valheim/p/EK_Solutions/ChestButler/)'s Organize (a press that previews first, and its most moves per run, 500, the default here), [Hearthkeeper](https://thunderstore.io/c/valheim/p/MaddCatter365/Hearthkeeper/)'s Consolidate (an optional timed cycle), OdinStorage's Consolidate and RunicStorage's SmartSort, was only read about (the chest research, 2026-09-29) | — | `src\Overflow\`; its moves are the chest service's (`parts\Chests\ChestMove.cs`, unchanged) |
| A chest's name kept in the chest's own record under the game's `text` key, as MyLittleUI's rename keeps it (up to 32 characters), so a name given in either shows in both (0.5.0) | [MyLittleUI](https://thunderstore.io/c/valheim/p/shudnal/MyLittleUI/), by shudnal | Unlicense | The key and the length only (`src\SetupName.cs`); its code wasn't copied. |
| Settings folded under one heading in F1, a row opening and closing each feature's | Map View Rules' fold rows (the pack) | GPL-3.0 (the pack) | `src\Folds.cs`, `src\FoldDrawer.cs`, `src\ManagerLink.cs` |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [HexQuickStackStorage](https://thunderstore.io/c/valheim/p/Hex_Viking/HexQuickStackStorage/) | Hex_Viking | No licence | Looked for at start by plugin ID (`com.hex.quickstackstorage`): while it's installed, StoreStash stays off. Its favorites and trash files are read once each. |
| [EquipmentAndQuickSlots](https://thunderstore.io/c/valheim/p/RandyKnapp/EquipmentAndQuickSlots/) | RandyKnapp | not checked | Never touched: the rows below the inventory's own, where it keeps its slots, are left alone. |
| [MultiUserChest](https://github.com/MSchmoecker/No-Chest-Block) | MSchmoecker | MIT | Looked for by the chest service this mod carries (see Reserve Stock's and Add to Cart's credits). |
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute; its GUIManager builds the trash can's question and the setup window in the game's style; its ConfigManagerUtils asks the configuration manager to rebuild its list when a fold row opens or closes. |
| [ConfigurationManager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/) 1.1.22 or later | shudnal | LGPL-3.0 | Not required: with it, each feature's settings fold under a row (the settings' `Category` and `Browsable` hints), and its list is rebuilt through Jötunn when one opens or closes. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's Place stacks button, holding E, the inventory's right-click and its cells' drawing, a chest's hover text (whose a marked chest is, what a set-up chest takes, and the tidy key), the chest window's update (a chest sorted as it opens) and the inventory's (kept open while the trash can asks). |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. The buttons (the two quick stacks', the two sorts', the trash can, the setup and tidy buttons) are copies, made at run time, of the game's own Place stacks button, and use the game's own tooltip, sound and messages. A tidy's plan follows how the game adds and takes items (`Inventory.AddItem`, `FindEmptySlot`, `TopFirst`, `RemoveItem`), modelled in plain code of its own to plan with. |

## Assets

- The button's chest-and-arrow picture, the personal button's (the same with a person in its corner), the sort and trash can pictures, the setup button's tag, the tidy button's two arrows, and the favorite and trash borders are drawn by the mod's code at run time (`src\Icon.cs`, `src\PersonalButton.cs`, `src\Sorting\SortTrashPictures.cs`, `src\SetupIcon.cs`, `src\Overflow\TidyIcon.cs`, `parts\ItemMarks\MarkBorder.cs`); no picture file ships.
- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
