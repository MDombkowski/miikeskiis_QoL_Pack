# Credits: World Settings

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time.

## Code copied in

None. Every line of this mod, and of the parts it carries (`parts\DayCycle`, `parts\Chests`), was written for miikeskii's QoL Pack.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| The day's split between day and night, where a morning begins and a sleep's skip | Valheim (`EnvMan.RescaleDayFraction`, `GetMorningStartSec`, `SkipToMorning`), by Iron Gate | proprietary | `parts\DayCycle` works the same way with a night of any length; nothing copied. |
| The QoL Pack's side of the handshake with miikeskii's Server-Side QoL Pack | The QoL Pack's chest service (`parts\Chests\ServerPack.cs`) | GPL-3.0 (the pack) | `parts\Chests`, a copy kept identical by `..\QoLMods\tools\sync-kit.ps1`; only its hello and the server's answer are used. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| miikeskii's Server-Side QoL Pack, with World Settings (Server) | miikeskii | GPL-3.0 | On the server: answers with the day's and night's length, the weather's and the wind's. |
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Three prefixes on the game's environment (the day's split, where a morning begins, a sleep's skip). |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |

## Assets

- The package's icon is the pack's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod has its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
