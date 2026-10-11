# Map View Rules

Decide which map pins show at which zoom, category by category. Zoomed in, see every berry bush; zoomed out, see only the dungeons, traders and ore you care about. Separate rules for the large map and the minimap, a readout of the zoom you're at, and a button in the settings window that fills a zoom in for you. Made for [Cartur's Map Pins](https://thunderstore.io/c/valheim/p/Cartur/Carturs_Map_Pins/), whose categories it uses; it works on the game's own pins too.

## What it does

- **Rules per category.** Every category of Cartur's Map Pins (ore, dungeons, pickables, spawners and the rest) has a section of rules, and so does every pickable group (berries, mushrooms, crops …). So do the pins you place by hand, death markers, the game's own markers, portals (XPortal Map Picker's portal pins; a portal pin you place yourself stays hand-placed), and the pins Cartur's keeps on your boats, carts and tames.
- **Icons and names apart.** On the large map, each section shows or hides its icons and its names separately: names without icons, icons without names, both, or neither.
- **Per zoom, per map.** For each section: whether the large map shows its icons, and its names; the zoom past which its icons disappear; the zoom past which its names disappear; from which zoom its icons shrink, and how small they get. The minimap has its own set (it never shows names).
- **Follow the level above.** Every value can be left to the section above it (-1, or Follow): a pickable group follows Pickables, a category follows All pins. Change All pins and every section that still follows it changes too.
- **Your own list.** Name a kind or a pin in "Pins with their own rules" (Cloudberry, Raspberry, Draugr Elite Spawner) and it gets a section of its own.
- **The zoom readout.** Switch it on and a corner of each map shows the zoom you're at, in the numbers the settings take.
- **"Use map zoom".** In a configuration manager's window (F1), every zoom setting has a button that fills in the map's current zoom. Open the map, zoom to where you want the cut-off, press F1, click.
- **The name under the pointer.** A pin whose name the rules hide shows it while the pointer is over the pin.
- **Your friends' map, your way.** What a cartography table gave you from your friends: everything, as the game draws it; their explored ground without the pins they placed by hand (the pins Cartur's Map Pins made still show); or their pins without their explored ground.

## The zoom numbers

Every zoom setting, and the readout, uses one number: **0 is fully zoomed in, 100 fully out**, and every turn of the mouse wheel moves it by the same amount (with Map Zoom's 18 notches, about 5.6 a notch). 100 in a "hide past" setting never hides.

## Good to know

- **It only changes what is drawn.** Every pin stays in the game's list, saved with your character, and shared through a cartography table exactly as before. Hide berries at far zoom and zoom back in: every berry pin is there. Nothing is ever removed.
- **Your friends aren't affected.** Your rules are yours: a friend reading a table you wrote gets every pin.
- **A hidden pin can't be clicked**, ticked or deleted by accident: the game ignores pins it doesn't draw. A pin whose name shows without its icon is still there, unseen, at its place under its name, and can be clicked there. Whenever its name doesn't show (it has none, the map is zoomed out past the game's names, or Cartur's hides it), it is hidden like any other.
- **The friends' map.** A friend's pin is one a table gave you that you haven't clicked to keep (the game's own shared-map checkbox counts it the same way). "Made by Cartur's" is told as for your own pins: by the record, the name, or the icon; every friend's pin drawn with one of Cartur's icons counts as Cartur's, also one a friend placed by hand with one (Cartur's icon picker). The game's checkbox on the large map still hides the friends' ground and pins together.
- **Cartur's own shrinking and name cut-off.** Cartur's Map Pins shrinks every pin as you zoom out and hides every name past a zoom. To use this mod's shrinking or name rules instead, switch those off in Cartur's General settings: `ZoomedOutPinScale` to 1 and `HideLabelsFromZoom` to 100. While both mods do it, pins shrink twice, and the log says so once.
- **Cartur's merging and crowding, alongside.** Cartur's still draws one of several same-named pins on the same patch of map, and shrinks crowded ones, as it always did: this mod shrinks an icon by drawing its picture smaller within the width the game gave it, which is what Cartur's measures. Two things to know:
  - it counts every pin, also those this mod hides, so a pin that shows can come out a little smaller beside hidden ones;
  - if the one it keeps of several same-named pins is one this mod hides, the others don't show either (a pin you placed by hand named like a hidden category's pins, near them).
  - Cartur's `MergeRepeatedPins` and `ShrinkCrowdedPins` switch those off.
- **Cartur's search box** fades what doesn't match, but can't bring back a pin these rules hide.
- **How it knows what a pin is.** Cartur's keeps a record of every pin it placed and what it is, beside its settings. This mod reads that record (it never writes it). A pin that isn't in it goes by its name; a friend's pin from a table also by its icon; a pin you placed yourself with one of Cartur's icons counts as hand-placed. Until the record has been read, a moment after you arrive, Cartur's pins are drawn as they are; for a world it has no record of yet, pins with its icons follow Other pins. The pins Cartur's keeps on your boats, carts and tames have their section while its own icons are on (with them off, they are other pins). Without Cartur's Map Pins, the sections for the game's own pins and All pins still apply.
- **Cut-offs as the readout shows them.** A section set to hide past 33 still shows at a readout of 33, and hides at 34.
- **Only you need it.** It's client-side: neither the server nor the other players need it.
- **Changes work at once.** No restart.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library
- Recommended: Cartur's Map Pins (1.6.0 read), and a configuration manager such as shudnal's ConfigurationManager for the buttons

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-MapViewRules-<version>.zip`. Keep the author `drumcowski` and the name `MapViewRules` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `MapViewRules.dll` into a folder named `drumcowski-MapViewRules` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.mapviewrules.cfg`, which appears after the first start; in the bundle, in its own file, in the section "Map View Rules" and one section "Map View Rules - …" per section of rules.

In the settings window every section of rules is listed under this mod's one heading ("Map View Rules" in the bundle, "General" on its own), as a fold-out: click its title row, "[+] Pickables - Berries", to open it, and "[-]" to close it. "(changed)" beside a title says one of its settings isn't at its default. With shudnal's ConfigurationManager (1.1.22 or later) they open and close as you click, and stay as you left them; with another manager every section shows open under its title.

**General**

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off shows every pin and name again at once and takes the readout away; on applies the rules again. |
| Zoom readout | off | Shows the zoom in a corner of each map. |
| Readout corner, large map | TopLeft | Which corner of the large map. |
| Readout corner, minimap | BottomLeft | Which corner of the minimap. |
| Name under the pointer | on | A name the rules hide shows while the pointer is over its pin. |
| Friends' map | Everything | What both maps draw of your friends' map from a table: Everything; ExploredGroundOnly (none of the pins they placed by hand); PinsOnly (none of their explored ground). Only drawing changes. |
| Pins with their own rules | Cloudberry,Lox,Boar,Wolf | Kinds or pin names, separated by commas, each getting a section "Override - <its words>". A name matches in any word order and case. The list is read when the settings window closes; its sections are there, last under the heading, the next time it opens. |

**Every section of rules** (All pins; Ore, Dungeons, Camps, Boss altars, Beehives, Pickables, Runestones, Lore stones, Chests, Spawners, Leviathans, Traders, Wisps, Landmarks, Props, Minibosses, Home; the six pickable groups; Boats, carts and tames; Hand-placed pins; Death markers; Game markers; Portals; Other pins; and each override)

| Setting | All pins | Every other section | What it does |
|---|---|---|---|
| Its title row (Unfolded) | closed | closed | Whether the settings window shows the section open. Only how that window shows it. |
| Large map: icons | on | Follow | Whether the large map draws these pins' icons. (Called "Large map: show" in 0.1.0; a value set then carries over, and "Large map: names" takes the same value once, so the maps look as they did.) |
| Large map: names | on | Follow | Whether the large map draws their names: with their icons, or alone where the icons are hidden. Both off: not drawn at all. |
| Large map: hide icons past zoom | 100 | -1 | Past this zoom, neither icons nor names. 100 never hides. |
| Large map: hide names past zoom | 100 | -1 | Past this zoom, icons without names. 100 never hides. |
| Large map: shrink from zoom | 100 | -1 | Past this zoom, icons shrink evenly down to the size below. 100 never shrinks. |
| Large map: size fully zoomed out (%) | 20 | -1 | How small they get, 10 to 100. |
| Minimap: show, hide icons past zoom, shrink from zoom, size fully zoomed out | on, 100, 100, 20 | Follow, -1 | The same for the minimap. |

**Sections with defaults of their own** (miikeskii's own rules, since 0.2.2; every value not named here follows, as above):

| Section | Its own defaults |
|---|---|
| Chests | Large map: hide icons past 30, hide names past 10 |
| Dungeons | Large map: hide icons past 30, hide names past 20 |
| Lore stones, Ore, Runestones | Large map: hide icons past 20, hide names past 10 |
| Spawners | Large map: hide icons past 30, hide names past 20 |
| Pickables, Pickables - Berries | Large map: icons Show, names Show, hide icons past 10, hide names past 0 |
| Override - Boar, Override - Lox, Override - Wolf | Large map: hide icons past 10, hide names past 10; Minimap: show Hide |
| Override - Cloudberry | Minimap: show Hide |

**Out of the box, miikeskii's own rules** (since 0.2.2): All pins never hides and never shrinks, and every section follows it but those in the table above, which hide their icons and names past a zoom, show pickables and berries, and keep Boar, Lox, Wolf and Cloudberry off the minimap. Give a section a cut-off and only that section changes. An older settings file still at an old default moves to the new one once, the override sections too; a setting you changed stays as you set it. To have this mod shrink pins and hide names everywhere, as Cartur's Map Pins does, set All pins' "shrink from" and "hide names past", and switch Cartur's own off (above). Cartur's look of shrinking from 1% zoom-out down to 20% size, with names only within 3%, is about 15, 20 and 30 here.

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Map View Rules". Once per world it says how it sorted the map's pins (by Cartur's record, by name, by icon …). If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and the maps draw every pin as they do without the mod. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\MapViewRules.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-MapViewRules-<version>.zip`. Until Map View Rules is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn.
