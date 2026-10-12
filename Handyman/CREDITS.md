# Credits: Handyman

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Recorded on 2026-09-30, from research done on 2026-09-29 and Map Zoom's credits for the shared parts.

## Code copied in

None. Every line of this mod was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| The idea: R picks the hammer's Repair instead of putting the hammer away | miikeskii's own idea (2026-09-29) | — | The whole mod. |
| A key that picks Repair while held | ZenConstruction's hammer key (press and hold for Repair), by ZenDragon | none found | Seen only in its README; no code read or taken. Here a hold of the key puts the hammer away and a tap picks Repair. |
| Tap versus hold of the hide button | Valheim's `Player.Update`, by Iron Gate: its gamepad hide button counts as a tap only when let go within 0.33 s | proprietary | `src\KeyWatch.cs` tells a tap from a hold in its own way, with the same 0.33 s as its default; nothing copied. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Runs this mod's code just before the game's player update each frame. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |
| Unity's Input System | Unity Technologies | Unity Companion License | Reads whether the game's Hide key is still held; it ships with the game. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
