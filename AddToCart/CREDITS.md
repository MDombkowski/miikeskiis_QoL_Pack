# Credits: Add to Cart

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-24.

## Code copied in

None. Every line of this mod, and of the chest service and the catalogue it carries (`parts\Chests`, `parts\Catalogue`), was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| Finding the chests near the player: a list of every chest as it loads (a patch on `Container.Awake`), as AzuCraftyBoxes keeps its own; the game's own access test reached by reflection | [AzuCraftyBoxes](https://thunderstore.io/c/valheim/p/Azumatt/AzuCraftyBoxes/), by Azumatt (the list), and [HexQuickStackStorage](https://thunderstore.io/c/valheim/p/Hex_Viking/HexQuickStackStorage/), by Hex_Viking (the access test) | MIT No Attribution, none stated | `parts\Chests\ChestFinder.cs` and `ChestHooks.cs`, written anew; nothing copied. |
| Asking a chest's owner with the game's own request before changing it, so it works next to players without the mod | [AutoStash](https://github.com/dbendu/valheim-auto-stash), by dbendu (its place-stacks request), and [Stackmaster](https://thunderstore.io/c/valheim/p/JStack424/Stackmaster/), by JStack424 (ownership before taking, recheck after) | MIT, MIT | `parts\Chests\ChestClaims.cs` uses the open request instead, and waits for the ownership itself; no code taken. |
| Leaving one of each item in every chest, and reading that setting from AzuCraftyBoxes | [AzuCraftyBoxes](https://thunderstore.io/c/valheim/p/Azumatt/AzuCraftyBoxes/), by Azumatt | MIT No Attribution | `src\Settings.cs` reads its "Leave One Item" setting, never writes it. |
| Pulling a build piece's materials from chests | [GrabMaterials](https://thunderstore.io/c/valheim/p/DeathMonger/GrabMaterials/), [HammerCraftingMaterials](https://thunderstore.io/c/valheim/p/DrummerCraig/HammerCraftingMaterials/), [Nearby Chests](https://thunderstore.io/c/valheim/p/TeamRobo/Nearby_Chests/) | none found, MIT, MIT | The idea only; the menu, cart and preview are his design. |
| Checking the biome working-out, and the catalogue's list of what isn't a building: Jötunn's generated lists of every vanilla piece, recipe, item and location | [Jötunn documentation](https://valheim-modding.github.io/Jotunn/data/), by the JotunnLib Team | MIT (the Jötunn repository) | Nothing shipped: `parts\Catalogue\Biomes.cs` works everything out from the game's own data at run time, and its home biomes for things found only in locations were checked against the location list. |
| How players group Valheim's items: the families' names and members, the tools by use, the weapons by type, bait with the fishing rod | [The Valheim wiki](https://valheim.fandom.com) (its Materials and Food pages, and its MaterialsNav, ToolsNav and WeaponsNav navboxes), by its contributors | CC BY-SA 3.0 (Fandom) | `parts\Catalogue\Taxonomy.cs` names the groups, read from the game's own data first; no text copied. |
| The browsing design: a list on the left picking what shows, headings that follow the pick, progression order, the player's own lists beside the categories | UX research (Nielsen Norman Group, the Baymard Institute and others) and other games' menus (Factorio, Minecraft) | ideas only | `parts\Catalogue\Browse.cs` and the window; nothing copied. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (two field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute, and its GUI styling (wood panel, buttons, text, the filter box, the Group and Sort lists, the checkboxes) and input blocking for the window. |
| [AzuCraftyBoxes](https://thunderstore.io/c/valheim/p/Azumatt/AzuCraftyBoxes/) | Azumatt | MIT No Attribution | Optional: its "Leave One Item" setting is read, if it's installed. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's handling of a chest owner's answer, so the service's own requests don't open a chest window. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.
- The thumbnails are the game's own icons, the favorite star is the build menu's own star, and the arrows that open a family in the list on the left are the game's own marker (the one Jötunn's dropdowns use), all read at run time.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
