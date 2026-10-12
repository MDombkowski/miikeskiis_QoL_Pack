# Changelog

## 0.2.1 (2026-10-11)

- **Messages fit the screen**: a message line too wide for your screen is broken at spaces into lines that fit, measured by the game's own text, as every mod of miikeskii's QoL Pack now does.

## 0.2.0 (2026-10-10)

- **A power you haven't unlocked is a padlock** (miikeskii's ask): no boss icon and no name in the wheel, only a padlock in its place; the middle of the wheel says "Locked" and why, and picking it says it's locked without naming it. A new setting, "How a locked power looks", keeps the old look (the icon dimmed, with a small padlock) as its other choice.
- **A power unlocks when its boss's trophy is on its sacrificial stone** in this world, whoever placed it: "What counts as earned" has a new choice, TrophyAtStone, and it's the default. The game keeps no record of a placed trophy away from the stones, so PowerWheel notes each stone it sees holding its trophy (whenever you're near the start temple) and remembers it on your character, for each world; a power you took at its stone counts too, and so does the power you have. A settings file still at the old default, TakenAtStone, moves to it once.

## 0.1.2 (2026-10-10)

- **Fixed: PowerWheel did nothing until the game's own wheel (G) had been opened once since the game started** (miikeskii's test of 0.1.0, found by 0.1.1's key log). The game sets up its wheel's data the first time its own wheel opens (and probably when its Settings window does); until then PowerWheel left every press to the game, so F used the power at once and a key of its own did nothing. Now, when that data isn't set up yet, PowerWheel sets it up the way the game does (the game's own call, with the game's own data), and the wheel opens on the first hold. A line in the log says so, once.
- Everything of 0.1.1 stays: the setting "Write what it sees of the key to the log" (off out of the box) and the warning if its patch on Player.Update stops running.

## 0.1.1 (2026-10-10)

- **A setting to find out why 0.1.0 did nothing for miikeskii:** with F set as the key, the power was used as he pressed it and no wheel came; with a key of his own, nothing happened at all. Nothing he had in hand explains it, and reading the game's and the mod's code found no cause, so this version says in the log what happens in his game.
- **A new setting, "Write what it sees of the key to the log"** (off out of the box): a line, with its frame number, for each press of the key (what the keyboard saw and what the mod read), each tap, hold and drop, and each opening of the wheel, with why the mod left a press to the game (each failed check by name) or why the wheel didn't open, and what a pick did; its settings again whenever they change. An error in the log switches only the log off.
- **A warning in the log if its patch on the game's Player.Update stops running, or never runs,** for 5 seconds with your character in the world, listing the Harmony patches on that method; and a line when it runs again.
- Otherwise the same as 0.1.0.

## 0.1.0 (2026-10-09)

- **First version**, as a mod of miikeskii's QoL Pack and on its own (PowerWheel is miikeskii's name for it).
- **A tap of the forsaken power key (F) uses the power,** as in the game, at the release rather than the press.
- **Holding it (0.33 s by default) opens the game's own radial wheel** with an icon for every forsaken power, in the order the game is played; the name of the power under the pointer shows in the wheel's centre, with "Ready" or the time left. The power you have is marked, and the pointer starts on it.
- **Letting go picks the power under the pointer,** through the game's own call, as at its stone; a left click picks too; a right click, Escape or letting go in the centre changes nothing, and letting go on the power you have (or without moving) never uses it, whatever the settings. The wait before a power can be used again is never touched.
- **Only earned powers can be picked:** those not taken at their stone show dimmed with a padlock, out of the box.
- It follows the game's own key and its rebinding; a key of its own is a setting. With a gamepad in the classic layout, D-pad down does the same and the right stick aims: let go of D-pad down while the stick still points at the power, or press RT. With a build tool, hoe or cultivator in hand, the key works as in the game.
- Eighteen settings, each taking effect at once.
- On out of the box (his ask).
