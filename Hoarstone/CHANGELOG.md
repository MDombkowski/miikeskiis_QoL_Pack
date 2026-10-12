# Changelog

## 0.2.0 (2026-10-11)

- **Lists picked from pictures**: "Pieces that never count" and "Its cost" have a Pick button in the settings window (F1): it opens a window of everything the list can hold, each with its picture, where a click puts a thing on the list or takes it off, with an amount box and Up and Down for the cost. The typed box stays beside it, and the list is written as the mod reads it, with any names the game doesn't know kept. The window is set in the new section "Lists (every mod)" (its size, its pictures', whether things not found yet show).

## 0.1.2 (2026-10-11)

- **The pack's other mods can ask where its settlements are**: whether a place is inside a settlement's shape and where the nearest Hoarstone is, so House Trained can keep tames on a Hoarstone's ground. Nothing changes in what Hoarstone itself does.

## 0.1.1 (2026-10-11)

- **Messages fit the screen**: a message line too wide for your screen is broken at spaces into lines that fit, measured by the game's own text, as every mod of miikeskii's QoL Pack now does.

## 0.1.0 (2026-10-10)

- **First version**, as a mod of miikeskii's QoL Pack and on its own.
- **A settlement's whole shape**, worked out from the pieces players built, as the agreed Settlement Shape Lab does: settlements sorted by DBSCAN; on squares of 2 m, every piece grown by half the gap, what that encloses filled (the sea stops the spread), shrunk back (never from the sea), a margin and a shore margin over the water. Worked out on a worker thread, again only when pieces, land or settings change.
- **Monsters don't spawn inside it**: the game's own test of a spawn point, raids' included; fixed spawn points too; nests as a setting, off.
- **Any size**: shapes are worked out from the records the game keeps of every piece it has seen this session, and each zone's shape is saved on that zone's controller, by the game that owns it, where its records are whole; a game that has seen only part of a large settlement uses the saved shape for the rest.
- **The Hoarstone**, next to the Ward in the hammer's menu: a real Ward, marked and switched off. With this mod it's no ward: its own name, tint, hover text and E (shows its settlement's outline), and taking it down gives its cost back (a Troll, an Abomination and a Stone Golem trophy, Ectoplasm ×10, Root ×10, Ymir Flesh ×3, miikeskii's pick). To friends without the mod, a switched-off Ward.
- **Where**: only settlements with a Hoarstone (the default, miikeskii's call), or every settlement.
- **Outlines** on the ground (the Ward's own circle dashes) and on both maps: Left Alt + F3 for every settlement, E on a Hoarstone for its own, and while pointing at or placing one.
