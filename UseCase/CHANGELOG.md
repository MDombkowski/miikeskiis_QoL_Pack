# Changelog

## 0.2.0 (2026-10-11)

- **Lists picked from pictures**: "Never list these" and "Never show a list for these resources" have a Pick button in the settings window (F1): it opens a window of everything the list can hold, each with its picture, where a click puts a thing on the list or takes it off, pieces, items and creatures each a tab for the first. The typed box stays beside it, and the list is written as the mod reads it, with any names the game doesn't know kept. The window is set in the new section "Lists (every mod)" (its size, its pictures', whether things not found yet show).

## 0.1.4 (2026-10-11)

- Settings name no other mod unless this mod works with it, stands aside for it or makes room for it: "The key" and "In the inventory and chests" no longer name other mods; what they do is unchanged.

## 0.1.3 (2026-10-09)

- The catalogue it carries changed (quick picks and food's main stat); nothing changes here.

## 0.1.2 (2026-10-06)

- **miikeskii's own settings are the defaults now**: "Show the panel" is on out of the box, and so is the Selling section (what Haldor pays).
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.

## 0.1.1 (2026-09-30)

- **The catalogue part it carries changed** (with StoreStash 0.4.0's chest setups): it gains setups (`ItemPicks`), which Use Case doesn't use; nothing else changes in it.

## 0.1.0 (2026-09-30)

- **First release** (miikeskii's idea of 2026-09-29): beside the game's item tooltip, a panel lists everything the resource is used in, with its "· X": Building, Crafting, Upgrading and Refueling (the most a piece holds, as the game's hover says), then the game's other uses (processing, boss altars, locked doors, taming and saddles, fishing bait, ammo, fireworks, buying for coins, Hildir's chests, the guardian stones), each a switch; selling and the ballista's trophy targets off out of the box.
- **Read from the game once per world,** so other mods' recipes and pieces count; switched-off recipes left out, seasonal ones marked with their season, upgrade-only recipes only under Upgrading, no upgrades for items that can't be upgraded, self-uses (the serving tray) left out by a setting. The altars and locked doors inside the world's locations from a list read from the game's own (1.0.16), and learned as they load near you.
- **Spoilers hidden** out of the box ("??? · 6", last in their section), by the game's own known recipes and pieces; altars and doors until seen; everything shown when the game shows everything.
- Long lists in columns, sections in the pack's order (progression, as Add to Cart lists them), two ignore lists, keys to show, pin and ignore (none out of the box). Its settings under one heading in F1, each part behind a title row.
- **Off out of the box** ("Show the panel"): nothing changes for a player until they switch it on.
