using System.Collections.Generic;
using BepInEx.Configuration;
using QoLMods.Chests;
using QoLMods.Kit;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>What "Say what was added" does.</summary>
    internal enum SayAdded
    {
        Off,
        TopLeft,
        Centre,
    }

    /// <summary>Which chests a light's fuel comes from.</summary>
    internal enum FuelFrom
    {
        ChestsNearYou,
        ChestsNearTheLight,
    }

    /// <summary>Which light goes first when fuel is short.</summary>
    internal enum ShortFirst
    {
        TheEmptiest,
        TheNearest,
    }

    /// <summary>Where the reserves and maximums are counted.</summary>
    internal enum CountAround
    {
        AroundYou,
        AroundEachStation,
    }

    internal enum WhichChests
    {
        AnyChestICanOpen,
        OnlyChestsIBuilt,
    }

    internal enum ReservedChests
    {
        LeftAlone,
        UsedLikeAnyOther,
    }

    /// <summary>Where a station with linked input chests gets its fuel when they have none.</summary>
    internal enum LinkedFuel
    {
        FromChestsNearby,
        OnlyFromTheLinkedChests,
    }

    /// <summary>Lights and stations a friend's game without Topper-Upper looks after.</summary>
    internal enum FriendsWithout
    {
        LeaveThem,
        FeedThemAsAHandWould,
    }

    /// <summary>
    /// The player's settings (card A77's table and the research's section 9; his rule 3: every fork a setting). Alone, the
    /// mod keeps them in BepInEx\config\modprojects.topperupper.cfg, General and one section per part; inside miikeskii's
    /// QoL Pack, in the bundle's file, "Topper-Upper" and "Topper-Upper - &lt;part&gt;". A configuration manager's window (F1)
    /// lists them all under the one heading, each part behind a title row that opens and closes it (Folds; his pick, one
    /// heading per mod). Every one is read when it's used, so a change works at once. Keys are permanent from 0.1.0. Out of
    /// the box: lights and fires on, stations off (his pick, until his friend switches the server's feeding off).
    /// The order band 200 to 249 and the part "Loose items" belong to the loose-item pickup (BindLooseItems).
    /// </summary>
    internal static partial class Settings
    {
        internal const string LightsPart = "Lights and fires";
        internal const string StationsPart = "Stations";
        internal const string FirstPart = "What each station takes first";
        internal const string CountsPart = "Reserves and maximums";
        internal const string ChestsPart = "Which chests";
        internal const string LinksPart = "Linking and products";
        internal const string LoosePart = "Loose items";
        internal const string FriendsPart = "Friends and the server";

        /// <summary>The loose-item pickup's band of orders (its own part's title row and settings), highest first.</summary>
        internal const int LooseOrderTop = 249;
        internal const int LooseOrderBottom = 200;

        internal static ConfigEntry<bool> Enabled { get; private set; }

        // General
        internal static ConfigEntry<bool> FeedLights { get; private set; }
        internal static ConfigEntry<bool> FeedStations { get; private set; }
        internal static ConfigEntry<float> Range { get; private set; }
        internal static ConfigEntry<SayAdded> Say { get; private set; }
        internal static ConfigEntry<int> LookPerFrame { get; private set; }
        internal static ConfigEntry<int> FeedsPerFrame { get; private set; }
        internal static ConfigEntry<float> LightsEvery { get; private set; }
        internal static ConfigEntry<float> StationsEvery { get; private set; }
        internal static ConfigEntry<float> CountEvery { get; private set; }
        internal static ConfigEntry<bool> LogFeeds { get; private set; }

        // Lights and fires
        internal static ConfigEntry<float> LightRange { get; private set; }
        internal static readonly Dictionary<string, ConfigEntry<bool>> LightKinds = new Dictionary<string, ConfigEntry<bool>>();
        internal static ConfigEntry<bool> OtherLights { get; private set; }
        internal static ConfigEntry<bool> Candles { get; private set; }
        internal static ConfigEntry<bool> WorldFires { get; private set; }
        internal static ConfigEntry<LightTopUp> LightTopUpAt { get; private set; }
        internal static ConfigEntry<FuelFrom> LightFuelFrom { get; private set; }
        internal static ConfigEntry<float> LightChestRange { get; private set; }
        internal static ConfigEntry<ShortFirst> WhenShort { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> LightKey { get; private set; }
        internal static ConfigEntry<KeySteps> LightKeySteps { get; private set; }
        internal static ConfigEntry<bool> LightHover { get; private set; }

        // Stations
        internal static readonly Dictionary<string, ConfigEntry<bool>> StationKinds = new Dictionary<string, ConfigEntry<bool>>();
        internal static ConfigEntry<bool> OtherStations { get; private set; }
        internal static ConfigEntry<float> StationRadius { get; private set; }
        internal static ConfigEntry<StationTopUp> StationTopUpAt { get; private set; }
        internal static ConfigEntry<bool> FuelOnlyWhileWorking { get; private set; }
        internal static ConfigEntry<bool> TapFermenter { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> StationKey { get; private set; }
        internal static ConfigEntry<KeySteps> StationKeySteps { get; private set; }
        internal static ConfigEntry<bool> StationHover { get; private set; }

        // What each station takes first
        internal static readonly Dictionary<string, ConfigEntry<string>> Firsts = new Dictionary<string, ConfigEntry<string>>();
        internal static ConfigEntry<string> KilnWoods { get; private set; }
        internal static ConfigEntry<string> NeverTake { get; private set; }

        // Reserves and maximums
        internal static ConfigEntry<string> Reserves { get; private set; }
        internal static ConfigEntry<string> Maximums { get; private set; }
        internal static ConfigEntry<CountAround> CountWhere { get; private set; }
        internal static ConfigEntry<float> CountRange { get; private set; }
        internal static ConfigEntry<bool> CountReserved { get; private set; }
        internal static ConfigEntry<bool> CountCarried { get; private set; }
        internal static ConfigEntry<bool> CountStations { get; private set; }

        // Which chests
        internal static ConfigEntry<bool> LeaveOne { get; private set; }
        internal static ConfigEntry<WhichChests> Chests { get; private set; }
        internal static ConfigEntry<bool> PersonalChests { get; private set; }
        internal static ConfigEntry<ReservedChests> Reserved { get; private set; }

        // Linking and products
        internal static ConfigEntry<KeyboardShortcut> LinkKey { get; private set; }
        internal static ConfigEntry<bool> Glow { get; private set; }
        internal static ConfigEntry<Color> InputColour { get; private set; }
        internal static ConfigEntry<Color> OutputColour { get; private set; }
        internal static ConfigEntry<LinkedFuel> FuelWhenLinkedEmpty { get; private set; }
        internal static ConfigEntry<bool> LinkedKeepOne { get; private set; }
        internal static ConfigEntry<NoOutputChoice> NoLinkedOutput { get; private set; }
        internal static ConfigEntry<FullOutputChoice> LinkedOutputFull { get; private set; }
        internal static ConfigEntry<bool> OnlyWhereHeld { get; private set; }
        internal static ConfigEntry<bool> FollowSetups { get; private set; }
        internal static ConfigEntry<bool> LeaveOthersMarked { get; private set; }
        internal static ConfigEntry<float> WaitBeforePutting { get; private set; }
        internal static ConfigEntry<bool> EmptyWindmillEarly { get; private set; }

        // Friends and the server
        internal static ConfigEntry<FriendsWithout> Friends { get; private set; }
        internal static ConfigEntry<bool> StandAside { get; private set; }

        /// <summary>Goes up whenever any setting of this module changes, so what was worked out from them (the lists) is
        /// worked out again.</summary>
        internal static int Version { get; private set; }

        private static ModuleSettings bound;
        private static int order;
        private static ConfigEntry<bool> foldUnfolded;
        private static List<ConfigEntryBase> foldSettings;

        /// <summary>The loose-item pickup's part (Settings.Loose.cs, its own file): it binds its settings between
        /// "Linking and products" and "Friends and the server", with BeginFold(LoosePart), Add and EndFold, its orders
        /// from LooseOrderTop down to LooseOrderBottom. Left out, the part isn't there.</summary>
        static partial void BindLooseItems(ModuleSettings settings);

        internal static void Bind(ModuleSettings settings)
        {
            bound = settings;
            Enabled = settings.Enabled;
            Enabled.SettingChanged += (sender, args) => Version++;

            // The chest settings every mod of the pack shares (card A69): the range, "Chests beyond the server's reach"
            // and the log lines, in "Chests (every mod)".
            ChestSettings.Bind(settings);

            order = 990;
            FeedLights = Add(ModuleSettings.General, "FeedLights", true, "Feed lights and fires",
                "Torches, sconces, braziers, fires, the hearth and the hot tub near you are topped up with their fuel from the " +
                "chests near you, by the rules in \"Lights and fires\" and your reserves. Only the ones your game looks after " +
                "(the first player near a base looks after it); never from a chest another player's game has.");
            FeedStations = Add(ModuleSettings.General, "FeedStations", false,
                "Feed stations",
                "Smelters, the blast furnace, the charcoal kiln, the windmill, the spinning wheel, the eitr refinery, the " +
                "Frigid Kiln, the fermenter, the shield generator and the Frost Foundry are fed from the chests near them by " +
                "your rules (\"Stations\", \"What each station takes first\", \"Reserves and maximums\"), and what they make " +
                "is put away (\"Linking and products\"). Off out of the box until the server's own feeding (ServersideQoL's " +
                "AutoProcess, FeedFromContainers) is switched off, so the two don't feed the same station.");
            // 0 follows the shared range ("Chests (every mod)"), as the pack's other chest mods do (card A69).
            Range = Add(ModuleSettings.General, "Range", 0f, "Chest range (metres)",
                "How far from you a chest may be for lights' fuel (\"chests near you\") and for counting your reserves. " +
                ChestSettings.OwnRangeNote,
                new AcceptableValueRange<float>(0f, ChestSettings.MostRange));
            Say = Add(ModuleSettings.General, "SayAdded", SayAdded.Off, "Say what was added",
                "Off: it works quietly. TopLeft: a line at the top left for each feed (\"Added 10 Iron scrap to the Smelter\"). " +
                "Centre: the same in the middle of the screen.");
            LookPerFrame = Add(ModuleSettings.General, "LookPerFrame", 4, "Things looked at per frame",
                "How many lights and stations whose turn has come are looked at in one frame. More notices faster; fewer costs " +
                "less. 4 looks at 240 things a second at 60 frames a second.",
                new AcceptableValueRange<int>(1, 20));
            FeedsPerFrame = Add(ModuleSettings.General, "FeedsPerFrame", 1, "Feeds per frame",
                "How many lights or stations are fed in one frame. A burst on arriving at a base after a long absence is spread " +
                "over the next frames.",
                new AcceptableValueRange<int>(1, 5));
            LightsEvery = Add(ModuleSettings.General, "LightsEvery", 10f, "Look at each light every (seconds)",
                "How often each light or fire near you is looked at. A torch burns one resin in hours, so a few seconds is " +
                "plenty.",
                new AcceptableValueRange<float>(2f, 120f));
            StationsEvery = Add(ModuleSettings.General, "StationsEvery", 3f, "Look at each station every (seconds)",
                "How often each station is looked at, for feeding and for what it made.",
                new AcceptableValueRange<float>(1f, 60f));
            CountEvery = Add(ModuleSettings.General, "CountEvery", 2f, "Count the chests at most every (seconds)",
                "How often what the chests hold is counted again for your reserves and maximums. In between, each feed takes " +
                "off what it took.",
                new AcceptableValueRange<float>(0.5f, 10f));
            LogFeeds = Add(ModuleSettings.General, "LogFeeds", false, "Write what's fed to the log",
                "For testing: a line in BepInEx\\LogOutput.log for each feed and each put-away (what, how many, from and to " +
                "where). Off: no lines.");

            BeginFold(LightsPart, 950);
            LightRange = Add(LightsPart, "LightRange", 50f, "Light range (metres around you)",
                "Lights and fires within this distance of you are topped up.",
                new AcceptableValueRange<float>(5f, 128f));
            foreach (Kind kind in Kinds.Lights)
            {
                LightKinds[kind.Prefab] = Add(LightsPart, kind.Key, kind.Default, kind.Name,
                    $"Topped up from chests: {kind.Name}. {kind.Note ?? string.Empty}".Trim());
            }
            Candles = Add(LightsPart, "Candles", false, "Resin candles",
                "Refill resin candles too. The game itself won't let a hand refill them (they last their 3 resin and go out), " +
                "so this goes past the game's own rule.");
            OtherLights = Add(LightsPart, "OtherLights", true, "Other kinds of light",
                "Player-built lights and fires that aren't named above (another mod's torches) are topped up too.");
            WorldFires = Add(LightsPart, "WorldFires", false, "Fires the world made",
                "Fires and torches that weren't built by a player (a camp's fire, a dungeon's torch). Off: only what players built.");
            LightTopUpAt = Add(LightsPart, "LightTopUpAt", LightTopUp.RoomForOne, "Top up when it has room for",
                "RoomForOne: as soon as one more fuel fits (a torch stays nearly full). HalfEmpty: once it's half empty. Empty: " +
                "only once it's out.");
            LightFuelFrom = Add(LightsPart, "LightFuelFrom", FuelFrom.ChestsNearYou, "Take its fuel from",
                "ChestsNearYou: chests within the chest range of you. ChestsNearTheLight: only chests within the range below " +
                "of the light itself.");
            LightChestRange = Add(LightsPart, "LightChestRange", 10f, "Range around the light (metres)",
                "With ChestsNearTheLight: how near the light a chest must be.",
                new AcceptableValueRange<float>(1f, 50f));
            WhenShort = Add(LightsPart, "WhenShort", ShortFirst.TheEmptiest, "When fuel is short, first",
                "TheEmptiest: the light with the least fuel left is topped up first. TheNearest: the one nearest you.");
            LightKey = Add(LightsPart, "LightKey", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt), "Key: feed this one or leave it alone",
                "Pointing at a light or fire, switches it between fed and left alone; saved on it, for every player on the pack. " +
                "The game's own E isn't used for that press.");
            LightKeySteps = Add(LightsPart, "LightKeySteps", KeySteps.FedAndLeftAlone, "What the key does",
                "FedAndLeftAlone: switches between the two. FedLeftAloneAndAsItsKind: goes round three: as its kind's switch " +
                "says, fed whatever its kind says, left alone.");
            LightHover = Add(LightsPart, "LightHover", true, "Hover line",
                "A line in a light's hover text: fed or left alone, the key, and why it isn't fed when it isn't (\"Resin kept: " +
                "48 near you, 50 kept\").");
            EndFold();

            BeginFold(StationsPart, 890);
            foreach (Kind kind in Kinds.Stations)
            {
                StationKinds[kind.Prefab] = Add(StationsPart, kind.Key, kind.Default, kind.Name,
                    $"Fed from chests while \"Feed stations\" is on: {kind.Name}. {kind.Note ?? string.Empty}".Trim());
            }
            OtherStations = Add(StationsPart, "OtherStations", false, "Other mods' stations",
                "Stations another mod adds (its own smelters and kilns) are fed as the game's own are.");
            StationRadius = Add(StationsPart, "StationRadius", 10f, "Chest radius around a station (metres)",
                "A station takes from chests within this distance of it (unless it has input chests linked), and puts what it " +
                "made into chests within it. ServersideQoL used 4.",
                new AcceptableValueRange<float>(1f, 50f));
            StationTopUpAt = Add(StationsPart, "StationTopUpAt", StationTopUp.HalfEmpty, "Top up when it has room for",
                "HalfEmpty: once half its queue is free, then filled up: fewer, larger feeds. RoomForOne: as soon as one fits.");
            FuelOnlyWhileWorking = Add(StationsPart, "FuelOnlyWhileWorking", true, "Fuel only while there's something to process",
                "A smelter, furnace or refinery gets coal or sap only while it has something to smelt or refine, so an idle " +
                "one doesn't lock fuel away; the Frost Foundry only while it's casting; the stone oven only while it's baking.");
            TapFermenter = Add(StationsPart, "TapFermenter", true, "Tap the fermenter when it's ready",
                "A ready fermenter is tapped, and its meads put away like any product.");
            StationKey = Add(StationsPart, "StationKey", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt), "Key: feed this one or leave it alone",
                "Pointing at a station, switches it between fed and left alone; saved on it, for every player on the pack. The " +
                "game's own E isn't used for that press.");
            StationKeySteps = Add(StationsPart, "StationKeySteps", KeySteps.FedAndLeftAlone, "What the key does",
                "FedAndLeftAlone: switches between the two. FedLeftAloneAndAsItsKind: goes round three: as its kind's switch " +
                "says, fed whatever its kind says, left alone.");
            StationHover = Add(StationsPart, "StationHover", true, "Hover lines",
                "Lines in a station's hover text: fed or left alone, its linked chests, the keys, and why it isn't fed when it " +
                "isn't (\"Black Metal at its most: 121/120\").");
            EndFold();

            BeginFold(FirstPart, 840);
            Firsts["smelter"] = Add(FirstPart, "SmelterFirst", "SilverOre, IronScrap, IronOre, BronzeScrap, TinOre, CopperScrap, CopperOre", "Smelter",
                "What the smelter takes first, by the game's prefab names, separated by commas: the first gets the room first, " +
                "and one stopped by its reserve or maximum passes its turn. Names left out go last, in the game's order.");
            Firsts["blastfurnace"] = Add(FirstPart, "BlastFurnaceFirst", "GoldOre, FlametalOreNew, BlackMetalScrap", "Blast Furnace",
                "What the blast furnace takes first (GoldOre is Petrified Tissue, which makes Bloodgold). Names left out go last.");
            KilnWoods = Add(FirstPart, "KilnWoods", "Wood", "Charcoal kiln: the woods it may take",
                "The woods the charcoal kiln takes from chests, in order, by prefab names: Wood alone out of the box, so Fine " +
                "wood and Corewood (RoundLog) are kept for building. Empty: every wood it burns.");
            Firsts["windmill"] = Add(FirstPart, "WindmillFirst", "Oat, Barley, OatSeeds", "Windmill",
                "What the windmill takes first. Seeds last: Barley and Oat Seeds are also what their crops are planted from " +
                "(and the reserves keep some of each).");
            Firsts["fermenter"] = Add(FirstPart, "FermenterFirst", string.Empty, "Fermenter",
                "Which mead bases the fermenter takes first, by prefab names (MeadBaseHealthMinor …). Empty: the game's order.");
            Firsts["piece_shieldgenerator"] = Add(FirstPart, "ShieldFirst", "BoneFragments, CharredBone", "Shield Generator",
                "Which fuel the shield generator takes first.");
            Firsts["piece_turret"] = Add(FirstPart, "BallistaFirst", "TurretBoltWood", "Ballista",
                "Which missiles the ballista takes first (it holds one kind at a time; loaded, it takes only more of that kind).");
            NeverTake = Add(FirstPart, "NeverTake", string.Empty, "Never take, for anything",
                "Items never taken for any light or station, by prefab names, separated by commas.");
            EndFold();

            BeginFold(CountsPart, 810);
            Reserves = Add(CountsPart, "Reserves", "Resin:50, Barley:50, Flax:50, OatSeeds:50", "Keep in reserve",
                "Items kept back for everything this mod takes, as item:amount by prefab names. \"Resin:50\": torches are " +
                "topped up only while more than 50 Resin are near you, and never below 50. Barley, Flax and Oat Seeds are " +
                "kept as seed stock.");
            Maximums = Add(CountsPart, "Maximums", string.Empty, "Keep at most",
                "Products not made past an amount, as item:amount by prefab names. \"BlackMetal:120\": scrap stops going into the " +
                "blast furnace once 120 Black Metal are near you, counting what's queued in stations and lying at their " +
                "outputs (below), and starts again when you take some.");
            CountWhere = Add(CountsPart, "CountWhere", CountAround.AroundYou, "Count them",
                "AroundYou: reserves and maximums count the chests near you. AroundEachStation: the chests near each station, " +
                "for its own feed.");
            CountRange = Add(CountsPart, "CountRange", 0f, "Count range (metres)",
                "How far the count reaches. 0: the chest range (General).",
                new AcceptableValueRange<float>(0f, ChestSettings.MostRange));
            CountReserved = Add(CountsPart, "CountReserved", true, "Count reserved chests",
                "Chests reserved with Reserve Stock count toward reserves and maximums (they're never taken from).");
            CountCarried = Add(CountsPart, "CountCarried", false, "Count what you carry",
                "What you carry counts toward reserves and maximums too.");
            CountStations = Add(CountsPart, "CountStations", true, "Count what stations hold and what lies at their outputs",
                "For maximums: what's queued in the stations nearby (and a windmill's flour, a fermenter's meads), and what " +
                "lies at their outputs, counts as made. Off: only the chests count, and a full furnace can overshoot by its " +
                "queue.");
            EndFold();

            BeginFold(ChestsPart, 780);
            LeaveOne = Add(ChestsPart, "LeaveOne", true, "Leave one of each item in every chest",
                "One of each stackable item stays in every chest it takes from, so the quick stack still knows where it belongs.");
            Chests = Add(ChestsPart, "Chests", WhichChests.AnyChestICanOpen, "Which chests",
                "AnyChestICanOpen: every chest you could open by hand (never a private one or one behind a ward that isn't " +
                "yours). OnlyChestsIBuilt: only chests you built. The world's own chests and gravestones are never used.");
            PersonalChests = Add(ChestsPart, "PersonalChests", true, "Use my personal chests",
                "On: your own personal chests are used like any other. Off: they're left alone.");
            Reserved = Add(ChestsPart, "ReservedChests", ReservedChests.LeftAlone, "Reserved chests",
                "Chests reserved with Reserve Stock. LeftAlone: never taken from, and put into only when linked to a station as " +
                "its output. UsedLikeAnyOther: used as any chest.");
            EndFold();

            BeginFold(LinksPart, 760);
            LinkKey = Add(LinksPart, "LinkKey", new KeyboardShortcut(KeyCode.J, KeyCode.LeftAlt), "Key: link this station's chests",
                "Pointing at a station, starts linking its chests: then E on a chest makes it an input (the station takes only " +
                "from its inputs), Left Shift + E an output (what it makes goes there); the same again unlinks it. The key " +
                "again at the station, opening a menu, walking 30 m away or a minute without a change ends it.");
            Glow = Add(LinksPart, "Glow", true, "Linked chests glow",
                "While you point at a station, and while linking, its linked chests glow (only on your screen).");
            InputColour = Add(LinksPart, "InputColour", new Color(0.2f, 1f, 0.3f, 1f), "Input colour", "The glow of a linked input chest.");
            OutputColour = Add(LinksPart, "OutputColour", new Color(0.3f, 0.6f, 1f, 1f), "Output colour", "The glow of a linked output chest.");
            FuelWhenLinkedEmpty = Add(LinksPart, "FuelWhenLinkedEmpty", LinkedFuel.FromChestsNearby, "Fuel when the linked inputs have none",
                "A station with linked input chests takes its fuel from them too. FromChestsNearby: when they have none, from " +
                "the chests near it. OnlyFromTheLinkedChests: never from anywhere else.");
            LinkedKeepOne = Add(LinksPart, "LinkedKeepOne", true, "Linked input chests keep one of each",
                "One of each item stays in a linked input chest too. Off: it may be emptied.");
            NoLinkedOutput = Add(LinksPart, "NoLinkedOutput", NoOutputChoice.PutAwayLikeTheQuickStack, "Products with no linked output",
                "PutAwayLikeTheQuickStack: into chests near the station, by the quick stack's rules below. LeaveWhereTheyFall: " +
                "left on the ground for you.");
            LinkedOutputFull = Add(LinksPart, "LinkedOutputFull", FullOutputChoice.PutAwayLikeTheQuickStack, "When the linked output is full",
                "PutAwayLikeTheQuickStack: the rest into chests near the station. LeaveWhereTheyFall: left on the ground. " +
                "StopFeedingTheStation: left, and the station isn't fed until its output has room again.");
            OnlyWhereHeld = Add(LinksPart, "OnlyWhereHeld", true, "Put away only into chests that already hold it",
                "As the quick stack: a product goes only into a chest already holding some (or set up to take it). Off: any " +
                "chest near the station with room.");
            FollowSetups = Add(LinksPart, "FollowSetups", true, "Follow chest setups",
                "Chests set up with StoreStash to take only some things get only those, and get them first.");
            LeaveOthersMarked = Add(LinksPart, "LeaveOthersMarked", true, "Leave chests others marked as theirs alone",
                "A chest another player marked as theirs (StoreStash's Left Alt + H) gets nothing put away.");
            WaitBeforePutting = Add(LinksPart, "WaitBeforePutting", 1f, "Wait before putting a product away (seconds)",
                "How long a product lies at its station's output before it's put away.",
                new AcceptableValueRange<float>(0.5f, 10f));
            EmptyWindmillEarly = Add(LinksPart, "EmptyWindmillEarly", false, "Empty the windmill as soon as it holds flour",
                "The windmill keeps its flour until a stack is full or its input changes. On: its flour comes out (and is put " +
                "away) as soon as it holds some.");
            EndFold();

            // The loose-item pickup's part, its own band of orders (Settings.Loose.cs).
            order = LooseOrderTop;
            BindLooseItems(settings);

            BeginFold(FriendsPart, 190);
            Friends = Add(FriendsPart, "FriendsWithout", FriendsWithout.LeaveThem, "Lights and stations a friend's game without this mod looks after",
                "LeaveThem: not fed. FeedThemAsAHandWould: fed from your chests the way pressing E does, one message to that " +
                "friend's game per item; if they walk away while it's on its way, that item is lost.");
            StandAside = Add(FriendsPart, "StandAside", true, "Stand aside while the server feeds a station",
                "A station near you that the server takes over for a moment (ServersideQoL's AutoProcess writes stations that " +
                "way) is left alone for a minute, so the two don't write it at the same instant.");
            EndFold();
        }

        /// <summary>A light kind's switch, or null for a kind not named.</summary>
        internal static ConfigEntry<bool> LightKind(string prefab) => prefab != null && LightKinds.TryGetValue(prefab, out ConfigEntry<bool> on) ? on : null;

        /// <summary>A station kind's switch, or null for a kind not named.</summary>
        internal static ConfigEntry<bool> StationKind(string prefab) => prefab != null && StationKinds.TryGetValue(prefab, out ConfigEntry<bool> on) ? on : null;

        /// <summary>A station's "takes first" line, or null for a kind without one.</summary>
        internal static string FirstOf(string prefab) => prefab != null && Firsts.TryGetValue(prefab, out ConfigEntry<string> line) ? line.Value : null;

        /// <summary>The chest range of you, in metres (its own, or the shared one).</summary>
        internal static float ChestRange => ChestSettings.RangeOf(Range);

        /// <summary>The range the reserves and maximums are counted in, in metres.</summary>
        internal static float CountingRange => CountRange != null && CountRange.Value >= ChestSettings.LeastRange
            ? Mathf.Min(CountRange.Value, ChestSettings.MostRange)
            : ChestRange;

        /// <summary>The chest rules as the settings say right now, within <paramref name="range"/> metres. The Obliterator's
        /// container is always left out (Obliterator; code review M2).</summary>
        internal static ChestRules Rules(float range) => new ChestRules
        {
            Range = range,
            OnlyBuiltByMe = Chests.Value == WhichChests.OnlyChestsIBuilt,
            LeaveOne = LeaveOne.Value,
            Skip = PersonalChests.Value ? SkipObliterator : SkipObliteratorAndPersonal,
            UseReserved = Reserved.Value == ReservedChests.UsedLikeAnyOther,
        };

        /// <summary>The rules for counting: the same chests, with his personal chests counted even when they aren't used
        /// (as reserved chests are), leave-one off (it's for taking), reserved ones apart.</summary>
        internal static ChestRules CountRules(float range) => new ChestRules
        {
            Range = range,
            OnlyBuiltByMe = Chests.Value == WhichChests.OnlyChestsIBuilt,
            Skip = SkipObliterator,
        };

        private static readonly System.Func<Container, bool> SkipObliterator = Obliterator.Is;

        // His own personal chests too (the game's CheckAccess already keeps everyone else's out).
        private static readonly System.Func<Container, bool> SkipObliteratorAndPersonal =
            chest => Obliterator.Is(chest) || chest.m_privacy == Container.PrivacySetting.Private;

        /// <summary>Binds one setting of a part, the next order down, as part of the open fold-out (if any). For the
        /// loose-item pickup's part too.</summary>
        internal static ConfigEntry<T> Add<T>(string part, string key, T value, string name, string description, AcceptableValueBase range = null)
        {
            ConfigEntry<T> entry = bound.Bind(part, key, value, name, order--, description, range);
            entry.SettingChanged += (sender, args) => Version++;
            foldSettings?.Add(entry);
            return entry;
        }

        /// <summary>Opens a part's fold-out at <paramref name="top"/> (its title row's order; its settings follow, one order
        /// down each): a title row, then its settings, all under the module's one heading. The title row is a setting of the
        /// part's own ("Unfolded"), which remembers whether it is open. For the loose-item pickup's part too.</summary>
        internal static void BeginFold(string part, int top)
        {
            order = top;
            var settings = new List<ConfigEntryBase>();
            foldUnfolded = bound.Bind(part, "Unfolded", false, part, order--,
                "Opens and closes this part of the settings in the window. It changes nothing else.", null, FoldDrawer.For(part, settings));
            foldSettings = settings;
        }

        internal static void EndFold()
        {
            Folds.Add(foldUnfolded, foldSettings, bound.SectionOf(ModuleSettings.General));
            foldUnfolded = null;
            foldSettings = null;
        }
    }
}
