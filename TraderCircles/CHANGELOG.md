# Changelog

## 0.1.1 (2026-10-06)

- **miikeskii's own settings are the defaults now** (card A63): circles for Haldor, Hildir and the Bog Witch are all on out of the box.
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.

## 0.1.0 (2026-09-25)

- **First version**, as a mod of drumcowski's QoL Mods and on its own.
- See-through circles on the big map around every place Haldor, Hildir and the Bog Witch could still be, each place somewhere inside its circle and never at a set point, the same every session and for every player in the world, cut to the ground where that trader can appear, and gone once the trader is found by anyone.
- Five sizes, 600 to 2000 m across, each with circles of its own, so changing the size doesn't point at the place.
- One switch per trader, all off at first; the size (1200 m across), how solid the fill is, a thin outline, each trader's colour and the small map are settings that work at once.
- Asks the unmodded server as a vegvisir does and catches the answers itself: no pins, messages or head turns, and never a place in the log. It asks only while that catch is in place and the game still hands it the answers, and the catch stays in place even after an error switches the mod off.
