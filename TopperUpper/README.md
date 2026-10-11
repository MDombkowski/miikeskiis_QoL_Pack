# Topper-Upper

Your torches, fires and stations topped up from your own chests, by your own rules. Torches, sconces, braziers, campfires, the hearth and the hot tub near you are kept fuelled; smelters, the blast furnace, the charcoal kiln, the windmill, the spinning wheel, the eitr refinery, the Frigid Kiln, the fermenter, the shield generator and the Frost Foundry are fed from the chests near them, and what they make is put away. And loose items on the ground can go into the chests nearby that already hold them, without the freeze the server's own pickup causes.

**Out of the box, lights and fires are on; stations and loose items are off.** Switch "Feed stations" on in the settings once nothing else feeds your stations, and "Pick up loose items into chests" once nothing else picks them up (on a server running ServersideQoL, once its feeding and its auto pickup are switched off).

## What it does

### Lights and fires

- **Topped up with their own fuel** from the chests near you: resin for torches and sconces, coal for braziers, wood for fires, the hearth and the hot tub, guck and greydwarf eyes for the coloured torches. A torch is topped up as soon as one more resin fits, or when half empty, or only when out: your choice.
- **One switch per kind** of light. Resin candles (the game won't refill them by hand) and fires the world made (a camp's fire) are off.
- **Your reserve is kept.** With "Resin:50" in "Keep in reserve" (out of the box), torches are topped up only while more than 50 resin are in the chests near you, and never below 50.
- **When fuel is short**, the emptiest light goes first (or the nearest).

### Stations

- **Fed from the chests near each station** (10 m out of the box), or only from the chests you linked to it.
- **What each station takes first** is a line you can change: the smelter takes Silver ore, then Iron scrap, Iron ore, Bronze scrap, Tin, Copper scrap, Copper ore; the blast furnace Petrified tissue, then Flametal ore, then Black metal scrap; the windmill Oat, then Barley, then Oat seeds. The charcoal kiln takes only plain Wood, so Fine wood and Corewood stay for building.
- **Reserves:** nothing is taken below what you keep. Out of the box 50 each of Barley, Flax and Oat seeds stay as seed stock.
- **Maximums:** "BlackMetal:120" stops the blast furnace once 120 Black metal are near you, counting what's already queued in stations and lying at their outputs, and starts it again when you take some.
- **Fuel only while there's something to process**, so an idle smelter doesn't lock coal away.
- **The fermenter** is filled when empty and tapped when ready.
- **Off at first, a switch each:** the stone oven's fuel (it burns wood even when empty), the ballista's missiles, and harvesting beehives, birds' nests and sap extractors.
- Food on cooking stations is left to you: it burns while nobody watches.

### Leave one alone

- **Left Alt + E** pointing at a light or station switches it between fed and left alone. It's saved on it, for everyone playing with the pack, and its hover text says which.

### Linked chests

- **Left Alt + J** pointing at a station starts linking. Then **E** on a chest makes it an input (the station takes only from its inputs), and **Left Shift + E** an output (what it makes goes there); the same again unlinks it. **Left Alt + J** at the station again, opening a menu, walking away or a minute without a change ends it.
- The links are saved on the station, by where the chests stand: a chest you rebuild in the same spot is still linked.
- While you point at the station, its inputs glow green and its outputs blue.

### What stations make

- Bars, coal, flour, thread, eitr, Liquid Frost and meads go into the linked output chest, or, with none linked, into chests near the station that already hold some, the way the quick stack puts things away (chest setups followed, chests others marked as theirs left alone). Or they stay where they fall: your choice.
- A full output chest: the rest put away the same way, left where it falls, or the station waits until there's room.

### Loose items

- **Into the chests nearby that already hold them** (or are set up for them; chests others marked as theirs are left alone), within 64 m. A broken stone deposit beside a chest holding stone goes in over a few frames, with one line over the chest ("Stone x87"), never one per stone.
- **Only what your own game looks after** (what you mined, chopped or killed nearby), a few seconds after it's made, once it lies still or floats. It never asks another player's game for anything.
- **Left where it lies:** anything within 4 m of a player (so you can pick it up by hand), what you dropped on purpose, food near a tame animal that eats it, an egg lying alone, a fish still swimming, what lies at the output of a station this mod feeds (its own rules put that away), and the kinds you list in "Never pick up".
- **Off out of the box:** switch "Pick up loose items into chests" on once the server's own pickup is off. If the server still asks for your drops, the mod says so once.
- **A quieter log** (on): when another game asks yours for an item yours no longer has, the game's log lines for it are left out, and one line a minute says how many. Nothing else changes. Switch it off while testing the freeze.

## Keys

| Key | Where | What it does |
|---|---|---|
| Left Alt + E | pointing at a light or station | Fed or left alone |
| Left Alt + J | pointing at a station | Start or end linking its chests |
| E | on a chest, while linking | Link it as an input, or unlink it |
| Left Shift + E | on a chest, while linking | Link it as an output, or unlink it (on a gamepad, its alt button with Use) |

E is the game's Use: while one of these keys is held, the game's own E doesn't act on that light, station or chest. Left Alt + E at a chest is Reserve Stock's, and Left Alt + J at a chest is StoreStash's chest setup; at a light or station they're this mod's. Every key can be changed in the settings.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1): everything is under one heading, each part folded. They are kept in `BepInEx\config\modprojects.topperupper.cfg`; in the bundle, in its own file, in the sections starting "Topper-Upper". Every setting works at once.

## Playing with friends

- **Each light and station is fed by one game only:** the game of the player who looks after it (the first player to arrive at a base looks after what's around him). Two friends with the pack never feed the same thing.
- **It takes only from chests your own game already has.** It never asks another player's game for a chest or an item, so it adds nothing to the network but the changes themselves.
- **Your rules count where your game feeds.** At a friend's base their game, and their settings, do the feeding.
- A light or station a friend without the pack looks after isn't fed (a setting can feed it the way pressing E does; off by default).
- A base nobody is at isn't fed: the game itself doesn't run stations nobody is near.
- **Loose items go only into chests your own game has.** At a base where a friend's game looks after the chests, your drops stay on the ground until yours does (or you pick them up).

## Other mods

- **ServersideQoL on the server:** its feeding (AutoProcess, `FeedFromContainers`) and this mod would both feed your stations. Switch that off on the server, then "Feed stations" on here. Its auto pickup (AutoStore, `AutoPickup`) is what most likely freezes your game when a big deposit breaks beside a chest holding stone: have it switched off, then "Pick up loose items into chests" on here.
- **AzuAutoStore, McAutoStore** and other mods that pick up loose items aren't recognised: keep this mod's pickup off while one runs.
- **AutomaticFuel, LazyVikings:** while one is installed, Topper-Upper feeds nothing and says so in the log. Others that do the same (OttoFuel, Valheim Plus's auto fuel) aren't recognised yet: switch theirs off.
- **TimedTorchesStayLit:** with its "Stay Lit" on, your torches look lit to you at night whatever their fuel. Switch "Stay Lit" off to see the real fuel.
- **Craft from Chests** (in the pack): its E-press fill still works; set the kiln's woods in both.

## Good to know

- **Only you need it.** It's client-side: neither the server nor the other players need it.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.
- One of each item stays in every chest it takes from (a setting), so the quick stack still knows where things go.
- The Obliterator is never used as a chest. In the pack, a chest a craft is counting on while its bar runs is also left alone until it's done (on its own, Topper-Upper can't see Craft from Chests' crafts).

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-TopperUpper-<version>.zip`. Keep the author `drumcowski` and the name `TopperUpper` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `TopperUpper.dll` into a folder named `drumcowski-TopperUpper` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Topper-Upper". If the game has changed so that the mod can't find what it needs, that kind of light or station is left alone and the log says why; an error while playing switches the mod off for the session, and lights and stations work as in the game; an error in the loose-item pickup or the quieter log switches off only that part. "Write what's fed to the log" adds a line for each feed, and one for each round of loose items put away.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\TopperUpper.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and the changelog agree on the version, builds, and writes `dist\drumcowski-TopperUpper-<version>.zip`. Until Topper-Upper is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, `kit\` is a copy of the QoL Mods kit that runs it, and `parts\` copies of the pack's shared code it uses; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: written by Claude (Anthropic) agents for miikeskii.
