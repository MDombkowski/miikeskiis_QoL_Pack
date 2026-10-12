using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using QoLMods.Catalogue;
using QoLMods.Chests;
using QoLMods.Kit;
using UnityEngine;

namespace AddToCart
{
    /// <summary>Which chests the cart may take from.</summary>
    internal enum WhichChests
    {
        AnyChestICanOpen,
        OnlyChestsIBuilt,
    }

    /// <summary>Whether to leave one of each item in every chest.</summary>
    internal enum LeaveOne
    {
        FollowAzuCraftyBoxes,
        Always,
        Never,
    }

    /// <summary>How a tab groups what it shows when it first opens (and always, with "Remember each tab" off).</summary>
    internal enum DefaultGroup
    {
        Auto,
        Biome,
        None,
    }

    /// <summary>How a tab sorts what it shows when it first opens (and always, with "Remember each tab" off).</summary>
    internal enum DefaultSort
    {
        Progression,
        Name,
    }

    /// <summary>When the thumbnails of items that fit an item stand carry Item Stand Icons' mark.</summary>
    internal enum StandMarks
    {
        AsItemStandIcons,
        Always,
        Off,
    }

    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.addtocart.cfg, in the section
    /// General; inside miikeskii's QoL Pack, in the bundle's file, in the section "Add to Cart". Every one takes effect
    /// at once: the mod reads them each time it counts, fetches or draws the window.
    ///
    /// 0.3.0 retired the setting "Sort" (0.2.0's one sort for every tab): each tab now keeps its own Group and Sort, and
    /// Biome became a way to group. Its line may stay in an old settings file, where it does nothing.
    /// </summary>
    internal static class Settings
    {
        /// <summary>AzuCraftyBoxes' plugin ID, for "Follow AzuCraftyBoxes".</summary>
        private const string AzuCraftyBoxes = "Azumatt.AzuCraftyBoxes";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> OpenKey { get; private set; }

        internal static ConfigEntry<float> Range { get; private set; }

        internal static ConfigEntry<WhichChests> Chests { get; private set; }

        internal static ConfigEntry<LeaveOne> LeaveOneInChests { get; private set; }

        internal static ConfigEntry<bool> LeaveOneOfGear { get; private set; }

        internal static ConfigEntry<bool> TrueCounts { get; private set; }

        internal static ConfigEntry<bool> TrueCountsSwitch { get; private set; }

        internal static ConfigEntry<bool> OnlyWhatImShort { get; private set; }

        internal static ConfigEntry<bool> ListPlantings { get; private set; }

        internal static ConfigEntry<WindowSize> Size { get; private set; }

        internal static ConfigEntry<UnavailableLook> Unavailable { get; private set; }

        internal static ConfigEntry<float> Opacity { get; private set; }

        internal static ConfigEntry<bool> FavoritesFirst { get; private set; }

        internal static ConfigEntry<float> ScrollRows { get; private set; }

        internal static ConfigEntry<bool> ExpandableFamilies { get; private set; }

        internal static ConfigEntry<bool> RememberEachTab { get; private set; }

        internal static ConfigEntry<DefaultGroup> GroupAtFirst { get; private set; }

        internal static ConfigEntry<DefaultSort> SortAtFirst { get; private set; }

        internal static ConfigEntry<string> CategoryOrder { get; private set; }

        internal static ConfigEntry<string> MaterialOrder { get; private set; }

        internal static ConfigEntry<string> KindOrder { get; private set; }

        internal static ConfigEntry<string> StationOrder { get; private set; }

        internal static ConfigEntry<string> FamilyOrder { get; private set; }

        internal static ConfigEntry<bool> LocateClearsAll { get; private set; }

        internal static ConfigEntry<StandMarks> ItemStandMarks { get; private set; }

        internal static ConfigEntry<bool> StandFilter { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first. Keys never change once released; new ones get new keys.
            Enabled = settings.Enabled;

            // The chest settings every mod of the pack shares (card A69): the range, "Chests beyond the server's reach"
            // (which was this mod's own setting since card A64) and the log lines.
            ChestSettings.Bind(settings);

            OpenKey = settings.Bind(ModuleSettings.General, "OpenKey", new KeyboardShortcut(KeyCode.K),
                "Key to open the cart", 23,
                "Opens and closes the Add to Cart window. Escape closes it too. The game itself doesn't use K.");

            // Since card A69 (2026-10-08): 0 follows the shared range ("Chests (every mod)"), the default now; a file still
            // at the old default, 150, moves to it once (ChestSettings).
            Range = settings.Bind(ModuleSettings.General, "Range", 0f,
                "Chest range (metres)", 22,
                "How far from you a chest may be for the cart to count and take from it. " + ChestSettings.OwnRangeNote,
                new AcceptableValueRange<float>(0f, ChestSettings.MostRange));
            settings.MovedDefault(Range, ChestSettings.DefaultRange, 2);
            ChestSettings.Reaches("Add to Cart", "Chest range (metres)", Range);   // the list in "Chests (every mod)" (card B13)

            Chests = settings.Bind(ModuleSettings.General, "Chests", WhichChests.AnyChestICanOpen,
                "Which chests", 21,
                "AnyChestICanOpen: every chest you could open by hand (a friend's public chest too, never a private one " +
                "or one behind a ward that isn't yours). OnlyChestsIBuilt: only chests, carts and ships you built. " +
                "Chests the world made itself, and gravestones, are never used.");

            LeaveOneInChests = settings.Bind(ModuleSettings.General, "LeaveOneInChests", LeaveOne.FollowAzuCraftyBoxes,
                "Leave one of each item in chests", 20,
                "Leaves one of each item that stacks in every chest, so quick stack still knows which chest it belongs in. " +
                "Gear (anything that doesn't stack: armour, weapons, tools, shields) is never kept back, unless \"Leave one " +
                "of gear too\" says so. FollowAzuCraftyBoxes: as AzuCraftyBoxes' own \"Leave One Item\" setting says when " +
                "that mod is installed, and leave one when it isn't. Always. Never.");

            // Defaults moved to his own settings (card A63, 2026-10-06): a file still at an old default moves once (the kit).
            OnlyWhatImShort = settings.Bind(ModuleSettings.General, "OnlyWhatImShort", false,
                "Take only what I'm short", 19,
                "On: a fetch takes only what you aren't already carrying. Off (the default): it takes the cart's full " +
                "amount, whatever you carry.");
            settings.MovedDefault(OnlyWhatImShort, true, 1);

            ListPlantings = settings.Bind(ModuleSettings.General, "ListPlantings", false,
                "List plantings", 18,
                "On: the Build tab also lists the cultivator's plantings (crops and saplings, one seed each), for " +
                "fetching the seeds for a field. Off: they're left out. Counts the next time the window opens.");

            Size = settings.Bind(ModuleSettings.General, "WindowSize", WindowSize.Large,
                "Window size", 17,
                "How big the window is drawn, over the game's own GUI scale. Large (the default): a quarter bigger. Normal: as 0.3.0 drew it. " +
                "ExtraLarge: half as big again. Large and ExtraLarge are kept to what fits your screen: on a 16:9 screen at " +
                "the game's default GUI scale, ExtraLarge comes out about 38% bigger; with the game's GUI scale set " +
                "higher, the window starts bigger and less is added. The text stays sharp at every size. Changes at once.");

            Unavailable = settings.Bind(ModuleSettings.General, "UnavailableThumbnails", UnavailableLook.GreyAndFaded,
                "Thumbnails the chests can't cover", 16,
                "How a thumbnail looks when the chests can't cover one more of it. GreyAndFaded: grey, and faded to the " +
                "opacity below. Grey: grey, not faded. Faded: faded to the opacity below, in colour.");

            Opacity = settings.Bind(ModuleSettings.General, "UnavailableOpacity", Looks.DefaultOpacity,
                "Opacity of thumbnails the chests can't cover", 15,
                "How much of a thumbnail the chests can't cover still shows, with GreyAndFaded or Faded: 0.9 almost " +
                "all of it, 0.05 almost none. 0.075 by default.",
                new AcceptableValueRange<float>(Looks.LeastOpacity, Looks.MostOpacity));
            settings.MovedDefault(Opacity, Looks.OldDefaultOpacity, 1);

            FavoritesFirst = settings.Bind(ModuleSettings.General, "FavoritesFirst", true,
                "Favorites first", 14,
                "On: the favorites among what a tab shows come first, under a heading of their own. The window's " +
                "\"First\" switch changes this too. Middle-click a thumbnail to star it and put it in your lists; " +
                "your favorites and lists are the game's own, the same as in the build menu.");

            ScrollRows = settings.Bind(ModuleSettings.General, "ScrollRows", 2f,
                "Scroll speed (rows per notch)", 13,
                "How far one notch of the mouse wheel scrolls the window's lists, in rows of thumbnails.",
                new AcceptableValueRange<float>(0.5f, 10f));

            ExpandableFamilies = settings.Bind(ModuleSettings.General, "ExpandableFamilies", true,
                "Expandable families", 12,
                "On: in the list on the left, families (and Building Structures) open to show their members, one click " +
                "away. Off: only the families show, and their members appear as headings when one is picked.");

            RememberEachTab = settings.Bind(ModuleSettings.General, "RememberEachTab", true,
                "Remember each tab's group and sort", 11,
                "On: each tab keeps its own Group and Sort, while you play and between sessions (kept with your " +
                "character). Off: every tab opens on the defaults below.");

            GroupAtFirst = settings.Bind(ModuleSettings.General, "DefaultGroup", DefaultGroup.Auto,
                "Default group", 10,
                "How a tab groups what it shows at first. Auto: headings that follow what you pick on the left (a " +
                "family's members, what walls are made of …). Biome: a heading per biome. None: no headings.");

            SortAtFirst = settings.Bind(ModuleSettings.General, "DefaultSort", DefaultSort.Progression,
                "Default sort", 9,
                "How a tab sorts what it shows at first. Progression: in the order you unlock things, Meadows to the " +
                "Deep North, then the game's own order. Name: A to Z.");

            const string orderNote = " Names separated by commas; a family's members follow it in brackets. Names left " +
                                     "out go at the end, and names the mod doesn't know are ignored. Empty: the default.";

            CategoryOrder = settings.Bind(ModuleSettings.General, "BuildCategoryOrder", Orders.DefaultCategoryLine,
                "Build category order", 8,
                "The order of the Build tab's categories in the list on the left, and of their headings." + orderNote);

            MaterialOrder = settings.Bind(ModuleSettings.General, "BuildMaterialOrder", Orders.DefaultMaterialLine,
                "Build material order", 7,
                "The order of the Build tab's materials in the list on the left, and of their headings." + orderNote);

            KindOrder = settings.Bind(ModuleSettings.General, "CraftKindOrder", Orders.DefaultKindLine,
                "Craft kind order", 6,
                "The order of the Craft tab's kinds in the list on the left, and of their headings." + orderNote);

            StationOrder = settings.Bind(ModuleSettings.General, "CraftStationOrder", Orders.DefaultStationLine,
                "Craft station order", 5,
                "The order of the Craft tab's stations (Made At) in the list on the left, and of their headings: by " +
                "default the order you can first build them." + orderNote);

            FamilyOrder = settings.Bind(ModuleSettings.General, "TakeFamilyOrder", Orders.DefaultFamilyLine,
                "Take family order", 4,
                "The order of the Take tab's families and their members in the list on the left, and of their " +
                "headings." + orderNote);

            LocateClearsAll = settings.Bind(ModuleSettings.General, "LocateClearsAll", true,
                "Locate: opening one chest clears every label", 3,
                "On (the default): when several chests hold the item you're locating, opening any one of them clears " +
                "every label. Off: opening a chest clears only its own label.");
            settings.MovedDefault(LocateClearsAll, false, 1);

            // Nothing new out of the box: Item Stand Icons is off until switched on (card A63; its "Show the marks" is then on).
            ItemStandMarks = settings.Bind(ModuleSettings.General, "ItemStandMarks", StandMarks.AsItemStandIcons,
                "Item stand marks", 2,
                "A small mark on the thumbnails of items that fit an item stand, as Item Stand Icons draws on item icons: " +
                "an up arrow for the wall stand, a right arrow for the flat one, both arrows for both, and a mannequin " +
                "for the armor stand. AsItemStandIcons: whenever Item Stand Icons shows its marks, in its look and by its " +
                "rule (so none while Item Stand Icons is off, as it is out of the box; its \"Only while aiming\" is never for these " +
                "thumbnails). Always: whatever Item Stand Icons does, in its look if it's installed, else a plain one. Off: " +
                "never.");

            // Off out of the box (MODULES.md rule 4), like the marks: a new heading in the list on the left.
            StandFilter = settings.Bind(ModuleSettings.General, "ItemStandFilter", false,
                "Item stand filter", 1,
                "Off (the default). On: on the Craft and Take tabs the list on the left gets a heading, FITS A STAND, with " +
                "a row for everything that fits a stand, then one for the wall stand, the flat stand and the armor " +
                "stand: click one to browse those items together, as any other row. Only rows with something in them " +
                "show. The same rule as the marks: Item Stand Icons' where it's installed, else the game's own. On or " +
                "off, the marks above are their own setting.");

            // Card A83 (2026-10-09, his words): gear spared by leave-one, and true counts with a question before the last
            // one of anything goes. Below every other setting: every order above is taken (0 is the kit's hidden one).
            LeaveOneOfGear = settings.Bind(ModuleSettings.General, "LeaveOneOfGear", false,
                "Leave one of gear too", -1,
                "Off (the default): \"Leave one of each item in chests\" keeps back only things that stack, so a chest's only " +
                "helmet, sword, tool or shield (anything that doesn't stack) can always be taken. On: one of each piece of " +
                "gear stays in each chest as well. Arrows, bombs and the like stack, so they follow \"Leave one of each item " +
                "in chests\" either way.");

            TrueCounts = settings.Bind(ModuleSettings.General, "TrueCounts", false,
                "True counts", -2,
                "Off (the default): every count leaves out the one \"Leave one of each item in chests\" keeps back in each " +
                "chest. On: every count is the whole count, the last ones included, on every tab, and a fetch that would " +
                "take the last one of something from a chest asks first: take it anyway, take all but one, or cancel. The " +
                "switch \"True counts\" on the Take tab changes this too. With leave one off, every count is whole already.");

            TrueCountsSwitch = settings.Bind(ModuleSettings.General, "TrueCountsSwitch", true,
                "True counts switch on the Take tab", -3,
                "On (the default): the Take tab shows the switch \"True counts\" beside Locate. Off: it's hidden, and the " +
                "setting \"True counts\" above decides alone.");
        }

        /// <summary>The list orders as the settings say right now.</summary>
        internal static Orders ListOrders() => Orders.From(CategoryOrder.Value, MaterialOrder.Value, KindOrder.Value,
                                                                           StationOrder.Value, FamilyOrder.Value);

        /// <summary>The chest rules as the settings say right now.</summary>
        internal static ChestRules Rules() => new ChestRules
        {
            Range = ChestSettings.RangeOf(Range),
            OnlyBuiltByMe = Chests.Value == WhichChests.OnlyChestsIBuilt,
            LeaveOne = LeavesOne(),
            LeaveOneOfGear = LeaveOneOfGear.Value,
        };

        /// <summary>Whether to leave one of each item, following AzuCraftyBoxes' own setting when asked to.</summary>
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
