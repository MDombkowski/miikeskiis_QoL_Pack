# Item Stand Icons

A small mark on every item in Valheim that fits an item stand, saying which one: an up arrow for the wall stand, a right arrow for the flat stand, both arrows for both; and a small mannequin on every item the armor stand takes. It shows wherever item icons are drawn: your inventory, chests, the hotbar, the crafting menu and the trader; or, if you like, only while you aim at a stand.

## What it does

- **The two stands.** The game has two item stands, both called "Item Stand": one hangs on a wall (vertical), the other lies on a floor or table (horizontal). Which items each takes is the game's own rule, and the mod asks the game itself, so a mod that lets more items onto stands shows too.
  - **Up arrow:** the wall stand only. No item of the game fits only the wall stand today, but a mod could make some.
  - **Right arrow:** the flat stand only: all foods and meads, the Egg, Barley Flour, Hildir's three chests and the four tankards (about 130 items).
  - **Both arrows:** both stands: nearly every weapon, shield, helmet, tool, trophy and fish (about 370 items).
  - **No arrow:** neither stand takes it, such as armour, arrows, the Torch, most materials, and Frostcore (which is on both stands' lists but has no model to hang).
- **The armor stand: a mannequin**, at the middle of the icon's right edge: every helmet, chest piece, pair of legs and cape, the Megingjord, and the weapons, bows, tools, torches and shields it holds, about 310 items. Many of them get both marks: a sword fits both item stands and the armor stand. The armor stand's own rule, asked of the game too: each of its slots takes its kinds of item, and it hangs only what has a model to show.
- **Only while aiming, if you like.** Switch on "Only while aiming at a stand" and your inventory's and hotbar's marks show only while you aim at an item stand or an armor stand, when the game shows its line under the crosshair ("Attach item", or "Take" once something hangs there). Aim, then open your inventory: the aim holds while it's open and you stand still. Aiming at an item stand shows the arrows (a food's right arrow at the wall stand too), at an armor stand the mannequin; "WhatFitsIt" shows only what that very stand takes. Whatever you open (a chest, a workbench, a trader) is what you aim at then, so your own inventory's and hotbar's marks go while it's open.
- **Where:** your inventory (with EquipmentAndQuickSlots' equipment and quick slots), chests (and other mods' inventory windows), the hotbar at the top left of the screen (and EquipmentAndQuickSlots' quick-slot bar), the crafting menu's list, the recipe you picked and its ingredients, and a trader's list. The build menu's costs too, if you switch them on. Each place has its own switch.
- **On the icon:** the bottom right corner by default, small (8 pixels on a full-size icon), on the icon's bottom, which it shares with a stack's count and the durability bar; the middle of the icon's left edge ("Where on the icon": LeftMiddle) is the one spot free on every icon in the game, so nothing already on an icon is covered (hotbar numbers, upgrade levels, food and no-teleport marks, counts, durability). The arrows are drawn by the mod, white with a dark outline, because the game's fonts have no arrows.
- **Off until you switch it on.** Out of the box the mod is off and no icon changes: switch on "Enabled" in the settings (since 0.2.1; "Show the marks" is then on already). An older settings file moves to these defaults once where it still holds the old ones (so a copy that was on at the old defaults is now off); a setting you changed stays as you set it.

**Add to Cart**, miikeskii's cart mod, draws the same marks on its thumbnails, following these settings (its own setting "Item stand marks" can also show them always, or never), and can list everything that fits a stand together ("Item stand filter").

## Good to know

- **Only you need it.** It's client-side: neither the server nor the other players need it. The marks show what your own game allows, which is what counts when you hang something: a friend with a mod that lets more items onto stands may hang more than you can.
- **Changes work at once.** Change a setting in game and the marks follow; no restart.
- **Fewer marks:** switch off "Mark items both stands take" to mark only the items that fit just one stand, or "Mark items the armor stand takes"; switch on "Only while aiming at a stand"; or set "Only while holding a key" to see them only while you hold that key.
- **Other mods that do the same job:** none marks items for item stands that we know of. Item Stand All Items (which lets any item onto stands) is followed by itself: with it in its default mode, nearly every item gets both arrows, which is right. If you'd rather not see the marks, switch off "Show the marks" (or the whole mod, "Enabled"); nothing of it stays on your icons.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-ItemStandIcons-<version>.zip`. Keep the author `drumcowski` and the name `ItemStandIcons` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `ItemStandIcons.dll` into a folder named `drumcowski-ItemStandIcons` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.itemstandicons.cfg`, which appears after the first start; in the bundle, in the section "Item Stand Icons" of its own file.

"Stands that count" and "Armor stands that count" have a **Pick** button beside their text box there, which opens a window of everything the list can hold, each with its picture: a click puts a thing on the list or takes it off. The text box stays for typing.

| Setting | Default | What it does |
|---|---|---|
| Enabled | **off** | Off takes every mark off the icons at once; on brings them back (with "Show the marks" on). |
| Show the marks | on | The switch for all the marks (the mod itself is off out of the box). |
| Mark items only the wall stand takes | on | The up arrow. |
| Mark items only the flat stand takes | on | The right arrow. |
| Mark items both stands take | on | Both arrows (about 370 items): off gives fewer marks. |
| Mark items the armor stand takes | on | The mannequin (about 310 items), at its own place. |
| Look | DrawnArrows | DrawnArrows: small arrows and the mannequin, drawn by the mod. ArrowCharacters: the arrow characters as text (the game's fonts lack them, so they come from a wider fallback font). Letters: V (vertical, the wall stand), H (horizontal, the flat one), VH. In both text looks the armor stand's mark is A. |
| Mark for both stands | SideBySide | How the drawn mark for both stands puts its arrows: SideBySide, Stacked (up above right) or Corner (one corner, a head at each end, the most compact). |
| Colour | white | The marks' colour; the drawn arrows keep their dark outline. |
| Dark outline | on | A thin dark outline round the drawn arrows. |
| Size (pixels) | 8 | How tall a mark is on a full-size icon (64 pixels); smaller icons get smaller marks. 8 to 24. |
| Where on the icon | BottomRight | Where the arrows sit: a corner (BottomRight shares its corner with a stack's count and the durability bar; a corner covers something in some places), the left edge's middle (LeftMiddle, free on every icon), or the right edge's (RightMiddle, where the mannequin sits by default: move one of them). |
| Nudge right (pixels) | 0 | Moves the arrows right (a minus: left), -16 to 16. |
| Nudge up (pixels) | 0 | Moves the arrows up (a minus: down), -16 to 16. |
| Where on the icon (armor stand) | RightMiddle | Where the mannequin sits: the right edge's middle, across from the arrows, or any other place. |
| Nudge right (armor stand, pixels) | 0 | Moves the mannequin right (a minus: left), -16 to 16. |
| Nudge up (armor stand, pixels) | 0 | Moves the mannequin up (a minus: down), -16 to 16. |
| Only while holding a key | none | None: the marks show all the time. A key: only while you hold it. |
| Only while aiming at a stand | off | On: the marks show only while you aim at an item stand or an armor stand (the game's "Attach item" or "Take" line under the crosshair). The aim holds while your inventory is open and you stand still. With a key above too, both are needed. |
| Aiming holds back | YourItems | YourItems: your inventory and the hotbar (with EquipmentAndQuickSlots' slots and bar); chests, the crafting menu and the trader keep their marks. EveryPlace: every place. Either way, whatever you opened (a chest, a workbench, a trader) is what you aim at then, so your own inventory's and hotbar's marks go while it's open; with EveryPlace, every mark does. |
| Aiming shows | ThatStandsMarks | ThatStandsMarks: at an item stand the arrows (every arrow, whichever item stand), at an armor stand the mannequin. WhatFitsIt: only the items the stand you aim at takes (the game's armor stand takes all its kinds at one spot; a stand from a mod with a spot per slot narrows to that slot). EveryMark: every mark, whichever stand. |
| In your inventory | on | Your inventory, with EquipmentAndQuickSlots' slots. |
| In chests | on | A chest's window, and other mods' inventory windows. |
| On the hotbar | on | The hotbar, and EquipmentAndQuickSlots' quick-slot bar. |
| In the crafting list | on | The recipes down the left of the crafting menu. |
| On the chosen recipe | on | The large picture of the recipe you picked, and the small one of an item you upgrade. |
| On the ingredients | on | The ingredients of the recipe you picked. |
| At the trader | on | A trader's list of things for sale. |
| On the build menu's costs | off | The materials of the piece in hand; those slots have no free spot, so a mark covers part of them. |
| Which items count | GameCheck | GameCheck: the game's own check, so a mod's change to it shows. StandListsOnly: only the stands' own lists and each item's model. |
| Stands that count | itemstand, itemstandh | The stands asked, by prefab name: itemstand is the wall stand, itemstandh the flat one. The boss stones and the altars' holders never count. |
| Also count other stand pieces | off | Also every other piece in the game that holds an item as an item stand does, or wears gear as the armor stand does: stands other mods add, and the Mysterious Rock (the game's two unbuildable armor stands take only what the buildable one takes). |
| Armor stands that count | ArmorStand | The armor stands asked, by prefab name: ArmorStand is the game's own. Empty: no mannequins. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Item Stand Icons". If the game has changed so that the mod can't find a place it marks, that place stays unmarked and the log says which; if it can't find any, it switches itself off and says why. The same goes for an error while playing: the marks go, and the icons are the game's own.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\ItemStandIcons.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-ItemStandIcons-<version>.zip`. Until Item Stand Icons is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, `kit\` is a copy of the QoL Mods kit that runs it, and `parts\ItemStands` a copy of the item stands part it shares with Add to Cart (the rules, the drawn arrows and mannequin, the mark); how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Made with AI

Its code was written by Claude (Anthropic) agents, directed by miikeskii.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn.
