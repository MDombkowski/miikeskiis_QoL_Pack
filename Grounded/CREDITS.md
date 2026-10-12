# Credits: Grounded

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-10-09.

## Code copied in

None. Every line of this mod was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| Lists of prefab names in a setting, compared in lower case and read again when the text changes | Bar None, by miikeskii (this pack) | GPL-3.0 (the pack's) | `src\Logic\NameList.cs` has the same shape as Bar None's `PieceList`; written again for this mod, which can't use another module's code. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (two field names; no code) |

## Studied, nothing taken

Read for what they offer players, by their Thunderstore pages (and Infinity Hammer's `hammer.md` on GitHub); none of their code was read or used. None lays a piece on the floor beneath, passing through rugs.

| Mod | By | Licence | What it does that is near |
|---|---|---|---|
| Infinity Hammer 1.88.0 | JereKuusela | Unlicense | Moves the build ghost up and down by hand (Page Up and Page Down). |
| Runic Precision Build Tool 2.0.10 | Chazman | not stated | A precision mode with fine height steps. |
| Homestead 1.3.2 | sighsorry | not stated | Grid snapping and nudging while building. |
| PrecisionBuild 1.1.1 | awuushy | not stated | A grid on surfaces; pieces don't sink into floors. |
| WallHide 1.0.2 | coemt | not stated | Rugs on tilted surfaces and walls. |
| UnRemove 1.0.0 | Neobotics | not stated | Undoing a removal, offered as a way to overlap pieces. |
| Extra Snap Points Made Easy 2.1.1 | Searica | GPL-3.0 | A centre snap point for pieces without one, rugs included. |
| Gizmo 1.16.0 | ComfyMods | GPL-3.0 | Free rotation on all axes; a list of prefab names in its settings. |
| BuildRestrictionTweaksSync 1.3.0 | Searica | not stated | Switches for each placement refusal. |
| PlanBuild 0.20.0 | MathiasDecrock | WTFPL | Its planned pieces, named `<piece>_planned`, count as their pieces here by a setting. |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's aiming test for building (`Player.PieceRayTest`). |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
