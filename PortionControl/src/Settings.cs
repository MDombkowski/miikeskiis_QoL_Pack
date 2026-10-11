using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace PortionControl
{
    /// <summary>Where the extra buttons sit: in rows inside the split window, which grows taller, or the one-click
    /// buttons in a panel beside it (card A17: two layouts to look at in game).</summary>
    internal enum SplitLayout
    {
        InTheWindow,
        BesideTheWindow,
    }

    /// <summary>What a button shows when it can't be used: greyed out (his answer 10 on A17), or not at all.</summary>
    internal enum WhenUnusable
    {
        GreyedOut,
        Hidden,
    }

    /// <summary>Where Max weight's amount goes (A49): on the cursor, like every other button, so the game moves it when
    /// he drops it; or straight across, from a chest into his inventory (or, for "just enough", from his inventory into
    /// the open chest).</summary>
    internal enum MaxWeightTarget
    {
        OnTheCursor,
        StraightAcross,
    }

    /// <summary>Max weight on a stack that is already his (A49): greyed out, hidden, or the mirror: just enough of it to
    /// stop being encumbered, to put in a chest.</summary>
    internal enum OwnStack
    {
        GreyedOut,
        Hidden,
        JustEnoughToStopBeingEncumbered,
    }

    /// <summary>The one-click buttons while CurrencyPocket is installed.</summary>
    internal enum WithCurrencyPocket
    {
        OneClickButtonsHidden,
        OneClickButtonsShown,
    }

    /// <summary>How a percentage of the stack becomes a whole number (his call of 2026-09-30: down by default, so 99% of
    /// 20 is 19).</summary>
    internal enum PercentRounding
    {
        Down,
        Up,
    }

    /// <summary>A right-click's leftover (50 split into 7s leaves 1): it stays where the stack was (his answer), or comes
    /// along as a stack of its own.</summary>
    internal enum Leftover
    {
        LeaveIt,
        TakeIt,
    }

    /// <summary>Which kinds the chest window's -1 (and Max weight when it leaves one) keeps one of: every kind, gear
    /// too, from his words "each item type"; or only the kinds that stack, as the pack's chest service leaves one.</summary>
    internal enum LeaveOneOf
    {
        EveryKind,
        StackableKindsOnly,
    }

    /// <summary>What a click on the chest window's Max weight does (his answer: all it can on a left click, one of each
    /// kind left on a right click).</summary>
    internal enum MaxWeightClick
    {
        TakeAllItCan,
        LeaveOneOfEachKind,
    }

    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.portioncontrol.cfg, in the section
    /// General; inside miikeskii's QoL Pack, in the bundle's file, in the section "Portion Control". Every one takes effect
    /// at once, also while a window is open: the windows read them every frame they show. A configuration manager (F1)
    /// lists them in six fold rows under the one heading (Folds); the keys are the ones 0.1.0 wrote, so nothing he set is
    /// lost.
    /// </summary>
    internal static class Settings
    {
        internal static ConfigEntry<bool> Enabled { get; private set; }

        // Split window.
        internal static ConfigEntry<SplitLayout> Layout { get; private set; }

        internal static ConfigEntry<string> FixedAmounts { get; private set; }

        internal static ConfigEntry<WhenUnusable> TooBig { get; private set; }

        internal static ConfigEntry<string> Percentages { get; private set; }

        internal static ConfigEntry<PercentRounding> Rounding { get; private set; }

        internal static ConfigEntry<bool> PercentShowsAmount { get; private set; }

        internal static ConfigEntry<bool> AllButOne { get; private set; }

        internal static ConfigEntry<bool> SliderButtons { get; private set; }

        internal static ConfigEntry<bool> AmountBox { get; private set; }

        internal static ConfigEntry<bool> TypeAtOnce { get; private set; }

        // Max weight in the split window.
        internal static ConfigEntry<bool> MaxWeight { get; private set; }

        internal static ConfigEntry<bool> MaxWeightShowsAmount { get; private set; }

        internal static ConfigEntry<MaxWeightTarget> MaxWeightGoes { get; private set; }

        internal static ConfigEntry<OwnStack> ForMyOwnStack { get; private set; }

        internal static ConfigEntry<WhenUnusable> WhenNone { get; private set; }

        internal static ConfigEntry<float> LeaveFree { get; private set; }

        // Right-click an amount.
        internal static ConfigEntry<bool> RightClickSplits { get; private set; }

        internal static ConfigEntry<Leftover> RightClickLeftover { get; private set; }

        // Chest window.
        internal static ConfigEntry<bool> ChestAllButOne { get; private set; }

        internal static ConfigEntry<LeaveOneOf> ChestLeaveOneOf { get; private set; }

        internal static ConfigEntry<bool> ChestMaxWeight { get; private set; }

        internal static ConfigEntry<MaxWeightClick> ChestMaxWeightLeftClick { get; private set; }

        internal static ConfigEntry<MaxWeightClick> ChestMaxWeightRightClick { get; private set; }

        internal static ConfigEntry<float> ChestMaxWeightRaise { get; private set; }

        // Amounts by item, category and stack size.
        internal static ConfigEntry<bool> AmountsByKind { get; private set; }

        internal static ConfigEntry<string> ByItem { get; private set; }

        internal static ConfigEntry<string> ByCategory { get; private set; }

        internal static ConfigEntry<string> ByStackSize { get; private set; }

        // Keys and other mods.
        internal static ConfigEntry<KeyboardShortcut> AllButOneKey { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> MaxWeightKey { get; private set; }

        internal static ConfigEntry<WithCurrencyPocket> CurrencyPocket { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first; each fold's title row takes the top of its range. Out of the box everything is
            // on but the amounts by kind, at miikeskii's calls of 2026-09-30 (over the pack's rule that a module changes
            // nothing a player already sees until a setting asks for it): the split window replaces BetterSplitDialog,
            // and stands aside while that is installed; the chest window's two buttons are new, and change nothing the
            // game shows.
            Enabled = settings.Enabled;
            string general = ModuleSettings.General;
            string heading = settings.SectionOf(general);

            // ---- Split window (99 to 88)
            ConfigEntry<bool> splitFold = Folds.TitleRow(settings, "FoldSplitWindow", "Split window", 99);
            // Layout, Percentages, the amount on them and typing at once: his own settings made the defaults (card A63,
            // 2026-10-06); a file still at an old default moves once (the kit's MovedDefault).
            Layout = settings.Bind(general, "Layout", SplitLayout.BesideTheWindow,
                "Layout", 98,
                "BesideTheWindow (the default): the slider buttons inside the split window, and the one-click buttons " +
                "(percentages, fixed amounts, All -1, Max weight) in a panel beside it. InTheWindow: every button in rows " +
                "inside the window, which grows taller.");
            settings.MovedDefault(Layout, SplitLayout.InTheWindow, 1);

            FixedAmounts = settings.Bind(general, "FixedAmounts", "1, 2, 5, 10, 20, 30",
                "Fixed amounts", 97,
                "One-click amounts that never change, separated by commas. A click puts exactly that many on the cursor " +
                "and closes the window; a right-click splits the stack into stacks of it (Right-click an amount). Up to " +
                "12; leave empty for none. \"Amounts by item, category and stack size\" can set other lists for some items.");

            TooBig = settings.Bind(general, "FixedAmountBiggerThanTheStack", WhenUnusable.GreyedOut,
                "A fixed amount bigger than the stack", 96,
                "GreyedOut: shown, but it can't be clicked (say 20 on a stack of 12). Hidden: not shown at all.");

            Percentages = settings.Bind(general, "Percentages", "20, 25",
                "Percentages", 95,
                "One-click shares of the stack, in percent, separated by commas; each button says its percentage (\"25%\"). " +
                "A click puts that many on the cursor and closes the window; a right-click splits the stack into stacks of " +
                "it. Two that come to the same amount show once. Up to 8; leave empty for none. 20 and 25 by default.");
            settings.MovedDefault(Percentages, "20, 40, 60, 80", 1);

            Rounding = settings.Bind(general, "PercentagesRound", PercentRounding.Down,
                "Percentages round", 94,
                "How a share becomes a whole number. Down: 99% of 20 is 19, 25% of 50 is 12. Up: 99% of 20 is 20, 25% of " +
                "50 is 13. Either way a share is never less than 1 or more than the stack.");

            PercentShowsAmount = settings.Bind(general, "PercentageButtonsShowTheAmount", true,
                "Percentage buttons show the amount", 93,
                "On (the default): \"25% (12)\". Off: \"25%\".");
            settings.MovedDefault(PercentShowsAmount, false, 1);

            AllButOne = settings.Bind(general, "AllButOne", true,
                "All -1", 92,
                "A one-click button that takes the whole stack but one (a stack of 50 gives 49).");

            SliderButtons = settings.Bind(general, "SliderButtons", true,
                "Slider buttons", 91,
                "Min, -1, 50%, +1 and Max under the slider. They only move the slider; OK (or Enter) takes the amount.");

            AmountBox = settings.Bind(general, "AmountBox", true,
                "Amount box", 90,
                "A box beside the amount to type a number into; the slider follows as you type, and Enter takes it.");

            TypeAtOnce = settings.Bind(general, "TypeIntoTheBoxAtOnce", true,
                "Type into the box as the window opens", 89,
                "On (the default): the amount box has the keyboard as soon as the window opens, so the digits you type go " +
                "into it. Off: the window reads the digits and the arrow keys as the game does, until you click the box.");
            settings.MovedDefault(TypeAtOnce, false, 1);
            Folds.Add(splitFold, heading, Layout, FixedAmounts, TooBig, Percentages, Rounding, PercentShowsAmount, AllButOne,
                      SliderButtons, AmountBox, TypeAtOnce);

            // ---- Max weight in the split window (79 to 73)
            ConfigEntry<bool> maxFold = Folds.TitleRow(settings, "FoldMaxWeight", "Split window: Max weight", 79);
            MaxWeight = settings.Bind(general, "MaxWeight", true,
                "Max weight", 78,
                "A one-click button that takes as much of a chest's stack as you can carry without being encumbered, " +
                "worked out with the game's own weights (Megingjord, meads and the world's carry-weight setting count).");

            MaxWeightShowsAmount = settings.Bind(general, "MaxWeightShowsAmount", true,
                "Max weight: the amount on the button", 77,
                "On: \"Max weight (37)\". Off: \"Max weight\".");

            MaxWeightGoes = settings.Bind(general, "MaxWeightGoes", MaxWeightTarget.OnTheCursor,
                "Max weight: where the amount goes", 76,
                "OnTheCursor: like every other button, so you drop it where you want it. StraightAcross: from a chest " +
                "straight into your inventory (and \"just enough\" from your inventory straight into the open chest); what " +
                "doesn't fit stays where it was. With no room at all, an equipped stack, a quest item, or a chest " +
                "another player's game is looking after, it goes on the cursor instead.");

            ForMyOwnStack = settings.Bind(general, "MaxWeightForMyOwnStack", OwnStack.GreyedOut,
                "Max weight: on a stack already mine", 75,
                "A stack in your own inventory is already carried. GreyedOut: the button shows, greyed out. Hidden: it " +
                "doesn't show. JustEnoughToStopBeingEncumbered: the button becomes \"Just enough (5)\", the fewest of " +
                "that stack to put down (in a chest, say) to stop being encumbered.");

            WhenNone = settings.Bind(general, "MaxWeightWhenNone", WhenUnusable.GreyedOut,
                "Max weight: when you can take none", 74,
                "When you can't carry even one more (or, for \"just enough\", you aren't encumbered). GreyedOut: the " +
                "button shows, greyed out. Hidden: it doesn't show.");

            LeaveFree = settings.Bind(general, "WeightToLeaveFree", 0f,
                "Max weight: weight to leave free", 73,
                "How far under your carry limit Max weight keeps you, in the split window and in the chest window. 0 takes " +
                "you right up to it: the game counts you as encumbered only above it.",
                new AcceptableValueRange<float>(0f, 50f));
            Folds.Add(maxFold, heading, MaxWeight, MaxWeightShowsAmount, MaxWeightGoes, ForMyOwnStack, WhenNone, LeaveFree);

            // ---- Right-click an amount (69 to 67)
            ConfigEntry<bool> rightFold = Folds.TitleRow(settings, "FoldRightClick", "Split window: right-click an amount", 69);
            RightClickSplits = settings.Bind(general, "RightClickSplitsIntoStacks", true,
                "Right-click an amount: split into stacks of it", 68,
                "On: a right-click on a fixed amount or a percentage splits the stack into as many stacks of that amount as " +
                "it makes (50 resin, right-click 10: five stacks of 10). From a chest they go into your inventory; from " +
                "your inventory into the open chest, or with no chest open, into empty cells of your own inventory. Each " +
                "new stack takes an empty cell; what no longer fits stays where it was. Not a stack you have equipped.");

            RightClickLeftover = settings.Bind(general, "RightClickLeftover", Leftover.LeaveIt,
                "Right-click an amount: the leftover", 67,
                "What is left over when the stack doesn't split evenly (50 into 7s leaves 1). LeaveIt: it stays where the " +
                "stack was. TakeIt: it comes along as a stack of its own.");
            Folds.Add(rightFold, heading, RightClickSplits, RightClickLeftover);

            // ---- Chest window (59 to 53)
            ConfigEntry<bool> chestFold = Folds.TitleRow(settings, "FoldChestWindow", "Chest window", 59);
            ChestAllButOne = settings.Bind(general, "ChestTakeAllButOne", true,
                "-1 beside Take all", 58,
                "A -1 button right of the chest window's Take all: takes everything but one of each kind, as Take all " +
                "takes (8 stacks of 20 troll hide and 37 wolf pelts: all but 1 troll hide and 1 wolf pelt). The one left " +
                "is in that kind's first stack, top row first. Not in a grave's window.");

            ChestLeaveOneOf = settings.Bind(general, "ChestLeaveOneOf", LeaveOneOf.EveryKind,
                "-1 and Max weight: leave one of", 57,
                "EveryKind: every kind of item, gear too (three bronze swords: two go). StackableKindsOnly: only kinds that " +
                "stack; every piece of gear goes. A kind is the item's name, whatever its quality.");

            ChestMaxWeight = settings.Bind(general, "ChestMaxWeight", true,
                "Max weight button", 56,
                "A button with the game's encumbered picture above the chest window's weight: takes as much from the chest " +
                "as you can carry without being encumbered, the chest's stacks in reading order (top row first), each as " +
                "much as fits. \"Max weight: weight to leave free\" counts. Greyed out while nothing more fits.");

            ChestMaxWeightLeftClick = settings.Bind(general, "ChestMaxWeightLeftClick", MaxWeightClick.TakeAllItCan,
                "Max weight button: a left click", 55,
                "TakeAllItCan: down to the last of a kind. LeaveOneOfEachKind: one of each kind stays in the chest (\"-1 " +
                "and Max weight: leave one of\" says which kinds), so you remember what is stored where and quick stack " +
                "still finds it. In a grave, either click takes all it can (a grave left holding anything never goes).");

            ChestMaxWeightRightClick = settings.Bind(general, "ChestMaxWeightRightClick", MaxWeightClick.LeaveOneOfEachKind,
                "Max weight button: a right click", 54,
                "The same choices, for a right click.");

            ChestMaxWeightRaise = settings.Bind(general, "ChestMaxWeightRaise", 0f,
                "Max weight button: raise it by", 53,
                "How far above its place (4 above the weight box's picture) the button sits, in the window's own units; " +
                "below 0 lowers it.",
                new AcceptableValueRange<float>(-40f, 120f));
            Folds.Add(chestFold, heading, ChestAllButOne, ChestLeaveOneOf, ChestMaxWeight, ChestMaxWeightLeftClick,
                      ChestMaxWeightRightClick, ChestMaxWeightRaise);

            // ---- Amounts by item, category and stack size (49 to 45)
            ConfigEntry<bool> kindFold = Folds.TitleRow(settings, "FoldAmountsByKind", "Amounts by item, category and stack size", 49);
            AmountsByKind = settings.Bind(general, "AmountsByKind", false,
                "Amounts by item, category and stack size", 48,
                "On: the split window offers the amounts set below for the items they name, instead of \"Fixed amounts\" and " +
                "\"Percentages\". The most specific wins, each kind of button apart: an item, then a category's member, then " +
                "a category, then a stack size. A list is written as those are, a % after a percentage: \"10, 20, 50, 25%\". " +
                "A list with no percentages keeps the next one's; 0 means no fixed amounts, 0% no percentages.");

            ByItem = settings.Bind(general, "AmountsByItem", string.Empty,
                "By item", 47,
                "Items and their amounts: tick an item and type its list. Search the list by name. In the file: " +
                "\"$item_coal: 10, 20, 50; $item_resin: 5, 25%\".",
                null, KindDrawers.Items);

            ByCategory = settings.Bind(general, "AmountsByCategory", string.Empty,
                "By category", 46,
                "The pack's categories (as Add to Cart lists them) and their members, each with its own list. In the file: " +
                "\"Wood: 10, 25, 50; Metals/Iron: 5, 10\".",
                null, KindDrawers.Categories);

            ByStackSize = settings.Bind(general, "AmountsByStackSize", string.Empty,
                "By stack size", 45,
                "Every item that stacks to one size (10, 20, 50 …), each size with its own list. In the file: " +
                "\"50: 5, 10, 25; 10: 1, 2, 5\".",
                null, KindDrawers.StackSizes);
            KindDrawers.TakeWholeRow(ByItem, ByCategory, ByStackSize);
            Folds.Add(kindFold, heading, AmountsByKind, ByItem, ByCategory, ByStackSize);

            // ---- Keys and other mods (39 to 36)
            ConfigEntry<bool> keysFold = Folds.TitleRow(settings, "FoldKeysAndMods", "Keys and other mods", 39);
            AllButOneKey = settings.Bind(general, "AllButOneKey", KeyboardShortcut.Empty,
                "Key for All -1", 38,
                "A key that presses All -1 while the split window is open. None by default. The window already reads " +
                "the digits, the left and right arrows, Enter and Escape.");

            MaxWeightKey = settings.Bind(general, "MaxWeightKey", KeyboardShortcut.Empty,
                "Key for Max weight", 37,
                "A key that presses Max weight while the split window is open. None by default.");

            CurrencyPocket = settings.Bind(general, "WithCurrencyPocket", WithCurrencyPocket.OneClickButtonsHidden,
                "With CurrencyPocket installed", 36,
                "Only matters while the mod CurrencyPocket is installed. BetterSplitDialog hides its one-click buttons " +
                "with it, because the two together once copied coins; the cause was never written down. " +
                "OneClickButtonsHidden: the percentages, fixed amounts, All -1 and Max weight stay hidden in the split " +
                "window (the slider buttons and the amount box still show). OneClickButtonsShown: they show.");
            Folds.Add(keysFold, heading, AllButOneKey, MaxWeightKey, CurrencyPocket);
        }

        /// <summary>True in the frame the key goes down with its modifiers held, read through the game's own input.</summary>
        internal static bool Pressed(KeyboardShortcut key) =>
            key.MainKey != KeyCode.None && ZInput.GetKeyDown(key.MainKey, false) && key.Modifiers.All(modifier => ZInput.GetKey(modifier, false));
    }
}
