# Credits: PowerWheel

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Recorded on 2026-10-09, from the research on card A75 (`research/2026-10-09_powerwheel-forsaken-power-wheel.md` in the project's vault) and Map Zoom's credits for the shared parts.

## Code copied in

None. Every line of this mod was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| The idea: hold the power key for a wheel of the forsaken powers, icons for all, names on hover | miikeskii's own idea (2026-10-09) | — | The whole mod. |
| Opening a page of a mod's own on the game's radial wheel, through a subclass of the game's root page | [BossRules](https://github.com/sighsorry1029/BossRules) 1.1.6, by sighsorry: its "Remote Forsaken Power Selection" (opened with K, not by holding the power key) | GPL-3.0 | Its `ForsakenPowerRadial.cs` was read only to learn how the game's wheel takes a page from a mod; no code taken. `src\Wheel.cs` and `src\Controls.cs` were written from the game's own code. |
| Hold to open a wheel, let go to pick, letting go in the middle keeps what you had | PingWheel 0.2.5, by Ains; Skeleton Selector 1.2.1, by ITBunnies; EquipWheelRadial 1.0.0, by zonzcroupi | MIT (PingWheel); not stated | Seen only in their READMEs; no code read or taken. |
| What counts as earned: a power taken at its stone, from the character's own record | ForsakenPowersPlusRemastered 2.0.4, by turbero (studied for the project in 2026-09) | none found | `src\PowerRules.cs` asks the game's own record (`Player.HaveUniqueKey`); nothing copied. |
| The wheel itself, its look, its controls and the hold time of 0.33 s | Valheim's radial menu (`Valheim.UI.RadialBase`) and `Player.HandleRadialInput`, by Iron Gate | proprietary | Used whole at run time through its public members; the 0.33 s is the game's own hold for its gamepad wheel. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Runs this mod's code just before the game's player update each frame, and hides the wheel's drop hints on the power page. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |
| Unity's Input System and TextMeshPro | Unity Technologies | Unity Companion License | Reads whether the power key is still held; draws the names under the icons and the description beside the wheel. Both ship with the game. |

## Assets

- The padlock on a locked power is drawn in code (`src\Padlock.cs`).
- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
