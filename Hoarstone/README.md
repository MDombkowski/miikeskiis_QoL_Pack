# Hoarstone

No more hidden torches in every corner of your base. Hoarstone works out a settlement's whole shape from the pieces you built, closes the gaps, follows the coast, and keeps monsters from spawning anywhere inside it. Place a Hoarstone to mark a settlement.

## What it does

- **A settlement's whole shape.** Pieces built close together are one settlement; a road of torches doesn't join two places, and an outpost down the road is its own. Gates and broken stretches of a wall narrower than "Gaps to close" are closed, and everything a wall encloses is inside, even where the wall stops at the sea. A margin goes all round, and a shore margin out over the water. Ground you raised or dug with the hoe counts.
- **No monsters spawn inside it**, as none spawn near a campfire or a workbench today. Raid monsters spawn outside and come in. The world's fixed spawn points inside it stop too; nests can, with a setting.
- **The Hoarstone** sits next to the Ward in the hammer's menu, once you know its materials: a Troll, an Abomination and a Stone Golem trophy, Ectoplasm ×10, Root ×10 and Ymir Flesh ×3 (a setting). It marks its settlement. Point at it to see the settlement's outline; press E on it to keep the outline shown, and again to hide it. Taking it down gives its cost back.
- **Which settlements**: only those with a Hoarstone in them, out of the box, so nothing changes until someone places one. Set "Where" to EverySettlement and every settlement counts, with or without one.
- **Outlines**: Left Alt + F3 shows every settlement's outline, on the ground around you and on the map. Settlements that don't keep monsters out are outlined too, in grey, to show where a Hoarstone would make one.
- **Any size.** A settlement too big for one game to hold at once still works: once your game has seen all of it (walk round it, then through it), each part's shape is saved in the world as you pass through, for everyone with the mod.

## Good to know

- **Only you need it**, and friends without it play as before: no one is kept off a server. Monsters spawn in the game that looks after a place (usually that of the first player who came near and stayed); where that's a friend without this mod, they spawn there as usual.
- **To a friend without it, a Hoarstone is a Ward that's switched off.** It blocks no one. Taken down while their game looks after it, it drops a Ward's materials; while a game with the mod looks after it, what it cost.
- **Friends with the mod: keep the Settlements and Shape settings alike.** Each part of a settlement is saved by the game that looks after it, with that game's settings.
- **It stops spawns, not visitors.** Monsters from outside can still walk in.
- **Changes work at once.** Change a setting in game and the shapes are worked out again within a second or two. A saved shape that shrinks (a wall taken down, a smaller margin) follows once your game has seen that settlement again.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-Hoarstone-<version>.zip`. Keep the author `drumcowski` and the name `Hoarstone` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `Hoarstone.dll` into a folder named `drumcowski-Hoarstone` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1): all under the one heading "Hoarstone", each part folded under its title. They are kept in `BepInEx\config\modprojects.hoarstone.cfg`, which appears after the first start; in the bundle, in its own file's "Hoarstone" sections. The configuration manager describes every setting; the main ones:

| Setting | Default | What it does |
|---|---|---|
| Keep monsters from spawning in settlements | on | The whole point; off leaves outlines and the Hoarstone working. |
| Where | OnlyWithAHoarstone | Or EverySettlement. |
| Pieces this close belong together | 24 m | How near pieces must be to join one settlement. |
| Smallest settlement | 8 pieces | Fewer isn't a settlement, unless a Hoarstone stands in it. |
| Gaps to close | 30 m | Openings narrower than this are closed. |
| Margin, shore margin | 8 m, 20 m | Added round the shape, and out over the water. |
| Nests inside a settlement too | off | Stop nests and the like spawning into a settlement. |
| The Hoarstone's cost | three trophies and more | Item prefab names with amounts. |
| It's also a ward | off | On: its placer can switch it on as a real Ward. |
| Show outlines | Left Alt + F3 | Every settlement's outline, on the ground and the map. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Hoarstone"; "Write what it works out to the log" adds a line each time the shapes are worked out. If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and monsters spawn as without it. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\Hoarstone.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-Hoarstone-<version>.zip`. Until Hoarstone is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too): the shape method in `src\Shapes\` (pure arithmetic), the records, land and saved shapes in `src\World\`, the spawn checks in `src\Spawns.cs`, the Hoarstone in `src\Stone\`, the outlines in `src\Draw\`. Its own plugin is in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Made with AI

This mod was written by Claude (Anthropic) agents for miikeskii.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn.
