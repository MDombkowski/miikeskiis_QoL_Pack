# Topper-Upper

Your torches, fires and stations topped up from your own chests, by your own rules. Torches, sconces, braziers, campfires, the hearth and the hot tub near you are kept fuelled; smelters, the blast furnace, the charcoal kiln, the windmill, the spinning wheel, the eitr refinery, the Frigid Kiln, the fermenter, the shield generator and the Frost Foundry are fed from the chests near them, and what they make is put away. And loose items on the ground go into the chests nearby that already hold them, in a few batches, never asking another game for each item.

**Out of the box, lights and fires, stations and loose items are all on.** Switch "Feed stations" off in the settings if another mod feeds your stations, and "Pick up loose items into chests" off if another mod picks them up.

## What it does

### Lights and fires

- **Within 50 m of you** (its own setting; 0 is the range in "Chests (every mod)"), **topped up with their own fuel** from the chests near you, or near each light: resin for torches and sconces, coal for braziers, wood for fires, the hearth and the hot tub, guck and greydwarf eyes for the coloured torches. A torch is topped up as soon as one more resin fits, or when half empty, or only when out: your choice.
- **One switch per kind** of light. Resin candles (the game won't refill them by hand) and fires the world made (a camp's fire) are off.
- **Your reserve is kept.** With "Resin:50" in "Keep in reserve" (out of the box), torches are topped up only while more than 50 resin are in the chests near you, and never below 50.
- **When fuel is short**, the emptiest light goes first (or the nearest).

### Stations

- **Fed from the chests near each station** ("Input radius around a station": 10 m out of the box; 0 is the range in "Chests (every mod)"), or only from the chests you linked to it.
- **What each station takes first** is a line you can change: the smelter takes Silver ore, then Iron scrap, Iron ore, Bronze scrap, Tin, Copper scrap, Copper ore; the blast furnace Petrified tissue, then Flametal ore, then Black metal scrap; the windmill Oat, then Barley, then Oat seeds. The charcoal kiln takes only plain Wood, so Fine wood and Corewood stay for building.
- **Reserves:** nothing is taken below what you keep. Out of the box 50 each of Barley, Flax and Oat seeds stay as seed stock.
- **Maximums:** "BlackMetal:120" stops the blast furnace once 120 Black metal are near you, counting what's already queued in stations and lying at their outputs, and starts it again when you take some.
- **Fuel only while there's something to process**, so an idle smelter doesn't lock coal away.
- **The fermenter** is filled when empty and tapped when ready.
- **Off at first, a switch each:** the stone oven's fuel (it burns wood even when empty), the ballista's missiles, and harvesting beehives, birds' nests and sap extractors.
- Food on cooking stations is left to you: it burns while nobody watches.

### Catching up while you're away

- A station only runs while someone is near it. **With "Catch up while away" on** (off out of the box), when a base loads again its smelters, kilns, windmill, spinning wheel, eitr refinery and Frigid Kiln make up at once what they would have made in the time players were on the server, fed from the chests near them by your rules all along. What they made goes into the chests as stacks, the way products are put away (when "Products with no linked output" leaves them where they fall, or the chests have no room, they lie at the station in whole stacks), and one line says it: "Smelter caught up 3 h 12 min: Copper x96".
- **Only stations this mod feeds catch up:** "Feed stations" must be on, and for the fermenter "Tap the fermenter when it's ready" too.
- **A fermenter that was ready** is tapped, filled and fermented again as often as the time allows.
- **Only time with players on counts**, at most 24 hours of play at once (a setting); a night with the server empty never does. Alone, only the time you're in the world counts. A night slept through counts as time with players on, since the world's clock jumps ahead.
- **The windmill** catches up with the wind when you left it, the wind when you come back, or its biome's average: your choice.
- Fires and torches don't catch up. Whatever was already in a station always finishes, as the game itself would do.
- A base with many stations spreads the work over frames, one station at a time.

### Leave one alone

- **Left Alt + E** pointing at a light or station switches it between fed and left alone. It's saved on it, for everyone playing with the pack, and its hover text says which.

### Linked chests

- **Left Alt + J** pointing at a station starts linking. Then **E** on a chest makes it an input (the station takes only from its inputs), and **Left Shift + E** an output (what it makes goes there); the same again takes that away. **One chest can be both** an input and an output. **Left Alt + J** at the station again, opening a menu, walking away or a minute without a change ends it.
- While you link, every chest you look at says what it is to the station and what **E** and **Left Shift + E** will do to it.
- The links are saved on the station, by where the chests stand: a chest you rebuild in the same spot is still linked.
- While you point at the station, its inputs glow green, its outputs blue, and a chest that is both amber.

### What stations make

- Bars, coal, flour, thread, eitr, Liquid Frost and meads go into the linked output chest, or, with none linked, into chests that already hold some within "Output radius around a station" (its own setting, so it can reach farther than the input; out of the box the range in "Chests (every mod)"), the way the quick stack puts things away (chest setups followed, chests others marked as theirs left alone). Or they stay where they fall: your choice.
- A full output chest: the rest put away the same way, left where it falls, or the station waits until there's room.

### Loose items

- **Into the chests nearby that already hold them** (or are set up for them; chests others marked as theirs are left alone), within 64 m of the item (0 is the range in "Chests (every mod)"). A broken stone deposit beside a chest holding stone goes in over a few frames, with one line over the chest ("Stone x87"), never one per stone.
- **Only what your own game looks after** (what you mined, chopped or killed nearby), a few seconds after it's made, once it lies still or floats. It never asks another player's game for anything.
- **Left where it lies:** anything within 4 m of a player (so you can pick it up by hand), what you dropped on purpose, food near a tame animal that eats it, an egg lying alone, a fish still swimming, what lies at the output of a station this mod feeds (its own rules put that away), and the kinds you list in "Never pick up".
- **On out of the box.** If a mod on the server picks up loose items itself, it asks for your drops and takes them first; Topper-Upper says so once.
- **A quieter log** (on): when another game asks yours for an item yours no longer has, the game's log lines for it are left out, and one line a minute says how many. Nothing else changes. Switch it off while testing the freeze.

## Keys

| Key | Where | What it does |
|---|---|---|
| Left Alt + E | pointing at a light or station | Fed or left alone |
| Left Alt + J | pointing at a station | Start or end linking its chests |
| E | on a chest, while linking | Make it an input, or no longer one |
| Left Shift + E | on a chest, while linking | Make it an output, or no longer one (on a gamepad, its alt button with Use) |

E is the game's Use: while one of these keys is held, the game's own E doesn't act on that light, station or chest. Left Alt + E at a chest is Reserve Stock's, and Left Alt + J at a chest is StoreStash's chest setup; at a light or station they're this mod's. Every key can be changed in the settings.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1): everything is under one heading, each part folded. They are kept in `BepInEx\config\modprojects.topperupper.cfg`; in the bundle, in its own file, in the sections starting "Topper-Upper". Every setting works at once.

What each station takes first, the kiln's woods, "Never take, for anything", "Keep in reserve", "Keep at most" and "Never pick up" have a **Pick** button beside their text box there, which opens a window of everything the list can hold, each with its picture: a click puts a thing on the list or takes it off. The text box stays for typing.

## Playing with friends

- **Each light and station is fed by one game only:** the game of the player who looks after it (the first player to arrive at a base looks after what's around him). Two friends with the pack never feed the same thing.
- **It takes only from chests your own game already has.** It never asks another player's game for a chest or an item, so it adds nothing to the network but the changes themselves.
- **Your rules count where your game feeds.** At a friend's base their game, and their settings, do the feeding.
- A light or station a friend without the pack looks after isn't fed (a setting can feed it the way pressing E does; off by default).
- A base nobody is at isn't fed: the game itself doesn't run stations nobody is near. With "Catch up while away" on, its stations make up that time when someone comes back.
- **With miikeskii's Server-Side QoL Pack on the server**, the catch-up counts only the time players were on (every player, with the pack or without), and the server can switch it on or off for everyone. The server pack can also hand your game the drops and stations of friends without the pack near you; your game then puts them away and feeds them as its own. That goes by "Feed stations" as a whole, not by kind: a friend's station of a kind you switched off is handed to you all the same, and stays unfed. Without the server pack, the catch-up counts your own time online in that world. When you host the world yourself, your own catch-up settings are the ones that count.
- **Loose items go only into chests your own game has.** At a base where a friend's game looks after the chests, your drops stay on the ground until yours does (or you pick them up).

## Other mods

- **Another mod on the server that feeds stations or picks up loose items** (not miikeskii's Server-Side QoL Pack, which works with this mod): it and this mod would both do it. Have it switched off on the server. One that asks your game for every item on the ground can freeze your game when a big deposit breaks; Topper-Upper says so once when the server asks.
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

Look in `BepInEx\LogOutput.log` for lines from "Topper-Upper". If the game has changed so that the mod can't find what it needs, that kind of light or station is left alone and the log says why; an error while playing switches the mod off for the session, and lights and stations work as in the game; an error in the loose-item pickup, the quieter log or the catch-up switches off only that part (nothing taken from a chest for a catch-up is lost: it goes back, or lies at the station). "Write catch-ups to the log" says, for each station, what it caught up and from which clock, or why it didn't. "Write what's fed to the log" adds a line for each feed, and one for each round of loose items put away.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\TopperUpper.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and the changelog agree on the version, builds, and writes `dist\drumcowski-TopperUpper-<version>.zip`. Until Topper-Upper is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, `kit\` is a copy of the QoL Mods kit that runs it, and `parts\` copies of the pack's shared code it uses; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: written by Claude (Anthropic) agents for miikeskii.
