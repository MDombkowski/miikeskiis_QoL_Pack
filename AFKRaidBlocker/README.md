# AFK Raid Blocker

Made with AI. Step away from Valheim without coming back to a base flattened by trolls. While you're away from the keyboard, the game's raids don't start on you; while anyone nearby is still playing, raids come as usual.

## What it does

- **You're away** after 5 minutes without moving, pressing a key, touching the mouse or a gamepad (a setting, 1 to 60 minutes, or never).
- **Sit or lie down to go away at once.** The Sit key, /sit, /rest or /relax: you're away as soon as you sit or lie down, and back when you stand up (or, lying down, when a hit knocks you up). A chair or a bed can count too (settings, off at first).
- **While you're away, raids don't start on you.** The game picks where a raid starts from the players at a base; you're left out of that pick until you're back, while nobody near you is playing. A short line on screen says so as you go and as you come back.
- **Only when everyone near you is away.** While a friend within 96 m (a raid's reach; a setting) is still playing, or doesn't have this mod, raids can start on you as usual, and reach you both. A raid already under way goes on.
- **Back at once**: away by the minutes, at your first movement, key press or real mouse movement (a key left held down counts once, as it's pressed); sitting or lying down, when you stand up, whatever you press meanwhile. Dying doesn't bring you back: the game respawns you by itself.
- **A mark for your friends** (a setting, off at first): while you're away, friends who have this mod see "(AFK)" beside your name over your head.

## Good to know

- **Only you need it.** It's client-side: neither the server nor the other players need it, on a dedicated server, in a world you host, or in single player. A friend without it plays as before and gets raids as before.
- **Nothing else changes.** Your comfort, your rested bonus and everything else about your base work as usual while you're away; only the raid pick leaves you out.
- **Changes work at once.** Change a setting, and it counts from the next moment; no restart.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-AFKRaidBlocker-<version>.zip`. Keep the author `drumcowski` and the name `AFKRaidBlocker` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `AFKRaidBlocker.dll` into a folder named `drumcowski-AFKRaidBlocker` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.afkraidblocker.cfg`, which appears after the first start; in the bundle, in the section "AFK Raid Blocker" of its own file.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off: raids come to you as usual at once, and your mark goes. |
| Away after this many minutes | 5 | 1 to 60, or 0 for never (then only sitting or lying down makes you away). |
| What counts as being there | Movement, keys and the mouse | Gamepads too. Or "Movement only": you can then look around or open the map and still go away. Keys and the mouse count only while the game's window has the focus. |
| Sitting or lying down by an emote counts | on | Your way of saying you've stepped away. |
| Which emotes count | sit, rest, relax | Sitting, lying on your back, lying on your side. Any emote's chat name can be added, separated by commas; it counts while you hold its pose. |
| Sitting in a chair counts | off | Chairs, benches (a ship's too) and thrones. |
| Lying in a bed counts | off | |
| Sitting or lying makes you away | At once | Or "After the same minutes" there. |
| What's held off while you're away | Raids that need a base | Or "Every random event", the wolves of the mountains and plains included. |
| Someone still playing within (m) lets raids come | 96 | A raid's reach; up to 128. It counts the players your game has loaded around you, and anyone farther who shares their position on the map. 0: raids are held off whoever is near. |
| A message going away and coming back | In the middle of the screen | Or top left, or none. |
| Others see you're away | off | A mark beside your name for friends who have this mod. |
| Show other players' away mark | on | |
| The away mark's text | AFK | Shown in brackets, in grey. |
| Write to the log going away and coming back | off | |

## If something goes wrong

Turn on "Write to the log going away and coming back", and look in `BepInEx\LogOutput.log` for lines from "AFK Raid Blocker": each says when you went away, why, and when you came back. If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and raids come as they do without it. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\AFKRaidBlocker.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-AFKRaidBlocker-<version>.zip`. Until AFK Raid Blocker is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too; `src\Logic\` holds the away rules as plain code), its own plugin in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx and Jötunn.
