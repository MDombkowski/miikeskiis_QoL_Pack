# Credits: Trader Circles

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-24, and from XPortal Map Picker's credits for the shared parts.

## Code copied in

None. Every line of this mod was written for it.

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| The idea: circles on the map around where the traders could be, as a search area | His own; the nearest earlier mod is [ShowMeTheGoods](https://github.com/searica/ShowMeTheGoods), by searica | GPL-3.0 | No code taken. ShowMeTheGoods needed the server and drew a new random circle each time; this mod needs only the players' games and keeps each circle fixed. |
| Drawing on its own layer over the big map, clipped to the map's frame and under the pins | [Map Rings](https://thunderstore.io/c/valheim/p/Tchernobill/Valheim_Map_Rings/), by Tchernobill | none found (so nothing copied) | `src\CircleLayer.cs` and `src\CircleGraphic.cs` follow the same approach, written from its description in the research; no code taken. |
| Asking the server the vegvisir's question, and where the answer lands | Valheim's `Game.DiscoverClosestLocation` and `RPC_DiscoverLocationResponse`, by Iron Gate | proprietary | `src\ServerQuestion.cs` sends the same routed RPC; `src\Patches.cs` catches the answers. Nothing copied. |
| Where each trader can be: the camps' data and the world generator's limits | Valheim's `ZoneSystem` and `WorldGenerator`, by Iron Gate | proprietary | `src\Ground.cs` reads the camps' data at run time and calls the world generator's own tests; the three distance limits of meadows, swamps and the Black Forest are written from its code. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (two field names; no code) |
| The fixed random numbers behind each circle: FNV-1a as the hash, SplitMix64 as the stream | Public-domain algorithms (Fowler, Noll and Vo; Vigna) | public domain | `src\CircleFit.cs`, written here |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's handler for the server's answer. |
| Valheim | Iron Gate | proprietary | The game's own classes, compiled against where they are installed; nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
