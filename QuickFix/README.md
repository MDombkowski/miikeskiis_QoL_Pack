# Quick Fix

Made with AI. Repair your gear in one go instead of one click at a time: a Grinding Wheel mends everything without metal in it, a Smith's Anvil everything with metal, and you can build both wherever you like, not only beside a Forge. Out in the field, a Sharpening Stone from your hotbar mends your gear wherever you stand.

## What it does

- **The Grinding Wheel and the Smith's Anvil repair everything at once.** Look at one and press Use (E): every piece of gear in your inventory, worn or in your bags, that it covers is repaired to full. Out of the box the Grinding Wheel mends non-metallic gear (leather, bone, wood, cloth) and the Smith's Anvil metallic gear (anything whose recipe takes a metal bar); either can be set to the other, or to all. Their hover text says what each repairs.
- **Build them anywhere.** The Grinding Wheel and the Smith's Anvil no longer need a Forge within 5 m. They still need a Workbench near to build, as in the game, and 2 m of room from each other. Placed away from a Forge they don't raise its level.
- **A Sharpening Stone mends your gear in the field.** Press its hotbar key (or right-click it in your inventory): one bar runs, a second per item, and when it fills your worn gear gets back half of its full durability. It costs one stone. Press it again, attack, sprint, jump or dodge to stop it; a stopped repair keeps your stone.
- **Which gear the stone mends is yours to choose:** tools, weapons, armour, shields and anything else (an axe counts as a tool and a weapon), metallic gear or not, and items always or never mended, by name.
- **Or let the stone wear like a tool.** Instead of one stone a use, a share of a stone's durability per use or per item: the stone in use comes off its stack with its own durability bar, and when it's worn out the next stone carries on. A worn stone keeps a cell of its own: it never stacks with other stones, and nothing stacks onto it.
- **Repairs near stations** (off at first): opening a crafting station can repair everything it can mend at once, and coming within 4 m of a station, a Grinding Wheel or a Smith's Anvil can repair what it mends, once each time you come near.

## Good to know

- **Only you need it.** It's client-side: your gear's durability is your own. Neither the server nor the other players need it, and a friend without it sees ordinary Grinding Wheels and Smith's Anvils, wherever you built them.
- **Only what the game can repair.** Torches and the few items with no recipe (the Crown of Roots, the Headband) are never touched, and nothing is mended past full.
- **Metallic means a metal bar in its recipe:** Copper, Tin, Bronze, Iron, Silver, Black Metal, Flametal or Bloodgold (a setting). The Forge of Potential's idols don't count. Early Axes and the Grappling Hook have no bar, so they count as non-metallic unless you add their parts to the list.
- **Repairs raise your Crafting skill** as the game's own repair does (a setting).
- **Changes work at once.** Change a setting, and it counts from the next moment; no restart.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-QuickFix-<version>.zip`. Keep the author `drumcowski` and the name `QuickFix` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `QuickFix.dll` into a folder named `drumcowski-QuickFix` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1): they sit under one heading, "Quick Fix", each part behind a title you click to open. They are kept in `BepInEx\config\modprojects.quickfix.cfg`, which appears after the first start; in the bundle, in the sections "Quick Fix" and "Quick Fix - <part>" of its own file.

"Metal bars", "Always repairs" and "Never repairs" have a **Pick** button beside their text box there, which opens a window of everything the list can hold, each with its picture: a click puts a thing on the list or takes it off. The text box stays for typing.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off: repairs work as in the game, and the two pieces need a Forge near again. |
| Metal bars | Copper, Tin, Bronze, Iron, Silver, BlackMetal, FlametalNew, Gold, Flametal | What makes gear metallic, by prefab names. |
| Repairs raise Crafting | on | |
| **Grinding Wheel and Smith's Anvil** | | |
| Repair with the Use key | on | |
| What the Grinding Wheel repairs | Repairs Non-Metallic Gear | Or Repairs Metallic Gear, or Repairs All. |
| What the Smith's Anvil repairs | Repairs Metallic Gear | The same three. |
| Place them with no Forge near | on | |
| **Near stations** | | |
| Repair on using a crafting station | off | Everything that station can repair, at once, as you open it. |
| Repair near crafting stations | off | Once each time you come near. |
| Repair near a Grinding Wheel or Smith's Anvil | off | Once each time you come near. |
| Distance (metres) | 4 | 1 to 15. |
| **Sharpening Stone** | | |
| Repair with a Sharpening Stone | on | |
| How much it repairs | 50% of full durability | 25%, 50%, 75% or 100%. |
| Repairs tools, weapons, armour, shields, other gear | all on | |
| Metallic gear too | on | |
| Always repairs / Never repairs | empty | Prefab names, separated by commas. |
| What a repair costs | One stone used | Or a share of a stone's durability per use, or per item repaired. |
| Share per use (%) / Share per item (%) | 25 / 10 | |
| How long it takes | Seconds per item | Or a flat time. |
| Seconds per item / Flat time (seconds) | 1 / 5 | |
| On cancel | Stone kept, nothing repaired | Or Stone used, nothing repaired. |
| Moving cancels | off | |
| Being staggered cancels | off | |
| While repairing | The equip motion | Or the crafting pose, or nothing. |

## If something goes wrong

If the game has changed so that the mod can't find what it needs, it switches itself off and says why in `BepInEx\LogOutput.log`, and repairs work as in the game. The same goes for an error while playing.

## Building from source

`dotnet build -c Release` in this folder makes `bin\Release\net472\QuickFix.dll`. It needs the .NET SDK with the .NET Framework 4.7.2 Developer Pack, and compiles against the game's and your mod profile's DLLs where they already are; `-p:ValheimManaged=<valheim_Data\Managed>` and `-p:ModProfile=<profile folder>` point it elsewhere. `tools\package.ps1` builds it and packs the zip.
