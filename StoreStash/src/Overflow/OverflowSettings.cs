using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace StoreStash
{
    /// <summary>What a tidy covers (card A42).</summary>
    internal enum TidyCovers
    {
        /// <summary>The chests set up like the one he points at, or has open.</summary>
        TheChestsGroup,

        /// <summary>Every group of chests set up alike within reach.</summary>
        EveryGroupInRange,
    }

    /// <summary>When two chests count as set up alike (card A42, decision 1).</summary>
    internal enum AlikeMeans
    {
        /// <summary>Their setups are the same, biomes and all.</summary>
        Identical,

        /// <summary>Their setups are the same once their biomes are left out.</summary>
        IgnoringBiomes,
    }

    /// <summary>What a tidy does with things no setup in the group takes.</summary>
    internal enum StrayChoice
    {
        LeftWhereTheyAre,
        KeptTogether,
    }

    /// <summary>Whether the chests a tidy changed are sorted (StoreStash's own sorting).</summary>
    internal enum TidySort
    {
        /// <summary>As Sorting's "Sort a chest as it opens" says: every chest, only chests he built, or none.</summary>
        LikeOpening,
        Always,
        Never,
    }

    /// <summary>What a tidy says after it.</summary>
    internal enum TidySay
    {
        OneLine,
        Detailed,
        Nothing,
    }

    /// <summary>
    /// The settings of Stack Overflow (card A42, runbook job 27, built inside StoreStash at his pick, 2026-10-05): chests set
    /// up alike act as one chest. StoreStash's section "Stack Overflow": in the single release a section of its own in
    /// modprojects.storestash.cfg, in the bundle part of the one "StoreStash" section, listed in F1 under StoreStash's
    /// heading behind its fold row (400), its switch open under it (399), then its groups, each row right above its settings:
    /// The tidy key and button (390; 389 to 385), How a tidy tidies (380; 379 to 371), Which chests take part (370; 369 to
    /// 366), By itself (360; 359, 358), What you're told (350; 349 to 346). Keys prefixed Overflow. The spill at quick stack
    /// time, the chest holding the most of an item first and the next when it's full, is "Which chest first"'s rule "...the
    /// one holding the most of it" (General): no setting of its own. Out of the box nothing a player sees changes (MODULES.md
    /// rule 4): the button is off, nothing tidies by itself, and only a press of the key tidies. Every setting takes effect
    /// at once.
    /// </summary>
    internal static class OverflowSettings
    {
        internal const string Part = "Stack Overflow";

        internal static ConfigEntry<bool> On { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> Key { get; private set; }

        internal static ConfigEntry<bool> ShowButton { get; private set; }

        internal static ConfigEntry<Vector2> ButtonOffset { get; private set; }

        internal static ConfigEntry<TidyCovers> Covers { get; private set; }

        internal static ConfigEntry<int> AskOver { get; private set; }

        internal static ConfigEntry<AlikeMeans> Alike { get; private set; }

        internal static ConfigEntry<Keeper> Keeps { get; private set; }

        internal static ConfigEntry<bool> MakeRoom { get; private set; }

        internal static ConfigEntry<bool> Merge { get; private set; }

        internal static ConfigEntry<int> LeaveFree { get; private set; }

        internal static ConfigEntry<bool> FreeFirst { get; private set; }

        internal static ConfigEntry<StrayChoice> Strays { get; private set; }

        internal static ConfigEntry<TidySort> Sort { get; private set; }

        internal static ConfigEntry<int> MostStacks { get; private set; }

        internal static ConfigEntry<float> Range { get; private set; }

        internal static ConfigEntry<bool> Carts { get; private set; }

        internal static ConfigEntry<bool> Ships { get; private set; }

        internal static ConfigEntry<bool> OthersMarked { get; private set; }

        internal static ConfigEntry<bool> AfterQuickStack { get; private set; }

        internal static ConfigEntry<int> EveryMinutes { get; private set; }

        internal static ConfigEntry<TidySay> Say { get; private set; }

        internal static ConfigEntry<bool> Sound { get; private set; }

        internal static ConfigEntry<bool> LightUp { get; private set; }

        internal static ConfigEntry<bool> KeyHint { get; private set; }

        /// <summary>True while StoreStash runs, is switched on, and Stack Overflow is on. Chest setups must be on too: without
        /// them no chest is set up, so none is alike.</summary>
        internal static bool Active => Module.IsOn && On != null && On.Value && SetupSettings.Active;

        internal static void Bind(ModuleSettings settings)
        {
            On = settings.Bind(Part, "OverflowOn", true,
                "Stack Overflow", 399,
                "On: chests set up alike (Chest setups) act as one chest. A tidy (its key, or its button) moves things between " +
                "them so each kind sits in as few of them as possible, moving other things out of the way where it must. Off: " +
                "no tidying. Either way, quick stack fills the chest holding the most of an item first and the next when it's " +
                "full, by \"Which chest first\" (Quick stack, What goes where).");

            // The tidy key and button.
            Key = settings.Bind(Part, "OverflowKey", new KeyboardShortcut(KeyCode.N, KeyCode.LeftAlt),
                "Tidy key", 389,
                "Pointing at a set-up chest, or with its window open: tidies the chests set up like it (or every group in " +
                "reach: \"What a tidy covers\"). Never while you type, or while a configuration manager's window (F1) is open.");

            ShowButton = settings.Bind(Part, "OverflowButton", false,
                "Tidy button in the chest window", 388,
                "A button beside an open set-up chest's window, under the setup button (or in its place), that does what the " +
                "tidy key does.");

            ButtonOffset = settings.Bind(Part, "OverflowButtonOffset", Vector2.zero,
                "Tidy button position (offset)", 387,
                "Moves the button from its place right of Place stacks, in the chest window's own units: x to the right, y up. " +
                "For when another mod puts something there.");

            Covers = settings.Bind(Part, "OverflowCovers", TidyCovers.TheChestsGroup,
                "What a tidy covers", 386,
                "TheChestsGroup: the chests set up like the one you point at (or have open). EveryGroupInRange: every group of " +
                "chests set up alike within reach, wherever you point.");

            AskOver = settings.Bind(Part, "OverflowAskOver", 0,
                "Ask first when a tidy would move more than (stacks)", 385,
                "A tidy that would move more stacks than this says how many first, and a second press within 10 seconds " +
                "tidies. A tidy by itself (after a quick stack, or on a timer) never asks: it moves no more than this many " +
                "stacks, and the rest waits for a press. 0: never ask.",
                new AcceptableValueRange<int>(0, 2000));

            // How a tidy tidies.
            Alike = settings.Bind(Part, "OverflowAlike", AlikeMeans.Identical,
                "Chests set up alike means", 379,
                "Identical: their setups are exactly the same, biomes and all. IgnoringBiomes: the same once their biomes are " +
                "left out (\"Hides from the Plains\" and \"Hides from the Meadows\" are one group). A thing still goes only " +
                "into a chest whose own setup takes it.");

            Keeps = settings.Bind(Part, "OverflowKeeper", Keeper.HoldingMost,
                "Which chest keeps a kind", 378,
                "A kind already all in one chest stays there while it has room. For one spread over several, of the chests " +
                "that can hold all of it, which one gets it. HoldingMost: the one already holding the most of it, as quick " +
                "stack's \"...the one holding the most of it\" counts, then the bigger, then the nearer. Biggest: the one with " +
                "the most cells. Nearest: the one nearest to you as you tidy. A kind too big for one chest fills the roomiest, " +
                "then as few others as the room allows.");

            MakeRoom = settings.Bind(Part, "OverflowMakeRoom", true,
                "Move other things out of the way", 377,
                "On: things in the way go to their own chests to make room (when every chest is full, one stack waits in a " +
                "chest with a free cell). Off: a kind only moves into the free room of a chest it prefers, and nothing is moved " +
                "for another's sake.");

            Merge = settings.Bind(Part, "OverflowMerge", true,
                "Merge part-stacks between chests", 376,
                "On: part-stacks of one thing in two chests are topped up into the chest that keeps it, to free a cell. Off: " +
                "only moves that bring a kind together (a stack moving into a chest still tops up its part-stacks there, as " +
                "the game always adds).");

            LeaveFree = settings.Bind(Part, "OverflowLeaveFree", 0,
                "Leave free cells in each chest", 375,
                "A tidy leaves this many empty cells in each chest where it can: a kind goes into a chest that keeps them free " +
                "if one can hold all of it, and uses them rather than be split over more chests (unless \"Keep free cells even " +
                "if a kind splits\" is on). With \"Move other things out of the way\" off, it never uses them.",
                new AcceptableValueRange<int>(0, 10));

            FreeFirst = settings.Bind(Part, "OverflowFreeFirst", false,
                "Keep free cells even if a kind splits", 374,
                "With \"Leave free cells in each chest\" above 0. On: the free cells are kept while the group has room for " +
                "them, a kind split over two chests (or more) if it must. Off: a kind is never split to keep them.");

            Strays = settings.Bind(Part, "OverflowStrays", StrayChoice.LeftWhereTheyAre,
                "Things no setup in the group takes", 373,
                "LeftWhereTheyAre: things in these chests that none of their setups takes (put there by hand, or before the " +
                "setup) stay put. KeptTogether: they're gathered too, among the group's chests.");

            Sort = settings.Bind(Part, "OverflowSort", TidySort.LikeOpening,
                "Sort the chests a tidy changed", 372,
                "LikeOpening: as \"Sort a chest as it opens\" says (Sorting), so a chest a tidy changed looks as it would when " +
                "you open it, its part-stacks merged. Always: by Sorting's settings, whatever that one says. Never: left as the " +
                "moves leave them. Only while Sorting is on.");

            MostStacks = settings.Bind(Part, "OverflowMostStacks", 500,
                "Most stacks moved per tidy", 371,
                "A tidy stops after moving this many stacks; press again to go on.",
                new AcceptableValueRange<int>(50, 2000));

            // Which chests take part.
            Range = settings.Bind(Part, "OverflowRange", 0f,
                "Tidy radius (metres)", 369,
                "How far from you a chest set up alike may be to take part. 0: quick stack's own search radius (Quick stack, Which chests). " +
                "\"Which chests\" (Quick stack) counts as for quick stack. While \"Use this range in every mod\" (Chests, every " +
                "mod) is on, that range.",
                new AcceptableValueRange<float>(0f, QoLMods.Chests.ChestSettings.MostRange));
            QoLMods.Chests.ChestSettings.ReachesThrough("StoreStash, " + Part + " (tidy)", "Tidy radius (metres)", Range,
                                                        "StoreStash's Search radius");   // card B13

            Carts = settings.Bind(Part, "OverflowCarts", false,
                "Carts' chests take part", 368,
                "A set-up cart's chest is tidied with the chests set up like it. Its chest is the cart's: while it's being " +
                "pulled, its puller's game refuses it, and it's left out.");

            Ships = settings.Bind(Part, "OverflowShips", false,
                "Ships' chests take part", 367,
                "A set-up ship's chest is tidied with the chests set up like it, but never one with someone else aboard: taking " +
                "the chest over would take the ship's steering from them.");

            OthersMarked = settings.Bind(Part, "OverflowOthersMarked", true,
                "Leave chests others marked alone", 366,
                "On: a chest someone else marked as theirs (Personal chests' mark) is never part of a tidy. Off: it's tidied " +
                "like any chest set up alike that you could open.");

            // By itself.
            AfterQuickStack = settings.Bind(Part, "OverflowAfterQuickStack", false,
                "Tidy after each quick stack", 359,
                "After each quick stack (P or H, the key or its button), the groups of the set-up chests it put things into are " +
                "tidied too, a moment later. It says something only when it moved something.");

            EveryMinutes = settings.Bind(Part, "OverflowEveryMinutes", 0,
                "Tidy every so many minutes", 358,
                "Every group of chests set up alike within reach is tidied this often, wherever you are. It says something only " +
                "when it moved something. 0: never. Each tidy claims the chests it changes from their owners and holds them " +
                "for a few seconds, so friends nearby may find them busy for a moment.",
                new AcceptableValueRange<int>(0, 60));

            // What you're told.
            Say = settings.Bind(Part, "OverflowSay", TidySay.OneLine,
                "Say what moved", 349,
                "OneLine: \"Tidied 3 chests: 14 stacks moved\". Detailed: that, and a line for each chest a tidy put things " +
                "into (\"3 Leather Scraps, 2 Deer Hide → Pelt chest\"). Nothing: only when it can't tidy. Where \"Message " +
                "after a quick stack\" puts it (Quick stack, Messages and sounds; top left while that's off).");

            Sound = settings.Bind(Part, "OverflowSound", true,
                "Sound after a tidy", 348,
                "The game's own sound of moving items, when a tidy moved anything.");

            LightUp = settings.Bind(Part, "OverflowLightUp", false,
                "Light up the chests a tidy changed", 347,
                "The chests a tidy changed glow for two seconds, in the light-up colour (Quick stack, Messages and sounds).");

            KeyHint = settings.Bind(Part, "OverflowKeyHint", false,
                "Tidy key hint on set-up chests", 346,
                "A line with the tidy key under every set-up chest you point at, as the game shows its own keys.");

            Folds.Fold overflow = Folds.Add(settings, Part, "OverflowUnfolded", Part, 400, new ConfigEntryBase[]
            {
                On,
            });
            Folds.AddGroup(settings, Part, "OverflowKeyUnfolded", "The tidy key and button", 390, new ConfigEntryBase[]
            {
                Key, ShowButton, ButtonOffset, Covers, AskOver,
            }, overflow);
            Folds.AddGroup(settings, Part, "OverflowHowUnfolded", "How a tidy tidies", 380, new ConfigEntryBase[]
            {
                Alike, Keeps, MakeRoom, Merge, LeaveFree, FreeFirst, Strays, Sort, MostStacks,
            }, overflow);
            Folds.AddGroup(settings, Part, "OverflowWhichUnfolded", "Which chests take part", 370, new ConfigEntryBase[]
            {
                Range, Carts, Ships, OthersMarked,
            }, overflow);
            Folds.AddGroup(settings, Part, "OverflowItselfUnfolded", "By itself", 360, new ConfigEntryBase[]
            {
                AfterQuickStack, EveryMinutes,
            }, overflow);
            Folds.AddGroup(settings, Part, "OverflowToldUnfolded", "What you're told", 350, new ConfigEntryBase[]
            {
                Say, Sound, LightUp, KeyHint,
            }, overflow);

            Key.SettingChanged += (sender, args) => TidyButton.TipChanged();   // the button's tooltip names the key
            Covers.SettingChanged += (sender, args) => TidyButton.TipChanged();   // and says what a click covers
        }

        /// <summary>How a tidy tidies, as the settings say now.</summary>
        internal static TidyPlan.Rules PlanRules() => new TidyPlan.Rules
        {
            Keeper = Keeps.Value,
            MakeRoom = MakeRoom.Value,
            Merge = Merge.Value,
            LeaveFree = Mathf.Clamp(LeaveFree.Value, 0, 10),
            FreeFirst = FreeFirst.Value,
            Strays = Strays.Value == StrayChoice.KeptTogether,
            MostStacks = Mathf.Clamp(MostStacks.Value, 50, 2000),
        };
    }
}
