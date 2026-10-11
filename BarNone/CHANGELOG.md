# Changelog

## 0.2.2 (2026-10-08)

- **Reaching through pieces works again beside JereKuusela's ESP** (card A50, miikeskii's report of 2026-10-08: he could no longer fuel a fire through a Cage Floor 2x2). ESP gives every building piece a hover text of its own, and Bar None took any piece with a hover text for one you can use, like a gate, which it never reaches through. Now only a piece you can actually use (open, sit on, put something on) stops the reach; gates, doors and shutters still open as before.

## 0.2.1 (2026-10-06)

- **miikeskii's own settings are the defaults now** (card A63): reaching through pieces and reaching creatures behind them, smoke passing through, projectiles passing through and enemies' shots too, and beehives and birds' nests, windmills, and the Wisp Fountain and Eternal Pyre seeing the sky through listed pieces, all on out of the box. Items falling through stay off. The smoke and projectile parts still stand aside while JereKuusela's Smoke_Collision or Projectile_Collision is installed.
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.

## 0.2.0 (2026-09-30)

- **Items fall through pieces** (miikeskii's ask, after 0.1.0 passed his test): loose items (a creature's drops, the eggs a hen lays, what you drop yourself, what a smelter or a kiln puts out) fall through the listed pieces instead of landing on them. Its own list, the cage floors to start (the Straw rug already lets items through); "Items: except these items" keeps the ones it names on top (your animals' food: list all of it); "Items: write each touch to the log" names items and pieces for both lists. Items already lying on a listed piece fall too, within seconds of their area loading. Food set out as a piece stays. Off out of the box.
- Twenty-seven settings.

## 0.1.0 (2026-09-30)

- **First version**, as a mod of miikeskii's QoL Pack and on its own (card A50). Written from scratch to replace JereKuusela's Smoke_Collision and Projectile_Collision; each of its parts stands aside while the matching one of those is installed.
- **Reach** through see-through pieces: use what's behind a cage wall, a crystal wall or a fence within your usual reach (fuel a fire, open a chest, pick a crop, pick up an item). Gates and anything else you can use are never reached through. Off out of the box.
- **Smoke** passes through listed pieces, and a fire right under one keeps burning. Off out of the box.
- **Projectiles** fly through listed pieces, for the shooters switched on (players' by default) and up to a size. Off out of the box.
- **Sky:** crops and saplings see the sky through listed pieces, so they grow under a cage floor. **On for everyone** (miikeskii's call), since a crop that can't grow is destroyed when it ripens by whichever game has it then. Beehives, windmills and wisp fountains each have a switch of their own, off.
- Twenty-three settings; each part has its own list of pieces.
