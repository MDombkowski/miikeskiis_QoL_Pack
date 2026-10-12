# Z-Fighter

Made with AI. Floors laid into each other, beams crossing, stone walls overlapping their neighbours: where two pieces share a surface, the game can't tell which one is in front, and their textures flicker and stripe as you move. Z-Fighter draws one of the two a millimetre bigger on your screen, so the flicker stops.

## What it does

- **Overlapping pieces stop flickering.** Where two building pieces are laid into each other, one of them is drawn about a millimetre bigger on every side, so its surface sits just over the other's. A third piece laid into both is drawn a step bigger again, and so on.
- **You choose which one shows on top**: the smaller piece (a 1x1 floor laid into a 2x2 shows), the bigger one, by kind (beams and poles over roofs, roofs over walls, walls over floors), or no preference.
- **Only where surfaces meet.** A piece that only touches its neighbours, or stands in another without sharing a surface with it (a wall standing in a floor), is drawn exactly as the game draws it.
- **Roofs too.** Roofs, stairs and the slanted beams and walls are drawn bigger without tilting, and lifted, so their sloped surfaces come apart as well.
- **Nothing moves.** Only how the piece is drawn on your screen changes: where it stands, what you bump into, where the next piece snaps, and what is saved in the world all stay as they were.
- **Rugs are left alone** by default: the game, and Grounded, already lay them at heights of their own.

## Good to know

- **Only you need it.** It's client-side: neither the server nor the other players need it, and a friend without it sees their own game as before.
- **Changes work at once.** Change a setting, or switch the mod off, and every piece is drawn again within a moment; no restart. Switched off, every piece is drawn exactly as the game draws it.
- **A few pieces can't be helped**, where the game draws a part on the same object it uses for bumping into it (some doors and gates, chests, pots and banners, and a few 67° pieces), and turrets, which aim from inside their drawing: those pieces stay as the game draws them, and the pieces laid into them are drawn off them instead.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-ZFighter-<version>.zip`. Keep the author `drumcowski` and the name `ZFighter` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `ZFighter.dll` into a folder named `drumcowski-ZFighter` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.zfighter.cfg`, which appears after the first start; in the bundle, in the section "Z-Fighter" of its own file.

"Listed pieces" and "Pieces left alone" have a **Pick** button beside their text box there, which opens a window of everything the list can hold, each with its picture: a click puts a thing on the list or takes it off. The text box stays for typing.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off: every piece is drawn as the game draws it, at once. |
| Which one shows on top | SmallerPiece | SmallerPiece, BiggerPiece, ByKind (beams and poles over roofs, roofs over walls, walls over floors) or NoPreference. Pieces of nearly one size are only kept apart, never ordered by size. |
| Nudge (mm) | 1 | How far apart two overlapping surfaces are drawn, 0.5 to 5 millimetres. Bigger stops the flicker farther away. |
| Which pieces | EveryPiece | Or OnlyListedPieces: only the pieces on "Listed pieces". |
| Listed pieces | none | Prefab names, separated by commas (wood_floor, wood_floor_1x1, wood_beam …). |
| Pieces left alone | the rugs | Prefab names always drawn as the game draws them. |
| Pieces built by others | FixedToo | Or OnlyMine: others' pieces drawn as the game draws them, and yours drawn off theirs. |
| Log what it does | off | Writes to the log how many pieces overlap and how many are drawn nudged. |

## Building from source

`dotnet build -c Release` in this folder, with the .NET SDK and the .NET Framework 4.7.2 Developer Pack. The project compiles against the game's and the mod profile's DLLs where they are installed; to use other folders: `dotnet build -c Release -p:ValheimManaged="<valheim_Data\Managed>" -p:ModProfile="<profile folder>"`. `tools\package.ps1` builds it and writes the zip to `dist\`.
