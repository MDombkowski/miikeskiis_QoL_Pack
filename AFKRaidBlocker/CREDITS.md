# Credits: AFK Raid Blocker

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Recorded on 2026-10-10.

## Code copied in

None. Every line of this mod was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| How the game picks where a raid starts: each player's report (`ZNet.m_serverSyncedPlayerData`, its "baseValue" and "possibleEvents"), sent by `ZNet.SendServerSyncPlayerData` and read by `RandEventSystem.RetrievePlayerEventData` before each roll | Valheim, by Iron Gate | proprietary | `src\Patches.cs` changes only what his report says while he is away; nothing copied. |
| The emotes, chairs and beds as the game keeps them (`ZDOVars.s_emote`, `Player.InBed`, `Player.GetAttachPoint`, `Chair`) and the names over heads (`EnemyHud.UpdateHuds`) | Valheim, by Iron Gate | proprietary | `src\Away.cs` and `src\Mark.cs` read them; nothing copied. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (two field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
