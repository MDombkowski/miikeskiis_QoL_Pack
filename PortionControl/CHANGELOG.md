# Changelog

## 0.2.7 (2026-10-11)

- "With CurrencyPocket installed" names BetterSplitDialog again: Portion Control stands aside for that mod, and its buttons copying coins beside CurrencyPocket are why Portion Control's own are hidden out of the box. What it does is unchanged.

## 0.2.6 (2026-10-11)

- Settings name no other mod unless this mod works with it, stands aside for it or makes room for it: "With CurrencyPocket installed" explains its out-of-the-box choice without naming another split mod; what it does is unchanged.

## 0.2.5 (2026-10-11)

- **Messages fit the screen**: a message line too wide for your screen is broken at spaces into lines that fit, measured by the game's own text, as every mod of miikeskii's QoL Pack now does.

## 0.2.4 (2026-10-09)

- -1 and Max weight grey out whenever the game's own Take all is greyed (a friend's chest you may only look at, StoreStash 0.7.0), and the split window's right-click amounts and Max weight straight across keep out of such a chest.
- The catalogue it carries changed (quick picks); nothing changes here.

## 0.2.3 (2026-10-06)

- **miikeskii's own settings are the defaults now**: the one-click buttons in a panel beside the split window ("Layout" BesideTheWindow, was InTheWindow), percentages 20 and 25 (was 20, 40, 60, 80), the percentage buttons showing the amount, and the amount box taking the keyboard as the window opens.
- An older settings file moves to the new defaults once, only where it still holds the old default: a setting changed by hand stays as it is (the kit's new `ModuleSettings.MovedDefault`, remembered in a hidden setting, `DefaultsMoved`). The log names what moved.

## 0.2.2 (2026-09-30)

- **The chest window's Max weight button sits clear of the weight box's picture** (miikeskii's first look at 0.2.0: it covered the picture's top). That picture is centred on the box's top edge and reaches 16 above it; the button now goes 4 above the picture, 16 higher than before, and follows the box if another mod moves it.
- **A new setting, "Max weight button: raise it by"** (0 by default), to move it up or down from there.

## 0.2.1 (2026-09-30)

- **The catalogue part it carries changed** (with StoreStash 0.4.0's chest setups): it gains setups (`ItemPicks`), which Portion Control doesn't use; nothing else changes in it.

## 0.2.0 (2026-09-30)

miikeskii's feedback on 0.1.0.

- **The chest window gains two buttons:** **-1**, right of Take all, takes everything but one of each kind, as Take all takes (the one left in that kind's first stack; not in a grave's window); **Max weight**, the game's encumbered picture above the chest's weight, takes as much from the chest as you can carry without being encumbered: all it can on a left click, one of each kind left on a right click, each a setting.
- **A right-click on an amount** (a fixed amount or a percentage) splits the stack into stacks of it: from a chest into your inventory, from your inventory into the open chest, or with none open within your inventory. The leftover stays where it was (a setting). On by default.
- **Percentage buttons say "25%"** (a setting adds the amount), and **percentages round down** by default (99% of 20 is 19), a setting.
- **Amounts by item, category and stack size,** off until switched on: lists in the settings window, items ticked from a list you can search.
- **While BetterSplitDialog or Abacus is installed,** only the split window stands aside; the chest window's buttons still work.
- **The settings** sit in six groups that open and close in F1. Every setting of 0.1.0 keeps its key, so what you set stays.

## 0.1.0 (2026-09-30)

- **First version**, as a mod of miikeskii's QoL Pack and on its own, written from scratch to replace BetterSplitDialog.
- The game's split window gains an **amount box**, **slider buttons** (Min, -1, 50%, +1, Max), one-click **percentages** (20, 40, 60, 80), your one-click **fixed amounts** (1, 2, 5, 10, 20, 30; one bigger than the stack greyed out), **All -1**, and **Max weight**: as much of a chest's stack as you can carry without being encumbered, worked out with the game's own weights. Every one-click amount goes on the cursor and closes the window.
- **Two layouts** to choose from: in the window, or beside it.
- **On for everyone out of the box,** at miikeskii's call: it stands aside, with a line in the log, while BetterSplitDialog or Abacus is installed, so a friend who keeps BetterSplitDialog sees no change. With CurrencyPocket installed its one-click buttons stay hidden unless a setting says otherwise.
