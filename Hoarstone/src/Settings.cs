using System.Collections.Generic;
using BepInEx.Configuration;
using Hoarstone.Shapes;
using QoLMods.Kit;
using QoLMods.ListPicker;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>Which settlements keep monsters out.</summary>
    internal enum WhichSettlements
    {
        OnlyWithAHoarstone,
        EverySettlement,
    }

    /// <summary>The size of the squares a shape is worked out in.</summary>
    internal enum SquareSize
    {
        OneMetre,
        TwoMetres,
        FourMetres,
    }

    /// <summary>Where outlines are drawn.</summary>
    internal enum OutlinePlace
    {
        OnTheGround,
        OnTheMap,
        Both,
    }

    /// <summary>
    /// The player's settings (card A88's report, the plan's table; his rule 3). Alone, the mod keeps them in
    /// BepInEx\config\modprojects.hoarstone.cfg, General and one section per part; inside miikeskii's QoL Pack, in the
    /// bundle's file, "Hoarstone" and "Hoarstone - &lt;part&gt;". A configuration manager's window (F1) lists them all under
    /// the one heading, each part behind a title row that opens and closes it (Folds). Every one is read when it is used,
    /// so a change works at once. Out of the box nothing a player meets changes until someone places a Hoarstone ("Where",
    /// his call of 2026-10-10) or turns "Where" to every settlement (MODULES.md rule 4).
    /// </summary>
    internal static class Settings
    {
        internal const string SettlementsPart = "Settlements";
        internal const string ShapePart = "Shape";
        internal const string SpawnsPart = "Spawns";
        internal const string StonePart = "The Hoarstone";
        internal const string OutlinesPart = "Outlines";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        // General
        internal static ConfigEntry<bool> KeepSpawnsOut { get; private set; }
        internal static ConfigEntry<WhichSettlements> Where { get; private set; }
        internal static ConfigEntry<bool> LogWork { get; private set; }

        // Settlements
        internal static ConfigEntry<int> JoinDistance { get; private set; }
        internal static ConfigEntry<int> Neighbours { get; private set; }
        internal static ConfigEntry<int> SmallestSettlement { get; private set; }
        internal static ConfigEntry<string> NeverCount { get; private set; }

        // Shape
        internal static ConfigEntry<int> GapsToClose { get; private set; }
        internal static ConfigEntry<int> Margin { get; private set; }
        internal static ConfigEntry<int> ShoreMargin { get; private set; }
        internal static ConfigEntry<SquareSize> Detail { get; private set; }

        // Spawns
        internal static ConfigEntry<bool> FixedSpawnPoints { get; private set; }
        internal static ConfigEntry<bool> Nests { get; private set; }
        internal static ConfigEntry<bool> RaidMonsters { get; private set; }
        internal static ConfigEntry<bool> LetInWhatTheGameLetsIn { get; private set; }

        // The Hoarstone
        internal static ConfigEntry<bool> InHammerMenu { get; private set; }
        internal static ConfigEntry<string> Cost { get; private set; }
        internal static ConfigEntry<bool> AlsoAWard { get; private set; }
        internal static ConfigEntry<Color> Tint { get; private set; }
        internal static ConfigEntry<bool> PointingShows { get; private set; }

        // Outlines
        internal static ConfigEntry<KeyboardShortcut> ShowKey { get; private set; }
        internal static ConfigEntry<OutlinePlace> DrawWhere { get; private set; }
        internal static ConfigEntry<Color> KeptColour { get; private set; }
        internal static ConfigEntry<bool> ShowOthers { get; private set; }
        internal static ConfigEntry<Color> OthersColour { get; private set; }

        /// <summary>Goes up whenever a setting a shape depends on changes, so the shapes are worked out again.</summary>
        internal static int ShapeVersion { get; private set; }

        /// <summary>Goes up whenever "Pieces that never count" changes.</summary>
        internal static int NeverCountVersion { get; private set; }

        private static ModuleSettings bound;
        private static int order;
        private static ConfigEntry<bool> foldUnfolded;
        private static List<ConfigEntryBase> foldSettings;

        internal static void Bind(ModuleSettings settings)
        {
            bound = settings;
            Enabled = settings.Enabled;
            order = 990;

            KeepSpawnsOut = Add(ModuleSettings.General, "KeepSpawnsOut", true, "Keep monsters from spawning in settlements",
                "On: monsters don't spawn anywhere inside a settlement's shape (the settlements \"Where\" picks), as they don't " +
                "spawn near a campfire or a workbench. It stops spawns, not monsters walking in. It works wherever the game " +
                "deciding the spawns runs this mod: the game that looks after the place, usually that of the first player who " +
                "came near and stayed. Off: monsters spawn as in the game; outlines and the Hoarstone still work.");
            Where = Add(ModuleSettings.General, "Where", WhichSettlements.OnlyWithAHoarstone, "Where",
                "OnlyWithAHoarstone (the default): only settlements with a Hoarstone in them keep monsters out, so nothing " +
                "changes until someone places one. EverySettlement: every group of pieces players built that counts as a " +
                "settlement (see Settlements below), with or without a Hoarstone.");
            LogWork = Add(ModuleSettings.General, "LogWork", false, "Write what it works out to the log",
                "For testing: each time the shapes are worked out, how many settlements, squares and milliseconds, and each " +
                "saved shape written, in BepInEx\\LogOutput.log.");

            BeginFold(SettlementsPart);
            JoinDistance = AddShape(SettlementsPart, "JoinDistance", 24, "Pieces this close belong together (m)",
                "A piece this close to another joins its settlement. Larger joins places farther apart into one settlement.",
                new AcceptableValueRange<int>(8, 50));
            Neighbours = AddShape(SettlementsPart, "Neighbours", 4, "Neighbours a piece needs to pass that on",
                "A piece with at least this many pieces within the distance above (itself included) brings the pieces near " +
                "it into its settlement; one with fewer joins but brings no one. At 1 a line of torches along a road joins " +
                "two places into one; at 4 it doesn't.",
                new AcceptableValueRange<int>(1, 8));
            SmallestSettlement = AddShape(SettlementsPart, "SmallestSettlement", 8, "Smallest settlement (pieces)",
                "Fewer pieces than this, and it isn't a settlement (a lone portal, a campfire on the road). A settlement with a " +
                "Hoarstone in it counts however small.",
                new AcceptableValueRange<int>(1, 40));
            NeverCount = Add(SettlementsPart, "NeverCount", "", "Pieces that never count",
                "Pieces left out of every settlement, by their prefab names, separated by commas (for example portal_wood). " +
                "Ships and carts never count, since they move.");
            NeverCount.SettingChanged += (sender, args) => NeverCountVersion++;
            // The Pick buttons in F1 (card B03): this list and the cost's picked from pictures of the game's things, their
            // text written as World\Records and Shapes\CostText read it.
            PickerSettings.Bind(bound);
            Lists.Attach(NeverCount, new ListSpec
            {
                Module = Module.Name,
                Kinds = new[] { ThingKind.Pieces },
                Format = new ListFormat { Separators = ",", Joiner = ", " },
                Says = "Pieces left out of every settlement (ships and carts never count anyway).",
                Warn = text => Module.Log?.LogWarning(text),
            });
            EndFold();

            BeginFold(ShapePart);
            GapsToClose = AddShape(ShapePart, "GapsToClose", 30, "Gaps to close (m)",
                "Openings in a settlement narrower than this (a gate, a broken palisade, the space between two houses) are " +
                "closed, and what they enclose is inside. Wider bays and openings stay open. 0 follows the pieces alone.",
                new AcceptableValueRange<int>(0, 60));
            Margin = AddShape(ShapePart, "Margin", 8, "Margin (m)",
                "Added all round the shape, like the edge a campfire's circle leaves today.",
                new AcceptableValueRange<int>(0, 30));
            ShoreMargin = AddShape(ShapePart, "ShoreMargin", 20, "Shore margin (m)",
                "How far the shape reaches out over the water from its land. Land and water are the ground's height against " +
                "the sea, so ground raised or dug with the hoe counts, once your game has been near it this session.",
                new AcceptableValueRange<int>(0, 40));
            Detail = AddShape(ShapePart, "Detail", SquareSize.TwoMetres, "Detail",
                "The squares a shape is worked out in. TwoMetres (the default) is far finer than spawning needs; OneMetre " +
                "takes about four times the work, FourMetres a quarter.");
            EndFold();

            BeginFold(SpawnsPart);
            FixedSpawnPoints = Add(SpawnsPart, "FixedSpawnPoints", true, "Fixed spawn points too",
                "On: the world's fixed spawn points inside a settlement (at ruins, camps and the like you built around) don't " +
                "spawn either, as the game already stops them near a campfire or a workbench.");
            Nests = Add(SpawnsPart, "Nests", false, "Nests inside a settlement too",
                "On: nests and other spawners (greydwarf nests, bone piles, the like) don't spawn into a settlement. The game " +
                "itself never stops them near a base, so this is off out of the box.");
            RaidMonsters = Add(SpawnsPart, "RaidMonsters", true, "Raid monsters too",
                "On: a raid's monsters don't spawn inside a settlement either, as the game already keeps them out of its " +
                "base circles: they spawn outside and come in. Deep inside a large settlement a raid may find nowhere to " +
                "spawn, as it can't among enough torches today. Off: they may spawn inside.");
            LetInWhatTheGameLetsIn = Add(SpawnsPart, "LetInWhatTheGameLetsIn", true, "Let in what the game lets into a base",
                "On: whatever the game itself lets spawn inside a base may spawn inside a settlement too (today only the " +
                "falling cinders of the Ashlands' weather, and a few fixed spawn points; no wild creature). Off: those are " +
                "kept out as well.");
            EndFold();

            BeginFold(StonePart);
            InHammerMenu = Add(StonePart, "InHammerMenu", true, "The Hoarstone in the hammer's menu",
                "On: the Hoarstone sits next to the Ward in the hammer's menu, once you know all of its materials. Off takes it " +
                "out; Hoarstones already placed stay.");
            Cost = Add(StonePart, "Cost", CostText.Default, "Its cost",
                "What placing a Hoarstone costs, by item prefab names with an amount after a colon, separated by commas. " +
                "Taking one down gives back what it cost when it was placed. The game that looks after it makes that drop: " +
                "where that's the game of a friend without this mod, it drops a Ward's materials, since to that game it's a " +
                "Ward. Left empty or unreadable, it costs what a Ward does.");
            Lists.Attach(Cost, new ListSpec
            {
                Module = Module.Name,
                Kinds = new[] { ThingKind.Items },
                Format = new ListFormat
                {
                    Separators = ",;",
                    Joiner = ", ",
                    Amounts = true,
                    NewAmount = 1,
                    LeastAmount = 1,
                    MostAmount = 9999,
                    AmountLeftOut = 1,
                    AmountsAddUp = true,
                    Ordered = true,
                    IgnoreCase = false,
                },
                Says = "What placing a Hoarstone costs, in the order the build menu shows it.",
                EmptyMeans = "a Ward's cost",
                Warn = text => Module.Log?.LogWarning(text),
            });
            AlsoAWard = Add(StonePart, "AlsoAWard", false, "It's also a ward",
                "Off (the default): a Hoarstone is no ward at all, and E on it shows or hides its settlement's outline. On: " +
                "its placer can switch it on as a Ward with E, and then it protects its area from everyone not on its list, " +
                "friends with or without this mod alike. A Hoarstone switched on stays a Ward to everyone until its placer " +
                "switches it off.");
            Tint = Add(StonePart, "Tint", new Color(0.62f, 0.8f, 1f, 1f), "Its tint",
                "The colour a Hoarstone is tinted in your game, so you can tell it from a Ward. White leaves it as a Ward looks. " +
                "Only games with this mod see the tint.");
            PointingShows = Add(StonePart, "PointingShows", true, "Pointing at it shows its settlement",
                "On: while you point at a Hoarstone, its settlement's outline shows, instead of a Ward's circle.");
            EndFold();

            BeginFold(OutlinesPart);
            ShowKey = Add(OutlinesPart, "ShowKey", new KeyboardShortcut(KeyCode.F3, KeyCode.LeftAlt), "Show outlines",
                "Shows or hides every settlement's outline. Left Alt + F3 by default; the game uses F3 for nothing.");
            DrawWhere = Add(OutlinesPart, "Where", OutlinePlace.Both, "Where",
                "OnTheGround: on the ground around you. OnTheMap: on the big map and the small map in the corner. Both (the " +
                "default).");
            KeptColour = Add(OutlinesPart, "KeptColour", new Color(0.95f, 0.71f, 0.23f, 1f), "Colour of a settlement kept free",
                "The outline's colour where monsters don't spawn.");
            ShowOthers = Add(OutlinesPart, "ShowOthers", true, "Settlements not kept free too",
                "On: settlements that don't keep monsters out (no Hoarstone, while \"Where\" is OnlyWithAHoarstone) are " +
                "outlined too, in their own colour, to show where a Hoarstone would make one.");
            OthersColour = Add(OutlinesPart, "OthersColour", new Color(0.85f, 0.85f, 0.85f, 0.75f), "Their colour",
                "The outline's colour for a settlement that doesn't keep monsters out.");
            EndFold();
        }

        /// <summary>The square size in metres.</summary>
        internal static double Cell => Detail.Value == SquareSize.OneMetre ? 1 : Detail.Value == SquareSize.FourMetres ? 4 : 2;

        /// <summary>The shape's options as the settings say now.</summary>
        internal static ShapeOptions Options() => new ShapeOptions
        {
            Eps = JoinDistance.Value,
            MinPts = Neighbours.Value,
            MinSize = SmallestSettlement.Value,
            Gap = GapsToClose.Value,
            Margin = Margin.Value,
            Shore = ShoreMargin.Value,
            Cell = Cell,
            StoneRules = true,
        };

        /// <summary>The tile layers "Where" asks for: bit 1 the Hoarstone layer, bit 2 the other.</summary>
        internal static int KeptLayers => Where.Value == WhichSettlements.EverySettlement ? 3 : 1;

        private static ConfigEntry<T> Add<T>(string part, string key, T value, string name, string description, AcceptableValueBase range = null)
        {
            ConfigEntry<T> entry = bound.Bind(part, key, value, name, order--, description, range);
            foldSettings?.Add(entry);
            return entry;
        }

        private static ConfigEntry<T> AddShape<T>(string part, string key, T value, string name, string description, AcceptableValueBase range = null)
        {
            ConfigEntry<T> entry = Add(part, key, value, name, description, range);
            entry.SettingChanged += (sender, args) => ShapeVersion++;
            return entry;
        }

        // Each part is a fold-out: a title row, then its settings, all under the module's one heading. The title row is a
        // setting of the part's own ("Unfolded"), which remembers whether it is open.
        private static void BeginFold(string part)
        {
            var settings = new List<ConfigEntryBase>();
            foldUnfolded = bound.Bind(part, "Unfolded", false, part, order--,
                "Opens and closes this part of the settings in the window. It changes nothing else.", null, FoldDrawer.For(part, settings));
            foldSettings = settings;
        }

        private static void EndFold()
        {
            Folds.Add(foldUnfolded, foldSettings, bound.SectionOf(ModuleSettings.General));
            foldUnfolded = null;
            foldSettings = null;
        }
    }
}
