# XPortal Map Picker

Choose where an XPortal portal leads by clicking it on the map instead of finding it in the list.

[XPortal](https://github.com/SpikeHimself/XPortal) lets any portal lead to any other, picked from a drop-down list of portal names. This small companion mod adds a **Map** button beside that list. Click it, and the big map opens with a marker on every portal you could choose. Click a marker, and that portal becomes the destination.

**Also part of miikeskii's QoL Pack.** This mod is one of miikeskii's Valheim mods, which also come together as one package, *miikeskii's QoL Pack*. Install either this or the bundle: with both installed, this copy stands aside and the bundle's copy runs.

## How to use it

1. Use a portal as usual. XPortal's "Hail, traveller!" panel opens.
2. Click **Map**, beside the destination list.
3. The panel steps aside and the big map opens on a clean map: your explored map, your own position and where the other players are right now, with no other pins, and a portal marker with its name on every portal the list offers. The map is centred on the portals and zoomed out just far enough to show them all.
4. Click a portal's marker, or its name. The panel comes back with that portal chosen as the destination.
5. Press **OK** as usual to save it, or **Cancel** to forget it.

To leave the map without choosing, right-click, press Esc or press the map key. The panel comes back unchanged.

While you are picking, the map's usual clicks are paused: a click on the map itself does nothing, a right click cancels instead of deleting a pin, and a double click doesn't add one. Dragging and zooming work as normal, and the markers follow.

## Good to know

- **Only you need it.** The destination still reaches the server through XPortal's own OK button, exactly as when you pick from the list, so neither the server nor the other players need this mod.
- **Nothing is added to your map.** The markers are drawn by this mod over the map, not made of map pins, so they are never saved, never shared at a cartography table, and gone as soon as you pick, cancel or leave the world. Your own pins are only hidden while you pick, and come back straight after. The other players stay on the map while you pick; a setting hides them too.
- **XPortal decides what you can choose.** The markers are exactly the portals in XPortal's list.
- **Portals where you have never been show too**, such as a friend's portal; a setting hides them.
- **No map, no button.** The Map button hides where XPortal hides its Ping button: in worlds played without a map, and when the server has turned on XPortal's `PingMapDisabled` setting.
- **Mouse only**, for now. With a gamepad, keep using the list.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library
- XPortal by SpikeHimself (made for XPortal 1.2.25)

## Installing

- **With Thunderstore Mod Manager:** Settings, then "Import local mod from file", and choose `drumcowski-XPortalMapPicker-0.4.0.zip`.
- **By hand:** copy `XPortalMapPicker.dll` into a folder named `XPortalMapPicker` inside `BepInEx\plugins`, so it ends up at `BepInEx\plugins\XPortalMapPicker\XPortalMapPicker.dll`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

All the settings can be changed in game with a configuration manager (shudnal's ConfigurationManager opens with F1), and every change takes effect at once. They are kept in `BepInEx\config\modprojects.xportalmappicker.cfg`, which appears after the first start. In the bundle, the same settings sit in one section, "XPortal Map Picker", of the bundle's own settings file.

| Section | Setting | Default | What it does |
|---|---|---|---|
| General | Enabled | on | Turns the mod on or off. Off takes the Map button away, at once if a portal's panel is open, and ends any picking; XPortal then works exactly as it does without this mod. |
| General | Button label | `Map` | The text on the button. The button grows to fit it, but never gets wider than XPortal's Ping button, so keep it short. |
| Map | Clean map while picking | on | Hides every other pin on the big map while you pick (your own pins, other mods' pins, pings, and other players unless the next setting is on), so only the portals show. The pins come back the moment you stop picking. |
| Map | Show players while picking | on | On the clean map, keeps showing where the other players are right now: their live icons, with their names when the map is zoomed in far enough for the game to show pin names. Every other pin stays hidden. |
| Map | Show portals in unexplored areas | on | Shows portals that stand where your map is still dark. Off shows only the portals in places your map has uncovered, by you or through a shared map. |
| Map | Portal names | Always | When the names show below the markers: always, or only under the mouse. |
| Map | Marker size (%) | 100 | How big the markers are, in percent of the game's own map pins (50 to 300). |
| Map | Zoom to fit portals | on | Centres the map on the portals and zooms out just far enough to show them all. Off opens the map the usual way, centred on you. Your own zoom is put back when picking ends. |

## Compatibility

- **XPortal.** Made for XPortal 1.2.25. This mod works with parts of XPortal's panel that aren't meant for other mods, so a future XPortal may change them. If it does, this mod switches itself off when the game starts and writes a warning to `BepInEx\LogOutput.log`, and XPortal keeps working as usual.
- **Cartur's Map Pins.** Works alongside it. Its pins are ordinary map pins, so they are hidden while you pick (unless "Clean map while picking" is off) and come back afterwards. Its display settings don't apply to the portal markers, which aren't pins. With "Clean map while picking" off and its icon picker switched on, a Shift+click on the map can still open its pin editor on one of your own pins.
- **MyLittleUI and Resizable Minimap.** No overlap expected: they change other parts of the map.
- Other mods that change what clicks on the big map do may get in the way while you are picking.

## If something goes wrong

- **No Map button?** Check that "Enabled" is on, then look in `BepInEx\LogOutput.log` for a line from "XPortal Map Picker". It says whether the mod started, or which part of XPortal or the game it couldn't find.
- **Your pins are still hidden after picking?** That shouldn't happen: picking is built to put them back however it ends, even after an error. Leaving the world and joining again brings them back in any case. Please report it with `BepInEx\LogOutput.log`.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\XPortalMapPicker.dll`.
- **What's where:** `src\` is the mod itself, written as a module of miikeskii's QoL Pack; `standalone\Plugin.cs` makes it a plugin of its own; `kit\` is a copy of the QoL Mods kit, which runs a module (never edit it here: the kit's own copy lives with the bundle, and its rules are in the bundle's `MODULES.md`). This folder builds on its own; the bundle compiles `src\` from here as well.
- **Package:** `tools\package.ps1` builds the mod and writes `dist\drumcowski-XPortalMapPicker-<version>.zip` for Thunderstore Mod Manager's "Import local mod from file". It checks that the project file, `src\Module.cs` and this changelog agree on the version. The icon, `icon.png`, is drawn by `tools\make-icon.ps1`.
- **The author field:** the zip's `manifest.json` carries `"author": "drumcowski"`, because the mod manager's local import requires one and reads `Author-Name-Version` from the file name. It matches the author given at the first import, so a newer version replaces the installed one rather than landing beside it (0.2.0's zip said "ModProjects"). A real Thunderstore upload would drop the author field, since Thunderstore takes the author from the team that uploads the package.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. If yours are elsewhere, override the two paths: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"` (`tools\package.ps1` takes `-ValheimManaged` and `-ModProfile`).
- **After an XPortal or Valheim update,** run `tools\check-hooks.ps1`. It reads the installed DLLs without running them and lists anything this mod finds by name that has changed. Rebuilding checks the rest.

## Credits

XPortal is by SpikeHimself: https://github.com/SpikeHimself/XPortal. It does all the real work (the portal list, the linking and the server side). This mod only adds a button to its panel and borrows its list.

Built with BepInEx, HarmonyX and Jötunn. What it borrows, from whom and under which licence: `CREDITS.md`.
