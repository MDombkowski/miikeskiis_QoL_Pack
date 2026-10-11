# House Trained

Tames that behave at home. Three parts, each with its own switch: your tames stop breaking your buildings (every tame out of the box, or only the kinds you list), creatures' area attacks hurt the creatures around them, and your tames stay in their pens: no climbing out on each other's backs, heads kept out of the walls, no biting the wall at an enemy outside, and no standing at the wall all day.

**Out of the box every part is on** (since 0.1.1, miikeskii's own settings). Each part changes the game your friends already play: switch off any you don't want. An older settings file still at an old default moves to the new one once; a setting you changed stays as you set it.

## What it does

### Tames spare buildings ("Tames spare buildings")

- **Your tames no longer damage what players built:** every tame out of the box (wolves, boars, hens, asksvin, Lox, the staffs' companions): their bite, their stomp, and their running trample. Walls, floors, fences, chests and stations stay whole. "Which tames" can make it only the tames in a list (Lox, at first) instead.
- **Only the damage is dropped:** a bite still stops at the wall in its way, so tames don't start biting through walls. Wild creatures still damage buildings as before.
- **Ships and carts** are spared too (their own switch, on). **Crops and saplings** have a switch of their own, on, and **the world's own buildings** (ruins, villages) one, off. So do the Trollstav's troll and the Staff of the Wild's roots, which the game doesn't count as tamed.
- **"Share of the damage they still do"** lets a little through instead of none (0% out of the box).
- **Both computers can spare a hit:** the one that runs the tame, before the hit is sent, and the one that owns the building, where it lands. So a building is spared if either runs this mod. At your base your own game usually runs both.
- Trees a stomp fells still fall where they fall.

### Friendly fire ("Area attacks hurt the creatures around them")

- **Wild creatures' area attacks also hurt the creatures around them**, not only their enemies: a troll's ground slam hurts the greydwarfs beside it, a blob's burst the draugr, a fuling shaman's fireball the fulings, a wild Lox's stomp the Lox around it.
- **Each kind has its own switch:**
  - the game's area attacks (stomps, slams, spins);
  - their shockwaves, clouds, fire patches and novas (never a healing or shielding cloud);
  - exploding projectiles' blasts;
  - wide melee swings that sweep through several things;
  - projectiles stopping on friends in their way (off).
- **Never from a tame or a staff's summon, and never on a player.** Your tames are safe from wild creatures' friendly fire unless "Your tames hurt by wild creatures' friendly fire" is on.
- **"Also hurts its own kind"**, **"Bosses' area attacks too"** (a boss's attacks then hurt its own summons), **"Creatures that never deal it"** and **"Share of the damage a friendly hit does"** shape it further. While this part is on, the game's own friendly fire (Morgen's rolls, an asksvin's headbutt) follows the same settings.
- **"Creatures fight back"** (off) lets a creature hurt by a friend fight it for a while (30 s by default).
- **"Only when every player nearby runs the pack"** (off) deals friendly fire only while every player near the creature runs this mod with friendly fire on, so everyone nearby sees the same fight. Games with friendly fire on say hello to each other for this; nothing is written into the world.

### Tames in pens ("Tames in pens")

One switch for the part, and one for each of its four points, all on once the part is on. "Which tames" is every tame out of the box. "What counts as a wall" is pieces players built (or any building piece).

1. **Kept in** ("Pens: kept in"):
   - A tame never counts another creature as ground, so it can't walk up a neighbour's flank, rest on its back, or climb out over a wall from a stack. One left on another creature for a second is nudged off (at 5 m/s out of the box).
   - It can't climb onto a building piece by an edge higher than its own step above the bottom of its body (0.6 m for the Lox, 0.3 m for wolves, boars, hens, asksvin and moose), so it can't walk up onto a fence, a wall or a chest. Floors, stairs and ramps still carry it.
   - A newborn, or an egg, lands on its parent's side of a fence or wall, not inside it or beyond it.
   - A calf growing up, or a chick hatching, that would start inside a wall is moved out of it, toward its side.
   - A ridden tame keeps the game's rules unless "Also while ridden" is on.
2. **Heads out of the walls** ("Pens: heads"): a tame wandering about stops before its head reaches into a wall, allowing for how far it slides once it stops (a moose or an asksvin a metre or so); still turning toward where it's going, it turns in place rather than push its head into a wall; it picks places to stand where its head stays clear (with point 4 on), and walks away from a wall its head is already in (pushed there, or a wall built through it). How far each tame's head reaches comes from the game's own data (a Lox's runs 4.4 m ahead of its middle). Fighting, following and riding are left alone.
3. **No fighting enemies through a wall** ("Pens: enemies behind walls"):
   - A tame doesn't see an enemy behind a wall, measured from its body (a wolf pressing a wall pokes its eyes through it), so it doesn't attack across it: no more biting the pen's wall because a greydwarf stands outside.
   - It lets go of an enemy behind a wall that it has no way to walk to, and takes the nearest one it can reach instead. Once a wild creature breaks the wall or the gate opens, there is a way, and it goes for it.
4. **No standing at the wall** ("Pens: wandering"):
   - A tame wandering about picks, the game's way around where it stands (or the spot it was told to stay at), only places it can walk all the way to, on its own ground (a coop's floor, not its roof), with its body a little way from the walls and its head clear. If nothing fits it stands where it is until its next pick. In the game's own rule a point outside the pen ends at the wall, which is why tames spend most of their time there.
   - A walk that gets nowhere ends.
   - A tame caught near a spawner (boars from runestones and abandoned farms) stops running back toward its old home, and wanders around itself rather than around it. A wolf told to stay keeps its spot.
   - "How far tames wander" scales the game's own range.

## Good to know

- **Each game decides for the creatures it runs.** A creature's AI, its attacks and its physics run on one computer, usually the first player near it, or its rider. So a friend's game decides for the creatures it runs, by its own settings. A friend without this mod sees the game's own behaviour from the creatures their game runs.
- **Out of the box every part is on.** Switch off the parts you don't want; every setting works at once.
- **Switched off, it leaves nothing behind:** every part goes back to the game's rules at once, and what it changed on attacks under way, area objects and projectiles in the world is put back (as each friendly fire switch is switched off, too). Nothing is ever written into the world or into a creature.
- **Not everything can be caught:** an area object with triggers hits a creature on that creature's own game, and on a game that didn't create it, it carries no attacker. Those hits stay as in the game. With a share set, the game adds a flat amount to every creature's hit after it on a world whose world level is raised.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Mods that do the same job

None of these is needed; with one installed, switch off the matching part of House Trained (or that mod's own setting):

- **StructureDamageTweaks** (tames not damaging player structures): "Tames spare buildings".
- **SolidHitboxes** (AI friendly fire): "Area attacks hurt the creatures around them". It must be on the server and every player too; House Trained needs neither.
- **Expand World Factions** (who is whose enemy): "Area attacks hurt the creatures around them", since friendly fire follows the game's own enemies.

A mod that switches idle tames' AI off inside bases (Skarif's ValheimPerformanceOverhaul, its "Tamed Animal Idle Sleep", on in its own defaults) leaves "Tames in pens" nothing to work with for those tames: asleep, they don't move, eat or fight until a player comes within about 7 m, so a pen nobody stands at breeds slower. That's the other mod, not House Trained; switch that setting off in its own menu (F6) if it matters to you.

**Beside ValheimPerformanceOverhaul:** since 0.1.1, House Trained no longer patches `MonsterAI.DoAttack` (its "no attack across a wall" backstop moved to `Humanoid.StartAttack`), so that mod's conflict guard no longer switches its AI throttling off because of House Trained.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-HouseTrained-<version>.zip`. Keep the author `drumcowski` and the name `HouseTrained` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `HouseTrained.dll` into a folder named `drumcowski-HouseTrained` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1): they sit under one heading, "House Trained", each part behind a title row that opens and closes it. They are kept in `BepInEx\config\modprojects.housetrained.cfg`, which appears after the first start; in the bundle, in its own file, in the sections "House Trained" and "House Trained - <part>".

| Part | Setting | Default | What it does |
|---|---|---|---|
| | Enabled | on | The whole mod. |
| Tames spare buildings | Tames spare buildings | **on** | The part's switch. |
| | Which tames | EveryTame | EveryTame or TheList. |
| | The list | Lox | Names separated by commas: as the game shows them, prefab names or $ tokens. |
| | Summons that aren't tames | off | The Trollstav's troll, the Staff of the Wild's roots. |
| | Their running trample too | on | A Lox's or moose's trample above 5 m/s. |
| | Buildings players built | on | |
| | Ships and carts | on | |
| | The world's own buildings | off | Ruins, villages. |
| | Crops and saplings | on | And what they grow into. |
| | Share of the damage they still do, % | 0 | 0 to 100. |
| | Write each spared hit to the log | off | For testing. |
| Friendly fire | Area attacks hurt the creatures around them | **on** | The part's switch. |
| | The game's area attacks | on | Stomps, slams, spins. |
| | Shockwaves, clouds, fire patches and novas | on | Never a healing or shielding cloud. |
| | Exploding projectiles' blasts | on | Also projectiles that burst as they fly. |
| | Wide melee swings too | on | Swings that sweep through several things; a friend in the way stops them. |
| | Projectiles stop on friends in their way | off | A friend in the way shields what's behind it. |
| | Share of the damage a friendly hit does, % | 100 | 0 (none) to 500. |
| | Also hurts its own kind | on | |
| | Bosses' area attacks too | on | |
| | Creatures that never deal it | (none) | Names separated by commas. |
| | Your tames hurt by wild creatures' friendly fire | off | |
| | Creatures fight back | off | |
| | How long a grudge lasts, s | 30 | 5 to 300. |
| | Only when every player nearby runs the pack | off | |
| | How near counts as nearby, m | 100 | 20 to 500. |
| | Write each friendly hit to the log | off | For testing. |
| Tames in pens | Tames in pens | **on** | The part's switch. |
| | Which tames | EveryTame | Or TheList. |
| | The list | Lox | |
| | What counts as a wall | PiecesPlayersBuilt | Or AnyBuildingPiece. For all four points. |
| | Also while ridden | off | The climbing rules for a ridden tame too. |
| | Write what it does to the log | off | For testing. |
| Pens: kept in | No standing on creatures | on | |
| | Nudge off a creature, m/s | 5 | 0 to 5; 0, no nudge. |
| | No climbing onto building pieces | on | |
| | Highest step onto a building piece, m | 0 | 0, each tame's own step; else that, for every tame. |
| | Births land inside | on | Eggs too. |
| | Grown-ups don't start in walls | on | Hatchlings too. |
| Pens: heads | Keep heads out of walls | on | |
| | Margin ahead of the head, m | 0.3 | 0 to 2. |
| | Walk away when the head is in a wall | on | |
| Pens: enemies behind walls | Don't see or attack through walls | on | |
| | Let go of enemies they can't reach | on | And take the nearest one they can. |
| | Let go after, s | 1 | 0 to 10. |
| Pens: wandering | Wander only where they can walk | on | |
| | Keep this far from walls, m | 0.5 | From the body; 0 to 5. |
| | Tries before standing still | 6 | Two at each distance: the game's, half, a quarter… |
| | End a stuck walk after, s | 2 | 0, never. |
| | Ignore a far-away spawn point | on | |
| | … farther than × the wander range | 2 | The tame's own range. |
| | How far tames wander, % | 100 | 25 to 200. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "House Trained". If the game has changed so that a piece of the mod can't find what it needs, that piece stays off and the log says which and what that means; the rest keeps working. An error while playing switches the whole mod off for the session, puts back what it changed, and says so.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\HouseTrained.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and the changelog agree on the version, builds, and writes `dist\drumcowski-HouseTrained-<version>.zip`. Until House Trained is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: written by Claude agents (Anthropic) for miikeskii.
