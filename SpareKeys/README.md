# Spare Keys

See every key the game and your mods use, and every key they leave free, on a picture of your keyboard and mouse. Press **Insert** in a world to open it.

## What you see

- **Your keyboard, coloured.** Green keys are free. Blue keys are free on their own but taken with Ctrl, Alt or Shift (a small C, A or S in the corner says which). Grey keys are in use. Purple keys are shared: two things use them, but never at the same moment (Left Alt + E pointing at a chest for one, pointing at an armour stand for the other), so one press never does both. Red keys are a clash: two things that can work at the same moment, so one press does both. Amber keys may be in use: found only inside a mod's code, or a mod's button with no setting behind it (often a key of its own window).
- **Who uses a key, and when.** Point at a key: the line under the picture names everything that uses it (the game with your own key bindings, every mod's key settings, your console binds) and when each works ("at a chest", "the hammer in hand", "the inventory open"), and for a clash which two fire together. For a key in use it suggests up to three free keys nearby.
- **Hold Ctrl, Alt or Shift** and the picture shows the keys with it held: Alt + J may be taken where J alone is free. The None, Ctrl, Alt and Shift buttons above the picture do the same (a left click for the left key, a right click for the right one).
- **A list to browse** under the picture: everything, the free keys, the clashes, the shared keys, the game's keys, the pack's, and each mod's, with a search box. Click a key on the picture to list only its rows.
- **Help while you pick a key.** While a key setting in the configuration manager's window (F1) waits for you to press a key, the same picture shows beside the window, judged for that setting: a key used only where that setting never works shows purple, free for it. The same happens in the game's own Settings, Controls tab, while it waits for a new key. These panels only show: press the key you want, as usual.
- **It only looks.** It never changes a setting of the game's or of another mod.

## Its keys

| Key | What it does |
|---|---|
| Insert | Opens and closes the key map (in a world) |
| Escape | Closes it |

The key map also opens from a button at the top of Spare Keys' settings in F1.

## Good to know

- **As far as can be seen.** A mod that reads a key in its code with no setting for it can still use a key the map shows as free. Spare Keys reads every mod's code once when the game starts, to find most of those, and shows them in amber, as it does a mod's buttons with no setting behind them.
- **When each key works** comes from the code: the game's, the pack's, and the mods most players carry beside it. Another mod's key it knows nothing about counts as working anywhere (a setting can show it as maybe a clash instead), and you can tell it when such a key works in "Whens for other mods' keys", one line per key, for example "Some Mod: Toggle key = world, hand the hammer". Shared keys can also be shown simply as used, or counted as clashes.
- **The game's own keys work whatever else you hold:** Alt + Q still auto-runs. So a key the game uses counts as used with every modifier too. A setting changes that.
- **Keys used only in menus, while typing, or with a window open** (the inventory, a chest, the map) don't count out of the box, since a key for walking about never meets them. Turn on that setting when you pick a key for a window.
- **The panel beside F1** works with shudnal's ConfigurationManager, the one most Valheim mod packs carry.
- **Bindrune** lists every key too. With it installed, Spare Keys leaves Insert to it: pick another key in Spare Keys' settings, or use its button in F1.
- **Only you need it.** It works on your computer alone; nothing is sent, and the server and other players don't need it.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-SpareKeys-<version>.zip`. Keep the author `drumcowski` and the name `SpareKeys` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `SpareKeys.dll` into a folder named `drumcowski-SpareKeys` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1) and find Spare Keys: its key, the window's size and opacity, the keyboard it draws (full, without a number pad, or a laptop's), the colours, whether the when shows on hover, what counts as used, shared keys, keys whose when isn't known, your own whens for other mods' keys, and the panels while you pick a key. Every change works at once.

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Spare Keys". If the game or the configuration manager has changed so that something can't be found, only the part that needs it stays off (the panel beside F1, or the one in the Controls menu), and the log says so. An error while playing switches Spare Keys off for the rest of the session and closes its window; nothing else in the game changes.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn. Made with AI: written by Claude agents (Anthropic) for miikeskii.
