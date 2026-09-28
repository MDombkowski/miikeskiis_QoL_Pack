# miikeskii's QoL Pack

Made with AI. A collection of quality-of-life mods.

## What's inside

| Mod | What it does | Needs |
|---|---|---|
| **XPortal Map Picker** 0.4.0 | A Map button beside XPortal's destination list: choose where a portal leads by clicking it on the big map, on a clean map showing only the portals and the other players. | XPortal (made for 1.2.25) |
| **Map Zoom** 0.1.0 | The mouse wheel zooms the big map in fewer notches (18 end to end instead of about 44, a setting) and toward the mouse pointer. | nothing more |
| **Trader Circles** 0.1.0 | See-through coloured circles on the big map around every place Haldor, Hildir or the Bog Witch could still be, each place somewhere inside, the same for everyone in the world, gone once the trader is found. Each trader switched on in its settings; all off at first. | nothing more (asks the unmodded server as a vegvisir does) |
| **Add to Cart** 0.3.3 | Press K for a window of everything you can build, craft or process, and every item in the chests near you (Take): fill a cart, see what it needs, what the chests can give and what's short, and one Fetch takes it from those chests, asking each chest's owner first so nothing is lost or copied. Browse by a list on the left (categories, materials, kinds, stations, families) with headings that follow what you pick, in progression order; your favorites and lists are the game's own; Locate labels over the chests holding an item. Three window sizes. | nothing more (reads AzuCraftyBoxes' Leave One Item if it's there) |

More mods join as they are made.

## Other mods it needs or touches

- **Installed with it** (the mod manager adds them): **BepInExPack for Valheim**, by denikson, the mod loader; and **Jötunn**, by ValheimModding, the Valheim modding library.
- **Changed by it, if you have it: XPortal**, by SpikeHimself. XPortal Map Picker patches XPortal's destination panel: it adds a Map button to the destination row, making room for it, hides the panel while you pick a portal on the map, and then picks that portal in XPortal's destination list. It reads XPortal's list of portals, and XPortal's copy of the server's PingMapDisabled setting (the Map button hides wherever XPortal hides its Ping button). It doesn't change XPortal's portals or settings: the destination still reaches the server through XPortal's own OK button. Without XPortal, only XPortal Map Picker stays off. The pack doesn't install XPortal.
- **Read by it, never changed: AzuCraftyBoxes**, by Azumatt. Add to Cart's setting "Leave one of each item in chests" follows AzuCraftyBoxes' own "Leave One Item" setting when AzuCraftyBoxes is installed; without it, Add to Cart leaves one of each stackable item. The setting can also say always or never. The pack doesn't install AzuCraftyBoxes.
- **Optional, to change the settings in game:** a configuration manager, such as shudnal's ConfigurationManager (F1).
- **Its own mods, shared on their own before:** see *Already have one of these mods on its own?* below.

## Good to know

- **Only you need it.** Every mod inside is client-side: neither the server nor the other players need the bundle. (Trader Circles asks the server a question every unmodded server answers, the one a vegvisir asks; the server needs nothing new.)
- **One switch per mod.** Each mod's section of the settings starts with **Enabled**. Turning it off takes that mod's changes away at once, without a restart; on brings them back.
- **A mod that can't run switches only itself off.** If a mod needs another mod you don't have (XPortal Map Picker needs XPortal), or the game has changed under it, that one mod stays off with a line in the log saying why, and the rest carry on. The same goes for an error while playing.
- **Already have one of these mods on its own?** Remove it, or update it to its newest version: then, with the bundle installed, the separate copy stands aside and does nothing, and says so in the log. An older separate copy can't do that (XPortal Map Picker 0.3.0 and before), so the bundle's copy of that mod stays off instead and the log says to update or remove the old one; the game is never changed twice. The first time the bundle runs a mod, it takes that mod's settings over from its separate copy's settings file, so your choices carry across. That file is only read, never changed.

## Installing

- **With a mod manager** (Thunderstore Mod Manager, r2modman or Gale): find **miikeskiis QoL Pack** by miikeskii and install it. BepInExPack and Jötunn come with it, and its updates arrive like any other mod's.
- **Had it from a friend as a file?** It was shared as a zip named `drumcowski-QoL_Mods` until 0.6.2, and `miikeskii-QoL_Pack` for 0.6.3: remove whichever you have in the mod manager first, or they install side by side. Your settings carry over (the same settings file).
- **From its zip:** in the mod manager, Settings, then "Import local mod from file", and choose `miikeskii-miikeskiis_QoL_Pack-<version>.zip`. Keep the author `miikeskii` and the name `miikeskiis_QoL_Pack` as the import fills them in, so that a newer version later replaces this one instead of installing beside it.
- **By hand:** with BepInExPack for Valheim and Jötunn already installed, copy `QoLMods.dll` into a folder named `miikeskii-miikeskiis_QoL_Pack` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1): the bundle is one entry, **miikeskii's QoL Pack**, with a section per mod. Every change takes effect without a restart. The settings are kept in `BepInEx\config\modprojects.qolmods.cfg`, which appears after the first start.

Each setting's description shows in the configuration manager and in the settings file. For XPortal Map Picker, the section "XPortal Map Picker" holds: Enabled, Button label, Clean map while picking, Show players while picking, Show portals in unexplored areas, Portal names, Marker size (%) and Zoom to fit portals. For Map Zoom, the section "Map Zoom" holds: Enabled, Scroll notches, fully in to fully out, and Zoom toward the mouse pointer. For Trader Circles, the section "Trader Circles" holds: Enabled, Circles for Haldor, Circles for Hildir, Circles for the Bog Witch, Circle size (metres across), How solid the fill is (%), Thin outline, each trader's colour, and Also on the small map. For Add to Cart, the section "Add to Cart" holds: Enabled, Key to open the cart, Chest range (metres), Which chests, Leave one of each item in chests, Take only what I'm short, List plantings, Window size, Thumbnails the chests can't cover, Opacity of thumbnails the chests can't cover, Favorites first, Scroll speed (rows per notch), Expandable families, Remember each tab's group and sort, Default group, Default sort, Build category order, Build material order, Craft kind order, Craft station order, Take family order, and Locate: opening one chest clears every label.

## If something goes wrong

Look in `BepInEx\LogOutput.log`. The bundle writes one line at start listing every mod inside with its version and whether it is on. Each mod then writes under its own name ("XPortal Map Picker", for example), including why it switched off, if it did.

## Credits

Each mod's credits, gathered: `CREDITS.md` in the package. Built with BepInEx, HarmonyX and Jötunn. The code was written by Claude, Anthropic's AI (the Claude Opus 5 and Opus 5.5 models, working as Claude Code agents), for miikeskii.

## Licence

The pack and every mod in it are released under the **GNU General Public License, version 3** (GPL-3.0): `LICENSE` in the package. They come with no warranty. The source of every mod in the pack is at https://github.com/MDombkowski/miikeskiis_QoL_Pack; how to build it is in its `QoLMods/MODULES.md`. The mods it needs or works with keep their own licences.
