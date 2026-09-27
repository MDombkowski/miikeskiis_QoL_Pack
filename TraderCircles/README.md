# Trader Circles

Big, see-through coloured circles on the big map around every place a trader could still be: yellow for Haldor, pink for Hildir, green for the Bog Witch. Each circle holds one such place somewhere inside it, never at its centre, so the search is still yours to make. Once a trader is found, by you or anyone in the world, that trader's circles are gone.

## What it does

- **Circles, not pins.** Each trader has up to ten places the game has picked for it; it turns up at whichever one a player comes near first. Every one of those places gets a circle, 1200 m across by default.
- **The place is somewhere inside, never at a set point.** Each circle is moved off its place by a random amount in a random direction. That randomness comes from the world itself, so the circles are the same every time you play, and the same for every friend in the world who has the mod: laying one session's circles over another's tells nothing more.
- **Five sizes, each with circles of its own.** 600, 900, 1200, 1500 or 2000 m across. Changing the size draws new circles, not the same ones grown or shrunk around the place, which would point at it. Overlapping the circles of different sizes does narrow the search: all five together, to about 330 m across; 1200 and 1500 alone, to about 870 m. That's why there are only five.
- **Only ground the trader could stand on.** The part of a circle where that trader can't appear isn't drawn: too near the world's centre, too far out, or where its kind of ground can't exist at that distance. So a circle near those limits looks cut off along them. The circle itself is never moved to fit, so the place is equally likely anywhere in the part you see.
- **Gone once found.** The game shows a trader's icon on everyone's map as soon as anyone comes within a few hundred metres of its camp; from that moment its circles are gone.
- **Your choice of traders.** Each trader has its own switch, and all are off at first.

## Good to know

- **Only you need it.** It's client-side: the server doesn't need it, and your friends see circles only if they install it too.
- **How it knows the places.** Only the server knows where the traders' places are. The first time a trader is switched on in a world, the mod asks the server, with the same question a vegvisir asks. The answers are caught by the mod and kept in memory only: they never become pins, messages or head turns, and they are never written to the log or anywhere else. The mod asks only after checking, each time, that its catch for the answers is in place and that the game still hands the answers to it; if a game update ever broke that, the mod asks nothing, rather than risk the answers landing on your map as pins. Even if an error switches the mod off, the catch stays in place until you leave the game, so an answer still on its way can't become a pin either.
- **The server notes the question.** An unmodded server writes one line to its own log per question: which trader was asked about and how many places it sent back, not where they are.
- **Some game updates may move the circles.** A game update can make the server pick new places for traders nobody has found yet; the circles then follow the new places.
- **Changes work at once.** Change a setting in game and the map shows it; no restart.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-TraderCircles-<version>.zip`. Keep the author `drumcowski` and the name `TraderCircles` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `TraderCircles.dll` into a folder named `drumcowski-TraderCircles` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.tradercircles.cfg`, in the sections General and Look, which appears after the first start; in the bundle, in the section "Trader Circles" of its own file.

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off hides every circle at once; on shows them again. |
| Circles for Haldor | off | Circles around every place Haldor could still be. |
| Circles for Hildir | off | The same for Hildir. |
| Circles for the Bog Witch | off | The same for the Bog Witch. |
| Circle size (metres across) | 1200 | 600, 900, 1200, 1500 or 2000; each size has circles of its own. |
| How solid the fill is (%) | 25 | 5 to 90; low is more see-through. |
| Thin outline | off | A thin outline around each circle, in its own colour. |
| Haldor's colour, Hildir's colour, the Bog Witch's colour | yellow, pink, green | The circles' colours. |
| Also on the small map | off | Draws the circles on the small map in the corner too. At its usual zoom it shows less ground than one circle, so near a circle it is just tinted. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Trader Circles". It says when it asks the server about a trader, and once the answer is in, how many circles it drew and how many were cut, and whether its self-check passed (each place lies in the drawn part of its circle). It never writes where a place is. If the game has changed so that the mod can't find what it needs, it switches itself off and says why; the same goes for an error while playing.

- **No circles, and "hasn't answered"?** An unmodded server stays silent only when its world has no place left for that trader; a server mod may also stop the question.
- **A circle cut off along a curve?** That's the edge of the ground where its trader can appear: its inner or outer distance limit (Map Rings draws the same limits as rings), or the edge of the far south and north regions. The log says how many of a trader's circles are cut.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\TraderCircles.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-TraderCircles-<version>.zip`. Until Trader Circles is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn.
