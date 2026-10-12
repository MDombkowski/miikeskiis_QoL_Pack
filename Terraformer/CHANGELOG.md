# Changelog

## 0.3.2 (2026-10-11)

- Settings name no other mod unless this mod works with it, stands aside for it or makes room for it: four settings' descriptions ("Largest lift", "Precision raise can dig", "Lower ground needs a workbench", "Lower ground drops stone") no longer compare with another mod's tools; what each setting does is unchanged.

## 0.3.1 (2026-10-11)

- **Messages fit the screen**: a message line too wide for your screen is broken at spaces into lines that fit, measured by the game's own text, as every mod of miikeskii's QoL Pack now does.

## 0.3.0 (2026-10-10)

Three additions at miikeskii's word of 2026-10-10, each a part of its own in the settings.

- **Paint only.** Hold Left Alt with Paved road or Cultivate (the game's, or Terraformer's squares of them) and the tool only paints: its paving or tilled soil lands on the ground exactly as it lies, and not one point is raised, lowered, levelled or smoothed. The grid shows only the paint, and the build bar says "paint only". A setting makes the key a toggle. Pathen, Grass and the paths already only paint.
- **Slope, a new tool in the hoe.** It lays the ground in its area onto one even slope, shown on the grid before you click (rising points green, dropping ones orange):
  - High to low (out of the box): through the area's highest and lowest points, falling the way the ground there falls, so an even slope already there is left alone.
  - Closest to the ground: the even slope that moves the least earth.
  - Two clicks: the first click marks where the slope starts, the second where it ends, and a ramp as wide as the tool is laid between them.
  - Right Alt and the wheel set its edge, from a hard step (100 %) to a band that blends into the ground around. It can be set to only lower or only raise, to keep the ground's look, and to cost as Raise ground. Free and without a workbench out of the box, as Flatten.
  - A click that would change nothing ("Only raise" on ground above the slope, say) is refused with "Nothing here to slope.", costing no stamina, wear or stone.
- **Rectangles.** Hold Right Shift and turn the wheel to make any square tool wider or narrower: "Rectangle 9 × 5 · 8 × 4 m". Right Ctrl still grows both sides; the reset key makes it a square again.
- **The grown tree, before you plant.** Hold Left Alt while you place a sapling with the cultivator: the tree it will grow into stands there, full grown and see-through, at its facing and size. Q and E pick the birch's two shapes (the only sapling with more than one), Right Alt and the wheel the size. A sapling planted while it shows grows into exactly that tree, size and facing, PlantEasily's rows included, where the game that grows it has this version; otherwise it grows as the game picks. While the tree shows, the next sapling keeps the turn you gave it instead of the game's random one ("Keep my turn"), so a row faces one way.
- **Fixed: square tools turned the wrong way** (0.2.0): at 22.5° a square lay at -22.5° beside a building at 22.5°. Squares now turn the way buildings do; at 0° and 45° nothing changes.
- **F1:** Terraformer's settings sit under its one heading, its parts (Paint only, Slope, Grown trees, Dust) folding open and shut. "Rectangles for" and "Width key" are with the square tools' settings.

## 0.2.0 (2026-09-30)

From miikeskii's first test of 0.1.1 ("working quite well", with fourteen points).

- **A preview grid on the ground.** While you aim, it shows exactly which ground your click will change, each point as bright as its change, with a line at the true edge.
  - Lower ground lights only what it can still dig; the paint tools show their finish; Flatten shows the ground as it will be.
  - It replaces the game's round marker, which drew Raise ground and Lower ground at half their size. With the grid off, that marker is back, at the true size for a resized tool and for Terraformer's own (the game's own tools at their own size keep the game's).
- **Flatten and Flatten (square), new in the hoe.** They really level the ground to one height (the game's own Level ground only smooths, by at most 1 m a point).
  - The height is the ground you stand on, or another picked by a key held as you click: Left Shift where you point, Left Ctrl the lowest point, Left Alt the highest. The median and the average are choices too.
- **Lower ground (precision), new:** it digs a square down by its depth a click.
- **"Ground cannot be lowered further."** A Lower ground click that would lower nothing is refused, costing no stamina.
- **Undo, on Right Ctrl + Z.** It puts back your last stroke exactly and gives back its stone, up to your last 5 strokes. It never undoes over a friend's work. **The reset key moves to Right Ctrl + Backspace.**
- **The camera no longer zooms** while the radius or sharpness key is held. It used to, whenever the aim reached past building range.
- **Dust:** a switch per tool, in a new Dust section.
- **Sizes in metres:**
  - The message shows the width ("Radius 2.0 m · 4 m across"; squares with their side).
  - A notch lands on the next whole step.
  - The radius counts the ground a tool moves, not its painted rim.
  - Terraformer's own tools start at 4 m: round at 2 m radius, squares at 5 × 5.
  - Squares go up to 17 × 17 (16 m).
- **The square tools turn 22.5° a notch by default,** and the choices read 22.5°, 45° and Off.
- **Lower ground is there from the start:** the "Lower ground needs iron" setting is gone.
- **New pictures:** Paved road (path) and Cultivate (path) are Pathen's stroke in the game's paving stones and tilled soil. Flatten has a level sign.
- **A 0.1.x settings file moves once** to the new defaults of the square turn, the reset key and the largest square, where it still holds 0.1.x's. A key the game's input can't read is ignored and named once in the log, rather than switching Terraformer off.

## 0.1.1 (2026-09-30)

- **Its tools are in the menus out of the box** ("Tools in the hoe and cultivator" on by default), at miikeskii's call. Nothing else changes.

## 0.1.0 (2026-09-30)

- **First version**, as a mod of miikeskii's QoL Pack and on its own. Written from scratch to replace AdvancedTerrainModifiers, whose every tool it has; it stays off while that mod is installed.
- **Every round terrain tool resizes and sharpens** with Right Ctrl and Right Alt and the wheel (his AdvancedTerrainModifiers keys), the game's own included, and **each tool remembers its own radius and sharpness**, also after quitting; Right Ctrl + Z puts it back to its own.
- **Square tools** snap to the ground's points, **turn with the wheel in 45° steps** (sharing the game's own build turn, so a square takes the angle of the building last placed; Home straightens it), resize a row of points at a time (3 × 3 by default), and show their outline on the ground.
- **Twelve tools:** Level ground, Pathen, Paved road, Cultivate and Grass as squares; Raise ground (precision); Lower ground in the hoe (the Shovel's dig, shown once iron is found); Paved road and Cultivate as paths; Remove terrain changes.
- **No seams:** a resized or square stroke reaches every zone it changes. **Wards**, and the places where the game allows no building, are checked over the whole of a big stroke, Lower ground and every square. His strokes are worked out on his own game, and only his own strokes use his numbers, which also travel with each stroke.
- **Repainting is always saved:** the game's own check can't tell dirt from paved or cultivated ground, and can skip saving a stroke that turns one into another; while Terraformer is on, every such stroke that changed the paint is saved, the game's own included.
- **Off out of the box:** its tools stay out of the menus until "Tools in the hoe and cultivator" is on; the keys work on the game's own tools at once.
- Thirty-seven settings, each taking effect at once.
