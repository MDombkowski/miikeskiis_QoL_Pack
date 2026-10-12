using System.Collections.Generic;
using BepInEx.Configuration;
using QoLMods.ItemStands;
using QoLMods.Kit;
using QoLMods.ListPicker;
using UnityEngine;

namespace ItemStandIcons
{
    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.itemstandicons.cfg, in the section
    /// General; inside miikeskii's QoL Pack, in the bundle's file, in the section "Item Stand Icons". Every one takes
    /// effect at once: the marks are drawn from them every frame they show. Their keys are the item stands part's
    /// (StandShare), which Add to Cart reads to show the marks as they are set here.
    /// </summary>
    internal static class Settings
    {
        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<bool> ShowMarks { get; private set; }

        internal static ConfigEntry<bool> MarkWall { get; private set; }

        internal static ConfigEntry<bool> MarkFlat { get; private set; }

        internal static ConfigEntry<bool> MarkBoth { get; private set; }

        internal static ConfigEntry<bool> MarkArmor { get; private set; }

        internal static ConfigEntry<MarkLook> Look { get; private set; }

        internal static ConfigEntry<BothShape> BothShape { get; private set; }

        internal static ConfigEntry<Color> Colour { get; private set; }

        internal static ConfigEntry<bool> Outline { get; private set; }

        internal static ConfigEntry<int> Size { get; private set; }

        internal static ConfigEntry<MarkPlace> Place { get; private set; }

        internal static ConfigEntry<int> NudgeRight { get; private set; }

        internal static ConfigEntry<int> NudgeUp { get; private set; }

        internal static ConfigEntry<MarkPlace> ArmorPlace { get; private set; }

        internal static ConfigEntry<int> ArmorNudgeRight { get; private set; }

        internal static ConfigEntry<int> ArmorNudgeUp { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> HoldKey { get; private set; }

        internal static ConfigEntry<bool> WhileAiming { get; private set; }

        internal static ConfigEntry<AimPlaces> AimCovers { get; private set; }

        internal static ConfigEntry<AimMarks> AimShows { get; private set; }

        internal static ConfigEntry<bool> InInventory { get; private set; }

        internal static ConfigEntry<bool> InChests { get; private set; }

        internal static ConfigEntry<bool> OnHotbar { get; private set; }

        internal static ConfigEntry<bool> InCraftingList { get; private set; }

        internal static ConfigEntry<bool> OnChosenRecipe { get; private set; }

        internal static ConfigEntry<bool> OnIngredients { get; private set; }

        internal static ConfigEntry<bool> AtTrader { get; private set; }

        internal static ConfigEntry<bool> OnBuildCosts { get; private set; }

        internal static ConfigEntry<StandCheck> Rule { get; private set; }

        internal static ConfigEntry<string> Stands { get; private set; }

        internal static ConfigEntry<bool> OtherStands { get; private set; }

        internal static ConfigEntry<string> ArmorStands { get; private set; }

        /// <summary>The marks' size out of the box, his own (card A63; the item stands part's StandLook.DefaultSize, 14,
        /// before, and still Add to Cart's plain look without Item Stand Icons).</summary>
        private const int OwnSize = 8;

        private static readonly Dictionary<string, ConfigEntryBase> ByKey = new Dictionary<string, ConfigEntryBase>();
        private static int version;
        private static int built = -1;
        private static StandLook look;
        private static StandRule rule;

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first.
            Enabled = settings.Enabled;
            ByKey[StandShare.KeyEnabled] = Enabled;
            string general = ModuleSettings.General;

            // His own settings made the defaults (card A63, 2026-10-06): the mod itself is off out of the box
            // (Module.EnabledByDefault), so nobody's icons change at an update until it's switched on, and then it shows his
            // marks: "Show the marks" on, size 8, at the bottom right. A file still at an old default moves once.
            settings.MovedDefault(settings.Enabled, true, 1);
            ShowMarks = Bind(settings, general, StandShare.KeyShowMarks, true, "Show the marks", 100,
                "The switch for all the marks, on by default (the mod itself is off out of the box: \"Enabled\" above). " +
                "Off: no icon carries a mark. On: every item that fits an item stand gets a small mark on its icon saying " +
                "which stand: an up " +
                "arrow for the wall stand (vertical), a right arrow for the flat stand (horizontal), and both arrows for " +
                "both. In the game both are called Item Stand: one hangs on a wall, the other lies on a floor or table. " +
                "Every item the armor stand takes gets a second mark, a small mannequin. Which items count is the game's " +
                "own rule, asked of the stands.");
            settings.MovedDefault(ShowMarks, false, 1);

            MarkWall = Bind(settings, general, StandShare.KeyMarkWall, true, "Mark items only the wall stand takes", 96,
                "The up arrow. No item of the game fits only the wall stand today; a mod could make some.");

            MarkFlat = Bind(settings, general, StandShare.KeyMarkFlat, true, "Mark items only the flat stand takes", 95,
                "The right arrow: all foods and meads, and the Egg, Barley Flour, Hildir's three chests and the four " +
                "tankards, about 130 items.");

            MarkBoth = Bind(settings, general, StandShare.KeyMarkBoth, true, "Mark items both stands take", 94,
                "Both arrows: nearly every weapon, shield, helmet, tool, trophy and fish, about 370 items. Off gives fewer " +
                "marks: then only the items that fit just one of the stands are marked.");

            MarkArmor = Bind(settings, general, StandShare.KeyMarkArmor, true, "Mark items the armor stand takes", 93,
                "A small mannequin, apart from the arrows (at its own place, below): helmets, chest pieces, legs, capes, " +
                "and the weapons and shields it holds. On an armor stand each slot takes its own kinds; this marks an " +
                "item one of them takes.");

            Look = Bind(settings, general, StandShare.KeyLook, MarkLook.DrawnArrows, "Look", 90,
                "DrawnArrows: small arrows drawn by the mod, crisp at any size, and the mannequin. ArrowCharacters: the " +
                "arrow characters as text; the game's fonts have none, so they come from a wider fallback font. Letters: " +
                "V for the wall (vertical) stand, H for the flat (horizontal) one, VH for both. In both text looks the " +
                "armor stand's mark is A. They use the game's own number font, with its outline.");

            BothShape = Bind(settings, general, StandShare.KeyBothShape, QoLMods.ItemStands.BothShape.SideBySide, "Mark for both stands", 89,
                "How the drawn mark of an item both stands take puts its two arrows. SideBySide: up, then right. " +
                "Stacked: up above right. Corner: one corner with a head at each end, up the left and along the bottom, " +
                "the most compact. For the drawn arrows only.");

            Colour = Bind(settings, general, StandShare.KeyColour, Color.white, "Colour", 88,
                "The marks' colour. The drawn arrows keep their dark outline in any colour. White, apart from the orange " +
                "upgrade levels and the gold favourite border.");

            Outline = Bind(settings, general, StandShare.KeyOutline, true, "Dark outline", 87,
                "A thin dark outline round the drawn arrows, so they read over light pictures.");

            Size = Bind(settings, general, StandShare.KeySize, OwnSize, "Size (pixels)", 86,
                "How tall a mark is on a full-size icon (64 pixels, as the inventory's), 8 by default. Smaller icons, such " +
                "as the crafting list's and the trader's, get smaller marks in step.",
                new AcceptableValueRange<int>((int)StandLook.SmallestSize, (int)StandLook.LargestSize));
            settings.MovedDefault(Size, (int)StandLook.DefaultSize, 1);

            Place = Bind(settings, general, StandShare.KeyPlace, MarkPlace.BottomRight, "Where on the icon", 85,
                "Where the item stands' arrows sit. BottomRight (the default): the icon's bottom right corner, which it " +
                "shares with a stack's count and the durability bar, so a large mark can cover part of them. LeftMiddle: " +
                "the middle of the icon's left edge, the one spot free on every icon checked. RightMiddle: the middle of " +
                "its right edge, free nearly everywhere (where the armor stand's mannequin sits by default: move one of " +
                "them). The other corners cover something in some places: the top left holds the hotbar's numbers, the " +
                "top right upgrade levels and the food and no-teleport marks.");
            settings.MovedDefault(Place, MarkPlace.LeftMiddle, 1);

            NudgeRight = Bind(settings, general, StandShare.KeyNudgeRight, 0, "Nudge right (pixels)", 84,
                "Moves the arrows right, or left with a minus, in pixels on a full-size icon.",
                new AcceptableValueRange<int>(-16, 16));

            NudgeUp = Bind(settings, general, StandShare.KeyNudgeUp, 0, "Nudge up (pixels)", 83,
                "Moves the arrows up, or down with a minus, in pixels on a full-size icon.",
                new AcceptableValueRange<int>(-16, 16));

            ArmorPlace = Bind(settings, general, StandShare.KeyArmorPlace, MarkPlace.RightMiddle, "Where on the icon (armor stand)", 82,
                "Where the armor stand's mannequin sits. RightMiddle (the default): the middle of the icon's right edge; " +
                "the top right's marks (upgrade levels, food, no-teleport) end above it and the counts start below it. " +
                "The same places as the arrows' otherwise.");

            ArmorNudgeRight = Bind(settings, general, StandShare.KeyArmorNudgeRight, 0, "Nudge right (armor stand, pixels)", 81,
                "Moves the mannequin right, or left with a minus, in pixels on a full-size icon.",
                new AcceptableValueRange<int>(-16, 16));

            ArmorNudgeUp = Bind(settings, general, StandShare.KeyArmorNudgeUp, 0, "Nudge up (armor stand, pixels)", 80,
                "Moves the mannequin up, or down with a minus, in pixels on a full-size icon.",
                new AcceptableValueRange<int>(-16, 16));

            HoldKey = Bind(settings, general, StandShare.KeyHoldKey, KeyboardShortcut.Empty, "Only while holding a key", 79,
                "None (the default): the marks show all the time. A key: they show only while you hold it.");

            WhileAiming = Bind(settings, general, "OnlyWhileAiming", false, "Only while aiming at a stand", 78,
                "Off (the default): the marks show whenever they're on. On: they show only while you aim at an item stand " +
                "or an armor stand, when the game shows its line under the crosshair (\"Attach item\", or \"Take\" once " +
                "something hangs there). The aim holds while your inventory is open and you stand still, so aim, then " +
                "open it. Which places this holds back, and which marks show: the next two settings. With a key above " +
                "as well, both are needed.");

            AimCovers = Bind(settings, general, "AimingCovers", AimPlaces.YourItems, "Aiming holds back", 77,
                "YourItems (the default): your inventory and the hotbar (with EquipmentAndQuickSlots' slots and bar), " +
                "the items you'd put on the stand; chests, the crafting menu and the trader keep their marks. EveryPlace: " +
                "every place. Either way, whatever you opened (a chest, a workbench, a trader) is what you aim at then, so " +
                "your own inventory's and hotbar's marks go while it's open; with EveryPlace, every mark does.");

            AimShows = Bind(settings, general, "AimingShows", AimMarks.ThatStandsMarks, "Aiming shows", 76,
                "ThatStandsMarks (the default): at an item stand the arrows, at an armor stand the mannequin, every " +
                "arrow whichever item stand (a food's right arrow at the wall stand too). WhatFitsIt: only the items the " +
                "stand you aim at takes, each with its mark (the game's armor stand takes all its kinds at one spot; a " +
                "stand with a spot per slot, from a mod, narrows to that slot). EveryMark: every mark, whichever stand.");

            InInventory = Bind(settings, general, "InInventory", true, "In your inventory", 60,
                "Your inventory's cells, EquipmentAndQuickSlots' equipment and quick slots included.");

            InChests = Bind(settings, general, "InChests", true, "In chests", 59,
                "A chest's window, and other mods' inventory windows.");

            OnHotbar = Bind(settings, general, "OnHotbar", true, "On the hotbar", 58,
                "The hotbar at the top left of the screen, and EquipmentAndQuickSlots' quick-slot bar.");

            InCraftingList = Bind(settings, general, "InCraftingList", true, "In the crafting list", 57,
                "The recipes down the left of the crafting menu (the upgrade tab too).");

            OnChosenRecipe = Bind(settings, general, "OnChosenRecipe", true, "On the chosen recipe", 56,
                "The large picture of the recipe you picked in the crafting menu, and the small one of an item you upgrade.");

            OnIngredients = Bind(settings, general, "OnIngredients", true, "On the ingredients", 55,
                "The ingredients of the recipe you picked in the crafting menu.");

            AtTrader = Bind(settings, general, "AtTrader", true, "At the trader", 54,
                "A trader's list of things for sale.");

            OnBuildCosts = Bind(settings, general, "OnBuildCosts", false, "On the build menu's costs", 53,
                "The materials of the piece you're placing, at the bottom of the screen. Off by default: those slots have " +
                "no free spot (the name and the amount fill them), so a mark covers part of them.");

            Rule = Bind(settings, general, StandShare.KeyRule, StandCheck.GameCheck, "Which items count", 40,
                "GameCheck: the game's own check on each stand, so items another mod lets onto stands show too. " +
                "StandListsOnly: only the stands' own lists and each item's model, as the game has them without such a mod.");

            Stands = Bind(settings, general, StandShare.KeyStands, StandFit.DefaultStands, "Stands that count", 39,
                "The stands asked, by their prefab names, separated by commas: itemstand is the wall stand, itemstandh " +
                "the flat one. Placeable_HardRock, the Mysterious Rock, takes nothing the flat stand doesn't. Stands that " +
                "take their one item by themselves (the boss stones, the altars' holders) never count.");

            OtherStands = Bind(settings, general, StandShare.KeyOtherStands, false, "Also count other stand pieces", 38,
                "Also asks every other piece in the game that holds an item as an item stand does: stands other mods " +
                "add, and the Mysterious Rock. Each counts as the wall stand, the flat one or both, by the side it says. " +
                "Likewise every piece that wears gear as the armor stand does (the game's own two other armor stands, " +
                "which no hammer builds, take only what the buildable one takes).");

            ArmorStands = Bind(settings, general, StandShare.KeyArmorStands, StandFit.DefaultArmorStands, "Armor stands that count", 37,
                "The armor stands asked, by their prefab names, separated by commas: ArmorStand is the game's own. Empty: " +
                "no armor stand counts, so no item gets the mannequin.");

            // The Pick buttons in F1 (card B03): the two lists picked from pictures of the game's pieces that hold items or
            // wear gear, their text written as StandFit.Names reads it (split at commas, semicolons or spaces, exact case).
            PickerSettings.Bind(settings);
            Lists.Attach(Stands, StandList("The item stands asked.", IsStand, null));
            Lists.Attach(ArmorStands, StandList("The armor stands asked.", IsArmorStand, "no armor stand counts"));
        }

        // As StandFit counts them (review 1): an item stand that doesn't take its one item by itself, give nothing back or
        // grant a power; an armor stand with slots.
        private static bool IsStand(Thing thing)
        {
            ItemStand stand = thing.Prefab != null ? thing.Prefab.GetComponentInChildren<ItemStand>(true) : null;
            return stand != null && StandFit.Counts(stand);
        }

        private static bool IsArmorStand(Thing thing)
        {
            ArmorStand armor = thing.Prefab != null ? thing.Prefab.GetComponentInChildren<ArmorStand>(true) : null;
            return armor != null && armor.m_slots != null && armor.m_slots.Count > 0;
        }

        private static ListSpec StandList(string says, System.Func<Thing, bool> holds, string emptyMeans) => new ListSpec
        {
            Module = Module.Name,
            Kinds = new[] { ThingKind.Pieces },
            Format = new ListFormat { Separators = ",;", WhitespaceSeparates = true, Joiner = ", ", IgnoreCase = false },
            Holds = holds,
            Says = says,
            EmptyMeans = emptyMeans,
            Warn = text => Module.Log?.LogWarning(text),
        };

        private static ConfigEntry<T> Bind<T>(ModuleSettings settings, string part, string key, T value, string name, int order,
                                              string description, AcceptableValueBase range = null)
        {
            ConfigEntry<T> entry = settings.Bind(part, key, value, name, order, description, range);
            ByKey[key] = entry;
            entry.SettingChanged += (sender, args) => version++;
            return entry;
        }

        private static int keyFrame = -1;
        private static bool keyHeld;
        private static bool keyFailed;
        private static KeyboardShortcut failedKey = KeyboardShortcut.Empty;

        /// <summary>True while the marks show: the module on, "Show the marks" on, and its key held if it has one. The key
        /// is read once a frame, and an error reading it (a key the game's input can't read, or another mod's patch on
        /// it) hides the marks until the key is changed, rather than reach the game's drawing, which asks this outside the
        /// module's guard (reviews 1 and 2).</summary>
        internal static bool Showing
        {
            get
            {
                if (!Module.IsOn || !ShowMarks.Value)
                {
                    return false;
                }
                KeyboardShortcut key = HoldKey.Value;
                if (key.MainKey == KeyCode.None)
                {
                    return true;
                }
                int frame = Time.frameCount;
                if (frame != keyFrame)
                {
                    keyFrame = frame;
                    if (keyFailed && key.Equals(failedKey))
                    {
                        keyHeld = false;   // a key that failed: not tried again until it's changed (review 2)
                    }
                    else
                    {
                        keyFailed = false;
                        try
                        {
                            keyHeld = StandShare.KeyHeld(key);
                        }
                        catch (System.Exception error)
                        {
                            // Some keys (F13 to F15, Help, SysReq, the shifted characters …) aren't in the game's own
                            // key map, and its input throws for them.
                            keyHeld = false;
                            keyFailed = true;
                            failedKey = key;
                            Module.Log.LogWarning($"The key {key} of \"Only while holding a key\" can't be read ({error.Message}); " +
                                                  "the marks stay hidden until the key is changed.");
                        }
                    }
                }
                return keyHeld;
            }
        }

        /// <summary>How the marks look now (made again only after a setting changed).</summary>
        internal static StandLook CurrentLook
        {
            get
            {
                Refresh();
                return look;
            }
        }

        /// <summary>How the rule is asked now.</summary>
        internal static StandRule CurrentRule
        {
            get
            {
                Refresh();
                return rule;
            }
        }

        private static StandView view;

        /// <summary>What the marks show now, for Add to Cart in the same plugin (the item stands part's StandShare): the same
        /// object until something in it changes, since Add to Cart asks every frame its window is open (review 2).</summary>
        internal static StandView View()
        {
            bool showing = Showing;
            StandLook nowLook = CurrentLook;
            StandRule nowRule = CurrentRule;
            if (view == null || view.Showing != showing || !ReferenceEquals(view.Look, nowLook) || !ReferenceEquals(view.Rule, nowRule))
            {
                view = new StandView(showing, nowLook, nowRule);
            }
            return view;
        }

        private static void Refresh()
        {
            if (built == version && look != null)
            {
                return;
            }
            built = version;
            // The same reading Add to Cart makes of these settings between single releases, so both show alike.
            look = StandShare.LookFrom(Value);
            rule = StandShare.RuleFrom(Value);
        }

        private static object Value(string key) => ByKey.TryGetValue(key, out ConfigEntryBase entry) ? entry.BoxedValue : null;
    }
}
