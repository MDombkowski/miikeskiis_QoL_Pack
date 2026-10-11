# Credits: Terraformer

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-23 and 2026-09-24 (the vault's research on AdvancedTerrainModifiers) and checked again on 2026-09-30.

## Code copied in

None. Every line of this mod was written for it. It replaces AdvancedTerrainModifiers, whose code was studied to learn what it does and how the game's terrain works, and never copied (his rule for every mod). The fold-outs in the settings window (`src\Folds.cs`, `src\FoldDrawer.cs`, `src\ManagerLink.cs`, since 0.3.0) are the pack's own, as Topper-Upper and House Trained carry them.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| The whole mod's feature list: resizable and sharpenable terrain tools, square tools snapped to the ground's grid, a precision raise, a tool that puts the ground back, height-neutral paths, the shovel's "Lower ground" | [AdvancedTerrainModifiers](https://github.com/searica/TerrainTools), by Searica | GPL-3.0 | The tools and what each does; no code taken. Terraformer replaces it: it stays off while that mod is installed. |
| Scrolling a terrain tool's size with a key held | [HoeRadius](https://github.com/aedenthorn/ValheimMods), by aedenthorn | public domain | The size key; no code taken. |
| Square terrain tools that follow the ground's grid, with a marked square on the ground | [OCDheim](https://github.com/java-devil/OCDheim), by javadevils | MIT | The square tools' idea; no code taken. |
| Choosing a stroke's zones with its full size plus a margin | [PlanBuild](https://thunderstore.io/c/valheim/p/MathiasDecrock/PlanBuild/)'s terrain tools, by MathiasDecrock | WTFPL | How a big stroke reaches every zone it touches; no code taken. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute, and asking a configuration manager to rebuild its list (the settings' fold-outs, since 0.3.0). |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's terrain tools. |
| [PlantEasily](https://thunderstore.io/c/valheim/p/Advize/PlantEasily/) 2.3.0 | Advize | not checked (only studied, never shipped or copied) | Since 0.3.0: when it plants a row or grid of saplings with the grown tree's preview showing, every sapling of that click gets the picked tree. Its code was read to learn how it places them; nothing of it was copied. |
| Valheim | Iron Gate | proprietary | The game's own terrain pieces are copied at run time with changed settings; nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.
- Its tools' pictures in the build menu are the game's own terrain pictures, with small marks drawn on them by the mod as the game starts. The paved and cultivated paths' pictures (since 0.2.0) are the game's Pathen picture filled, as the game starts, with the colours of its own Paved road and Cultivate pictures. Nothing of the game's pictures ships here, and none of AdvancedTerrainModifiers' pictures is used.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
