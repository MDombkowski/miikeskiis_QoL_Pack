# Craft from Chests

Made with AI. Craft, build and fill your stations with what lies in the chests around you, as if you carried it, without the item copying and losing that other craft-from-chests mods can cause when a friend has a chest open.

## What it does

- **Crafting.** At a workbench, forge or any station, and when crafting by hand, the crafting menu counts what the chests near you hold. When you craft, what you carry goes first and the rest comes from the nearest chests as the crafting bar ends. Upgrades and the game's own "craft five" work the same way.
- **Craft an amount.** A number with + and - sits beside the Craft button: set how many you want and press Craft once, and they're made one after another, in the game's own x5 crafts as far as the amount goes (41 is eight x5 crafts and one single), so you get the x5 craft's bigger bonus and finish sooner. The mouse wheel over the number changes it; hold Shift to step by five, and hold Ctrl or right-click + for the most you can make. Picking another recipe puts it back to 1. The recipe's materials show what the whole amount needs, such as 3.8K/320, and the crafting bar counts down what's left. Your crafting skill rises exactly as much as making them one at a time. It stops when you cancel, close the menu, switch recipe or run out of materials. With MyLittleUI installed, its own + and - step aside while this is on.
- **Building.** The hammer, hoe and cultivator count the chests too. Placing a piece takes what you carry first, then the rest from the nearest chests.
- **Stations.** Press E on a smelter, charcoal kiln, blast furnace, spinning wheel, windmill, eitr refinery, fire, torch, cooking station, oven, fermenter, shield generator or ballista with none of what it takes on you, and it takes one from the nearest chest that has it. Hold **Left Shift** while pressing E to fill it as far as it goes: first from what you carry, then from the chests. The charcoal kiln takes only plain Wood from chests, so your Fine and Core wood stay for building (a setting).
- **The menus show how much you have.** Each amount in the crafting and build menus reads "have/need", such as 57/10, counting the chests. An amount you'd be short of without the chests flashes yellow; one short even with them flashes red, as the game's own does. The piece you're about to place shows how many of it you could place, such as Wood wall (38).
- **Left Alt + O** switches it off and on, for when you want to craft only from what you carry.

## How it keeps items safe

Valheim lets only one player's game change a chest at a time: opening a chest asks whoever looks after it to hand it over, and the answer is no while someone has it open. Other craft-from-chests mods skip that step and change their own copy of the chest, so a friend with the chest open, or a second player crafting from it at the same moment, can bring used items back or lose some.

This mod asks first, every time, the way the game's own chest window does, and only then takes:

- **A chest a friend has open is used only through their game.** If they run miikeskii's QoL Pack (0.10.0 or later), your game asks theirs for what you need: their game takes it out of the real chest (their open window shows it leave at once) and hands it over, and it lands in your inventory to be used from there. Only what fits in your inventory is asked for. A friend without the pack, or with an older one, keeps their open chest to themselves: it's left out, and the next chest is used instead, until they close it.
- **What you use is exactly what leaves the chests,** as your friends' games see them.
- **Nobody is ever kept out of a chest.** Asking only hands a chest over for a moment; it doesn't mark it as in use, and nothing is held while you craft or build. For three seconds after a take, a friend's press of E on that chest does nothing and his next press opens it, so he never sees it from before your take (a mod on the server that sorts chests or feeds stations from them waits too, but never makes the chest drop out of your counts). Once he has pressed E, your crafting and building leave that chest alone until two seconds after, so his next press gets it; if it's the only chest with what you need, you're told someone is about to open it.
- **Placing a piece isn't delayed:** the chests with the materials of the piece you picked are asked for as soon as you pick it. If a friend opened one of them meanwhile, the click tells you so, and the next one places it. A chest a friend is building from right now isn't asked for ahead; only your click asks for it. A chest a friend has open is asked only by your click too: it brings what the piece needs into your inventory, and the next click places it.
- **Your own open chest is shared with friends who ask,** the same way: what they take leaves your window as you watch. The stack you're dragging or splitting is left alone. While every chest mod of the pack is switched off, nothing is shared.
- **Timeouts:** a friend's game that doesn't answer within 4 seconds is left out of that craft, click or station. If the items still come, they land in your inventory, with a word; what doesn't fit by then is dropped at your feet, with a word. The rare cases: a player whose game crashes or leaves in the instant between the take and its arrival loses what was on its way; and if your friend's game crashes in the instant right after its take, their chest may still hold what you received.

**Everyone should swap.** A friend who still runs AzuCraftyBoxes (or another mod that takes from chests without asking) can still clash with others over a shared chest, so everyone on the server should use this instead.

## Good to know

- **Chests far away.** On a server, the game hands a chest only to a player within about 64 to 96 m of it; a chest farther out that no game can answer for is claimed by your own game when it's needed (since this version, with the setting "Chests beyond the server's reach" switched on: off out of the box), as the game's portals do, and used once the server's newest copy is in, between the server's two-second rounds: so it can take a second or two longer than a near chest. Chests your game hasn't loaded can't be used: at the usual "Classic" simulation distance that's beyond about 128 m (up to 160 m, more toward the corners of the area).
- **Only you need it.** It's client-side: neither the server nor the other players need it, and it works next to players without it. A friend's open chest is used only if they run miikeskii's QoL Pack too (their game says so when you join; nothing is added to the world).
- **Remove AzuCraftyBoxes first.** While AzuCraftyBoxes is installed, this mod stays off and says so in the log: two mods taking from the same chests at once could lose or copy items. The same goes for aedenthorn's CraftFromContainers.
- **Which chests.** Chests you could open by hand within 150 m (only what your game has loaded around you exists: see "Chests far away" below), never a world's own chest, a gravestone, someone else's personal chest or a chest behind a ward you aren't on. Your own personal chests are used too, unless you switch that off. One of each item is left in every chest, so quick stack still knows where things go. A chest reserved with Reserve Stock is never taken from, and an amount a reserved chest holds more of shows a small orange padlock with how many (the setting "Reserved chests" can hide it, or let crafting use reserved chests after all).
- **Changes work at once.** Change a setting in game and the next count uses it; no restart.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-CraftFromChests-<version>.zip`. Keep the author `drumcowski` and the name `CraftFromChests` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `CraftFromChests.dll` into a folder named `drumcowski-CraftFromChests` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.craftfromchests.cfg`, which appears after the first start; in the bundle, in the section "Craft from Chests" of its own file. The defaults are AzuCraftyBoxes' keys, with its range at 150 m and Leave One Item on.

"What the charcoal kiln takes from chests" has a **Pick** button beside its text box there, which opens a window of everything the list can hold, each with its picture: a click puts a thing on the list or takes it off. The text box stays for typing.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off: crafting, building and stations use only what you carry, as without the mod. |
| Key to switch it on and off | Left Alt + O | Switches Enabled, and says which. |
| Chest range (metres) | 150 | How far a chest may be. 5 to 200. |
| Which chests | every chest I can open | Or only chests, carts and ships you built. |
| Chests beyond the server's reach | off | On a server, the game hands a chest only to a player within about 64 to 96 m of it; on: a chest farther out that no game can answer for (never a cart's or a ship's) is claimed by your game when it's needed, as the game's portals do, and used once the server's newest copy is in, between the server's rounds (a second or two longer than a near chest). Off: such chests are left out, as before 0.31.0. Alone or hosting, it changes nothing. |
| Leave one of each item in chests | on | So quick stack still knows where things go. Only items that stack. |
| Use my personal chests | on | On: your own personal chests are used like any other. Off: they're left alone. |
| Reserved chests | left out, with a padlock | Chests reserved with Reserve Stock. LeftOutWithLock: never used, and a padlock with how many more they hold shows on the amounts. LeftOut: never used, nothing shown. UsedAsUsual: used like any other chest. |
| Craft from chests | on | Crafting and upgrading use the chests. |
| Build from chests | on | Placing pieces uses the chests. |
| Show how much you have | on | "57/10" in the requirement lists. |
| Amount format | {0}/{1} | {0} is what you have, {1} what's needed. |
| Colour when chests make up the rest | yellow | The colour that amount flashes to. |
| Show how many you can build | on | "(38)" after the piece's name. |
| Colour: can build / can't build | green / red | The colour of that number. |
| Fill stations from chests | on | E and Shift + E on stations use the chests. |
| Hold to fill | Left Shift | Hold with E to fill a station. |
| What the charcoal kiln takes from chests | Wood | Prefab names, separated by commas; empty for every wood it burns. |
| Show the fill hint | on | A line in a station's hover text naming the fill key. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Craft from Chests". If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and everything uses only what you carry. The same goes for an error while playing. If only one kind of station can't be filled any more after a game update, the log names it and the rest keeps working.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\CraftFromChests.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-CraftFromChests-<version>.zip`. Until Craft from Chests is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, `kit\` is a copy of the QoL Mods kit that runs it, and `parts\Chests\` a copy of the QoL Mods chest service it takes from chests with; how a module and a part are made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn.
