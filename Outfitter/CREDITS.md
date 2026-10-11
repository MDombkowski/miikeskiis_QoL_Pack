# Credits: Outfitter

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-29 (the vault's research on loadouts, equipment slots and armour stands).

## Code copied in

Nothing from anyone else's mod. Every line was written for miikeskii's own mods: the nearby items drawer (0.3.0, `src\Window\Drawer.cs`, `DrawerItems.cs` and `DrawerUi.cs`) is ported from his Add to Cart's Take tab (`..\AddToCart\src\Browser.cs`, its `Ui` and `Favorites.Star`), so nothing is owed. The mods below were studied (described in the vault's research, in its own words) to learn how the game's inventory and equipment work, and never copied (his rule for every mod).

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| Named gear sets, missing items shown, what he wore going back where the new piece came from | [Armory](https://thunderstore.io/c/valheim/p/DeathMonger/Armory/), by DeathMonger | none found | The idea of a loadout's window; no code taken. |
| Restocking stacks from nearby chests | [Quick Stack - Store - Sort - Trash - Restock](https://thunderstore.io/c/valheim/p/Goldenrevolver/Quick_Stack_Store_Sort_Trash_Restock/), by Goldenrevolver | not stated | Topping a loadout up; no code taken. |
| Equipping directly, without the game's queue | [EquipmentAndQuickSlots](https://thunderstore.io/c/valheim/p/RandyKnapp/EquipmentAndQuickSlots/), by RandyKnapp | none found | Instant equipping (the game's own `EquipItem`, called directly); no code taken. |
| Text codes shared by the clipboard, a version and compressed data | Factorio's blueprint strings, by Wube Software | proprietary game; the format is public | The share code's shape; no code taken. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |
| The Take tab: browsing every item in the chests nearby (the list on the left, Group, Sort, favourites first, the filter, thumbnails with counts) | Add to Cart, by miikeskii (this pack) | his own | The nearby items drawer (0.3.0): its code ported, in `src\Window\Drawer.cs`, `DrawerItems.cs` and `DrawerUi.cs`. |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [EquipmentAndQuickSlots](https://thunderstore.io/c/valheim/p/RandyKnapp/EquipmentAndQuickSlots/) 3.1.3 | RandyKnapp | none found | Optional: its public interface, by reflection, for where its slots are. |
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute, its GUI helpers, and asking a configuration manager to redraw its list. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's inventory window, for picking cells. |
| Valheim | Iron Gate | proprietary | Nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
