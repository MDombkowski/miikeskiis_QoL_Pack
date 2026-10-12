# House Trained

Tames that behave at home. Each part has its own switch: your tames stop breaking your buildings (every tame out of the box, or only the kinds you list), creatures' area attacks hurt the creatures around them, your tames stay in their pens (no climbing out on each other's backs, heads kept out of the walls, no biting the wall at an enemy outside, no standing at the wall all day), heal faster there and eat from your hand, every tame follows you as a wolf does, and, with the Hoarstone mod, tames keep to your base's ground and guard it.

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
   - A tame never counts another creature it stands on as ground, so it can't rest on a neighbour's back or climb out over a wall from a stack. A creature it only bumps at the side, and a player (a friend at its legs, feeding it or getting on), change nothing. One left on another creature for a second is nudged off (at 5 m/s out of the box), at most three times in ten seconds.
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

### Healing in pens ("Pens: healing")

- **A hurt tame inside a pen gets back 10% of its full health a minute** (a setting), hungry or not, on top of the game's own healing. Only a pen counts: fences, walls, gates, stakes, poles and beams players built close it in on almost every side (two open directions of sixteen allowed, out of the box), within 40 m. Out in the open it never applies.

### Feeding from the hand ("Feeding from the hand")

- **Look at your tame (or its saddle) and press the hotbar key of a food it eats** (a Cloudberry, Barley or Flax for a Lox): it eats one, as if it had found it on the ground, and a meal from the hand heals 20% of its full health (a setting). Only when it's hungry, out of the box; a tame that won't eat keeps your food back and says why. When a friend's game looks after the tame, that game feeds it.
- **A tame hungry for a long while may heal to full at once.** That is the game's own healing, not this mod's: a tame's slow healing stops while it's hungry, and once it eats, from the ground or from your hand, all the healing it missed comes back at its next heal, a moment later.

### Following ("Every tame follows and stays")

- **E on any of your tames makes it follow you, and E again makes it stay**, as the game lets you do with wolves: boars, Lox, hens, asksvin, moose. It's the game's own command, kept with the creature, so it follows you on a friend's game too, even one without this mod.
- **Its text says what E does:** [E] Follow, or [E] Stay while it follows (the game says [E] Pet, on wolves too). The pet still happens.
- **On a saddle, E rides**, as in the game: point at the creature itself (its head or flank) to make it follow. On a saddled asksvin the saddle covers most of its body, so aim at its head, or set a key for the saddle ("Follow or stay, at a saddle").
- **The staffs' summons** (the Dead Raiser's skeletons, the spirit callers' animals) follow their summoner by themselves and are left as they are, unless "The staffs' summons too" is on.
- **While a tame follows you, "Tames in pens" leaves it alone**, so it can follow you up a step or into a house; told to stay, it's under its rules again, and its spot is kept as a wolf's is.
- **With this part switched off**, E still makes a tame that follows you stay. Before removing the mod, tell your tames to stay: without it, E only pets a tame that isn't a wolf, so it can't be told to stop following.
- A follower doesn't come through a portal with you, as in the game: it waits where you left it and follows again when you come back near.

### With the Hoarstone ("Tames keep to a Hoarstone's ground")

**Only while the Hoarstone mod is on** (in miikeskii's QoL Pack, or its own release); its section's title in F1 says so, and whether it is.

- **A tame on the ground of a settlement with a Hoarstone stays on it:** it wanders only there, and walks back in when it ends up outside (pushed, fled, or after a fight). "The ground" is the settlement's shape that the Hoarstone keeps monsters from spawning in.
- **It leaves only to fight** an enemy (as far as the game lets it chase), then walks back in, **or to follow you** (each a setting, on). Told to stay out there, it stays, as in the game.
- **Return to base** (Left Alt + E, shown on a tame off the ground that has a home to go to): it stops following and walks back to the ground it came from, or to the nearest Hoarstone's.
- **Tames guard the ground:** a tame on it goes for an enemy anywhere on it, not only one it sees or hears, if it can walk there. A tame in a pen goes only while a door or gate of its pen is open (a setting: never, through an open gate, always); a tame told to stay keeps its spot. Deer and other harmless animals are left alone unless you switch them in.
- **Each game decides for the tames it looks after:** the game of the player nearest them, which must run House Trained and Hoarstone for any of this. With nobody near, tames stand still, as in the game.

### Tames that die or go missing ("Write tames' deaths and removals to the log")

- On out of the box, it only writes: a line in the game's log (`BepInEx\LogOutput.log`) whenever one of the world's tames dies on your game (its kind, name, place and what hit it last), and whenever any game removes a tame from the world (and whose game it was). If a tame goes missing, the log says what became of it.

## Good to know

- **Each game decides for the creatures it runs.** A creature's AI, its attacks and its physics run on one computer, usually the first player near it, or its rider. So a friend's game decides for the creatures it runs, by its own settings. A friend without this mod sees the game's own behaviour from the creatures their game runs.
- **Out of the box every part is on.** Switch off the parts you don't want; every setting works at once.
- **Switched off, it leaves nothing behind** (but a tame still following: tell it to stay first, see Following): every part goes back to the game's rules at once, and what it changed on attacks under way, area objects and projectiles in the world is put back (as each friendly fire switch is switched off, too). Only three things are ever written on a tame: the game's own follow or stay (its command), a mark that it was told to stay, and, with the Hoarstone, the place on the ground it goes back to; a game without this mod ignores the last two.
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
- For "With the Hoarstone": the Hoarstone mod (in miikeskii's QoL Pack, or its own release)

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-HouseTrained-<version>.zip`. Keep the author `drumcowski` and the name `HouseTrained` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `HouseTrained.dll` into a folder named `drumcowski-HouseTrained` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1): they sit under one heading, "House Trained", each part behind a title row that opens and closes it. They are kept in `BepInEx\config\modprojects.housetrained.cfg`, which appears after the first start; in the bundle, in its own file, in the sections "House Trained" and "House Trained - <part>".

The lists of tames and creatures have a **Pick** button beside their text box there, which opens a window of everything the list can hold, each with its picture: a click puts a thing on the list or takes it off. The text box stays for typing.

| Part | Setting | Default | What it does |
|---|---|---|---|
| | Enabled | on | The whole mod. |
| | Write tames' deaths and removals to the log | on | Only writes; changes nothing. |
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
| Pens: healing | Tames heal faster in pens | on | |
| | Healing in a pen, % a minute | 10 | Of full health. |
| | How far its pen's walls may be, m | 40 | |
| | Open directions allowed | 2 | Of sixteen around the tame. |
| | Show the healing numbers | on | |
| | Write healing to the log | off | For testing. |
| Feeding from the hand | Feed a tame from the hand | on | |
| | When | OnlyWhenHungry | Or AnyTime. |
| | When a tame won't eat | KeepItAndSayWhy | Or EatItYourself. |
| | Also wild creatures being tamed | off | |
| | A meal from the hand heals, % of full health | 20 | 0 to 100; 0, as in the game. |
| Following | Every tame follows and stays | **on** | E: follow, E again: stay. |
| | Which tames | EveryTame | Or TheList (wolves always follow). |
| | The list | Lox | |
| | The staffs' summons too | off | |
| | Follow or stay, at a saddle | None | A key, at a saddle where E rides. |
| | Say Follow or Stay on a tame | on | In place of [E] Pet. |
| | Pens' rules leave followers alone | on | |
| With the Hoarstone | Tames keep to a Hoarstone's ground | **on** | Only while Hoarstone is on. |
| | Which tames | EveryTame | Or TheList; never the staffs' summons. |
| | The list | Lox | |
| | … they may leave it to fight (and come back) | on | Off: an enemy off the ground is let go of. |
| | … they may leave it while following you | on | Off: they wait at the edge. |
| | "Return to base" | on | |
| | "Return to base" key | Left Alt + E | On a tame off the ground, or its saddle. |
| | Tames guard the ground | on | |
| | Tames in a pen guard it | ThroughAnOpenGate | Or Never, Always. |
| | How near a pen's walls are, m | 20 | 5 to 60. |
| | … against harmless animals too | off | Deer and the like. |
| | How far they go for an enemy, m | 0 | 0, anywhere on the ground; up to 300. |
| | Also settlements without a Hoarstone | off | |
| | Write what it does to the log | off | For testing. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "House Trained". If the game has changed so that a piece of the mod can't find what it needs, that piece stays off and the log says which and what that means; the rest keeps working. An error while playing switches the whole mod off for the session, puts back what it changed, and says so.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\HouseTrained.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and the changelog agree on the version, builds, and writes `dist\drumcowski-HouseTrained-<version>.zip`. Until House Trained is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: written by Claude agents (Anthropic) for miikeskii.
