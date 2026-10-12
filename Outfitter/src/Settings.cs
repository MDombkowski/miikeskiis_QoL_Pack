using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace Outfitter
{
    /// <summary>The three ways to put a loadout on (his words, 2026-09-29).</summary>
    internal enum EquipMode
    {
        FullyReplace,
        SwapGearOnly,
        SwapGearAndAddTheRest,
    }

    /// <summary>What happens when some of a loadout isn't nearby.</summary>
    internal enum MissingItems
    {
        Ask,
        EquipWhatsHere,
        Cancel,
    }

    /// <summary>How the missing-items question lists each item (card B21, his words of 2026-10-11: "show the icon and
    /// allow mouse-over hover to reveal the name of the item").</summary>
    internal enum MissingListLook
    {
        /// <summary>Its icon, how many and why; its name when the pointer is over the icon.</summary>
        IconNameOnHover,

        /// <summary>Its icon, its name, how many and why.</summary>
        IconAndName,
    }

    /// <summary>What happens to what he wears that the loadout he switches to doesn't wear (card B18, his feedback of
    /// 2026-10-11: "It needs to unequip equipped items when I do the switch to a new loadout").</summary>
    internal enum WornNotInLoadout
    {
        /// <summary>It comes off, whether or not a chest takes it: what no chest takes stays in his inventory.</summary>
        ComesOff,

        /// <summary>0.5's way (card A34, decisions 37 and 47): it comes off only when a chest takes it or the loadout puts
        /// something in its place.</summary>
        ComesOffOnlyIntoAChest,
    }

    /// <summary>What happens to a belt or trinket he wears that the loadout doesn't name (card B18, the review: a belt
    /// coming off away from chests takes its carry weight with it).</summary>
    internal enum WornUtilityNotInLoadout
    {
        /// <summary>0.5's way: it comes off only when a chest takes it or the loadout puts one in its place.</summary>
        KeepWearing,

        /// <summary>As "What I wear that the loadout doesn't" at ComesOff: it comes off whether or not a chest takes it.</summary>
        ComesOff,
    }

    /// <summary>How long putting a piece on takes.</summary>
    internal enum EquipTime
    {
        Instant,
        TheGamesOwn,
    }

    /// <summary>What Outfitter says when an equip is done.</summary>
    internal enum ResultMessage
    {
        OneLine,
        Detailed,
    }

    /// <summary>Which chests Outfitter takes from and puts into.</summary>
    internal enum WhichChests
    {
        AnyChestICanOpen,
        OnlyChestsIBuilt,
    }

    /// <summary>Whether to leave one of each stackable item in every chest.</summary>
    internal enum LeaveOne
    {
        FollowAzuCraftyBoxes,
        Always,
        Never,
    }

    /// <summary>Which chests a loadout's items come from.</summary>
    internal enum FetchFrom
    {
        SetUpChestsFirst,
        SetUpChestsOnly,
        AnyChest,
    }

    /// <summary>Which qualities of a piece of gear count as the loadout's.</summary>
    internal enum GearQuality
    {
        SavedOrBetter,
        Exact,
        Any,
    }

    /// <summary>Which copy of a piece of gear comes first, among copies of the same quality.</summary>
    internal enum PreferCopy
    {
        LeastWorn,
        MostWorn,
    }

    /// <summary>How "topping up" counts a loadout's stackable items.</summary>
    internal enum TopUp
    {
        PerItem,
        PerSavedStack,
    }

    /// <summary>What happens to more of an item than the loadout's count.</summary>
    internal enum MoreThanCount
    {
        PutAwayInFullyReplace,
        Keep,
        PutAway,
    }

    /// <summary>Whether items go back to the cells they were saved in.</summary>
    internal enum PlacesKept
    {
        AsSaved,
        Anywhere,
    }

    /// <summary>What happens when an item's saved cell holds something else.</summary>
    internal enum CellTaken
    {
        MoveTheOtherItem,
        UseTheNextFreeCell,
    }

    /// <summary>Where an item saved in one of EquipmentAndQuickSlots' quick slots goes when that mod isn't running.</summary>
    internal enum QuickSlotItemsWithout
    {
        FirstFreeMainCell,
        HotbarIfFree,
    }

    /// <summary>How the cells to save are picked.</summary>
    internal enum PickingCells
    {
        Both,
        HeldKeyAndClicks,
        ChooseCellsInTheWindow,
    }

    /// <summary>Where saved loadouts are kept.</summary>
    internal enum KeptWhere
    {
        WithTheCharacter,
        OnThisPC,
    }

    /// <summary>How big the window is drawn, over the game's GUI scale (Add to Cart's sizes: 1, 1.25, 1.5).</summary>
    internal enum WindowSize
    {
        Small,
        Medium,
        Large,
    }

    /// <summary>Which panel beside the window shows when the window opens (Always: the nearby items; job 30, Session 3
    /// added his inventory).</summary>
    internal enum DrawerOpens
    {
        AsILeftIt,
        Always,
        Never,
        AlwaysMyInventory,
    }

    /// <summary>What an equip does with the favourite gear he wears and carries (his words, 2026-10-06: "a config option
    /// that permits the Outfitter to override 'Favorites' items not being moved/unequipped/stored").</summary>
    internal enum FavouriteGear
    {
        Swaps,
        StaysWithMe,
    }

    /// <summary>How many of a stackable dragged into a loadout it asks for.</summary>
    internal enum DraggedStack
    {
        OneFullStack,
        One,
        WhatTheChestsHold,
    }

    /// <summary>Whether gear dragged into a loadout is worn.</summary>
    internal enum DraggedGear
    {
        WhenNothingElseIsWornThere,
        Never,
    }

    /// <summary>What a left click on a drawer item does.</summary>
    internal enum DrawerClick
    {
        FirstFreeCell,
        Nothing,
    }

    /// <summary>
    /// The player's settings (card A34's table, and what the build added; his rule 3). Alone, the mod keeps them in
    /// BepInEx\config\modprojects.outfitter.cfg, General and one section per part; inside miikeskii's QoL Pack, in the
    /// bundle's file, "Outfitter" and "Outfitter - &lt;part&gt;". A configuration manager's window (F1) lists them all under
    /// the one heading, each part behind a title row that opens and closes it (Folds; his look at F1, 2026-09-28). Every
    /// one is read when it is used, so a change works at once. Out of the box nothing a friend already sees changes:
    /// Outfitter adds a window behind a key of its own and a pick with Right Alt in his inventory (MODULES.md rule 4).
    /// Loadout Stands' section (job 30, src\Stands) is bound after these, with orders 299 down to 100.
    /// </summary>
    internal static class Settings
    {
        internal const string WindowPart = "Window and keys";
        internal const string EquipPart = "Equipping";
        internal const string ChestsPart = "Chests";
        internal const string CopyPart = "Which copy";
        internal const string PlacesPart = "Places";
        internal const string SavingPart = "Saving and codes";

        /// <summary>The orders Outfitter's own sections use, highest first.</summary>
        internal const int OwnTop = 990, OwnBottom = 300;

        /// <summary>The orders kept for Loadout Stands' section (job 30), highest first.</summary>
        internal const int StandsTop = 299, StandsBottom = 100;

        /// <summary>AzuCraftyBoxes' plugin ID, for "Follow AzuCraftyBoxes".</summary>
        private const string AzuCraftyBoxes = "Azumatt.AzuCraftyBoxes";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        // Window and keys
        internal static ConfigEntry<KeyboardShortcut> WindowKey { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> PickKey { get; private set; }
        internal static ConfigEntry<PickingCells> Picking { get; private set; }
        internal static ConfigEntry<WindowSize> Size { get; private set; }
        internal static ConfigEntry<bool> Drawer { get; private set; }
        internal static ConfigEntry<DrawerOpens> DrawerOpening { get; private set; }
        internal static ConfigEntry<bool> DrawerCarried { get; private set; }
        internal static ConfigEntry<DraggedStack> StackDragged { get; private set; }
        internal static ConfigEntry<DraggedGear> GearDragged { get; private set; }
        internal static ConfigEntry<DrawerClick> DrawerClicks { get; private set; }
        internal static ConfigEntry<bool> InventoryPanel { get; private set; }
        internal static ConfigEntry<bool> HoverHelp { get; private set; }

        // Equipping
        internal static ConfigEntry<EquipMode> Mode { get; private set; }
        internal static ConfigEntry<bool> AskMode { get; private set; }
        internal static ConfigEntry<MissingItems> Missing { get; private set; }
        internal static ConfigEntry<MissingListLook> MissingList { get; private set; }
        internal static ConfigEntry<bool> KeepWornWhenMissing { get; private set; }
        internal static ConfigEntry<WornNotInLoadout> NotWorn { get; private set; }
        internal static ConfigEntry<WornUtilityNotInLoadout> UtilityNotWorn { get; private set; }
        internal static ConfigEntry<FavouriteGear> GearFavourites { get; private set; }

        /// <summary>Each loadout's favourites switch out of the box (his words, 2026-10-07: "a config option (by default
        /// enabled) that overrides all favorited items 'stickiness'", the switch beside the modes per loadout).</summary>
        internal static ConfigEntry<bool> FavouritesGo { get; private set; }

        /// <summary>How many of its preferred food a slot that puts its stat first needs nearby (his words, 2026-10-07:
        /// "prioritize the stat as long as at least X are available").</summary>
        internal static ConfigEntry<int> FoodStatAtLeast { get; private set; }
        internal static ConfigEntry<EquipTime> Timing { get; private set; }
        internal static ConfigEntry<bool> Effects { get; private set; }
        internal static ConfigEntry<int> WaitSeconds { get; private set; }
        internal static ConfigEntry<bool> Confirm { get; private set; }
        internal static ConfigEntry<bool> WeightWarning { get; private set; }
        internal static ConfigEntry<ResultMessage> Result { get; private set; }

        // Chests
        internal static ConfigEntry<float> Range { get; private set; }
        internal static ConfigEntry<WhichChests> Chests { get; private set; }

        internal static ConfigEntry<LeaveOne> LeaveOneInChests { get; private set; }
        internal static ConfigEntry<FetchFrom> Fetch { get; private set; }
        internal static ConfigEntry<bool> PutSetUpFirst { get; private set; }
        internal static ConfigEntry<bool> PutOverflow { get; private set; }
        internal static ConfigEntry<bool> PutOthers { get; private set; }
        internal static ConfigEntry<bool> PutAnyChest { get; private set; }
        internal static ConfigEntry<bool> KeepHeld { get; private set; }
        internal static ConfigEntry<bool> LeaveOthersMarked { get; private set; }
        internal static ConfigEntry<bool> CartsAndShips { get; private set; }

        // Which copy
        internal static ConfigEntry<GearQuality> Quality { get; private set; }
        internal static ConfigEntry<bool> MatchStyle { get; private set; }
        internal static ConfigEntry<bool> ExactItem { get; private set; }
        internal static ConfigEntry<PreferCopy> Prefer { get; private set; }
        internal static ConfigEntry<bool> AmmoIsGear { get; private set; }
        internal static ConfigEntry<bool> ToolsAreGear { get; private set; }
        internal static ConfigEntry<bool> TorchesAreGear { get; private set; }
        internal static ConfigEntry<TopUp> TopUps { get; private set; }
        internal static ConfigEntry<MoreThanCount> Extra { get; private set; }
        internal static ConfigEntry<bool> LeaveFavourites { get; private set; }

        // Places
        internal static ConfigEntry<PlacesKept> Places { get; private set; }
        internal static ConfigEntry<CellTaken> Taken { get; private set; }
        internal static ConfigEntry<QuickSlotItemsWithout> QuickSlotsWithout { get; private set; }
        internal static ConfigEntry<bool> QuickSlotsInFullyReplace { get; private set; }

        // Saving and codes
        internal static ConfigEntry<bool> SaveOnlyPicked { get; private set; }
        internal static ConfigEntry<KeptWhere> Kept { get; private set; }
        internal static ConfigEntry<bool> HiddenAsWorn { get; private set; }
        internal static ConfigEntry<bool> ShareCodes { get; private set; }
        internal static ConfigEntry<bool> CodesCarryPlaces { get; private set; }

        /// <summary>How many of a food a new food slot brings (card A34, decision 72): read by the window as it makes one.</summary>
        internal static ConfigEntry<int> FoodSlotCount { get; private set; }

        /// <summary>Goes up whenever any setting changes, so whatever was worked out from them is worked out again.</summary>
        internal static int Version { get; private set; }

        internal static void Changed() => Version++;

        internal static void Bind(ModuleSettings settings)
        {
            Enabled = settings.Enabled;
            Enabled.SettingChanged += (sender, args) => Changed();
            // The chest settings every mod of the pack shares (card A69): the range, "Chests beyond the server's reach"
            // (which was this mod's own setting since card A64) and the log lines.
            QoLMods.Chests.ChestSettings.Bind(settings);
            var b = new SettingsBinder(settings, OwnTop, OwnBottom);

            b.BeginFold(WindowPart);
            WindowKey = b.Add(WindowPart, "WindowKey", new KeyboardShortcut(KeyCode.L), "Window key",
                "Opens and closes Outfitter's window: your loadouts, the one you pick in its places, and Equip. Escape closes " +
                "it too. Neither the game nor the pack's other mods use L.");
            PickKey = b.Add(WindowPart, "PickKey", new KeyboardShortcut(KeyCode.RightAlt), "Pick cells: hold this and click",
                "With your inventory open, hold this key and left-click a cell to pick it for saving (again to unpick it). " +
                "Right Alt is free: Shift, Ctrl and Left Alt clicks are the game's, MyLittleUI's and StoreStash's. None: no " +
                "key; pick in the window instead.");
            Picking = b.Add(WindowPart, "Picking", PickingCells.Both, "Picking the cells to save",
                "Both: the key above, and the window's \"Highlight cells\" in its My inventory panel (while it's on, a left " +
                "click on a cell of your inventory there highlights it). HeldKeyAndClicks: only the key. " +
                "ChooseCellsInTheWindow: only the window. Highlighting a loadout's own cells (to copy them) works either way.");
            Size = b.Add(WindowPart, "WindowSize", WindowSize.Medium, "Window size",
                "How big the window is drawn, over the game's own GUI scale, as Add to Cart's sizes. Small: as 0.2.0 drew it. " +
                "Medium (the default): a quarter bigger. Large: half as big again. Medium and Large are kept to what fits your " +
                "screen (with the drawer below open, both together). The text stays sharp at every size. Changes at once.");
            Drawer = b.Add(WindowPart, "Drawer", true, "Nearby items drawer",
                "On: \"Show nearby items\" on the window opens a panel beside it, like Add to Cart's Take tab: every item in the " +
                "chests nearby (and what you carry, below), to drag into a loadout's cells and build it from what your base " +
                "holds. Off: no button, no panel.");
            DrawerOpening = b.Add(WindowPart, "DrawerOpens", DrawerOpens.AsILeftIt, "The panel beside the window opens with",
                "Which panel shows beside the window when it opens. AsILeftIt: the one that showed when you last closed it " +
                "(or none). Always: the nearby items. AlwaysMyInventory: your inventory. Never: none.");
            DrawerCarried = b.Add(WindowPart, "DrawerCarried", true, "The drawer lists what I carry",
                "On: what you carry shows in the drawer too, under a heading of its own, beside what the chests nearby hold.");
            StackDragged = b.Add(WindowPart, "StackDragged", DraggedStack.OneFullStack, "A stackable dragged in comes as",
                "OneFullStack: as many as one stack of it holds (50 wood, 10 soups). One: one, for you to set (the scroll " +
                "wheel over its cell, or the box beside the grid). WhatTheChestsHold: what the chests nearby and you hold of " +
                "it, up to one stack.");
            GearDragged = b.Add(WindowPart, "GearDragged", DraggedGear.WhenNothingElseIsWornThere, "Gear dragged in goes on",
                "WhenNothingElseIsWornThere: armour, a cape, a belt or a weapon dragged into a loadout is worn, unless the " +
                "loadout already wears something there (the game's own rules for hands). Never: it's carried; mark it worn " +
                "beside the grid. Into one of EquipmentAndQuickSlots' gear slots it's always worn.");
            DrawerClicks = b.Add(WindowPart, "DrawerClick", DrawerClick.FirstFreeCell, "Left click on a drawer item",
                "FirstFreeCell: puts it in the loadout's first free cell (worn by the setting above). Nothing: only dragging " +
                "puts it in.");
            InventoryPanel = b.Add(WindowPart, "InventoryPanel", true, "My inventory panel",
                "On: \"Show my inventory\" on the window shows your own inventory beside it, as the game has it (the hotbar, " +
                "your rows, EquipmentAndQuickSlots' slots): drag a cell into the loadout (that very item goes in), or highlight " +
                "cells to copy them into a loadout or save them as a new one. Off: no button, no panel.");
            HoverHelp = b.Add(WindowPart, "HoverHelp", true, "Hover help",
                "On: pointing at one of the window's buttons for a moment shows a few words on what it does. Off: none.");
            b.EndFold();

            b.BeginFold(EquipPart);
            Mode = b.Add(EquipPart, "Mode", EquipMode.FullyReplace, "Equip mode",
                "FullyReplace: everything you carry that the loadout doesn't hold goes into the chests nearby, and the " +
                "loadout comes in its places. SwapGearOnly: only gear changes (what you wore goes away, the loadout's gear " +
                "comes); the rest of your inventory and the loadout's other items are left as they are. " +
                "SwapGearAndAddTheRest: the gear changes, and the loadout's other items come too (topped up to its counts), " +
                "while your own food and materials stay. With \"Ask the mode\" on, the window starts at this one.");
            AskMode = b.Add(EquipPart, "AskMode", true, "Ask the mode at every equip",
                "On: the window shows the three modes beside Equip, starting at the one above. Off: Equip uses the one " +
                "above without asking.");
            Missing = b.Add(EquipPart, "Missing", MissingItems.Ask, "Missing items",
                "When some of a loadout isn't in the chests nearby. Ask: a pop-up lists what's missing and offers \"Equip " +
                "what's here\". EquipWhatsHere: without asking, and says what was missing. Cancel: nothing changes, and it " +
                "says what's missing.");
            MissingList = b.Add(EquipPart, "MissingList", MissingListLook.IconNameOnHover, "Missing items in the pop-up",
                "How the pop-up lists what's missing. IconNameOnHover: each item's icon with how many and why; point at the " +
                "icon for its name. IconAndName: the icon, the name, how many and why. An item with no icon shows its name.");
            KeepWornWhenMissing = b.Add(EquipPart, "KeepWornWhenMissing", true, "Keep wearing mine when a piece is missing",
                "On: when the loadout's helmet (or any piece of gear) can't be had, you keep wearing the one you have in " +
                "that place, rather than being left without. Off: what you wore there goes away all the same.");
            NotWorn = b.Add(EquipPart, "WornNotInLoadout", WornNotInLoadout.ComesOff, "What I wear that the loadout doesn't",
                "ComesOff: when you put a loadout on, whatever you wear that it doesn't wear comes off, arrows and bolts " +
                "too (the game keeps them equipped with no bow in hand), so only the loadout's gear is on; what no chest " +
                "takes stays in your inventory. ComesOffOnlyIntoAChest: it comes off only when a chest takes it or the " +
                "loadout puts something in its place, so away from chests you keep wearing it. Belts and trinkets follow " +
                "the setting below. Either way \"Keep wearing mine when a piece is missing\" keeps yours on where the " +
                "loadout's own piece can't be had, and what never goes away stays as it is (favourites while they're left " +
                "alone, and in the swap modes what isn't gear by the switches in Which copy).");
            UtilityNotWorn = b.Add(EquipPart, "WornUtilityNotInLoadout", WornUtilityNotInLoadout.KeepWearing, "A belt or trinket the loadout doesn't name",
                "KeepWearing: a belt or trinket you wear that the loadout doesn't name comes off only when a chest takes it " +
                "or the loadout puts one in its place, so away from chests you keep its bonus (a Megingjord's carry weight). " +
                "ComesOff: it comes off like the rest, whether or not a chest takes it (the window's weight line counts the " +
                "carry weight you'd lose).");
            GearFavourites = b.Add(EquipPart, "FavouriteGear", FavouriteGear.Swaps, "My favourite gear in an equip",
                "Swaps: gear you marked as a favourite (StoreStash: Left Shift + right-click) comes off and goes away like any " +
                "other gear when a loadout replaces it (armour, weapons, and by their switches in Which copy arrows, tools and " +
                "torches), at the window and at a stand. StaysWithMe: favourite gear is one of your favourites like any other: " +
                "with \"Leave my favourites alone\" (Which copy) on, it never goes away (0.3.0's way), so a loadout's own piece " +
                "can go on only beside it. The rest of your favourites (food, materials) always follow that setting. Only " +
                "for a loadout whose \"Favourites go too\" (beside the modes in the window) is off.");
            FavouritesGo = b.Add(EquipPart, "FavouritesGo", true, "Equip modes put away my favourites",
                "On: putting a loadout on puts your favourites (StoreStash: Left Shift + right-click) away like any other " +
                "item, in all three modes: Fully replace stores every favourite the loadout doesn't hold, the swap modes " +
                "your favourite gear. This is where each loadout's own \"Favourites go too\" switch (beside the modes in " +
                "the window) starts; a loadout you've switched keeps its own. Off: favourites follow \"Leave my favourites " +
                "alone\" (Which copy) and \"My favourite gear in an equip\" above, as in 0.4.");
            FoodStatAtLeast = b.Add(EquipPart, "FoodStatAtLeast", 3, "Food slots by stat: at least",
                "A food slot set to \"Its stat\" first (its rule, beside the window) takes the food it prefers as long as " +
                "at least this many are nearby (or its whole count, if that's fewer), even when another food would fill " +
                "its count: 3 Honey glazed chickens before 10 Cooked deer meats for a slot of 10 wanting the most Health. " +
                "With fewer than this, a food that fills the count comes first. A slot set to \"Amount\" first always " +
                "fills its count first.", new AcceptableValueRange<int>(1, 100));
            Timing = b.Add(EquipPart, "EquipTime", EquipTime.Instant, "Equip time",
                "Instant: every piece is on at once (the game's own way takes about 12.5 s for a full set of armour). " +
                "TheGamesOwn: each piece goes on through the game's own queue, with its animation, one after another (a jump, a " +
                "dodge or a run clears the queue, as it does in the game); taking off stays instant.");
            Effects = b.Add(EquipPart, "Effects", true, "Equip sound and effect",
                "The game's sound and flash for putting gear on. Off quietens them, though some items still play their own " +
                "sound, as they do in the game.");
            WaitSeconds = b.Add(EquipPart, "WaitSeconds", 10, "Wait while I fight, up to (seconds)",
                "The game won't put gear on while you attack, dodge, swim, eat, or it's still changing your gear. Equip " +
                "waits until you can, up to this long, then gives up and says so.", new AcceptableValueRange<int>(0, 30));
            Confirm = b.Add(EquipPart, "Confirm", false, "Confirm before equipping",
                "On: Equip asks once more, saying what will go away and what will come, before anything moves.");
            WeightWarning = b.Add(EquipPart, "WeightWarning", true, "Weight warning",
                "The window shows what you'll carry after the equip, in red when it's over your carry weight.");
            Result = b.Add(EquipPart, "Result", ResultMessage.OneLine, "Result message",
                "OneLine: one line at the top left (what came, what went away, what's missing). Detailed: a line for each " +
                "part, and the same in the log.");
            b.EndFold();

            b.BeginFold(ChestsPart);
            // Since card A69 (2026-10-08): 0 follows the shared range ("Chests (every mod)"), the default now; a file still
            // at the old default, 150, moves to it once (ChestSettings).
            Range = b.Add(ChestsPart, "Range", 0f, "Chest range (metres)",
                "How far from you a chest may be for Outfitter to take from it and put into it. " + QoLMods.Chests.ChestSettings.OwnRangeNote,
                new AcceptableValueRange<float>(0f, QoLMods.Chests.ChestSettings.MostRange));
            settings.MovedDefault(Range, QoLMods.Chests.ChestSettings.DefaultRange, 1);
            QoLMods.Chests.ChestSettings.Reaches("Outfitter, " + ChestsPart, "Chest range (metres)", Range);   // card B13
            Chests = b.Add(ChestsPart, "Chests", WhichChests.AnyChestICanOpen, "Which chests",
                "AnyChestICanOpen: every chest you could open by hand (a friend's public chest too, never a private one or " +
                "one behind a ward that isn't yours). OnlyChestsIBuilt: only chests you built. Chests the world made, " +
                "gravestones and Reserve Stock's reserved chests are never used.");
            LeaveOneInChests = b.Add(ChestsPart, "LeaveOneInChests", LeaveOne.FollowAzuCraftyBoxes, "Leave one of each item in chests",
                "When taking a stackable item, leave one in each chest, so quick stack still knows where it belongs (never " +
                "a lone piece of gear). FollowAzuCraftyBoxes: as AzuCraftyBoxes' own \"Leave One Item\" setting says when " +
                "that mod is installed, and leave one when it isn't (as Add to Cart). Always. Never.");
            Fetch = b.Add(ChestsPart, "FetchFrom", FetchFrom.SetUpChestsFirst, "Take the loadout's items from",
                "SetUpChestsFirst: chests set up for an item (StoreStash's chest setups) before any other. SetUpChestsOnly: " +
                "only those. AnyChest: every chest alike, nearest first.");
            PutSetUpFirst = b.Add(ChestsPart, "PutSetUpFirst", true, "What goes away goes to its set-up chest first",
                "What you take off and put away goes first to the chests set up for it (an armour chest, a weapons " +
                "chest…), as StoreStash's quick stack sends it. Off: chest setups are ignored when putting away.");
            PutOverflow = b.Add(ChestsPart, "PutOverflow", true, "… what a set-up chest can't fit goes on to other chests",
                "On: when the chests set up for an item are full (or someone has them open), it goes to other chests. Off: " +
                "it stays with you.");
            PutOthers = b.Add(ChestsPart, "PutOthers", true, "What no set-up chest takes goes to chests without a setup",
                "On: an item no chest is set up for goes to chests without a setup. Off: only set-up chests are used.");
            PutAnyChest = b.Add(ChestsPart, "PutAnyChest", true, "Put away into any chest with room",
                "On: when no chest is set up for an item and none already holds it, it goes into the nearest chest with " +
                "room. Off: only into chests set up for it or already holding it; the rest stays with you. Never on the " +
                "ground.");
            KeepHeld = b.Add(ChestsPart, "KeepHeld", false, "A set-up chest also takes what it already holds",
                "On: a set-up chest also takes items its setup doesn't name, if it already holds some. Off: a set-up chest " +
                "takes only what its setup names (as StoreStash's quick stack does out of the box).");
            LeaveOthersMarked = b.Add(ChestsPart, "LeaveOthersMarked", false, "Leave chests others marked alone",
                "On: nothing is put into a chest another player marked as theirs with StoreStash (Left Alt + H). Taking " +
                "from it isn't affected.");
            CartsAndShips = b.Add(ChestsPart, "CartsAndShips", false, "Use the chests on carts and ships",
                "On: a cart's or ship's chest counts like any other (claiming it takes the cart or ship over for a moment). " +
                "Off: they're left alone. A ship someone else is aboard is always left alone.");
            b.EndFold();

            b.BeginFold(CopyPart);
            Quality = b.Add(CopyPart, "GearQuality", GearQuality.SavedOrBetter, "Gear quality",
                "SavedOrBetter: a piece of gear of the saved quality or better counts, the best one coming first. Exact: " +
                "only the saved quality. Any: any quality, the best first.");
            MatchStyle = b.Add(CopyPart, "MatchStyle", false, "Match the style",
                "On: shields and capes count only in the style that was saved.");
            ExactItem = b.Add(CopyPart, "ExactItem", false, "This exact item",
                "On: gear counts only when it's the one you saved: the same maker, quality and style, and other mods' data " +
                "on it (a magic item's enchantments, a gem's sockets). A friend's code knows no maker or data, so those count " +
                "only for your own loadouts. Off: any copy.");
            Prefer = b.Add(CopyPart, "PreferCopy", PreferCopy.LeastWorn, "Prefer the copy that's",
                "Among copies of the same quality: LeastWorn (the most durable) or MostWorn (use the worn ones up first).");
            AmmoIsGear = b.Add(CopyPart, "AmmoIsGear", true, "Arrows and bolts count as gear",
                "In the swap-gear modes, gear is armour and weapons, and these when on.");
            ToolsAreGear = b.Add(CopyPart, "ToolsAreGear", true, "Tools count as gear",
                "The hammer, hoe, cultivator and pickaxes, in the swap-gear modes.");
            TorchesAreGear = b.Add(CopyPart, "TorchesAreGear", true, "Torches count as gear",
                "Torches and lanterns held in a hand, in the swap-gear modes.");
            TopUps = b.Add(CopyPart, "TopUp", TopUp.PerItem, "Topping up counts",
                "PerItem: a loadout with 10 carrot soups tops up to 10 in all, wherever they are. PerSavedStack: each saved " +
                "stack is filled in its own cell.");
            Extra = b.Add(CopyPart, "MoreThanCount", MoreThanCount.PutAwayInFullyReplace, "More than the loadout's count",
                "When you carry more of an item than the loadout holds. PutAwayInFullyReplace: the extra goes away in " +
                "Fully replace and stays in the other modes. Keep. PutAway.");
            LeaveFavourites = b.Add(CopyPart, "LeaveFavourites", true, "Leave my favourites alone",
                "On: items you marked as favourites (StoreStash: Left Shift + right-click) never go away, in any mode; your " +
                "favourite gear follows \"My favourite gear in an equip\" (Equipping), which swaps it out of the box. Off: " +
                "every favourite goes away like any other item. Only for a loadout whose \"Favourites go too\" (beside the " +
                "modes in the window; out of the box by \"Equip modes put away my favourites\", Equipping) is off.");
            b.EndFold();

            b.BeginFold(PlacesPart);
            Places = b.Add(PlacesPart, "PlacesKept", PlacesKept.AsSaved, "Places kept",
                "AsSaved: each item goes back to the cell it was saved in (rows counted from the bottom, so a bigger " +
                "inventory keeps them in place; the hotbar stays the hotbar). Anywhere: items stay wherever they land.");
            Taken = b.Add(PlacesPart, "CellTaken", CellTaken.MoveTheOtherItem, "A saved cell is taken",
                "MoveTheOtherItem: whatever is in an item's saved cell moves to a free cell. UseTheNextFreeCell: the item " +
                "goes to the next free cell instead.");
            QuickSlotsWithout = b.Add(PlacesPart, "QuickSlotsWithout", QuickSlotItemsWithout.FirstFreeMainCell,
                "Quick-slot items without EquipmentAndQuickSlots",
                "Where an item saved in one of EquipmentAndQuickSlots' quick slots goes while that mod isn't running. " +
                "FirstFreeMainCell. HotbarIfFree: a free hotbar cell first.");
            QuickSlotsInFullyReplace = b.Add(PlacesPart, "QuickSlotsInFullyReplace", false, "Fully replace puts away quick-slot items",
                "On: Fully replace also puts away what sits in EquipmentAndQuickSlots' quick slots when the loadout doesn't " +
                "hold it. Off: what's in them stays (a loadout's own quick-slot item still takes its slot).");
            b.EndFold();

            b.BeginFold(SavingPart);
            SaveOnlyPicked = b.Add(SavingPart, "SaveOnlyPicked", true, "Save only the picked cells",
                "On: a loadout holds the cells you picked. Off: Save takes everything you carry and wear.");
            Kept = b.Add(SavingPart, "KeptWhere", KeptWhere.WithTheCharacter, "Where loadouts are kept",
                "WithTheCharacter: in the character's own file, so they go with it to every world and server. OnThisPC: in " +
                "a file on this PC (BepInEx\\config\\modprojects.outfitter.loadouts.txt), the same for all your characters.");
            HiddenAsWorn = b.Add(SavingPart, "HiddenAsWorn", true, "Weapons put away count as worn",
                "On: weapons you had put away (R) when saving are saved as held, so the loadout puts them in your hands.");
            ShareCodes = b.Add(SavingPart, "ShareCodes", true, "Share codes",
                "Copy a loadout as a code to paste to a friend (it goes to the clipboard), and paste a friend's code as a " +
                "new loadout. A code is only a list: it never makes an item.");
            CodesCarryPlaces = b.Add(SavingPart, "CodesCarryPlaces", true, "Codes carry each item's place",
                "On: a code says where each item sits. Off: only what and how many.");
            FoodSlotCount = b.Add(SavingPart, "FoodSlotCount", 10, "Food slots: count at first",
                "How many of its food a new food slot brings (the window's \"Make a food slot\" on an empty cell; a food made " +
                "into a slot keeps its own count). Change a slot's count beside the grid. At most one stack of the food " +
                "comes, whatever the count.", new AcceptableValueRange<int>(1, 100));
            b.EndFold();

            Stands.Stands.Bind(new SettingsBinder(settings, StandsTop, StandsBottom));
        }

        /// <summary>The chests Outfitter may use, as its settings say now.</summary>
        internal static QoLMods.Chests.ChestRules ChestRules() => new QoLMods.Chests.ChestRules
        {
            Range = QoLMods.Chests.ChestSettings.RangeOf(Range),
            OnlyBuiltByMe = Chests.Value == WhichChests.OnlyChestsIBuilt,
            LeaveOne = LeavesOne(),
            Skip = CartsAndShips.Value ? null : (System.Func<Container, bool>)OnVehicle,
        };

        /// <summary>A chest on a cart or a ship.</summary>
        internal static bool OnVehicle(Container chest) =>
            chest != null && (chest.GetComponentInParent<Vagon>() != null || chest.GetComponentInParent<Ship>() != null);

        /// <summary>Whether to leave one of each item, following AzuCraftyBoxes' own setting when asked to (as Add to
        /// Cart does).</summary>
        private static bool LeavesOne()
        {
            switch (LeaveOneInChests.Value)
            {
                case LeaveOne.Always:
                    return true;
                case LeaveOne.Never:
                    return false;
            }
            if (!Chainloader.PluginInfos.TryGetValue(AzuCraftyBoxes, out BepInEx.PluginInfo azu) || azu.Instance == null)
            {
                return true;   // not installed: leave one, as the setting's description says
            }
            // Its setting "Leave One Item" (section "2 - CraftyBoxes"), an On/Off toggle; read by key, never written.
            ConfigEntryBase entry = azu.Instance.Config.Keys
                .Where(key => key.Key == "Leave One Item")
                .Select(key => azu.Instance.Config[key])
                .FirstOrDefault();
            return entry == null || entry.BoxedValue?.ToString() != "Off";
        }
    }
}
