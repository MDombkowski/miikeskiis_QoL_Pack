using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using QoLMods.Chests;
using QoLMods.ItemMarks;
using QoLMods.Kit;
using UnityEngine;

namespace StoreStash
{
    /// <summary>Which chests quick stack may use.</summary>
    internal enum WhichChests
    {
        AnyChestICanOpen,
        OnlyChestsIBuilt,
    }

    /// <summary>What quick stack does with the hotbar, the inventory's top row (card A18).</summary>
    internal enum HotbarChoice
    {
        Off,
        UnfavoritedOnly,
        EverythingButWorn,
    }

    /// <summary>Where quick stack sends trophies: the chest service's TrophyRule, value for value (new ones only at the end,
    /// so a settings file's word keeps its meaning).</summary>
    internal enum TrophyChoice
    {
        AnyTrophyChest,
        LikeAnyItem,
        Never,

        /// <summary>To a chest holding trophies of the same biome, else any trophy chest (card A82).</summary>
        SameBiomeTrophyChest,

        /// <summary>Only to a chest holding trophies of the same biome (card A82).</summary>
        SameBiomeTrophyChestOnly,
    }

    /// <summary>Where the line after a quick stack shows.</summary>
    internal enum MessagePlace
    {
        TopLeft,
        Center,
        Off,
    }

    /// <summary>What the personal quick stack puts into his personal chests (card A43).</summary>
    internal enum PersonalItems
    {
        /// <summary>As "Only into chests that already hold the item" says for the quick stack key.</summary>
        LikeQuickStack,

        /// <summary>Only what a chest of his already holds (trophies as their setting says).</summary>
        WhatTheyHold,

        /// <summary>Anything the quick stack may put away: into his chests holding it first, then the nearest.</summary>
        Anything,
    }

    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.storestash.cfg, in the sections
    /// General, Favorites, Personal Stacker, Sorting, Trash, Chest setups and Stack Overflow; inside miikeskii's QoL Pack, in the bundle's
    /// file, in the one section "StoreStash". Every one takes effect at once: the mod reads them each time it stacks, sorts
    /// or draws. The defaults are his HexQuickStackStorage settings of 2026-09-29 (150 m, any chest he can open, trophies
    /// on, P, Left Shift + right-click for favourites), card A52's, card A43's for his personal chests, card A53's for
    /// sorting and the trash (SortSettings, TrashSettings) and card A41's for chest setups (SetupSettings).
    /// In a configuration manager's window (F1) they're all under StoreStash's heading (Folds), Enabled alone at the top
    /// (the kit's, 1000), then each feature behind a fold row of its own, its settings in groups behind rows of their own
    /// (card A41's second round, decision 22; card A82's point 5, his words: "sub-sections in the config for each key
    /// feature of StoreStash"). The window lists them by order, highest first, so each row's order sits right above its
    /// settings' (orders aren't kept in the file, so they can be renumbered freely; keys, parts and sections never change).
    /// Each feature has its own hundred, each group its own ten, with room between for settings to come: Quick stack 990
    /// (its four, then Which chests 980, What goes where 970, One stack by a click 960, Place stacks and holding E 950,
    /// Messages and sounds 940); Favorites 900; Personal quick stack 850 (Which chests are yours 840); Personal chests 800
    /// (Button in the chest window 790, Other players at your marked chests 780: MineSettings); Sorting 700, Trash 600
    /// (SortSettings, TrashSettings); Chest setups 500 (SetupSettings); Stack Overflow 400 (OverflowSettings); and two
    /// settings the mod keeps for itself, 20 and 19, never listed. The F1 layout in words: notes of session 44-B, card A82.
    /// </summary>
    internal static class Settings
    {
        private const string FavoritesPart = "Favorites";

        private const string PersonalPart = "Personal Stacker";

        // Which settings the file holds: 1, StoreStash 0.4.0's or before (or a new file); 2, from 0.5.0 on ("Which chest
        // first" in place of chest setups' three switches, carried over once: CarryOverOnce).
        private const int SettingsNow = 2;

        // StoreStash 0.4.0's three switches, by their keys, and the rule each became (card A41's second round, decision 28).
        private static readonly (string Key, ChestOrder Rule)[] OldSwitches =
        {
            ("SetupFirst", ChestOrder.SetUp), ("SetupSpecificFirst", ChestOrder.Specific), ("SetupMostFirst", ChestOrder.Most),
        };

        private static ConfigEntry<int> settingsVersion;
        private static string setupsSection;

        /// <summary>The gold of a favourite's border.</summary>
        internal static readonly Color Gold = new Color(1f, 0.78f, 0.2f, 1f);

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> Key { get; private set; }

        internal static ConfigEntry<bool> ShowButton { get; private set; }

        internal static ConfigEntry<Vector2> ButtonOffset { get; private set; }

        internal static ConfigEntry<float> Range { get; private set; }

        internal static ConfigEntry<WhichChests> Chests { get; private set; }

        internal static ConfigEntry<bool> PersonalChests { get; private set; }

        internal static ConfigEntry<bool> Carts { get; private set; }

        internal static ConfigEntry<bool> Ships { get; private set; }

        internal static ConfigEntry<bool> OnlyWhereHeld { get; private set; }

        internal static ConfigEntry<TrophyChoice> Trophies { get; private set; }

        internal static ConfigEntry<HotbarChoice> Hotbar { get; private set; }

        internal static ConfigEntry<bool> TopUpFirst { get; private set; }

        /// <summary>"Which chest first" (card A41's second round, decision 28): a list of rules as text (ChestOrderList).</summary>
        internal static ConfigEntry<string> WhichChestFirst { get; private set; }

        internal static ConfigEntry<bool> WithChestOpen { get; private set; }

        internal static ConfigEntry<bool> PlaceStacks { get; private set; }

        internal static ConfigEntry<bool> HoldE { get; private set; }

        internal static ConfigEntry<bool> ReservedByHand { get; private set; }

        internal static ConfigEntry<MessagePlace> Message { get; private set; }

        /// <summary>How much longer each line after the first keeps a message after a quick stack fully shown (MessageHold).</summary>
        internal static ConfigEntry<float> SecondsPerExtraLine { get; private set; }

        /// <summary>The longest a message after a quick stack stays on screen, its fade included (MessageHold).</summary>
        internal static ConfigEntry<float> LongestOnScreen { get; private set; }

        internal static ConfigEntry<bool> Sound { get; private set; }

        internal static ConfigEntry<bool> LightUp { get; private set; }

        internal static ConfigEntry<Color> LightUpColor { get; private set; }

        internal static ConfigEntry<bool> HotbarNoteShown { get; private set; }

        internal static ConfigEntry<KeyCode> FavoriteModifier { get; private set; }

        internal static ConfigEntry<bool> Borders { get; private set; }

        internal static ConfigEntry<Color> BorderColor { get; private set; }

        internal static ConfigEntry<bool> InChestWindows { get; private set; }

        internal static ConfigEntry<bool> ImportHex { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> PersonalKey { get; private set; }

        internal static ConfigEntry<bool> PersonalButton { get; private set; }

        internal static ConfigEntry<PersonalItems> PersonalWhatGoes { get; private set; }

        internal static ConfigEntry<bool> PersonalGameChests { get; private set; }

        internal static ConfigEntry<bool> PersonalMarked { get; private set; }

        internal static ConfigEntry<bool> PersonalBuilt { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> MineKey { get; private set; }

        internal static ConfigEntry<bool> MineLeaveOthers { get; private set; }

        internal static ConfigEntry<bool> MineHoverLine { get; private set; }

        internal static ConfigEntry<Color> MineColor { get; private set; }

        internal static ConfigEntry<bool> MineKeyHint { get; private set; }

        internal static ConfigEntry<Vector2> PersonalButtonOffset { get; private set; }

        /// <summary>One stack by a click (card A82): the key or button that quick-stacks the stack it points at.</summary>
        internal static ConfigEntry<KeyboardShortcut> OneStackKey { get; private set; }

        /// <summary>Whether one stack by a click works with a chest's window open too (card A82).</summary>
        internal static ConfigEntry<bool> OneStackWithChestOpen { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first; in the bundle every part shares one section, so the orders run on across them
            // (the class's summary has them all). Keys never change once released; new ones get new keys.
            Enabled = settings.Enabled;

            // The chest settings every mod of the pack shares (card A69): the range, "Chests beyond the server's reach"
            // (which was this mod's own setting since card A64) and the log lines.
            ChestSettings.Bind(settings);

            Key = settings.Bind(ModuleSettings.General, "QuickStackKey", new KeyboardShortcut(KeyCode.P),
                "Quick stack key", 989,
                "Puts what you carry into the chests nearby that already hold it (the rules below), with the inventory open or " +
                "closed. Never while you type in chat, a sign, a map pin's name, or the search boxes of the game and most " +
                "mods, or while a configuration manager's window (F1) is open.");

            Key.SettingChanged += (sender, args) => Button.KeyChanged();   // the button's tooltip names the key

            ShowButton = settings.Bind(ModuleSettings.General, "Button", true,
                "Quick stack button beside the inventory", 988,
                "A button beside your inventory that does what the key does.");

            ButtonOffset = settings.Bind(ModuleSettings.General, "ButtonOffset", Vector2.zero,
                "Button position (offset)", 987,
                "Moves the button from its place beside the armour and weight boxes, in the inventory's own units: x to the " +
                "right, y up. For when another mod puts something there.");

            WithChestOpen = settings.Bind(ModuleSettings.General, "WithChestOpen", true,
                "Quick stack while a chest is open", 986,
                "On: the key and the button work while a chest's window is open too, the open chest included. Off: they do " +
                "nothing then (Place stacks still works).");

            // Since card A69 (2026-10-08): 0 follows the shared range ("Chests (every mod)"), the default now; a file still
            // at the old default, 150, moves to it once (ChestSettings). Its most, 150 before, is the shared 250.
            Range = settings.Bind(ModuleSettings.General, "Range", 0f,
                "Search radius (metres)", 979,
                "How far from you a chest may be for the quick stack, Place stacks and holding E. " + ChestSettings.OwnRangeNote,
                new AcceptableValueRange<float>(0f, ChestSettings.MostRange));
            settings.MovedDefault(Range, ChestSettings.DefaultRange, 2);
            ChestSettings.Reaches("StoreStash (quick stack, Place stacks, holding E)", "Search radius (metres)", Range);   // card B13

            Chests = settings.Bind(ModuleSettings.General, "Chests", WhichChests.AnyChestICanOpen,
                "Which chests", 978,
                "AnyChestICanOpen: every chest you could open by hand (a friend's public chest too, never someone else's " +
                "private chest or one behind a ward that isn't yours). OnlyChestsIBuilt: only chests, carts and ships you " +
                "built. The world's own chests, gravestones, reserved chests (Reserve Stock) and a chest someone else has open " +
                "are never used.");

            PersonalChests = settings.Bind(ModuleSettings.General, "PersonalChests", true,
                "Use my personal chests", 977,
                "On: your own personal chests (the game's Personal Chests you built, and chests you marked as yours) are " +
                "quick stacked into like any other chest. Off: the quick stack key and button leave alone your Personal Chests " +
                "and the chests you marked, each while the personal quick stack key counts that kind (Personal quick stack, " +
                "below), so only it fills them; never every chest you built. Other players' personal chests are never used.");

            Carts = settings.Bind(ModuleSettings.General, "Carts", true,
                "Carts' chests", 975,
                "Quick stack uses the chests of carts nearby.");

            Ships = settings.Bind(ModuleSettings.General, "Ships", true,
                "Ships' chests", 974,
                "Quick stack uses the chests of ships nearby, but never one with someone else aboard: its chest belongs to the " +
                "ship, and taking the chest over would take the ship's steering from them.");

            // F1 (card A82, point 5): each feature under its own fold, the quick stack's first.
            Folds.Fold quick = Folds.Add(settings, ModuleSettings.General, "QuickStackUnfolded", "Quick stack", 990, new ConfigEntryBase[]
            {
                Key, ShowButton, ButtonOffset, WithChestOpen,
            });

            Folds.Fold whichChests = Folds.AddGroup(settings, ModuleSettings.General, "WhichChestsUnfolded", "Which chests", 980, new ConfigEntryBase[]
            {
                Range, Chests, PersonalChests, Carts, Ships,
            }, quick);

            OnlyWhereHeld = settings.Bind(ModuleSettings.General, "OnlyWhereHeld", true,
                "Only into chests that already hold the item", 969,
                "On: an item goes only to a chest that already holds some, as the game's own Place stacks does. Off: into any " +
                "chest the rules allow, chests that hold it first, then the nearest. The key and the button only: Place " +
                "stacks and holding E always put in only what the chest already holds, as the game's own do (trophies as " +
                "their setting says).");

            Trophies = settings.Bind(ModuleSettings.General, "Trophies", TrophyChoice.AnyTrophyChest,
                "Trophies", 968,
                "AnyTrophyChest: every trophy goes to any chest that holds a trophy of any kind. LikeAnyItem: only to a chest " +
                "holding that trophy. Never: trophies stay with you. SameBiomeTrophyChest: to a chest holding trophies from " +
                "the same biome (a Troll trophy goes where your other Black Forest trophies are), else to any chest holding a " +
                "trophy. SameBiomeTrophyChestOnly: only to a chest holding trophies from the same biome, else it stays with " +
                "you. A trophy's biome is where its creature first lives (bosses: their own biome). A chest already holding " +
                "that very trophy always comes first.");

            Hotbar = settings.Bind(ModuleSettings.General, "Hotbar", HotbarChoice.UnfavoritedOnly,
                "The hotbar (top row)", 967,
                "Off: the hotbar is left as it is. UnfavoritedOnly: what isn't a favourite goes, as from the other rows. " +
                "EverythingButWorn: everything on it goes except what you're wearing or holding, favourites too. Rows below " +
                "the inventory's own (EquipmentAndQuickSlots' equipment and quick slots, and its extra inventory rows if you " +
                "use them) are never touched.");

            TopUpFirst = settings.Bind(ModuleSettings.General, "TopUpFirst", true,
                "Top up part-stacks before empty cells", 966,
                "On: the part-stacks in every chest that takes an item are filled before any empty cell is used, nearest " +
                "chest first, within each step of \"Which chest first\" (below). Off: chest by chest, nearest first, as the " +
                "game's own Place stacks fills one chest.");

            WhichChestFirst = settings.Bind(ModuleSettings.General, "ChestOrder", ChestOrderList.DefaultText,
                ChestOrderDrawer.Title, 965, ChestOrderDescription, null, ChestOrderDrawer.Draw);
            var orderHints = ModuleSettings.HintsOf(WhichChestFirst);   // the kit's hints (Jötunn has a class of that name too)
            if (orderHints != null)
            {
                orderHints.HideSettingName = true;   // the drawer writes the name, and its rows take the whole width
            }

            Folds.AddGroup(settings, ModuleSettings.General, "WhatGoesWhereUnfolded", "What goes where", 970, new ConfigEntryBase[]
            {
                OnlyWhereHeld, Trophies, Hotbar, TopUpFirst, WhichChestFirst,
            }, quick);

            PlaceStacks = settings.Bind(ModuleSettings.General, "PlaceStacks", true,
                "Place stacks follows these rules", 949,
                "On: the chest window's Place stacks button puts in what the rules above allow: never your favourites or what " +
                "you're wearing, the hotbar as set, trophies as set. Off: the game's own Place stacks.");

            HoldE = settings.Bind(ModuleSettings.General, "HoldE", true,
                "Holding E follows these rules", 948,
                "On: holding E at a chest stacks by the same rules as Place stacks. Off: the game's own.");

            ReservedByHand = settings.Bind(ModuleSettings.General, "ReservedByHand", false,
                "Place stacks and holding E leave reserved chests alone too", 947,
                "Off: by hand, a reserved chest (Reserve Stock) takes a Place stacks or a held E like any other: it's a " +
                "choice made at that one chest. On: they do nothing at a reserved chest and say why. Quick stack to nearby " +
                "chests leaves reserved chests alone either way. Reserve Stock's own setting of the same name also works.");

            Folds.AddGroup(settings, ModuleSettings.General, "PlaceStacksUnfolded", "Place stacks and holding E", 950, new ConfigEntryBase[]
            {
                PlaceStacks, HoldE, ReservedByHand,
            }, quick);

            Message = settings.Bind(ModuleSettings.General, "Message", MessagePlace.TopLeft,
                "Message after a quick stack", 939,
                "A line such as \"34 items into 5 chests\": top left, in the middle of the screen, or not at all. With chest " +
                "setups' \"Say where things went\" on, it says where things went instead (Chest setups, below), and with " +
                "this off, those lines still show, top left. Place stacks and holding E say the game's own words.");

            SecondsPerExtraLine = settings.Bind(ModuleSettings.General, "MessageSecondsPerLine", 1f,
                "Seconds per extra line", 938,
                "Each line after the first keeps a message after a quick stack fully shown this much longer, before it fades as " +
                "the game's own messages do; a one-line message is never held. Messages of several lines come from chest " +
                "setups' \"Say where things went\". 0: the game's own timing.",
                new AcceptableValueRange<float>(0f, 5f));

            LongestOnScreen = settings.Bind(ModuleSettings.General, "MessageLongest", 10f,
                "Longest on screen (seconds)", 937,
                "The longest a message after a quick stack stays on screen, its fade included: the game's own fade takes 4 " +
                "seconds, so a long message is held fully shown for at most this less 4.",
                new AcceptableValueRange<float>(4f, 30f));

            Sound = settings.Bind(ModuleSettings.General, "Sound", true,
                "Sound after a quick stack", 936,
                "The game's own sound of moving items when a quick stack moved anything.");

            // On out of the box since card A63 (2026-10-06, his own settings made the pack's defaults), as are the personal
            // button, the setup button and "Say where things went"; a file still at the old default moves once (the kit).
            LightUp = settings.Bind(ModuleSettings.General, "LightUp", true,
                "Light up the chests that received items", 935,
                "The chests a quick stack put something into glow for two seconds, so you can see where things went. On by " +
                "default.");
            settings.MovedDefault(LightUp, false, 1);

            LightUpColor = settings.Bind(ModuleSettings.General, "LightUpColor", Gold,
                "Light-up colour", 934,
                "The colour those chests glow.");

            Folds.AddGroup(settings, ModuleSettings.General, "MessagesUnfolded", "Messages and sounds", 940, new ConfigEntryBase[]
            {
                Message, SecondsPerExtraLine, LongestOnScreen, Sound, LightUp, LightUpColor,
            }, quick);

            // One stack by a click (card A82, his words of 2026-10-09): new keys, on out of the box (a new gesture on a
            // button the game's inventory never uses, which changes nothing else); a group of their own in the quick stack's
            // fold.
            OneStackKey = settings.Bind(ModuleSettings.General, "OneStackKey", new KeyboardShortcut(KeyCode.Mouse2),
                "One stack: key or button", 959,
                "With your inventory open, point at a stack and press this to quick stack just that stack, by the quick " +
                "stack's own rules (the chests nearby, \"Only into chests that already hold the item\", chest setups, which " +
                "chest first), even a favorite or a stack on your hotbar, leaving your other stacks of it with you. Only as " +
                "much as the chests have room for goes. Never what you wear or hold. Mouse2 is the middle mouse button, which " +
                "the game's inventory doesn't use. Another key or button works too, with keys held with it (Left Control + " +
                "Mouse1 is Left Control + right-click, which then does only this); a plain left (Mouse0) or right (Mouse1) " +
                "click stays the game's own, and the favorite and trash marks keep their right-click. Not a gamepad button: " +
                "each already does something in the inventory. None: off.");

            OneStackWithChestOpen = settings.Bind(ModuleSettings.General, "OneStackWithChestOpen", true,
                "One stack while a chest is open", 958,
                "On: it works with a chest's window open too, the open chest included (it takes the stack if it may). Off: " +
                "only with no chest open.");

            Folds.AddGroup(settings, ModuleSettings.General, "OneStackUnfolded", "One stack by a click", 960, new ConfigEntryBase[]
            {
                OneStackKey, OneStackWithChestOpen,
            }, quick);

            // Not a choice: remembers that the note about the hotbar was shown, so it shows once. Not listed in F1.
            HotbarNoteShown = settings.Bind(ModuleSettings.General, "HotbarNoteShown", false,
                "Hotbar note shown", 20,
                "Set by the mod the first time a quick stack puts away something from your hotbar and says so, so that it " +
                "says so only once. Set it back to false to see the note again.");
            ModuleSettings.HintsOf(HotbarNoteShown).Browsable = false;

            // Not a choice either: read at the module's Resolve (CarryOverOnce), after the bundle has taken a single release's
            // settings over. Not listed in F1.
            setupsSection = settings.SectionOf(SetupSettings.Part);
            settingsVersion = settings.Bind(ModuleSettings.General, "SettingsVersion", 1,
                "Settings version", 19,
                "Which version of StoreStash's settings the file holds (2: \"Which chest first\" in place of chest setups' three " +
                "switches, from StoreStash 0.5.0 on). Kept by the mod; not shown in the settings window.");
            ModuleSettings.HintsOf(settingsVersion).Browsable = false;

            FavoriteModifier = settings.Bind(FavoritesPart, "FavoriteModifier", KeyCode.LeftShift,
                "Favorite: key held with a right-click", 899,
                "Hold it and right-click an item in your inventory to make its kind a favorite, or not: every item of that " +
                "kind, whatever its quality, now and later. Favorites stay with you when you quick stack. None: marking is off " +
                "(your favorites are kept).");

            Borders = settings.Bind(FavoritesPart, "Borders", true,
                "Favorite borders", 898,
                "A border around every favorite in your inventory.");

            BorderColor = settings.Bind(FavoritesPart, "BorderColor", Gold,
                "Favorite border colour", 897,
                "The colour of that border.");

            InChestWindows = settings.Bind(FavoritesPart, "InChestWindows", false,
                "Favorites in chest windows too", 896,
                "On: the border shows on favorites in a chest's window as well, and the right-click marks there too.");

            ImportHex = settings.Bind(FavoritesPart, "ImportHex", true,
                "Take in my HexQuickStackStorage favorites (once)", 895,
                "The first time the mod runs with this on, the favorites you had in HexQuickStackStorage " +
                "(BepInEx\\config\\HexQuickStackStorage.favorites.txt, if it's there) become yours here too. That file is " +
                "only read, and only ever once. Your favorites live in BepInEx\\config\\modprojects.marks.favorites.txt.");

            // F1: each feature after General behind a fold row of its own, under StoreStash's heading (Folds).
            Folds.Add(settings, FavoritesPart, "FavoritesUnfolded", "Favorites", 900, new ConfigEntryBase[]
            {
                FavoriteModifier, Borders, BorderColor, InChestWindows, ImportHex,
            });

            // His personal chests (card A43, his name for it: Personal Stacker): in F1 the personal quick stack's fold (850,
            // its own four 849 to 846, "Which chests are yours" 840) and the personal chests' (800, the marking 799 to 796,
            // MineSettings' two groups 790 and 780).
            PersonalKey = settings.Bind(PersonalPart, "PersonalKey", new KeyboardShortcut(KeyCode.H),
                "Personal quick stack key", 849,
                "Puts what you carry into your personal chests nearby, and nowhere else: the chests counted below. " +
                "Everything else follows the quick stack's own settings above: the search radius, carts' and ships' chests, " +
                "trophies, the hotbar, part-stacks first, your favorites, while a chest is open, the message, the sound and the " +
                "light-up. Never while you type. (Handyman's own key is H too, used only if its Key is set to OwnKey: then " +
                "change one.)");

            PersonalKey.SettingChanged += (sender, args) => StoreStash.PersonalButton.TipChanged();   // its tooltip names the key

            PersonalButton = settings.Bind(PersonalPart, "PersonalButton", true,
                "Personal quick stack button beside the inventory", 848,
                "A button beside your inventory that does what the personal quick stack key does; on by default. With the " +
                "quick stack button on too, the two share its place, a little smaller, this one below.");
            settings.MovedDefault(PersonalButton, false, 1);

            PersonalWhatGoes = settings.Bind(PersonalPart, "PersonalWhatGoes", PersonalItems.LikeQuickStack,
                "Personal quick stack: what goes in", 846,
                "LikeQuickStack: as \"Only into chests that already hold the item\" says for the quick stack key. " +
                "WhatTheyHold: only what a chest of yours already holds. Anything: anything the quick stack may put away, into " +
                "your chests that hold it first, then the nearest (a Personal Chest has only 6 cells).");

            PersonalWhatGoes.SettingChanged += (sender, args) => StoreStash.PersonalButton.TipChanged();   // its tooltip says what goes
            OnlyWhereHeld.SettingChanged += (sender, args) =>
            {
                Button.KeyChanged();   // both buttons' tooltips say what goes
                StoreStash.PersonalButton.TipChanged();
            };

            PersonalGameChests = settings.Bind(PersonalPart, "PersonalGameChests", true,
                "Personal chests: the game's Personal Chests I built", 839,
                "The game's own Personal Chest (only the character who built it can open it) counts as one of your personal " +
                "chests.");

            PersonalMarked = settings.Bind(PersonalPart, "PersonalMarked", true,
                "Personal chests: chests I marked as mine", 838,
                "A chest you marked as yours (the key below) counts as one of your personal chests, whoever built it.");

            PersonalBuilt = settings.Bind(PersonalPart, "PersonalBuilt", false,
                "Personal chests: every chest I built", 837,
                "Every chest, cart and ship you built with this character counts as one of your personal chests, marked or " +
                "not, unless someone else marked it as theirs.");

            MineKey = settings.Bind(PersonalPart, "MineKey", new KeyboardShortcut(KeyCode.H, KeyCode.LeftAlt),
                "Mark a chest as mine: key", 799,
                "Pointing at a chest, or with its window open, marks it as yours, or takes your mark off. The mark is saved on " +
                "the chest, so it lasts with the world, and friends on the pack see it. Anyone may mark a chest they could open " +
                "by hand; a chest someone else marked stays theirs until they take their mark off. The mark belongs to this " +
                "character, as the game's Personal Chests do.");

            MineLeaveOthers = settings.Bind(PersonalPart, "MineLeaveOthers", true,
                "Quick stack leaves chests others marked alone", 976,
                "On: the quick stack key and its button put nothing into a chest someone else marked as theirs (the personal " +
                "key never counts one as yours). Place stacks and holding E at that chest still work: a choice made by hand at " +
                "that one chest. Friends on the pack leave the chests you marked alone by their own copy of this setting, " +
                "unless they still run HexQuickStackStorage (StoreStash stays off for them, and Hex doesn't read marks).");

            MineHoverLine = settings.Bind(PersonalPart, "MineHoverLine", true,
                "Show whose a marked chest is", 797,
                "Pointing at a marked chest shows a line: Yours, or whose it is.");

            MineColor = settings.Bind(PersonalPart, "MineColor", Gold,
                "Colour of \"Yours\"", 796,
                "The colour of the Yours line on a chest you marked.");

            MineKeyHint = settings.Bind(PersonalPart, "MineKeyHint", false,
                "The mark key in every chest's hover text", 798,
                "Pointing at a chest you could mark shows the key and what it would do: Mark as yours, or Unmark.");

            PersonalButtonOffset = settings.Bind(PersonalPart, "PersonalButtonOffset", Vector2.zero,
                "Personal button position (offset)", 847,
                "Moves the personal quick stack button from its place, in the inventory's own units: x to the right, y up.");

            // His personal chests in two features (card A82, point 5): the personal quick stack (H) with which chests count
            // as his, and his marked chests (marking them, the chest window's button, other players at them: card A80). The
            // marking's row was a group of Personal Stacker's; its key stays. "Quick stack leaves chests others marked
            // alone" sits with the quick stack's other chest rules, in "Which chests".
            Folds.Fold personal = Folds.Add(settings, PersonalPart, "PersonalStackerUnfolded", "Personal quick stack", 850, new ConfigEntryBase[]
            {
                PersonalKey, PersonalButton, PersonalButtonOffset, PersonalWhatGoes,
            });
            Folds.AddGroup(settings, PersonalPart, "PersonalWhichUnfolded", "Which chests are yours", 840, new ConfigEntryBase[]
            {
                PersonalGameChests, PersonalMarked, PersonalBuilt,
            }, personal);
            Folds.Fold marked = Folds.Add(settings, PersonalPart, "MineUnfolded", "Personal chests", 800, new ConfigEntryBase[]
            {
                MineKey, MineKeyHint, MineHoverLine, MineColor,
            });
            Folds.AddTo(whichChests, MineLeaveOthers);
            MineSettings.Bind(settings, marked);   // the chest window's button and other players at his chests (card A80)

            SortSettings.Bind(settings);    // sorting (card A53): the part "Sorting", orders 700 to 666, its folds included
            TrashSettings.Bind(settings);   // the trash (card A53): the part "Trash", orders 600 to 575, its folds included
            SetupSettings.Bind(settings);   // chest setups (card A41): the part "Chest setups", orders 500 to 457, its folds included
            OverflowSettings.Bind(settings);   // Stack Overflow (card A42): the part "Stack Overflow", orders 400 to 346, its folds included
        }

        /// <summary>
        /// "Which chest first": what the list means, each rule, and the text the settings file keeps (card A41's second
        /// round, decision 28). The configuration manager shows it over the setting's rows; the file, above its line.
        /// </summary>
        private const string ChestOrderDescription =
            "Which chest an item goes to first, when more than one may take it: the rules ticked, top to bottom. The first rule " +
            "that tells two chests apart decides which comes first, and the chest it puts first gets all it can take before " +
            "the next one is touched. Chests set up for it: a chest set up to take the item (Chest setups) before every chest " +
            "that isn't. ...the most specific setup: among those, one set up for that item itself, then for a Gear kind, a " +
            "family's member, a whole family, then biomes alone. ...the one holding the most of it: among those, the one " +
            "already holding the most, so each kind gathers in one chest. These two only sort set-up chests among themselves, " +
            "so they stay below \"Chests set up for it\" and move with it. My personal chests: the chests \"Which chests are " +
            "yours\" counts (Personal quick stack, below) before every other chest; at the top, a personal chest already " +
            "holding an item gets it before a set-up chest (while \"What set-up chests can't fit goes on\" lets chests without a setup take what a " +
            "setup takes at all). Chests no rule tells apart go as before: those already holding it, then trophy chests, " +
            "then the nearest. In the settings window (F1), drag a row by its handle (::) or use Up and Down, and tick a row " +
            "to switch it on. In this file: the rules' names in order, separated by commas, a minus before one that's off " +
            "(SetUp, Specific, Most, -Personal); a name left out takes its usual place.";

        /// <summary>The rules for a quick stack into the chests nearby (the key and the button), as the settings say now.</summary>
        internal static StackRules Rules() => Rules(ChestOrderList.Parse(WhichChestFirst.Value).Effective(), ChestClaims.PlayerId());

        // The same, with "Which chest first" read (card A41's second round, decision 28): his personal chests are asked for
        // only while its rule is on.
        private static StackRules Rules(List<ChestOrder> order, long me) => new StackRules
        {
            Chests = new ChestRules
            {
                Range = ChestSettings.RangeOf(Range),
                OnlyBuiltByMe = Chests.Value == WhichChests.OnlyChestsIBuilt,
                Skip = LeftAlone,
            },
            Hotbar = (HotbarRule)(int)Hotbar.Value,
            Trophies = (TrophyRule)(int)Trophies.Value,
            TrophyBiome = TrophyBiomes.Reading(Trophies.Value),
            OnlyWhereHeld = OnlyWhereHeld.Value,
            TopUpFirst = TopUpFirst.Value,
            Keep = Favorites.IsFavorite,
            LeaveOthersMarked = MineLeaveOthers.Value,
            Setups = SetupSettings.Routing(byHand: false),
            Order = order,
            Personal = order.Contains(ChestOrder.Personal) ? chest => Personal.Counts(chest, me) : (System.Func<Container, bool>)null,
        };

        /// <summary>
        /// At the module's Resolve, once: StoreStash 0.4.0's three switches for the order of set-up chests ("Set-up chests
        /// first", "The most specific setup first", "The chest holding most of it first") became "Which chest first", and
        /// each that was off is switched off there. They're read from the settings file's entries nothing binds any more
        /// (BepInEx keeps them, ConfigFile's private OrphanedEntries), in the setups' section ("Chest setups" alone, the one
        /// "StoreStash" section in the bundle). Resolve, not the binding: the bundle takes a single release's settings over
        /// right after the module has bound its own, and before its Resolve. The version is written last: every write saves
        /// the file, and a save that fails leaves it to be done again. After Map View Rules' CarryOverOnce.
        /// </summary>
        internal static void CarryOverOnce()
        {
            if (settingsVersion == null || WhichChestFirst == null || settingsVersion.Value >= SettingsNow)
            {
                return;
            }
            try
            {
                string carried = CarryOver(settingsVersion.ConfigFile, setupsSection, WhichChestFirst);
                settingsVersion.Value = SettingsNow;
                if (carried.Length > 0)
                {
                    Module.Log.LogInfo($"Settings from before StoreStash 0.5.0: chest setups' {carried} carried into \"Which chest first\", " +
                                       "which now orders the chests (Quick stack, What goes where); its rows are switched off to match.");
                }
            }
            catch (System.Exception error)
            {
                // A file that can't be saved now never stops StoreStash starting (review round 4, as PutOrderRight). Only a save
                // can fail here, after the carry is made in memory, so this session follows it; the version is written last, so
                // the next start finishes it (review round 5's words).
                Module.Log.LogWarning($"Settings from before StoreStash 0.5.0 were carried over, but the settings file couldn't be saved ({error.GetType().Name}: {error.Message}); the next start finishes it.");
            }
        }

        /// <summary>
        /// The carry-over itself, on any settings file (the scratch harness gives it one of its own): each of 0.4.0's
        /// switches the file holds unbound in <paramref name="section"/> as false switches its rule off in
        /// <paramref name="order"/>; then all three are taken out of the file, carried or not (BepInEx keeps and saves the
        /// entries nothing binds, so they'd stay in it for good, doing nothing: 34-B's harness), and the file is saved.
        /// Returns the switches carried, in words ("\"Set-up chests first\" off"), or "".
        /// </summary>
        internal static string CarryOver(ConfigFile file, string section, ConfigEntry<string> order)
        {
            Dictionary<ConfigDefinition, string> unbound = Orphans(file);
            if (unbound == null || order == null)
            {
                return string.Empty;
            }
            ChestOrderList list = ChestOrderList.Parse(order.Value);
            var carried = new List<string>();
            bool removed = false;
            foreach ((string key, ChestOrder rule) in OldSwitches)
            {
                var old = new ConfigDefinition(section, key);
                if (unbound.TryGetValue(old, out string value) && bool.TryParse(value?.Trim(), out bool on) && !on &&
                    list.SetOn(rule, false))
                {
                    carried.Add($"\"{OldName(rule)}\" off");
                }
                removed |= unbound.Remove(old);
            }
            if (carried.Count > 0)
            {
                order.Value = list.Format();
            }
            if (removed)
            {
                file.Save();   // without the old switches, whatever the file's SaveOnConfigSet says (review round 4)
            }
            return string.Join(", ", carried);
        }

        /// <summary>At the module's Resolve, after the carry-over, every start: "Which chest first" written the one way its
        /// list is (ChestOrderList.PutRight), so a hand-written text that means the default no longer reads as changed in F1
        /// (the last look). Nothing is written when it's right already. A file that can't be saved now is only noted: this
        /// is how the text looks, never a reason for StoreStash not to start (review round 2 of the last look).</summary>
        internal static void PutOrderRight()
        {
            try
            {
                string right = WhichChestFirst != null ? ChestOrderList.PutRight(WhichChestFirst.Value) : null;
                if (right != null)
                {
                    WhichChestFirst.Value = right;
                }
            }
            catch (System.Exception error)
            {
                Module.Log.LogWarning($"\"Which chest first\" couldn't be written in the one way it reads ({error.GetType().Name}: {error.Message}); the list works as it is.");
            }
        }

        // 0.4.0's name for each switch, for the log line.
        private static string OldName(ChestOrder rule) =>
            rule == ChestOrder.SetUp ? "Set-up chests first" : rule == ChestOrder.Specific ? "The most specific setup first" : "The chest holding most of it first";

        // The file's entries nothing binds (BepInEx's own, kept and saved with the file); null where they can't be read.
        private static Dictionary<ConfigDefinition, string> Orphans(ConfigFile file)
        {
            if (file == null)
            {
                return null;
            }
            PropertyInfo orphans = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return orphans?.GetValue(file, null) as Dictionary<ConfigDefinition, string>;
        }

        /// <summary>
        /// The rules for one stack by a click (card A82): the quick stack key's own (Rules), for that very stack only, and
        /// never more of it than he clicked, across every round of the quick stack (OneStack.Clicked: a stack grown
        /// meanwhile, by a pickup or a merge, is left alone), even a favourite or on the hotbar (never what he wears or
        /// holds: StackItems); a trophy where "Trophies" is Never goes as any item, to a chest holding that trophy (his
        /// click is his choice).
        /// </summary>
        internal static StackRules OneStackRules(OneStack.Clicked clicked)
        {
            StackRules rules = Rules();
            rules.Pick = clicked.Picks;
            rules.Keep = null;
            rules.Hotbar = HotbarRule.EverythingButWorn;
            if (rules.Trophies == TrophyRule.Never)
            {
                rules.Trophies = TrophyRule.LikeAnyItem;
            }
            // The chest open in his window that his hands may only look at takes none of it (OneStack.LooksOnly: a click in
            // that window is a hand's move, refused there whatever its marker lets other players' mods do).
            System.Func<Container, bool> skip = rules.Chests.Skip;
            rules.Chests.Skip = chest => (skip != null && skip(chest)) || OneStack.LooksOnly(chest);
            return rules;
        }

        /// <summary>The rules for Place stacks and holding E at one chest: the item rules above, but only what the chest
        /// already holds (the game's own rule: one long press of E never empties his bags into a chest), or at a set-up
        /// chest what its setup takes, while "Place stacks and holding E follow a setup" is on (card A41); and a reserved
        /// chest taking what it's given unless "Place stacks and holding E leave reserved chests alone too" is on (then
        /// they never get this far: Hand says why). A chest someone else marked as theirs takes it too: his hand at that
        /// one chest is his choice.</summary>
        internal static StackRules HandRules()
        {
            StackRules rules = Rules();
            rules.OnlyWhereHeld = true;
            rules.Chests.Skip = null;
            rules.Chests.UseReserved = true;
            rules.LeaveOthersMarked = false;
            rules.Setups = SetupSettings.Routing(byHand: true);   // a set-up chest takes what its setup takes, by hand too
            return rules;
        }

        /// <summary>The rules for the personal quick stack (its key and button, card A43): the quick stack's own item rules,
        /// radius and carts' and ships' settings, into his personal chests only (Personal.Counts), whatever "Which chests"
        /// and "Use my personal chests" say for the other key; what goes in as its own setting says.</summary>
        internal static StackRules PersonalRules()
        {
            StackRules rules = Rules();
            long me = ChestClaims.PlayerId();
            rules.Chests.OnlyBuiltByMe = false;
            rules.Chests.Skip = VehicleLeftAlone;
            rules.OnlyWhereHeld = PersonalOnlyWhereHeld;
            rules.Filter = (chest, item) => Personal.Counts(chest, me);
            return rules;
        }

        /// <summary>Whether the personal quick stack puts in only what a chest of his already holds, as "Personal quick
        /// stack: what goes in" says (by default, as the quick stack key's own setting says).</summary>
        internal static bool PersonalOnlyWhereHeld =>
            PersonalWhatGoes.Value == PersonalItems.LikeQuickStack ? OnlyWhereHeld.Value : PersonalWhatGoes.Value == PersonalItems.WhatTheyHold;

        // The chests the settings leave alone besides the chest service's own rules: carts', ships', and for the quick stack
        // key and button, his personal chests (the game's Personal Chests he built, and chests marked as his), and the chest
        // open in his window that his hands may only look at (another player's marked chest, card A80): with that window open
        // the quick stack would put into it before his eyes, which the window says he can't, so it's left alone whatever
        // that player's "Their mods follow it too" says, as one stack's click is (44-B's last look, F3).
        private static readonly System.Func<Container, bool> LeftAlone = chest =>
            VehicleLeftAlone(chest) || (!PersonalChests.Value && Personal.LeftAloneByP(chest, ChestClaims.PlayerId()))
            || OneStack.LooksOnly(chest);

        private static readonly System.Func<Container, bool> VehicleLeftAlone = chest =>
            (!Carts.Value && chest.GetComponentInParent<Vagon>() != null) ||
            (!Ships.Value && chest.GetComponentInParent<Ship>() != null);

        /// <summary>True in the frame the key goes down with its modifiers held, read through the game's own input.</summary>
        internal static bool Pressed(KeyboardShortcut key) =>
            key.MainKey != KeyCode.None && ZInput.GetKeyDown(key.MainKey, false) && key.Modifiers.All(modifier => ZInput.GetKey(modifier, false));

        /// <summary>The key as the game's hints write one: "Left Shift + P".</summary>
        internal static string KeyText(KeyboardShortcut key) =>
            string.Join(" + ", key.Modifiers.Concat(new[] { key.MainKey }).Select(Spaced));

        /// <summary>A key's name as the game's hints write it: "LeftShift" as "Left Shift", "Alpha1" as "1".</summary>
        internal static string Spaced(KeyCode code)
        {
            string name = code.ToString();
            if (name.StartsWith("Alpha") && name.Length == 6)
            {
                return name.Substring(5);
            }
            var spaced = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    spaced.Append(' ');
                }
                spaced.Append(name[i]);
            }
            return spaced.ToString();
        }
    }
}
