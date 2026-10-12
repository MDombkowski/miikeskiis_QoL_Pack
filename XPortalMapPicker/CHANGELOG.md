# Changelog

## 0.6.3 (2026-10-11)

- **Messages fit the screen**: a message line too wide for your screen is broken at spaces into lines that fit, measured by the game's own text, as every mod of miikeskii's QoL Pack now does.

## 0.6.2 (2026-10-08)

- **XPortal's portal window can no longer lock you in**. XPortal gives its window the game's window group at the lowest priority, and the game makes such a window's buttons dead while any other window group of a higher priority is open, even an unseen one: at the portal at the world's centre, after dying and respawning there, the pointer moved but nothing lit up, Escape did nothing and only Alt+F4 got miikeskii out. While the window shows, the Map Picker now lifts it above every other window group, so its buttons, Enter (OK) and Escape (Cancel) work again.
- If the window is ever still dead, **Enter presses OK and Escape presses Cancel** for it (his ask), and the log says so.
- Each time the window opens with another window group in front of it, the log names that window, to find out which it was.

## 0.6.1 (2026-10-06)

- **miikeskii's own settings are the defaults now**: "Portals on the large map" and "Portals on the minimap" are on out of the box.
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.

## 0.6.0 (2026-09-29)

- **The portal pins look like the game's own:** the white portal pin, the one you can place yourself (and the one the picking map uses), instead of the full-colour build-menu picture, which stood out among the other pins (miikeskii's word).
- **A portal's name under the pointer:** a new setting, **Portal names on mouse-over** (on), shows a portal pin's name on the big map while the pointer (or a gamepad's crosshair) is over it, at any zoom, also where Cartur's Map Pins (zoomed out, or names crowding), Map View Rules or "Portal names on the large map" leaves it unnamed.

## 0.5.0 (2026-09-29)

- **Portals on your map.** Three new settings, in a new section "Portal pins": **Portals on the large map** and **Portals on the minimap** show every portal in XPortal's list as a pin on that map, with the portal's own picture from the build menu (a stone portal shows as one once your game has seen it). **Portal names on the large map** shows their names below them. The two map settings are off by default, so nothing changes until you turn one on; the names are on.
- The pins follow XPortal's list, read once a second: a new portal gets its pin, a renamed one's name changes, and a destroyed one's pin goes. They are never saved with your map, never shared at a cartography table, and can't be deleted with a right click. They stay off in worlds without a map and while the server has turned on XPortal's `PingMapDisabled`, and step aside while you pick a destination.
- A portal's name is shown as typed, without the game's text styling or translation (the game strips `$` and `<` from the names players type for their own pins, and the same is done here).
- The existing settings keep their keys and values, and show in the same order as before; only their order numbers changed, to make room for the new section.

## 0.4.0 (2026-09-25)

- **Now also part of drumcowski's QoL Mods**, the bundle of all drumcowski's Valheim mods, where it has its own section of the bundle's settings. On its own, this release works exactly as 0.3.0 did: same settings file, same settings, same behaviour.
- **With the bundle installed too,** this copy stands aside and does nothing, so the game isn't changed twice. The log says so, and this copy can be removed. The bundle takes this mod's settings over the first time it runs.
- After an error switches the mod off, its patches of the game's map clicks are now removed as well, so the map works exactly as without the mod until the next start.
- The package now includes `CREDITS.md`: what the mod borrows, from whom, and under which licence.

## 0.3.0 (2026-09-25)

- **Other players show while you pick.** The clean map keeps the other players' live icons, with their names when the map is zoomed in far enough for the game to show pin names, so you can see where your friends are as you choose a portal. Every other pin stays hidden as before. The new setting "Show players while picking", on by default, turns this off.
- The players are drawn by this mod as copies of the game's own player icons, below the portal markers, and follow them every frame. The game's pins themselves are left alone, so with the setting off the clean map is exactly as in 0.2.0.
- The package is now `drumcowski-XPortalMapPicker-<version>.zip`, the author name 0.2.0 was imported under, so importing it replaces 0.2.0 in Thunderstore Mod Manager instead of adding a second copy.

## 0.2.0 (2026-09-24)

- **A clean map while picking.** The big map shows your explored map and your own position with no other pins on it, only the portals you can choose. Every pin comes back the moment you stop picking. The setting "Clean map while picking" turns this off.
- **The mod's own portal markers.** The markers are no longer map pins, so nothing is ever added to your map, saved with it or picked up by other map mods. They follow the map as you pan and zoom, turn orange under the mouse, and are clicked directly; clicking a portal's name works too.
- **Portals in unexplored areas show**, such as a friend's portal somewhere you have never been. The setting "Show portals in unexplored areas" hides them.
- **Settings in game.** Every setting shows in a configuration manager (shudnal's ConfigurationManager opens with F1), in order and with plain names, and takes effect at once: Enabled, Button label, Clean map while picking, Show portals in unexplored areas, Portal names (always, or only under the mouse), Marker size and Zoom to fit portals.
- `PickRadius` is gone, because the markers are clicked directly. Its old line in the settings file is simply ignored.
- A package for Thunderstore Mod Manager's "Import local mod": `tools\package.ps1` builds `dist\ModProjects-XPortalMapPicker-0.2.0.zip`, with an icon drawn by `tools\make-icon.ps1`.

## 0.1.0 (2026-09-23)

- First version. A Map button beside XPortal's destination list opens the big map with a marker on every portal the list offers. Clicking a marker makes that portal the destination, and OK saves it through XPortal as usual.
- Built against XPortal 1.2.25, Jötunn 2.30.2 and BepInEx 5.4.23.
