# Changelog

## 0.1.2 (2026-10-11)

- **Messages fit the screen**: a message line too wide for your screen is broken at spaces into lines that fit, measured by the game's own text, as every mod of miikeskii's QoL Pack now does.

## 0.1.1 (2026-10-06)

- **miikeskii's own setting is the default now**: "R picks Repair" is on out of the box.
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.

## 0.1.0 (2026-09-30)

- **First version**, as a mod of miikeskii's QoL Pack and on its own ("R-Repair").
- With the hammer in hand, **a tap of R picks its Repair**, **a tap again goes back to the piece you had**, turned as it was, and **holding R puts the hammer away**, as R does in the game (a hold is 0.33 s by default).
- With the build menu open, a tap of R picks Repair and closes the menu.
- It follows the game's "Hide/show weapon" key and its rebinding; a key of its own is a setting.
- Only build tools with a Repair; every other item, and every other use of R, work as in the game.
- **Off out of the box:** R puts the hammer away as in the game until "R picks Repair" is on.
- Fourteen settings, each taking effect at once.
