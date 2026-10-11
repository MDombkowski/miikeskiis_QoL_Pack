# Terraformer

Terrain tools in the hoe and the cultivator.

- **A preview grid** on the ground shows exactly which ground your click will change, before you click.
- **Every terrain tool remembers** the radius and sharpness you give it.
- **Flatten** really levels ground.
- **Square tools** snap to the ground's grid and turn with the building turn, so the ground can follow a building set at an angle. Right Shift and the wheel make them rectangles.
- **Slope** lays the ground onto one even slope, or a ramp between two clicks.
- **Paint only:** hold Left Alt and Paved road or Cultivate paints the ground without reshaping it.
- **See the grown tree before you plant:** hold Left Alt while placing a sapling, pick its shape and size, and it grows into exactly that.
- **Lower ground** digs from the hoe's own menu.
- **Undo** takes back your last strokes.
- **No seams, no trespass:** a big stroke never leaves a seam at a zone border, and never reshapes a warded area it doesn't let you into.

It replaces AdvancedTerrainModifiers, and stays off while that mod is installed.

**Out of the box:**
- Terraformer's own tools are in the hoe's and cultivator's menus, after the game's own. "Tools in the hoe and cultivator" switches them all off, and each has its own switch.
- The radius and sharpness keys work on the game's own tools too.
- The preview grid shows for every terrain tool.

## What it does

### Size and sharpness

- **Hold Right Ctrl and turn the wheel to resize any round terrain tool.** Each notch is 0.5 m of radius (1 m across), landing on the next whole step. The new size shows in the middle of the screen, with its width: "Radius 2.0 m · 4 m across".
  - The radius is the ground the tool moves; the game's thin painted rim around it doesn't count.
  - Terraformer's own round tools start at 4 m across.
- **Hold Right Alt and turn the wheel to change how sharp a tool's edge is:** 0 % fades out gently, 100 % is as strong at the edge as in the middle.
- **Both work on the game's own tools** (Level ground, Raise ground, Pathen, Paved road, Cultivate, Grass), on Terraformer's own round tools, and on other mods' terrain pieces.
- **Each tool remembers its own values** when you put it away, open the build menu, pick another tool or quit the game. **Right Ctrl + Backspace** puts the tool in your hand back to its own.

### The preview grid

- **While you aim a terrain tool, a grid lies on the ground over exactly the points your click will change.** It follows the ground, with a line at the stroke's true edge.
- **Each point is as bright as its change,** so a soft tool fades toward its edge and a 100 % sharp one doesn't.
- **Ground the tool won't move stays dim.** Lower ground lights only the ground it can still dig.
- **Colours:**
  - The paint tools tint the grid in the finish you'll get: stone grey for paved, dark soil for cultivated, dirt, grass.
  - Raise shows green, the digs orange, Remove terrain changes magenta.
  - Flatten shows the ground as it will be, in blue.
  - It turns red where the stroke can't be placed.
- **It replaces the game's round marker.** With "Preview grid" off, that marker is back: drawn at the true size for a resized tool and for Terraformer's own, as the game draws it for the game's own tools at their own size, and at the paint's size while paint only is on.

### Square tools, for exact work

- **They snap to the ground's own points** and change a square of them.
- **They turn with the mouse wheel,** as building pieces do: 22.5° a notch by default, or 45°, or not at all. They share the game's own turn with every piece, so a square takes the angle of the building you last placed. **Home** turns it back to north.
  - Turned 45°, a square's north-east and south-west sides are straight and the other two stepped. The game draws the ground as 1 m squares split along one diagonal, so that's as straight as the ground can be.
- **Right Ctrl and the wheel resize them,** a row of points on each side at a time. They start at 5 × 5 points (4 m) and go up to 17 × 17 (16 m). The size shows with its side in metres.
- **Right Shift and the wheel make a rectangle:** the width changes and the length (along the way the tool faces) stays: "Rectangle 9 × 5 · 8 × 4 m". Right Ctrl still grows both sides, the wheel turns it through 180°, and the reset key makes it a square again. "Rectangles for" can keep this to Slope alone.

### Undo

- **Right Ctrl + Z puts back the ground your last stroke changed,** its height and its paint, exactly as it was.
  - It gives back the stone the stroke cost.
  - Press it again for the stroke before, up to your last 5 (a setting).
  - It works for every stroke of yours with the hoe or the cultivator, the game's own tools included.
- **It refuses, and says why,** where anyone has changed that ground since (so a friend's work is never undone), or where the ground is too far away to be loaded.
- **Your strokes are kept until you quit or leave the world.**

### Slope

- **Slope, in the hoe, lays the ground in its area onto one even slope.** The grid shows the result before you click: points that will rise in green, those that will drop in orange.
- **How it finds the slope** (a setting):
  - **High to low** (out of the box): through the highest and the lowest point in the area, falling the way the ground there already falls. An even slope is left as it is.
  - **Closest to the ground:** the even slope nearest the ground already there. It moves the least earth and irons out bumps.
  - **Two clicks:** click where the slope starts, then where it ends, and a ramp as wide as the tool is laid between them. The first click costs nothing. Each height is where you point, or what a Flatten key held at that click picks (Left Ctrl the lowest point, Left Alt the highest). The reset key, another tool or leaving building forgets the first click.
- **Its edge:** Right Alt and the wheel. At 100 % the whole area lies on the slope, with a step where it meets the ground around; softer, a band inside the edge blends back into the ground around. It starts at 50 %.
- **Square or round,** and a rectangle with the width key. "Past the high and low points" keeps it between them (out of the box) or lets it carry on sloping to the edge. It can only lower, or only raise.
- **It leaves dirt,** as Level ground and Flatten do, or keeps the ground's look ("Its ground"). It's free, as Flatten; "Costs as Raise ground" makes each click cost two stone near a workbench.
- **A click that would change nothing** ("Only raise" on ground above the slope, say) is refused with "Nothing here to slope.", costing no stamina, wear or stone.

### Paint only

- **Hold Left Alt with Paved road or Cultivate in hand, and the tool only paints:** its paving or tilled soil lands on the ground exactly as it lies, and not one point of the ground is raised, lowered, levelled or smoothed. Let go and the tool smooths as before.
- It works with the game's Paved road and Cultivate and with Terraformer's squares of them, each with its own switch. Pathen, Grass and the paths never shape the ground, so the key changes nothing there.
- While it's on, the grid shows only the paint and the build bar says "paint only" after the tool's name. A setting makes the key a toggle: press once to turn it on, again to turn it off.

### The grown tree, before you plant

- **Hold Left Alt while you place a sapling with the cultivator:** the tree it will grow into stands where the sapling goes, full grown and see-through, facing as the sapling faces and at the size it will have. The build bar names it: "Birch Sapling · Birch 2 of 2 · 85 % (50 % to 100 %)".
- **Q and E** step through the trees a sapling can become. Only the birch has two shapes; every other sapling grows one tree.
- **Right Alt and the wheel** set its size, within that sapling's own range. Each sapling remembers your pick.
- **Plant it while the tree shows, and it grows into exactly that tree,** at that size and facing, instead of the game's random one. Every sapling one click plants gets it, PlantEasily's rows included (each facing the way PlantEasily turned it). Crop seeds show their crop and keep their size and facing; vines, which grow against a wall, show nothing.
- **While the tree shows, the next sapling keeps your turn** instead of a random one, so a row faces one way.
- **The limit:** a sapling grows in the game of the first player near it when its time comes. If that game doesn't run this version of Terraformer (a friend without the pack, or with AdvancedTerrainModifiers, which keeps Terraformer off), the game picks the tree as usual.

### The tools, after the game's own in each menu

| Tool | Menu | What a click does |
|---|---|---|
| Level ground (square) | hoe | Smooths the square to the height of the ground you stand on (Left Shift: where you point), by at most 1 m in all, as the game's Level ground does; makes it dirt. |
| Flatten | hoe | Levels the ground to one height, all the way, and makes it dirt. It uses the height of the ground you stand on, or another the Flatten keys pick as you click: **Left Shift** where you point, **Left Ctrl** the lowest point, **Left Alt** the highest. The median and the average are choices too. Sharpness softens its edge; it starts at 100 %, flat to the edge. |
| Flatten (square) | hoe | Levels the square to one height, all the way, picked as Flatten's; makes it dirt. |
| Slope | hoe | Lays the square (or circle, or rectangle) onto one even slope, found as "How the slope is found" says; makes it dirt. Free. |
| Raise ground (precision) | hoe | Raises the square by the lift, never above the pointed height plus the lift, and never lowers any point. Right Alt and the wheel set the lift, up to 1 m. 2 Stone, near a workbench, as the game's Raise ground. |
| Lower ground | hoe | Digs where you point, like a pickaxe hit: the Shovel's dig, without the Shovel. It's there from the start. |
| Lower ground (precision) | hoe | Digs the square down by its depth, never below the pointed height minus the depth, and never raises any point. Right Alt and the wheel set the depth, up to 1 m. |
| Pathen (square) | hoe | Makes the square dirt, with no height change. |
| Paved road (square) | hoe | Smooths like Level ground (square) and paves the square. 1 Stone, near a stonecutter. |
| Paved road (path) | hoe | Paves with no height change, round and resizable. 1 Stone, near a stonecutter. |
| Paved road (path, square) | hoe | Paves the square with no height change. 1 Stone, near a stonecutter. |
| Remove terrain changes | hoe | Puts the square back to the world's own height and paint. |
| Cultivate (square) | cultivator | Smooths the square to where you point, by at most 1 m, and cultivates it, lined up for planting. |
| Cultivate (path) | cultivator | Cultivates with no height change, round and resizable. |
| Cultivate (path, square) | cultivator | Cultivates the square with no height change. |
| Grass (square) | cultivator | Grows grass back on the square, as the game's Grass does. |

- **"Ground cannot be lowered further."** When nothing a Lower ground click reaches can go any lower, the click is refused with that message, and it costs no stamina or wear. If any of it can go lower, it digs that part.
- **Dust:** each tool has a switch for the dust it kicks up where it lands (the Dust section). Its sound stays.
- **No seams:** a resized, square or Flatten stroke reaches every zone of the world it changes, however big, so the ground never splits along a zone border. AdvancedTerrainModifiers' big strokes did, since its 1.5.1.
- **Wards:** some strokes are refused if any part of them would change ground in a ward that doesn't let you in, or in a place where the game allows no building. That covers a stroke bigger than its tool's own size, Lower ground, Flatten and every square. A stroke of the game's own tools at their own size is checked as the game checks it.
- **Only your own strokes change:** your radius and sharpness never touch a friend's strokes or your pickaxe.

## Level ground, Flatten and Lower ground

- **The game's Level ground doesn't level: it smooths.** It pulls the ground toward the height of the ground you stand on, fully at the middle and not at all at the edge. The game lets that pull move any bit of ground at most 1 m in all, so a pillar or a cliff barely changes. Terraformer keeps Level ground as the game made it.
- **Flatten is the one that levels.** Every point it covers goes to one height, however far.
- **Lower ground digs** 0.5 m a click at its middle, with no 1 m limit.
- **One rule covers every tool:** the game never lets ground end up more than 8 m above or below where the world made it.

## Good to know

- **Only you need it.** It's client-side. Every stroke of yours is worked out on your own game and saved as the game's own ground, so friends and the server need nothing to see it. To do that, it takes over that bit of ground's record first, as the game does for whoever changes it. Your settings never change a friend's strokes, with or without Terraformer on their side.
- **No items.** Lower ground is a piece in the hoe's menu. An item from a mod the server doesn't have is deleted from chests on a server whose mods re-sort chests.
- **Replacing AdvancedTerrainModifiers:** uninstall it. Terraformer stays off while it's installed, and says so in the log.
  - The ground you shaped with it stays as it is.
  - Its Shovels disappear when it's gone; Lower ground in the hoe does their dig.
- **Changes work at once.** Change a setting in game and the next stroke uses it; switching a tool off takes it out of its menu at once.
- **Repainting is always kept.** The game decides whether to save a paint-only stroke from a plain sum of the colours. That sum is the same for dirt, paved and cultivated ground, so it can skip saving a stroke that turns one into another: you'd see it, friends wouldn't, and it would be gone when the area reloads.
  - While Terraformer is on, it makes sure every such stroke is saved, the game's own included.
  - Switched off or removed, a paint stroke of the game's own that falls wholly on ground a Terraformer square painted may not keep.
- **Flatten is free,** as the game's Level ground is. It can fill ground as well as cut it, where Raise ground costs stone.

## Mods that do the same job

If you prefer another mod for this, switch Terraformer off ("Enabled" off) and use the other. Switched off, it leaves nothing behind but the ground you shaped.

- **AdvancedTerrainModifiers** (and its fork, AdvancedTerrainModifiersCompatible): Terraformer replaces it, and stays off while it's installed.
- **Other terrain-tool mods** that resize the game's own tools (EarthWright, SmarterHoe, LevelAtFeet, TerraHoe, MyDirtyHoe and others): two mods resizing the same tool would fight, so use one. Mods whose tools are items or pieces of their own (PlanBuild's, Infinity Hammer's) don't touch Terraformer's.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** go to Settings, then "Import local mod from file", and choose `drumcowski-Terraformer-<version>.zip`. Keep the author `drumcowski` and the name `Terraformer` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `Terraformer.dll` into a folder named `drumcowski-Terraformer` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside, does nothing and says so in the log; you can remove it.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1).
- **Alone,** they are kept in `BepInEx\config\modprojects.terraformer.cfg`, which appears after the first start, in the sections General, Paint only, Slope, Grown trees and Dust.
- **In the bundle,** they are in its own file, in the sections "Terraformer", "Terraformer - Paint only", "Terraformer - Slope", "Terraformer - Grown trees" and "Terraformer - Dust".
- **In F1** they all sit under Terraformer's one heading: the general settings first, then each part behind a title row that opens and closes it ("[+] Slope"; with shudnal's ConfigurationManager 1.1.22 or later; with another, every part shows open under its title).

A file from 0.1.x moves to 0.2.0's new defaults once, where it still holds 0.1.x's: the square turn, the reset key and the largest square. A value you changed is kept.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off takes its tools out of the menus and gives the game's own tools back their own size, sharpness, marker and dust at once. |
| Tools in the hoe and cultivator | on | Off: only the game's own tools are in the menus; the keys still work on them. Each tool also has its own switch. |
| Tool: … (one for each tool above) | on | Off takes that tool out of its menu. |
| Radius key | Right Ctrl | Hold it and turn the wheel to resize a tool; with a square tool, a row of points on each side. |
| Radius per notch | 0.5 m | 0.1 to 2 m. A radius is half the width: 0.5 is 1 m across, 1 is 2 m, 2 is 4 m, 4 is 8 m, 8 is 16 m. |
| Largest radius | 20 m | 4 to 30 m. |
| Sharpness key | Right Alt | Hold it and turn the wheel to sharpen a tool that changes height, to set the precision tools' lift or depth, or Slope's edge; with a sapling and the grown tree showing, its size. |
| Sharpness per notch | 5 % | 1 to 25 %. |
| Remember each tool's settings | on | Off: every tool goes back to its own values when you stop building. |
| Keep them after quitting | on | Off: they last until you quit. |
| Reset key | Right Ctrl + Backspace | Puts the tool in your hand back to its own values. |
| Undo key | Right Ctrl + Z | Puts back the ground your last stroke changed, and the stone it cost. |
| Strokes to undo | 5 | 1 to 5. |
| Reverse the wheel | off | On: the wheel toward you makes things bigger, sharper or higher. |
| Square tools turn | 22.5° | 22.5° (the game's own steps; all four sides stepped, and a small square looks the same as an unturned one), 45°, or Off. |
| Straighten key | Home | Turns a square tool back to north. |
| Square tools resize | on | Off: every square is 5 × 5 (4 m), and none can be made a rectangle. |
| Largest square | 17 | Points along an edge, odd: 3 to 31 (a rectangle's length and width each). The points are 1 m apart, so 17 is 16 m. |
| Rectangles for | every square tool | Which square tools the width key makes rectangles of: every square tool, or Slope only. |
| Width key | Right Shift | Hold it and turn the wheel to make a square tool's area wider or narrower, its length staying. |
| Lift per notch | 0.05 m | The precision tools' lift and depth. |
| Largest lift | 1 m | Up to 8 m; each click of the precision raise still costs two stone. |
| Precision raise can dig | off | On: the raise's lift can go below zero, and the square is dug down instead. |
| Lower ground depth | 0.5 m | A click's depth at its middle; the pickaxe digs 0.5 m. |
| Lower ground needs a workbench | off | On: both Lower grounds work only near a workbench, as AdvancedTerrainModifiers' did. |
| Lower ground drops stone | off | On: a stone a third of the time, as a pickaxe hit. |
| Flatten to | your feet | The height Flatten levels to when no Flatten key is held: your feet, where you point, the lowest point, the highest point, the median or the average. |
| Flatten key 1, 2, 3 | Left Shift, Left Ctrl, Left Alt | Held as you click with Flatten, each picks its own height (below). Left Ctrl is also the game's crouch key. |
| Flatten key 1, 2, 3 levels to | where you point, the lowest point, the highest point | The height each key picks. |
| Check wards over the whole stroke | on | Also the places where the game allows no building. Off: the game's own checks, at the stroke's middle only (Lower ground's over its reach, as the pickaxe's dig). |
| Messages | Centre | Where new values are shown: Centre, TopLeft or Off. What undo did always shows. |
| Preview grid | on | Off: the game's round marker (at the true size for a resized tool and for Terraformer's own, and at the paint's size while paint only is on), and the square outline. |
| Square outline | on | With the grid off: the line around a square tool's square. Off: the game's own round marker shows where it goes. |

**Paint only:**

| Setting | Default | What it does |
|---|---|---|
| Paint-only key | Left Alt | Held with Paved road or Cultivate in hand, the tool only paints. Left Alt is none of the game's keys; it's also Flatten key 3, which works only with Flatten. |
| Hold or toggle | while held | Or pressed once on, again off. |
| Paint only: Paved road, Cultivate, Paved road (square), Cultivate (square) | on | Off: that tool shapes the ground as it paints, key or not. |
| "Paint only" by the tool's name | on | The build bar says "paint only" while it's on. |
| Toggle turns off when the tool is put away | on | With the toggle: putting the hoe or cultivator away turns it off. |
| Message as the toggle turns on and off | TopLeft | Centre, TopLeft or Off. |

**Slope:**

| Setting | Default | What it does |
|---|---|---|
| How the slope is found | high to low | High to low, closest to the ground, or two clicks (above). |
| Past the high and low points | level | Level: never above the high point or below the low one. Keep sloping: on to the edge. |
| Area | square | Square (a rectangle with the width key) or round (the radius key). |
| Edge | 50 % | Where its edge starts; Right Alt and the wheel change it, and Slope remembers. |
| Only lower, only raise, or both | both | Only lower cuts, only raise fills. |
| Its ground | dirt | Or as it was, keeping its paint. |
| Costs as Raise ground | off | On: two stone a click, near a workbench. |

**Grown trees:**

| Setting | Default | What it does |
|---|---|---|
| Preview key | Left Alt | Held while you place a sapling, the grown tree shows. |
| Hold or toggle | while held | Or pressed once on, again off. |
| Previous tree, Next tree | Q, E | Step through a sapling's trees while it shows. In building, the game's Q and E only step a piece's snap points, which saplings don't have. |
| The tree's size | picked with the key | Or as the game picks, the largest, the smallest. With the key: Right Alt and the wheel, from the largest. |
| Lock the tree it becomes | on | Off: the preview only shows, and the game picks when it grows. |
| Lock its facing | on | Off: the game turns the tree up to 11.25° either way when it grows. |
| Keep my turn for the next sapling | on | While the tree shows, the next sapling keeps your turn. |
| Preview opacity | 0.5 | 0 can't be seen, 1 is solid. |
| Name the tree in the build bar | on | The bar names the tree and its size. |
| Show the space it needs to grow | off | A ring of the room it needs (2 m for most trees, 3 m for an oak). |

**Dust:**

| Setting | Default | What it does |
|---|---|---|
| Dust: … (one for each of the game's six tools and each of Terraformer's) | on | Off: that tool kicks up no dust where it lands; its sound stays. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Terraformer". If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and the hoe and cultivator work as in the game. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\Terraformer.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-Terraformer-<version>.zip`. Until Terraformer is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:**
  - The mod's code is in `src\`; the bundle compiles it from here too.
  - Its own plugin is in `standalone\`.
  - `kit\` is a copy of the QoL Mods kit that runs it. How a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded, and nothing is copied or written outside this folder. If yours are elsewhere, override the two paths: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: its code was written by Claude agents (Anthropic), directed by miikeskii.
