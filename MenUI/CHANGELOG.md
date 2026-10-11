# Changelog

## 0.1.0 (2026-10-10)

- **First version**, as a mod of miikeskii's QoL Pack and on its own (card A94, his name).
- **The main menu fits the window**: when other mods' buttons push the last ones (Quit) below it, the buttons shrink just enough, their top staying put, down to 70% of their size; then the list also moves up, no higher than 60% of the window. A menu that fits is left exactly as the game made it.
- **Any mod's buttons**: it measures whatever the menu shows, every frame, so buttons added later, a menu just opened and a window changing size are all fitted at once. Only a menu that really reaches past the window is fitted, and it is put back exactly when it fits again or the mod is switched off (its size and its place tracked apart, the place as the anchored position, which a change of window or GUI size leaves alone).
- **The in-game menu (Esc)** is fitted the same way, shrunk around its middle (a setting).
- **The up and down arrow keys reach every button** of the main menu, other mods' included, in the order they are shown (a setting; a gamepad moves its own way, left alone).
- Settings: how to fit (shrink then move up, move up then shrink, shrink only), the smallest size, the space below the last button, the highest the main menu may reach.
- No patches: it looks at the menus after each frame and puts them back at once when switched off.
- One line in the log the first time a menu is fitted, describing the menu as found (for his first test).
