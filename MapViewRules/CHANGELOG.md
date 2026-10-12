# Changelog

## 0.2.2 (2026-10-06)

- **miikeskii's own rules are the defaults now**: "Pins with their own rules" is Cloudberry, Lox, Boar and Wolf out of the box (was empty), and some sections have defaults of their own in place of follow: Chests hide icons past 30 and names past 10, Dungeons 30 and 20, Lore stones, Ore and Runestones 20 and 10, Spawners 30 and 20; Pickables and Pickables - Berries show icons and names, hide icons past 10 and names past 0; the Boar, Lox and Wolf overrides hide icons and names past 10 and are off the minimap, and Cloudberry is off the minimap. All pins and every other section are unchanged. Keys and section names are unchanged.
- An older file's empty override list moves to the four names at the start, and their sections are bound at once (not after the list has settled); an override section bound later still takes these defaults for those four names, and an old value left in the file for one of them moves the same way, once.
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.

## 0.2.1 (2026-09-29)

- **The Portals section follows XPortal Map Picker 0.6.0's portal pins,** now drawn with the game's own white portal pin: a pin the game draws with that picture without being one a player placed (type None) is a portal's. A portal pin you place by hand stays a hand-placed pin.

## 0.2.0 (2026-09-29)

- **Icons and names apart** (miikeskii's ask, 2026-09-28): in every section, "Large map: show" becomes "Large map: icons" (the same setting: a value set in 0.1.0 carries over, and the new "Large map: names" under it takes the same value once, so the maps look as they did). A name whose icon is hidden is drawn alone, the pin unseen at its place under it; while that name doesn't show (no name, zoomed out past the game's names, or hidden by Cartur's), the pin is hidden like any other, so an invisible pin is never clicked. With both hidden the pin isn't drawn, and can't be clicked. The minimap, which has no names, is unchanged.
- **Friends' map**: a setting for what both maps draw of the friends' map a cartography table gave you: Everything (as the game), ExploredGroundOnly (none of the pins they placed by hand; every pin drawn with one of Cartur's Map Pins' icons still shows), PinsOnly (none of their explored ground). Only drawing changes.
- **A Portals section**: pins drawn with a portal's picture, as XPortal Map Picker's portal pins are, follow a section of their own.

## 0.1.0 (2026-09-27)

- **First version**, as a mod of miikeskii's QoL Pack and on its own.
- **Rules per section:** All pins; every category of Cartur's Map Pins and every pickable group; hand-placed pins, death markers, the game's own markers, the pins on boats, carts and tames, and other pins; and a section per kind or pin name in "Pins with their own rules".
- **Per section and per map:** shown or not; the zoom past which icons hide, and names hide; the zoom icons shrink from, and how small they get. Every value can follow the section above.
- **Only what is drawn changes:** every pin stays saved, shared through a table and tracked; a hidden pin can't be clicked or deleted.
- **The zoom readout** in a corner of each map, and a "Use map zoom" button beside every zoom setting in a configuration manager's window.
- **The name under the pointer** shows when the rules hide it.
- **In a configuration manager's window (F1)** every section of rules is a fold-out under the mod's one heading, opened and closed by its title row, not a heading of its own at the pack's top level (miikeskii's look at F1, 2026-09-28).
- Reads Cartur's Map Pins' record of its pins (1.6.0), never writes it.
