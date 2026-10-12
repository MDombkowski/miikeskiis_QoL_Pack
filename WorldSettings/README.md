# World Settings

Made with AI. On a server running World Settings (Server), your game follows the server's day: how long a day and a night last, how long each weather lasts and how long the wind holds. Your sky, your bed and the server's sleep then agree. In your own world, you set them yourself.

## What it does

- **Follows the server.** Every player's game works out the time of day for itself, from the server's clock and its own length of day, which the game never sends. When the server's World Settings (Server) sets a longer or shorter day or night, this tells your game, so your sun rises and sets when the server's does: you can go to bed when the others can, a sleep wakes you in the morning, and night creatures come out at the server's night.
- **Weather and wind** follow the server's too, when it changes how long they last.
- **The sky moves straight to the right time of day** when you join, or when a length changes, instead of sweeping round to it.
- **Your own world** (playing alone, or the game's Start Server): the "Your own world" settings below set the length of the day and the night, the weather and the wind, with nothing else installed. Each is 0, the game's own, until you set it. Friends who join with the pack follow it.
- **On a server without World Settings (Server)**, the game's own day (21 minutes of day, 9 of night) stays as it is.
- **The line under Enabled** in the settings window says which of these applies where you are.

## Good to know

- **Friends without it** keep the game's own day on such a server, out of step with yours and the server's: their bed may refuse while yours works.
- **The day number** is the world's age counted in the server's days, so it is smaller on a server with longer days.
- **It hears the server through miikeskii's Server-Side QoL Pack**, which says what the server runs to every player on miikeskii's QoL Pack. The server's world modifiers, saving and clock need nothing on your game.
- **With miikeskii's Server-Side QoL Pack (or one of its mods on its own) in your own game too**, it decides the day in your world instead of the "Your own world" settings: by its World Settings (Server), or the game's own without one.
- **Installed on its own beside Nap Time on its own**, only one of the two answers friends who join your world, and if it's Nap Time, your own world's day stays the game's own (friends couldn't follow it): install miikeskii's QoL Pack instead, which holds both. The line under Enabled says when this is so.
- **Switched off,** your game goes back to the game's own day at once.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library
- On someone else's server: World Settings (Server), inside miikeskii's Server-Side QoL Pack (or on its own). In your own world, nothing more.

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-WorldSettings-<version>.zip`. Keep the author `drumcowski` and the name `WorldSettings` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `WorldSettings.dll` into a folder named `drumcowski-WorldSettings` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.worldsettings.cfg`, which appears after the first start; in the bundle, in the section "World Settings" of its own file.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Follows a server that runs World Settings (Server), and sets your own world's day. Off: the game's own day, night, weather and wind, at once. The line under it says what applies where you are. |
| Your own world: length of the day | 0 | Minutes from sunrise to sunset in a world your own game runs. 0: the game's own (21 minutes). Up to 1440. |
| Your own world: length of the night | 0 | Minutes from sunset to sunrise. 0: the game's own (9 minutes). A sleep still skips to the next morning. Up to 1440. |
| Your own world: how long each weather lasts | 0 | Minutes each weather lasts. 0: the game's own (11.1 minutes). Up to 600. |
| Your own world: how long the wind holds | 0 | Minutes the wind's slowest change takes. 0: the game's own (16.7 minutes). Up to 600. |

On someone else's server, the lengths are that server's settings (World Settings (Server)).

## Building from source

`dotnet build -c Release` in this folder builds its own release; `tools\package.ps1` packs it. The bundle compiles `src\` from here. It carries copies of the QoL Pack's kit, its day cycle (`parts\DayCycle`), what it does in a world its own game runs (`parts\Serving`) and its chest service (`parts\Chests`, whose link to the server pack it uses), kept identical by `..\QoLMods\tools\sync-kit.ps1`.

## Credits

`CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: its code was written by Claude agents (Anthropic), directed by miikeskii.
