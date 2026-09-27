# Changelog

## 0.4.0 (2026-09-25)

- **Now also part of drumcowski's QoL Mods**, the bundle of all drumcowski's Valheim mods, where it has its own section of the bundle's settings. On its own, this release works exactly as 0.3.0 did: same settings file, same settings, same behaviour.
- **With the bundle installed too,** this copy stands aside and does nothing, so the game isn't changed twice. The log says so, and this copy can be removed. The bundle takes this mod's settings over the first time it runs.
- After an error switches the mod off, its patches of the game's map clicks are now removed as well, so the map works exactly as without the mod until the next start.
- The package now includes `CREDITS.md`: what the mod borrows, from whom, and under which licence.
- Not yet tested in game.

## 0.3.0 (2026-09-25)

- **Other players show while you pick.** The clean map keeps the other players' live icons, with their names when the map is zoomed in far enough for the game to show pin names, so you can see where your friends are as you choose a portal. Every other pin stays hidden as before. The new setting "Show players while picking", on by default, turns this off.
- The players are drawn by this mod as copies of the game's own player icons, below the portal markers, and follow them every frame. The game's pins themselves are left alone, so with the setting off the clean map is exactly as in 0.2.0.
- The package is now `drumcowski-XPortalMapPicker-<version>.zip`, the author name 0.2.0 was imported under, so importing it replaces 0.2.0 in Thunderstore Mod Manager instead of adding a second copy.
- Not yet tested in game.

## 0.2.0 (2026-09-24)

- **A clean map while picking.** The big map shows your explored map and your own position with no other pins on it, only the portals you can choose. Every pin comes back the moment you stop picking. The setting "Clean map while picking" turns this off.
- **The mod's own portal markers.** The markers are no longer map pins, so nothing is ever added to your map, saved with it or picked up by other map mods. They follow the map as you pan and zoom, turn orange under the mouse, and are clicked directly; clicking a portal's name works too.
- **Portals in unexplored areas show**, such as a friend's portal somewhere you have never been. The setting "Show portals in unexplored areas" hides them.
- **Settings in game.** Every setting shows in a configuration manager (shudnal's ConfigurationManager opens with F1), in order and with plain names, and takes effect at once: Enabled, Button label, Clean map while picking, Show portals in unexplored areas, Portal names (always, or only under the mouse), Marker size and Zoom to fit portals.
- `PickRadius` is gone, because the markers are clicked directly. Its old line in the settings file is simply ignored.
- A package for Thunderstore Mod Manager's "Import local mod": `tools\package.ps1` builds `dist\ModProjects-XPortalMapPicker-0.2.0.zip`, with an icon drawn by `tools\make-icon.ps1`.
- Not yet tested in game.

## 0.1.0 (2026-09-23)

- First version. A Map button beside XPortal's destination list opens the big map with a marker on every portal the list offers. Clicking a marker makes that portal the destination, and OK saves it through XPortal as usual.
- Built against XPortal 1.2.25, Jötunn 2.30.2 and BepInEx 5.4.23. Not yet tested in game.
