# Changelog

## 0.2.0 (2026-10-09)

- **Tames heal much faster in pens** (card A84, miikeskii's feedback of 2026-10-09): a hurt tame inside a pen gets back 10% of its full health a minute (a setting), hungry or not, on top of the game's own healing, whatever "Tames in pens" says. Only fences, walls, gates, stakes, poles and beams make a pen. New section **Pens: healing**.
- **Feed a tame from your hand** (card A84): look at it (or its saddle) and press the hotbar key of a food it eats (a Cloudberry for a Lox): it eats one, and a meal from the hand heals 20% of its health (a setting). Only when it's hungry, out of the box; a tame that won't eat keeps your food back and says why (or, by a setting, you eat it yourself). Wild creatures are left alone, so eating while you look at one works as before, unless **Also wild creatures being tamed** is on. A tame a friend's game looks after is fed by that game, if it runs House Trained. New section **Feeding from the hand**.

## 0.1.1 (2026-10-06)

- **miikeskii's own settings are the defaults now** (card A63): all three parts on out of the box (tames spare buildings, friendly fire, tames in pens); tames spare buildings for every tame ("Which tames" EveryTame, was TheList) and spare crops and saplings too; wide melee swings deal friendly fire too; the nudge off a creature 5 m/s (was 1). The testing logs stay off.
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.
- **Beside Skarif's ValheimPerformanceOverhaul** (card A64): the "no attack across a wall" backstop is a prefix on `Humanoid.StartAttack` (the one call `MonsterAI.DoAttack` makes to attack) instead of on `MonsterAI.DoAttack`, which that mod's conflict guard watches: a prefix there made it switch its AI throttling off for the whole session. The same attacks are held as before (a creature's on an enemy; never a player's, one at nobody, or a healer's on a friend). The README says what that mod's "Tamed Animal Idle Sleep" does to pens.

## 0.1.0 (2026-10-05)

- **First version**, as a mod of miikeskii's QoL Pack and on its own. Three parts, each with its own switch, all off out of the box.
- **Tames spare buildings:** tamed Lox (a list, or every tame) no longer damage what players built with their bite, stomp or running trample; ships and carts too, the world's own buildings and crops by their own switches, a share of the damage if wanted. Spared both where the hit is sent and where it lands, so a building is spared if either computer runs it.
- **Friendly fire:** wild creatures' area attacks, their shockwaves, clouds, fire patches and novas, and exploding projectiles' blasts also hurt the creatures around them (wide melee swings and projectiles stopping on friends by their own switches, off). Never from a tame or a staff's summon, never on a player, your tames safe unless switched; own kind, bosses, a list, a share, fighting back, and "only when every player nearby runs the pack".
- **Tames in pens**, his four points: kept in (no standing on creatures, no climbing onto building pieces by an edge, births, eggs, grow-ups and hatchlings on the pen's side of the walls), heads kept out of the walls, no fighting enemies through a wall (not seen, not attacked, let go of when out of reach), and wandering only where they can walk, off the walls, with far-away spawn points ignored.
