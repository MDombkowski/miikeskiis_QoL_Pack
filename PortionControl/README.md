# Portion Control

Made with AI. The split window (Shift + click on a stack) takes just the amount you want in one click: your own fixed amounts, percentages, All -1, and Max weight, as much of a chest's stack as you can carry without being encumbered. A right-click on an amount splits the stack into stacks of it. And every chest's window gains a -1 beside Take all (everything but one of each kind) and a Max weight button (as much of the chest as you can carry).

## What it does

Shift + click on a stack of two or more, in your inventory or a chest, opens the game's split window. Portion Control adds to it:

- **An amount box** beside the amount ("12/50"): it has the keyboard as the window opens (a setting), so type a number and the slider follows; Enter takes it.
- **Slider buttons:** Min, -1, 50%, +1 and Max. They only move the slider; OK (or Enter) takes the amount.
- **Percentages:** 20% and 25% of the stack (a setting takes any you like), each labelled with its percentage and the amount ("25% (12)"; a setting leaves the amount off), rounded down (99% of 20 is 19) or up, as you choose. One click puts that many on the cursor and closes the window. Two that come to the same amount show once.
- **Fixed amounts:** 1, 2, 5, 10, 20 and 30 by default, your own list in the settings. One click puts exactly that many on the cursor. An amount bigger than the stack is greyed out (or hidden, as you choose).
- **All -1:** the whole stack but one, in one click (a stack of 50 gives 49).
- **Max weight (37):** on a chest's stack, as much of it as you can carry without being encumbered, in one click. It uses the game's own weights, so Megingjord, meads, powers and the world's carry-weight setting all count, and it takes you right up to your limit: the game calls you encumbered only above it. On a stack already yours it is greyed out, or, as you choose, hidden, or "Just enough (5)": the fewest of that stack to put down (in a chest, say) to stop being encumbered.

Every one-click amount goes on the cursor, as the window's OK does, and you drop it where you want it; the game moves the items. Max weight can instead move its amount straight into your inventory (a setting). Ctrl + Shift + click, Enter, Escape, the digits and the arrow keys work as they do in the game.

**Right-click an amount** (a fixed amount or a percentage) to split the stack into as many stacks of it as it makes. 50 resin, right-click 10: five stacks of 10; right-click 7: seven stacks of 7, and the 1 left over stays where it was (or, as you choose, comes along as a stack of its own). From a chest they go into your inventory; from your inventory into the open chest, or, with no chest open, into empty cells of your own inventory. Each new stack takes an empty cell of its own; what finds no room stays where it was. Not a stack you have equipped.

**Your own amounts for some items** (off until you switch it on): in the settings, tick items from a list you can search and give each its own amounts ("10, 20, 50, 25%"), or give a category (Wood; Metals, or just Iron; Meads and Potions …) or every item that stacks to one size (50, 20, 10 …) its own. The split window then offers those for them. The most specific wins: an item's, then its category's, then its stack size's, then the general lists.

## The chest window

- **-1**, right of Take all: takes everything but one of each kind, as Take all takes. 8 stacks of 20 troll hide and 37 wolf pelts: all but 1 troll hide and 1 wolf pelt. The one left stays in that kind's first stack (top row first), so you remember what is stored where and quick stack still finds the chest. Not in a grave's window.
- **Max weight**, the game's encumbered picture above the chest's weight: takes as much from the chest as you can carry without being encumbered, the chest's stacks in reading order, each as much as fits. A left click takes all it can; a right click leaves one of each kind; each click is a setting. Greyed out while nothing more fits.

**Two layouts:** the one-click buttons in a panel beside the window (the default), or every button in rows inside the window, which grows taller (the setting "Layout").

## Good to know

- **It replaces BetterSplitDialog,** and does everything it did. While BetterSplitDialog (or Abacus, which adds buttons to the same window) is installed, Portion Control leaves the split window to it and says so in the log, so the two never put buttons on top of each other; the chest window's -1 and Max weight still work. Uninstall BetterSplitDialog to use Portion Control's split window.
- **With CurrencyPocket installed,** the one-click buttons stay hidden unless the setting "With CurrencyPocket installed" says otherwise: BetterSplitDialog hides its own with it, because the two together once copied coins (the cause was never written down). The slider buttons and the amount box still show.
- **Only you need it.** It's client-side: neither the server nor the other players need it.
- **Keyboard and mouse only.** Its buttons have no gamepad control.
- **Changes work at once,** also while the window is open. Switched off, the windows are the game's own again at once.
- **The chest window's buttons work only on a chest your game is looking after,** as the game's own window does: the game saves a chest only from the computer of the player who opened it.
- **It is also part of miikeskii's QoL Pack,** the bundle of all miikeskii's Valheim mods. With the bundle installed, this separate copy stands aside and does nothing, and says so in the log; you can remove it.

## Requirements

- BepInExPack for Valheim
- Jötunn, the Valheim library

## Installing

- **With Thunderstore Mod Manager or r2modman:** Settings, then "Import local mod from file", and choose `drumcowski-PortionControl-<version>.zip`. Keep the author `drumcowski` and the name `PortionControl` as the import fills them in, so that a newer zip later replaces this one instead of installing beside it.
- **By hand:** copy `PortionControl.dll` into a folder named `drumcowski-PortionControl` inside `BepInEx\plugins`. With a mod manager, `BepInEx` is inside your profile's folder.

## Settings

Open the settings in game with a configuration manager (shudnal's ConfigurationManager opens with F1). They are kept in `BepInEx\config\modprojects.portioncontrol.cfg`, which appears after the first start; in the bundle, in the section "Portion Control" of its own file. In F1 they sit under one heading in six rows that open and close (with shudnal's ConfigurationManager 1.1.22 or later; with another, each group shows open under its title).

**Split window**

| Setting | Default | What it does |
|---|---|---|
| Enabled | on | Off: the split window and the chest window are the game's own again, at once. |
| Layout | BesideTheWindow | BesideTheWindow: the slider buttons inside the window, the one-click buttons in a panel beside it. InTheWindow: every button in rows inside the window. |
| Fixed amounts | 1, 2, 5, 10, 20, 30 | Your one-click amounts, separated by commas; up to 12, empty for none. |
| A fixed amount bigger than the stack | GreyedOut | GreyedOut or Hidden. |
| Percentages | 20, 25 | One-click shares of the stack; up to 8, empty for none. |
| Percentages round | Down | Down (99% of 20 is 19) or Up (20). Never less than 1 or more than the stack. |
| Percentage buttons show the amount | on | On: "25% (12)". Off: "25%". |
| All -1 | on | The whole stack but one, in one click. |
| Slider buttons | on | Min, -1, 50%, +1 and Max under the slider. |
| Amount box | on | The box beside the amount. |
| Type into the box as the window opens | on | On: the digits you type go straight into the box. Off: the window reads them as the game does until you click the box. |

**Split window: Max weight**

| Setting | Default | What it does |
|---|---|---|
| Max weight | on | The Max weight button. |
| Max weight: the amount on the button | on | "Max weight (37)" or "Max weight". |
| Max weight: where the amount goes | OnTheCursor | OnTheCursor, or StraightAcross: straight into your inventory (and "just enough" straight into the open chest); what doesn't fit stays where it was. With no room at all, an equipped stack, a quest item, or a chest another player's game is looking after, it goes on the cursor instead. |
| Max weight: on a stack already mine | GreyedOut | GreyedOut, Hidden, or JustEnoughToStopBeingEncumbered. |
| Max weight: when you can take none | GreyedOut | GreyedOut or Hidden. |
| Max weight: weight to leave free | 0 | How far under your carry limit it keeps you, 0 to 50, here and in the chest window. |

**Split window: right-click an amount**

| Setting | Default | What it does |
|---|---|---|
| Right-click an amount: split into stacks of it | on | A right-click on a fixed amount or a percentage splits the stack into stacks of it. |
| Right-click an amount: the leftover | LeaveIt | LeaveIt: what doesn't split evenly stays where the stack was. TakeIt: it comes along as a stack of its own. |

**Chest window**

| Setting | Default | What it does |
|---|---|---|
| -1 beside Take all | on | The -1 button. |
| -1 and Max weight: leave one of | EveryKind | EveryKind: gear too (three bronze swords: two go). StackableKindsOnly: every piece of gear goes. A kind is the item's name, whatever its quality. |
| Max weight button | on | The Max weight button above the chest's weight. |
| Max weight button: a left click | TakeAllItCan | TakeAllItCan or LeaveOneOfEachKind. |
| Max weight button: a right click | LeaveOneOfEachKind | The same choices. |
| Max weight button: raise it by | 0 | How far above its place (just above the weight box's picture) the button sits, -40 to 120; below 0 lowers it. |

**Amounts by item, category and stack size**

| Setting | Default | What it does |
|---|---|---|
| Amounts by item, category and stack size | off | On: the split window offers the amounts below for the items they name. |
| By item | none | Tick items from a list you can search, and type each one's amounts. In the file: `$item_coal: 10, 20, 50; $item_resin: 5, 25%`. |
| By category | none | The pack's categories (as Add to Cart lists them) and their members, each with its amounts. In the file: `Wood: 10, 25, 50; Metals/Iron: 5, 10`. |
| By stack size | none | Every size items stack to, each with its amounts. In the file: `50: 5, 10, 25; 10: 1, 2, 5`. |

A list is written as the general ones are, a % after a percentage. A list with no percentages keeps the next one's; `0` means no fixed amounts, `0%` no percentages. The most specific wins, fixed amounts and percentages apart: an item, a category's member, a category, a stack size, then the general lists.

**Keys and other mods**

| Setting | Default | What it does |
|---|---|---|
| Key for All -1 | none | A key that presses All -1 while the window is open. |
| Key for Max weight | none | A key that presses Max weight while the window is open. |
| With CurrencyPocket installed | OneClickButtonsHidden | Only matters with CurrencyPocket: OneClickButtonsHidden or OneClickButtonsShown. |

## If something goes wrong

Look in `BepInEx\LogOutput.log` for lines from "Portion Control". If the game has changed so that the mod can't find what it needs, it switches itself off and says why, and the split window is the game's own. The same goes for an error while playing.

## Building from source

- **Build:** `dotnet build -c Release` in this folder. The result is `bin\Release\net472\PortionControl.dll`.
- **Package:** `tools\package.ps1` checks that the project file, `src\Module.cs` and this changelog agree on the version, builds, and writes `dist\drumcowski-PortionControl-<version>.zip`. Until Portion Control is released on its own, its zip carries the bundle's icon (`..\QoLMods\icon.png`).
- **What's where:** the mod's code is in `src\` (the bundle compiles it from here too), its own plugin in `standalone\`, `kit\` is a copy of the QoL Mods kit that runs it, and `parts\Catalogue\` a copy of the pack's catalogue (its categories, for the amounts by category); how a module is made: `..\QoLMods\MODULES.md`.
- **References:** it compiles against the game's DLLs and the BepInEx, HarmonyX and Jötunn DLLs where they are already installed. Nothing is downloaded and nothing is copied or written outside this folder. Override the two paths if yours are elsewhere: `-p:ValheimManaged="<Valheim>\valheim_Data\Managed" -p:ModProfile="<mod manager profile folder>"`.

## Credits

See `CREDITS.md`. Built with BepInEx, HarmonyX and Jötunn.
