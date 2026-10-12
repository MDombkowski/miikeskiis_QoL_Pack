# Nap Time

Made with AI. The game lets everyone sleep through the night to the morning; Nap Time lets everyone sleep through the day to the night. In your own world, or on a server running Nap Time (Server), you can lie down in your bed in the daytime, and once everyone is in bed the clock runs on to sunset and you all wake at dusk.

## What it does

- **Lie down by day.** The game's bed refuses the morning. Your own bed works from sunrise (or whenever the nap hours start), after the game's usual wait since you last woke. Its hover text says "Nap until dusk".
- **The nap.** Once every player is in bed, the clock runs on to sunset, as fast as the game's night skip, and everyone wakes at dusk, rested. "Good evening", not "Good morning".
- **The afternoon.** From noon the game already lets you sleep, past the night to the next morning. Out of the box each player picks: E sleeps to the morning as the game does and Left Alt + E naps until dusk; the nap happens only if everyone in bed chose it, otherwise everyone sleeps to the morning. The bed's hover text shows both keys. You can swap what each key does in the settings.
- **At night** your bed is the game's own: a sleep to the morning.
- **Your own world** (playing alone, or the game's Start Server): naps work with nothing else installed, by the "Your own world" settings below. Friends who join with the pack can nap with you.
- **On someone else's server**, naps work where it runs Nap Time (Server), by that server's hours; on a server without it, your bed is the game's own.
- **The line under Enabled** in the settings window says which of these applies where you are.

## Good to know

- **Friends without it** can't lie down in the morning, so a morning nap waits for them as the night skip waits for anyone not in bed. In the afternoon they can sleep, but only to the morning, so their bed counts as not choosing the nap.
- **With miikeskii's Server-Side QoL Pack (or one of its mods on its own) in your own game too**, it decides the naps in your world instead of the "Your own world" settings: by its Nap Time (Server), or none without one.
- **Installed on its own beside World Settings on its own**, only one of the two answers friends who join your world: install miikeskii's QoL Pack instead, which holds both. The line under Enabled says when this is so.
- **On a server that changes the length of the day** (World Settings (Server)), keep miikeskii's QoL Pack's World Settings on, so your sky and the server's agree on when the day is.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library
- On someone else's server: Nap Time (Server), inside miikeskii's Server-Side QoL Pack (or on its own). In your own world, nothing more.

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-NapTime-<version>.zip`. Keep the author `drumcowski` and the name `NapTime` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `NapTime.dll` into a folder named `drumcowski-NapTime` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.naptime.cfg`, which appears after the first start; in the bundle, in the section "Nap Time" of its own file. On someone else's server, when a nap may start and whether each player picks in the afternoon are that server's settings; in your own world, the last four below.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off: your bed is the game's own, at once. The line under it says what applies where you are. |
| E in the afternoon | SleepToMorning | What lying down with E does in the afternoon: SleepToMorning (the game's own) or NapUntilDusk. |
| Second key at your bed | Left Alt + E | Lying down with this key does what the next setting says. None: no second key. |
| Second key in the afternoon | NapUntilDusk | What lying down with the second key does in the afternoon: NapUntilDusk or SleepToMorning. |
| Messages | on | A short line when you lie down for a nap, and "Good evening" when one ends. |
| Naps in your own world | on | Naps in a world your own game runs (alone, or the game's Start Server). Off: your beds there are the game's own. |
| Your own world: naps from | 6 | From when in the day a nap may start (6 sunrise, 12 noon, 18 sunset). |
| Your own world: naps until | 18 | Before when a nap may start. A window that ends where or before it starts allows none. |
| Your own world: in the afternoon, a sleep goes to | EachPlayerPicks | EachPlayerPicks (by each player's key), Sunset (every afternoon sleep naps) or NextMorning (the game's own). |

## Building from source

`dotnet build -c Release` in this folder builds its own release; `tools\package.ps1` packs it. The bundle compiles `src\` from here. It carries copies of the QoL Pack's kit, its naps (`parts\Naps`), what it does in a world its own game runs (`parts\Serving`) and its chest service (`parts\Chests`, whose link to the server pack it uses), kept identical by `..\QoLMods\tools\sync-kit.ps1`.

## Credits

`CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: its code was written by Claude agents (Anthropic), directed by miikeskii.
