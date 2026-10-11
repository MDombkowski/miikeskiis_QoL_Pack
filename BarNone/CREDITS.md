# Credits: Bar None

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-29 (the vault's research on see-through pieces) and checked again on 2026-09-30.

## Code copied in

None. Every line of this mod was written for it. It replaces JereKuusela's Smoke_Collision and Projectile_Collision, which were studied (described in the vault's research, in its own words) to learn how the game's smoke and projectiles work, and never copied (his rule for every mod).

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| Smoke passing listed pieces by ignoring the pair at a puff's first touch; a fire's clear-space test run again without listed pieces, its length a setting | [Smoke_Collision](https://github.com/JereKuusela/valheim-smoke_collision), by JereKuusela | Unlicense (public domain) | The smoke part; no code taken. Bar None stands aside for it. |
| Projectiles skipping a hit on a listed piece, by shooter and size, and a log line per hit to find names | [Projectile_Collision](https://github.com/JereKuusela/valheim-projectile_collision), by JereKuusela | Unlicense (public domain) | The projectile part (its shooters split four ways); no code taken. Bar None stands aside for it. |
| Changing a plant's sky test with a patch on the game's own method | [PlantEverything](https://thunderstore.io/c/valheim/p/Advize/PlantEverything/), by Advize | GPL-3.0 | The sky part, per piece instead of everywhere; no code taken. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's hover, smoke, fire, projectile, plant, beehive, windmill and wisp tests. |
| Valheim | Iron Gate | proprietary | Nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
