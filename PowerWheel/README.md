# PowerWheel

Made with AI. Tap F to use your forsaken power, as always. Hold F, and a wheel of every forsaken power opens: let go on the one you want and it becomes yours, without walking back to its stone.

## What it does

- **Tap F: use your power.** Just as in the game, except that the power starts as you let go of F rather than as you press it: that's how the mod tells a tap from a hold.
- **Hold F: the wheel.** After a third of a second the game's own wheel opens (the one G opens), with an icon for each forsaken power. Point at one and its name shows in the middle of the wheel, with "Ready" or the time left before a power can be used again. The power you have is marked.
- **Let go to pick.** The power under the pointer becomes yours, exactly as if you had taken it at its stone, and the game shows it where it always shows your power. A left click picks too. A right click or Escape closes the wheel without a change, and so does letting go in the middle of the wheel.
- **Only the powers you've earned.** A power stays a padlock, with no boss icon or name, until its boss's trophy is on its sacrificial stone in your world (whoever placed it; the mod notes the stones as you pass the start temple, and a power you took at its stone counts too), and can't be picked until then. Switching never resets the wait before you can use a power again: it carries over to the new one, as at the stones.
- **Your key, your rebind.** It uses the game's own forsaken power key, so if you've moved that off F, the new key works the same. A key of its own is a setting.
- **With a gamepad** in the classic layout, hold D-pad down for the wheel and aim with the right stick. Let go of D-pad down while the stick still points at the power you want, or press RT.
- **With a hammer, hoe or cultivator in hand,** F works exactly as in the game.

## Good to know

- **Only you need it.** It's client-side: neither the server nor the other players need it, and nothing is saved in the world. The power you pick is saved with your character, as at a stone.
- **Everything is a setting.** Whether a tap uses the power, what letting go and each click do, names under every icon, which powers show and what counts as earned, what each power does shown beside the wheel, and more. Open them in game with a configuration manager (F1); each one says what it does, and every change works at once.
- **The wheel is the game's own,** so its size and animation follow your Radial Menu settings in the game's Gameplay settings.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Other mods

- **ForsakenPowersPlusRemastered:** its F8 still switches powers beside this mod. Once PowerWheel does what you need, you can remove it.
- **Other mods that use the game's wheel** (the one G opens): only one wheel shows at a time, so they never open over each other.
- **A mod that stops the power at times** (PlantEasily while planting, for one) still does: a tap uses the power through the game's own call.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-PowerWheel-<version>.zip`. Keep the author `drumcowski` and the name `PowerWheel` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `PowerWheel.dll` into a folder named `drumcowski-PowerWheel` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

The settings are kept in `BepInEx\config\modprojects.powerwheel.cfg`, which appears after the first start; in the bundle, in the section "PowerWheel" of its own file.

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "PowerWheel". If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and F works as in the game. The same goes for an error while playing.

If holding the key does nothing, switch on "Write what it sees of the key to the log" in its settings, try the key again, and look in the same log: a line for each press says what the mod made of it, or why it left the key to the game.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\PowerWheel.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and the changelog agree on the version, builds, and writes `dist\drumcowski-PowerWheel-<version>.zip`. Until PowerWheel is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, and `kit\` is a copy of the QoL Mods kit that runs it; how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: its code was written by Claude agents (Anthropic), directed by miikeskii.
