# Credits: House Trained

What this mod borrows, from whom, and under which licence, kept so the mod can be published properly at any time. Licences as recorded on 2026-09-29 and 2026-09-30 (the vault's research on creature damage and on tames in pens).

## Code copied in

None. Every line of this mod was written for it. The mods below were studied (described in the vault's research, in its own words) to learn how the game's creatures, attacks and pieces work, and never copied (his rule for every mod).

## Code or ideas adapted

| What | From | Licence | Where it shows here |
|---|---|---|---|
| Tames not damaging buildings players placed, decided where the hit lands on a piece | [StructureDamageTweaks](https://github.com/Gra-tak/ValheimStructureDamageTweaks), by Gratak | none found | The buildings part, which also stops the hit where it is sent, with its own lists and share; no code taken. |
| Creatures' attacks hitting their friends, with a damage multiplier | [SolidHitboxes](https://github.com/karkkant/valheim-solid_hitboxes), by Korppis | MIT | The friendly fire part, for area attacks, their area objects and blasts, with prefixes and postfixes only; no code taken. |
| A tame forgetting the spawn point a spawner gave it | [Valheim Community Patch](https://github.com/MidnightsFX/Valheim-Community-Patch), by MidnightsFX | GPL-3.0 | "Ignore a far-away spawn point", which ignores the point instead of clearing it, so nothing is written to the tame; no code taken. |
| The display-hint class for configuration managers: its class name and field names are the convention those managers look for | [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager), by the BepInEx team | LGPL-3.0 | `kit\ConfigurationManagerAttributes.cs` (field names; no code) |

## Works with, at run time (not shipped here)

| What | By | Licence | How it is used |
|---|---|---|---|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2 | JotunnLib Team | MIT | Required: its network-compatibility attribute, and asking a configuration manager to redraw its list. |
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.4.23 | BepInEx team | LGPL-2.1 | The mod loader. |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) 2.9.0 | BepInEx team | MIT | Patches the game's piece and character damage, its attacks and area objects, creatures' ground contact, sight, targets and wandering. |
| Valheim | Iron Gate | proprietary | Nothing of the game ships here. |

## Assets

- The package's icon is the bundle's, drawn by `..\QoLMods\tools\make-icon.ps1`, until this mod is released on its own.

## Written with AI

The code and these files were written by Claude (Anthropic) agents for miikeskii. Thunderstore's rules ask that such a package carry its "AI Generated" category.
