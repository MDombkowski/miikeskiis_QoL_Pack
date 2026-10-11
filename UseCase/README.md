# Use Case

Point at a resource in Valheim and see everything it's used in, and how many: beside the game's item tooltip, a panel lists what you build, craft, upgrade and refuel with it, and the game's other uses (smelting and cooking, boss altars, locked doors, taming, bait, ammo, fireworks, the traders), each with its "· X".

## What it does

- **A panel beside the tooltip,** right of it, or left of it when there's no room, always on screen, at any GUI scale. It never takes the pointer, so the game's tooltip stays as it is. It shows beside the tooltips of the inventory and chests, the trader's list, and the cost icons of the crafting panel and the build panel.
- **In sections:** Building, Crafting, Upgrading and Refueling first, then Processing, Summoning, Locked Doors, Taming, Fishing Bait, Ammo, Fireworks, Buying (for coins), Selling, Traders (Hildir's chests) and Guardian Stones. Each section has a switch, and their order is a setting.
- **"· X"** is what one build or craft takes; for fuel, the most the piece holds (a sconce · 6), as the game's own hover says; for upgrades, the amount per level; for a station that processes, how many it holds at once; for an altar, how many one summon takes; for a ware, its price. Resin, for example: "Refueling: Sconce · 6, Standing Wood Torch · 4, Snow Lantern · 6 …"; Withered Bone: "Crafting: Nimble Anklet · 5" and "Summoning: Bonemass · 10".
- **Read from your own game,** once per world: other mods' recipes, pieces and stations are listed too. Recipes the game has switched off aren't; seasonal ones (the Jack-o-turnip) are listed with their season.
- **Spoilers hidden:** a thing whose recipe you don't have yet reads "??? · 6", at the end of its section. A piece counts as known once the game has taught it (which needs its tool in your inventory); an altar or a locked door once it has loaded near you. When the game itself shows everything (the no-cost cheat, a world with everything unlocked), so does the panel.
- **Long lists run in columns** (Wood has 141 uses), as many as fit beside the tooltip without covering it, the rest said as "and 37 more"; or each section can show its first few lines, or fold to its heading and a count until a key is held.
- **Two ignore lists:** things never to list, and items that never get a panel. Write a thing's name as you see it ("Sconce"), its prefab name (`piece_walltorch`) or its token; case and spaces don't matter. A key (none out of the box) adds the item under the pointer to the second list.
- **Keys, if you want them** (none out of the box): show the panel only while a key is held, or switch it on and off with a key; pin it, to read a long list after the pointer moves on.

**On out of the box** since 0.1.2 (miikeskii's own settings): switch off "Show the panel" in its settings to hide it. Everything else is set to its default below. An older settings file still at an old default moves to the new one once; a setting you changed stays as you set it.

## Good to know

- **Only you need it.** It's client-side: it reads what is already on your computer and draws a panel. Nothing is sent, and nothing in the world or your character changes. The one file it writes is `BepInEx\config\modprojects.usecase.seen.txt`: the altars and locked doors each of your characters has seen, for the spoiler rule.
- **Changes work at once.** Change a setting in game and the next panel follows; no restart.
- **MyLittleUI** rewrites the game's tooltip text; this panel sits beside it and doesn't meet it. **VNEI** keeps its own window; the two don't meet either.
- **TimedTorchesStayLit** keeps some torches lit with no fuel; a setting leaves those out of Refueling on your computer (they are listed out of the box, since a friend without it refuels them).
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-UseCase-<version>.zip`. Keep the author `drumcowski` and the name `UseCase` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `UseCase.dll` into a folder named `drumcowski-UseCase` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.usecase.cfg`, which appears after the first start; in the bundle, in the sections "Use Case" and "Use Case - …" of its own file. In the window they all sit under the one heading, each part behind a title row that opens it (with shudnal's 1.1.22 or later; with another manager every part shows open).

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off takes the panel away at once and the game's tooltip is all that shows; on brings it back. |
| Show the panel | **on** | The panel itself. |
| When the panel shows | OnHover | OnHover: with the game's tooltip. WhileKeyHeld: only while the key is held. ToggledByKey: the key switches it on and off (on at first). With no key set, both show it on hover. |
| The key | none | For the two key choices above, and for unfolding sections when long lists fold. Left Alt is best avoided: MyLittleUI and HexQuickStackStorage use it over items. |
| Pin key | none | Pressed while the panel shows, it stays after the pointer moves on; pressed again, or once the window it was pinned in closes (the inventory, the trader; for the build panel's costs, once you stop building), it goes. |
| Key to stop listing an item's uses | none | Pressed while the panel shows, adds the item to "Never show a list for these resources". |
| **Where it shows:** in the inventory and chests · in the trader's list · on the crafting panel's costs · on the build panel's costs | on | Where the panel appears. |
| **What it lists:** Building · Crafting · Upgrading · Refueling · Processing · Summoning · Locked Doors · Taming · Fishing Bait · Ammo · Fireworks · Buying · Traders · Guardian Stones | on | Each section's switch. |
| Selling | on | What Haldor pays for it, on nearly everything. |
| Ballista Targets | off | The trophies that set the ballista's target. Off: it would put a line on every trophy. |
| The Obliterator's and the catapult's "anything" | off | The Obliterator turns anything into coal, and the catapult throws anything that fits an item stand; on, every such item says so. |
| Things that use themselves | off | The serving tray's food and feasts, which cost the food they set down. |
| Pieces TimedTorchesStayLit keeps lit | ShownAsTheGameHasThem | LeftOutOfRefueling: while that mod keeps them lit on your computer, they aren't listed under Refueling. |
| Things other mods add | Shown | MarkedWithTheirMod: the mod's name after the line (known for mods built with Jötunn). Hidden: left out. |
| Show "· X" | on | The amount after each line. |
| Upgrading's number | PerLevel | PerLevel: "· 2 per level". EachLevel: "· 2 / 4 / 8", what each level takes from level 2 up. |
| Processing's number | StationHolds | StationHolds: how many the station holds (Smelter · 10). PerProduct: how many one product takes. |
| Say how many a craft makes | off | "(makes 20)" after a recipe or station that makes more than one. |
| Say the station | off | The station a recipe or piece needs, after its line. |
| Building split by tool | off | One section per tool (Hammer, Cultivator, Hoe) instead of one Building section. |
| Section headings | on | Each section's heading. |
| Section order | Building, Crafting, Upgrading, Refueling, Processing, … | The sections' order, names separated by commas; names left out follow in this order. |
| Order inside a section | Progression | Progression: Meadows things first, as Add to Cart lists them. GameMenus: as the game's menus list them. AToZ: by name. |
| Title | Used In | The panel's first line; empty for none. |
| Bullet | • | What starts each line; empty for nothing. |
| Hide the names of unknown things | on | Unknown things read "???". |
| An unknown line reads | HiddenWithAmount | HiddenWithAmount: "??? · 6". Hidden: "???". LeftOut: not listed. CountedAtTheEnd: "3 more you haven't found". |
| Altars and locked doors are unknown until | Seen | Seen: until it has loaded near you, once. BossBeaten: an altar until its boss is beaten (a door until seen). NeverHidden. |
| Hide taming and bait until known | off | On: a creature reads "???" until you've held its trophy, a fish until you've held one. |
| Show everything when the game does | on | With the no-cost cheat or a world with everything unlocked, every name shows. |
| Seasonal recipes and pieces out of season | ListedMarked | ListedMarked: "Jack-o-turnip (Halloween)". Hidden: not listed to build or craft out of season. |
| Never list these | empty | Pieces, recipes (by what they make), stations and creatures never to list. |
| Never show a list for these resources | empty | Items that never get a panel. |
| Side | Beside | Beside (right, or left when there's no room), AlwaysRight, AlwaysLeft, Below. |
| Long lists | Columns | Columns, CapPerSection ("and 57 more"), FoldedUntilKeyHeld. |
| Lines per section when capped | 8 | For CapPerSection. |
| Most lines per column | 0 | 0: as many as fit on the screen. |
| Text size | 0 | 0: the game's tooltip text size. |
| Background opacity | 1 | 1: as the game's tooltip; less is more see-through. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Use Case". Once per world it says how many uses it read ("Read this world's uses: … uses of … items"). If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and the game's tooltips show as they do without it. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\UseCase.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-UseCase-<version>.zip`. Until Use Case is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too; `src\Index\` is the index of uses, kept apart so it could later be shared by other mods of the pack), its own plugin in `standalone\`, `kit\` is a copy of the QoL Mods kit that runs it, and `parts\Catalogue\` a copy of the catalogue part (the pack's order, and where each thing sits); how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn.
