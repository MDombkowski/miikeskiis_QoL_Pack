# Credits: Hoarstone

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Recorded on 2026-10-10.

## Code copied in

| What | From | Licence | Where |
|---|---|---|---|
| The settings' fold-outs in a configuration manager's window | miikeskii's own House Trained (Map View Rules' fold-outs, as Use Case carries them), in this pack | GPL-3.0 (the pack's) | `src\Folding\` (unchanged but for the namespace) |
| A map layer under the pins, from the world's metres to the map picture | miikeskii's own Trader Circles, in this pack | GPL-3.0 (the pack's) | `src\Draw\MapOutline.cs` (its layer and view, adapted to lines) |
| The Hoarstone's entry put in and out of a tool's menu | miikeskii's own Terraformer, in this pack | GPL-3.0 (the pack's) | `src\Stone\Menu.cs` (its menu refresh, adapted) |

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| The shape method: DBSCAN, grow, fill what is enclosed, shrink back, margins | The Settlement Shape Lab, written for miikeskii (2026-10-10), from published methods: DBSCAN (Ester, Kriegel, Sander and Xu, KDD 1996); closing and filling holes (Gonzalez and Woods, via the University of Edinburgh's HIPR2, and SciPy's `binary_fill_holes`); the alpha hull as a closing (Pateiro-López and Rodríguez-Casal); the distance transform (Felzenszwalb and Huttenlocher, Theory of Computing 2012); marching squares (Lorensen and Cline, via scikit-image) | the lab: GPL-3.0 (the pack's); the methods: published papers, no code taken | `src\Shapes\ShapeMath.cs` and `Outline.cs`, a line-for-line port of the lab's own script |
| The game's own spawn rules, base areas, Wards, pieces, zones and heightmaps | Valheim, by Iron Gate | proprietary | Read from the installed game to know where to hook; nothing copied |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute, and asking a configuration manager to rebuild its list. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's spawn tests, the Ward's hover text and E, placing and taking down pieces. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. The outline's dashes are the game's own Ward circle segments, used in the running game. |

## Assets

- The Hoarstone's picture in the hammer's menu is the game's own Ward's, in the running game.
- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
