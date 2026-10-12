# Changelog

## 0.4.0 (2026-10-11)

- **Lists picked from pictures**: The five lists of tames and creatures have a Pick button in the settings window (F1): it opens a window of everything the list can hold, each with its picture, where a click puts a thing on the list or takes it off, each creature pictured by its trophy. The typed box stays beside it, and the list is written as the mod reads it, with any names the game doesn't know kept. The window is set in the new section "Lists (every mod)" (its size, its pictures', whether things not found yet show).

## 0.3.0 (2026-10-11)

- **Every tame follows and stays, as a wolf does** (miikeskii's idea; on out of the box): E on a boar, a Lox, a hen, an asksvin or a moose makes it follow you, and E again makes it stay. It's the game's own command, kept with the creature, so a friend without this mod sees it follow too. Its text says [E] Follow or [E] Stay. On a saddle E still rides (point at the creature itself, or set a key for the saddle). The staffs' summons are left as they are unless you switch them in. New section **Following**.
- **Tames keep to a Hoarstone's ground** (miikeskii's idea; on out of the box, and only while the Hoarstone mod is on, as its section's title says): a tame on the ground of a settlement with a Hoarstone wanders only on it, and walks back in when it ends up outside. It may leave to fight an enemy and comes back afterwards, and it may leave to follow you; told to stay out there, it stays. **Return to base** (Left Alt + E, on a tame off the ground) sends it home. Tames **guard the ground**: a tame on it goes for an enemy anywhere on it, and a tame in a pen only while a door or gate of its pen is open. Each of these has its own setting. New section **With the Hoarstone**.
- **Tames in pens leaves a follower alone** while it follows (a setting, on), and keeps the spot of a tame told to stay as it keeps a wolf's.
- E makes a tame that follows you stay even with **Following** switched off. Tell your tames to stay before removing the mod: without it, E only pets a tame that isn't a wolf.

## 0.2.1 (2026-10-11)

- **A tame is never pushed for touching a player**: "No standing on creatures" took a tame's ground away whenever the game counted any creature or player at its feet as ground, and nudged it at 5 m/s every second, anywhere, a friend feeding it or getting on included. Now it acts only when a tame really stands on another creature (on its upper half, never on a player), and nudges it at most three times in ten seconds.
- **Write tames' deaths and removals to the log** (new, on): a line in the game's log whenever one of the world's tames dies on your game (its kind, name, place and what hit it last), or any game removes a tame from the world (and whose game it was). It only writes.
- **Feeding from the hand, explained**: a tame hungry for a long while may heal to full when it eats. That is the game's own healing catching up (it stops while a tame is hungry, and all it missed comes back at its next heal once it eats, from the ground or the hand), on top of the meal's 20%. The settings' descriptions say so now.
- **Messages fit the screen**: a message line too wide for your screen is broken at spaces into lines that fit, measured by the game's own text, as every mod of miikeskii's QoL Pack now does.

## 0.2.0 (2026-10-09)

- **Tames heal much faster in pens** (miikeskii's feedback of 2026-10-09): a hurt tame inside a pen gets back 10% of its full health a minute (a setting), hungry or not, on top of the game's own healing, whatever "Tames in pens" says. Only fences, walls, gates, stakes, poles and beams make a pen. New section **Pens: healing**.
- **Feed a tame from your hand**: look at it (or its saddle) and press the hotbar key of a food it eats (a Cloudberry for a Lox): it eats one, and a meal from the hand heals 20% of its health (a setting). Only when it's hungry, out of the box; a tame that won't eat keeps your food back and says why (or, by a setting, you eat it yourself). Wild creatures are left alone, so eating while you look at one works as before, unless **Also wild creatures being tamed** is on. A tame a friend's game looks after is fed by that game, if it runs House Trained. New section **Feeding from the hand**.

## 0.1.1 (2026-10-06)

- **miikeskii's own settings are the defaults now**: all three parts on out of the box (tames spare buildings, friendly fire, tames in pens); tames spare buildings for every tame ("Which tames" EveryTame, was TheList) and spare crops and saplings too; wide melee swings deal friendly fire too; the nudge off a creature 5 m/s (was 1). The testing logs stay off.
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.
- **Beside Skarif's ValheimPerformanceOverhaul**: the "no attack across a wall" backstop is a prefix on `Humanoid.StartAttack` (the one call `MonsterAI.DoAttack` makes to attack) instead of on `MonsterAI.DoAttack`, which that mod's conflict guard watches: a prefix there made it switch its AI throttling off for the whole session. The same attacks are held as before (a creature's on an enemy; never a player's, one at nobody, or a healer's on a friend). The README says what that mod's "Tamed Animal Idle Sleep" does to pens.

## 0.1.0 (2026-10-05)

- **First version**, as a mod of miikeskii's QoL Pack and on its own. Three parts, each with its own switch, all off out of the box.
- **Tames spare buildings:** tamed Lox (a list, or every tame) no longer damage what players built with their bite, stomp or running trample; ships and carts too, the world's own buildings and crops by their own switches, a share of the damage if wanted. Spared both where the hit is sent and where it lands, so a building is spared if either computer runs it.
- **Friendly fire:** wild creatures' area attacks, their shockwaves, clouds, fire patches and novas, and exploding projectiles' blasts also hurt the creatures around them (wide melee swings and projectiles stopping on friends by their own switches, off). Never from a tame or a staff's summon, never on a player, your tames safe unless switched; own kind, bosses, a list, a share, fighting back, and "only when every player nearby runs the pack".
- **Tames in pens**, his four points: kept in (no standing on creatures, no climbing onto building pieces by an edge, births, eggs, grow-ups and hatchlings on the pen's side of the walls), heads kept out of the walls, no fighting enemies through a wall (not seen, not attacked, let go of when out of reach), and wandering only where they can walk, off the walls, with far-away spawn points ignored.
