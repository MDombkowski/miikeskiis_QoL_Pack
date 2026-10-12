# Changelog

## 0.3.0 (2026-10-11)

- **Lists picked from pictures**: "Stands that count" and "Armor stands that count" have a Pick button in the settings window (F1): it opens a window of everything the list can hold, each with its picture, where a click puts a thing on the list or takes it off. The typed box stays beside it, and the list is written as the mod reads it, with any names the game doesn't know kept. The window is set in the new section "Lists (every mod)" (its size, its pictures', whether things not found yet show).

## 0.2.2 (2026-10-11)

- Settings name no other mod unless this mod works with it, stands aside for it or makes room for it: "Which items count" no longer names another mod as its example; what it does is unchanged.

## 0.2.1 (2026-10-06)

- **Off out of the box, and miikeskii's own look when switched on**: the mod's Enabled switch is off in a new settings file, so nobody's icons change at an update; switched on, "Show the marks" is on already, the marks are 8 pixels (was 14) and sit at the icon's bottom right (was the middle of its left edge). An older file whose Enabled was still on (the old default) is switched off once, like the other moved defaults.
- Add to Cart's "Item stand marks" (AsItemStandIcons) follows: none while Item Stand Icons is off.
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.

## 0.2.0 (2026-09-30)

miikeskii's three additions after his test of 0.1.0 ("Works great!"):

- **A mark for the armor stand:** a small mannequin (a head, a tunic, a pole on a base), drawn by the mod, on every item the armor stand takes, at the middle of the icon's right edge, across from the arrows: helmets, chest pieces, legs, capes, the Megingjord, and the weapons, bows, tools, torches and shields it holds (308 of the game's 894 items; 216 of them fit an item stand too, and get both marks). The armor stand's own rule, asked of the game: a slot takes its kinds of item (by an item's attach type where it has one, so a few meads fit and fist weapons don't), and the stand hangs only what has a model to show (chest pieces and legs always). Its switch, its place and its nudges are settings; "Armor stands that count" names the stands asked.
- **Only while aiming at a stand** (a setting, off by default): the marks on your inventory and hotbar show only while you aim at an item stand or an armor stand, by the game's own test for showing its "Attach item" or "Take" line under the crosshair. The aim holds while your inventory is open and you stand still. "Aiming holds back" can take in every place; "Aiming shows" picks that stand's kind of mark (the default), only what that very stand takes, or every mark.
- **RightMiddle** is a new place for either mark.
- Add to Cart 0.4.6 draws the mannequin too, and has an item stand filter of its own.

## 0.1.0 (2026-09-30)

- **First release** (miikeskii's idea of 2026-09-29): a small mark on every item that fits an item stand, saying which: an up arrow for the wall stand, a right arrow for the flat stand, both arrows for both. Worked out from the game's own rule, asked of the two stands, so a mod that lets more items onto stands shows too.
- **Wherever item icons are drawn,** each place with its own switch: your inventory (with EquipmentAndQuickSlots' slots), chests, the hotbar (and EquipmentAndQuickSlots' quick-slot bar), the crafting menu's list, the chosen recipe and its ingredients, a trader's list, and (off by default) the build menu's costs.
- **The arrows are drawn by the mod** (the game's fonts have none), white with a dark outline, in the middle of the icon's left edge, the one spot free on every icon; the look, the colour, the size, the place, the shape of "both", and letters or text instead, are settings.
- **Off out of the box:** switch on "Show the marks". "Only while holding a key" shows them only while you hold it.
- Its rule, arrows and mark are a part of the pack (`QoLMods\parts\ItemStands`), which Add to Cart 0.4.3 uses to draw the same marks on its thumbnails.
