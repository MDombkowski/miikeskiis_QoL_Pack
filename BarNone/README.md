# Bar None

See-through pieces that don't get in the way: cage walls and floors, crystal walls, carved darkwood dividers, roundpole fences and the like. Reach through them to what's behind (fuel a fire, open a chest, pick a crop), let smoke, projectiles and the sky through them, and let dropped items fall through them (eggs and meat through a pen's floor), each part with its own switch and its own list of pieces. It replaces JereKuusela's Smoke_Collision and Projectile_Collision, and each of its parts stands aside while the matching one of those is installed.

**Out of the box, reaching, smoke, projectiles and the sky part are on** (miikeskii's own settings, since 0.2.1): you reach through the listed pieces, smoke and every shot but ballistas' and tames' pass them, and crops and saplings under a cage floor grow. The sky part matters for everyone: a crop that can't grow is destroyed when it ripens, by whichever player's game has it then. Items are off until you switch them on. An older settings file still at an old default moves to the new one once; a setting you changed stays as you set it.

## What it does

- **Reach through see-through pieces** ("Reach: through see-through pieces"). What's behind a listed piece can be used as if the piece weren't there, within your usual reach (3.5 m from your eyes): add fuel to a fire (hold E to keep adding), open a chest, use a smelter's switches, pick crops, berries and mushrooms, pick up an item, put food on a cooking station or a trophy on a stand. Everything that acts on what you point at follows, other mods' too.
  - **Gates, doors and shutters are never reached through**, even if listed, so they still open; nor is anything else you can use.
  - Stations still want you close: a workbench behind a cage wall is usable only within 2 m of it (the forge 1.7 m), as in the game.
  - Wards and a private chest's owner still count, as in the game.
  - The hammer's repair and remove are untouched: listed pieces stay repairable and removable.
  - A creature behind the bars is out of reach unless "Reach: creatures behind them" is on (then you can pet or command a tame through them).
- **Smoke passes through** ("Smoke: passes through pieces"). Smoke rises through the listed pieces instead of piling up under them, so a fire, a smelter, a kiln or a furnace under a cage floor keeps going (they stop when their own smoke is trapped). A fire right under a listed piece keeps burning too ("Smoke: fires burn under listed pieces"; the game puts a fire out while something solid is within half a metre above its flames).
  - The smoke then fills whatever is above and smokes whoever is there.
  - Each game moves its own smoke, so a friend without this sees it pile up as usual, and can choke where you don't.
- **Projectiles pass through** ("Projectiles: pass through pieces"). Arrows, bolts, spears and staffs' shots fly through the listed pieces, for the shooters switched on: players' and enemies' by default (switch enemies' off to keep a cage a shield against them); ballistas' and catapults', and tames', each a switch, off. "Projectiles: largest that passes" keeps big ones (bombs, fireballs) out.
  - Each shot is flown by its shooter's game, so a friend's arrows pass only if their game has this on too.
  - "Projectiles: write each hit to the log" names every projectile, its size and what it hit, to find a piece's name for a list.
  - Another mod that acts on projectile hits (one that drops a spent arrow where it hit, say) may still act where a shot passed a cage wall: Bar None skips only the game's own hit.
- **The sky through** ("Sky: crops and saplings see the sky through pieces", on). Crops and saplings under listed pieces see the sky, so they grow. PlantEasily's green and red planting ghosts follow, since they ask the same test.
  - **Keep it on, with the same list, for every friend who may be near your fields:** whichever game has a crop when it ripens decides whether it grew, and one that says "no sun" destroys it. A friend without the pack can still wipe a caged field.
  - Beehives and birds' nests, windmills, and the Wisp Fountain and Eternal Pyre each have a switch of their own, on: they don't count listed pieces as cover.
  - **Nothing else changes:** shelter, resting, beds, crafting stations, fermenters, eggs hatching, fires in rain and wind, the Ashlands heat and the weather's wear on pieces all keep the game's rules, so a cage room still shelters you as before.
- **Items fall through** ("Items: fall through pieces"). Loose items fall through the listed pieces instead of landing on them: a creature's drops, the eggs a hen lays, what you drop yourself, what a smelter or a kiln puts out, and what was already lying there. So a pen with a cage floor over a collecting room lets its eggs and meat down. Food set out as a piece (on a table) stays; creatures, carts and ships never fall, nor do fish, swimming or caught (the game makes a fish one object, a creature in the water).
  - The Straw rug already lets items through in the game, as most rugs do (the deer and bear rugs' heads don't), so straw on a listed cage floor lets them through both.
  - **"Items: except these items"** keeps the items it names on top: the food you drop for your animals, which would otherwise fall out of their reach. List all of it: an animal goes only for the food nearest it, so one piece fallen out of reach can keep it hungry, and a hungry animal doesn't breed (nor does a hen lay). A boar eats `Carrot, Turnip, Onion, Mushroom, Raspberry, Blueberries`; a hen `Dandelion, Barley, BeechSeeds, BirchSeeds, CarrotSeeds, OnionSeeds, TurnipSeeds`.
  - Each item falls as the game that owns it moves it (a creature's drops and eggs by the game that was running the creature, what you drop by yours), so in multiplayer an item a friend's game owns falls only if their game has this on too, with the same lists. Everyone sees it where its owner's game puts it.
  - Flametal and snowballs fall through only until they first touch the ground: the game then carries them on a body of its own, which lands on pieces as usual.
  - An item that falls through a floor lying on the ground rests under it. To pick it up there, switch "Reach: through see-through pieces" on with that floor in its list (the cage floors are), or walk over it (eggs, and what you've just dropped yourself, aren't picked up that way).
  - "Items: write each touch to the log" names each item and each piece it touches, and whether your game lets it fall, to find names for both lists.

## The pieces

Each part has its own list, by the pieces' prefab names (the names in the game's data), separated by commas. The Cage Floor 1x1 has two: `iron_floor_1x1` for older builds and `iron_floor_1x1_v2` for new ones; every list has both.

| Piece | Prefab | Reach | Smoke | Projectiles | Sky | Items |
|---|---|---|---|---|---|---|
| Cage Floor 1x1 | `iron_floor_1x1_v2`, `iron_floor_1x1` | ✓ | ✓ | ✓ | ✓ | ✓ |
| Cage Floor 2x2 | `iron_floor_2x2` | ✓ | ✓ | ✓ | ✓ | ✓ |
| Cage Wall 1x1, 2x2 | `iron_wall_1x1`, `iron_wall_2x2` | ✓ | ✓ | ✓ | ✓ | |
| Iron Gate | `iron_grate` | (a door) | ✓ | ✓ | ✓ | |
| Dvergr Metal Wall | `piece_dvergr_metal_wall_2x2` | ✓ | ✓ | | ✓ | |
| Crystal Wall 1x1 | `crystal_wall_1x1` | ✓ | (glass) | (glass) | ✓ | |
| Carved Darkwood Divider | `darkwood_decowall` | ✓ | ✓ | | ✓ | |
| Roundpole Fence | `wood_fence` | ✓ | ✓ | | ✓ | |
| Roundpole Gate | `wood_fence_gate` | (a door) | ✓ | | ✓ | |
| Ashwood Decorative Wall, Divider, Window | `ashwood_decowall_2x2`, `ashwood_decowall_divider`, `ashwood_decowall_tree` | ✓ | ✓ | | ✓ | |
| Wood Stairs, Wood Ladder | `wood_stair`, `wood_stepladder` | | ✓ | | | |

## Good to know

- **Only you need it**, for reaching, smoke and projectiles: each works on your own game. Items fall by the game that owns them (above). The sky part is the one to share (above).
- **Changes work at once.** A list or a switch changed in game counts from the next frame: what you point at, smoke (puffs already in the air follow a changed list too), projectiles, items (one already lying on a newly listed piece falls through it, and one lying on a listed piece when its area loads, within a few seconds), crops (they re-check several times a second), fires (tested again a moment after the change) and windmills (measured again at once). Beehives and wisp fountains follow at their own next check, within seconds.
- **Switched off, it leaves nothing behind:** every part goes back to the game's rules, the same way. Crops under cage floors then have "no sun" again, and die when they ripen.
- **Replacing JereKuusela's mods:** uninstall Smoke_Collision and Projectile_Collision. While one is installed, Bar None's matching part stays off and says so in the log. Bar None's smoke part doesn't change the smoke's size, speed or lifetime, which Smoke_Collision could.

## Mods that do the same job

If you prefer another mod for a part, switch that part of Bar None off and use the other:

- **Smoke_Collision** (JereKuusela): switch off "Smoke: passes through pieces" (it stays off by itself while Smoke_Collision is installed).
- **Projectile_Collision** (JereKuusela): switch off "Projectiles: pass through pieces" (it stays off by itself while Projectile_Collision is installed).
- **PlantEverything** (Advize): with its "crops require sunlight" off, crops grow under anything, and Bar None's sky part has nothing left to do; switch "Sky: crops and saplings see the sky through pieces" off if you like. With it on, the two work together.
- **FloorsAreRoofs** (bonesbro) makes wood floors keep the rain off; it doesn't touch the pieces Bar None lists, so both can stay.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.barnone.cfg`, which appears after the first start, one section per part; in the bundle, in the section "Bar None" of its own file.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off stops every part at once. |
| Reach: through see-through pieces | on | Use what's behind a listed piece within your reach. |
| Reach: pieces | the list above | Prefab names, separated by commas. |
| Reach: creatures behind them | on | On: pet or command a tame through the bars, and see a creature's name and health. |
| Reach: only while holding | none | A key to hold for reaching through; none: always. |
| Smoke: passes through pieces | on | Smoke rises through listed pieces. |
| Smoke: pieces | the list above | Prefab names. |
| Smoke: fires burn under listed pieces | on | Only while smoke passes: listed pieces don't put out a fire right under them. |
| Smoke: clear space above fires (m) | 0.5 | The game's is 0.5; 0 turns the test off. Only while smoke passes. |
| Projectiles: pass through pieces | on | Projectiles fly through listed pieces. |
| Projectiles: pieces | the list above | Prefab names. |
| Projectiles: players' shots | on | Yours, and friends' on their own games. |
| Projectiles: ballistas' and catapults' shots | off | |
| Projectiles: tames' shots | off | |
| Projectiles: enemies' shots | on | Also the world's own (hot sparks, a tree's spawn). |
| Projectiles: largest that passes (m) | 0 | By the projectile's radius; 0 = any size. |
| Projectiles: write each hit to the log | off | The projectile, its size, its shooter, what it hit, and whether it passed. |
| Sky: crops and saplings see the sky through pieces | on | For everyone (see above). |
| Sky: pieces | the list above | Prefab names; keep it the same for every friend near your fields. |
| Sky: also through everything rain passes | off | Crops see the sky through everything the game lets rain through, wood floors too. |
| Sky: pieces take no growing space | off | A listed piece close to a crop doesn't take its room. |
| Sky: beehives and birds' nests see through them | on | |
| Sky: windmills see through them | on | More wind behind cage walls. |
| Sky: Wisp Fountain and Eternal Pyre see through them | on | |
| Items: fall through pieces | off | Loose items fall through listed pieces. |
| Items: pieces | the list above | Prefab names. |
| Items: except these items | none | Item prefab names that never fall through: your animals' food, all of it. |
| Items: write each touch to the log | off | Each item and each piece it touches, and whether your game lets it fall. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Bar None". If the game has changed so that a part can't find what it needs, or can't be hooked in (another mod's broken change to the same part of the game can cause that), only that part stays off, and the log says which; the others keep working. An error while playing switches reaching, smoke, projectiles and items off for the session (pieces block them as in the game; windmills then follow a changed switch within 5 seconds), but **never the sky part**, which keeps your caged crops alive. An error in one of the sky part's own tests switches only that test off, until the game is started again, and the log says what that means: if it's the crops' own, crops under listed pieces have "no sun" and die when they ripen, as in the game.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-BarNone-<version>.zip`. Keep the author `drumcowski` and the name `BarNone` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `BarNone.dll` into a folder named `drumcowski-BarNone` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\BarNone.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-BarNone-<version>.zip`. Until Bar None is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: its code was written by Claude agents (Anthropic), directed by miikeskii.
