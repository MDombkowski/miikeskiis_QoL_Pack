using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using QoLMods.Kit;
using QoLMods.ListPicker;
using UnityEngine;
using HoarstoneLink = QoLMods.HoarstoneGround.Ground;

namespace HouseTrained
{
    /// <summary>Which tames a part looks after.</summary>
    internal enum WhichTames
    {
        EveryTame,
        TheList,
    }

    /// <summary>What counts as a wall for a tame in a pen.</summary>
    internal enum WallKind
    {
        PiecesPlayersBuilt,
        AnyBuildingPiece,
    }

    /// <summary>Whether a tame in a pen guards a Hoarstone's ground (card B26).</summary>
    internal enum PennedGuard
    {
        Never,
        ThroughAnOpenGate,
        Always,
    }

    /// <summary>
    /// The player's settings (card A48's table, and what the build plan added; his rule 3). Alone, the mod keeps them in
    /// BepInEx\config\modprojects.housetrained.cfg, General and one section per part; inside miikeskii's QoL Pack, in the
    /// bundle's file, "House Trained" and "House Trained - &lt;part&gt;". A configuration manager's window (F1) lists them
    /// all under the one heading, each part behind a title row that opens and closes it (Folds; his look at F1,
    /// 2026-09-28). Every one is read when it is used, so a change works at once. Out of the box each part's own switch
    /// is on since card A63 (2026-10-06: his own settings made the pack's defaults; off before, MODULES.md rule 4), and a
    /// file still at an old default moves once (the kit's MovedDefault).
    /// </summary>
    internal static class Settings
    {
        internal const string SparePart = "Tames spare buildings";
        internal const string FriendlyPart = "Friendly fire";
        internal const string PensPart = "Tames in pens";
        internal const string KeptInPart = "Pens: kept in";
        internal const string HeadsPart = "Pens: heads";
        internal const string WallsPart = "Pens: enemies behind walls";
        internal const string WanderPart = "Pens: wandering";
        internal const string HealPart = "Pens: healing";
        internal const string FeedPart = "Feeding from the hand";
        internal const string FollowPart = "Following";
        internal const string GroundPart = "With the Hoarstone";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        // Tames spare buildings
        internal static ConfigEntry<bool> Spare { get; private set; }
        internal static ConfigEntry<WhichTames> SpareWhich { get; private set; }
        internal static ConfigEntry<string> SpareList { get; private set; }
        internal static ConfigEntry<bool> SpareSummons { get; private set; }
        internal static ConfigEntry<bool> SpareTrample { get; private set; }
        internal static ConfigEntry<bool> SparePlayerBuilt { get; private set; }
        internal static ConfigEntry<bool> SpareShips { get; private set; }
        internal static ConfigEntry<bool> SpareWorld { get; private set; }
        internal static ConfigEntry<bool> SpareCrops { get; private set; }
        internal static ConfigEntry<int> SpareShare { get; private set; }
        internal static ConfigEntry<bool> SpareLog { get; private set; }

        // Friendly fire
        internal static ConfigEntry<bool> Friendly { get; private set; }
        internal static ConfigEntry<bool> FfAreaAttacks { get; private set; }
        internal static ConfigEntry<bool> FfAreaObjects { get; private set; }
        internal static ConfigEntry<bool> FfBlasts { get; private set; }
        internal static ConfigEntry<bool> FfMelee { get; private set; }
        internal static ConfigEntry<bool> FfProjectiles { get; private set; }
        internal static ConfigEntry<int> FfShare { get; private set; }
        internal static ConfigEntry<bool> FfOwnKind { get; private set; }
        internal static ConfigEntry<bool> FfBosses { get; private set; }
        internal static ConfigEntry<string> FfNever { get; private set; }
        internal static ConfigEntry<bool> FfHurtTames { get; private set; }
        internal static ConfigEntry<bool> FfFightBack { get; private set; }
        internal static ConfigEntry<int> FfGrudge { get; private set; }
        internal static ConfigEntry<bool> FfOnlyAll { get; private set; }
        internal static ConfigEntry<int> FfNearby { get; private set; }
        internal static ConfigEntry<bool> FfLog { get; private set; }

        // Tames in pens
        internal static ConfigEntry<bool> Pens { get; private set; }
        internal static ConfigEntry<WhichTames> PensWhich { get; private set; }
        internal static ConfigEntry<string> PensList { get; private set; }
        internal static ConfigEntry<bool> PensRidden { get; private set; }
        internal static ConfigEntry<bool> PensLog { get; private set; }

        // Pens: kept in (his point 1)
        internal static ConfigEntry<bool> NoStacking { get; private set; }
        internal static ConfigEntry<float> Nudge { get; private set; }
        internal static ConfigEntry<bool> NoClimbing { get; private set; }
        internal static ConfigEntry<float> Step { get; private set; }
        internal static ConfigEntry<bool> Births { get; private set; }
        internal static ConfigEntry<bool> GrowUps { get; private set; }

        // Pens: heads (his point 2)
        internal static ConfigEntry<bool> Heads { get; private set; }
        internal static ConfigEntry<float> HeadMargin { get; private set; }
        internal static ConfigEntry<bool> EaseOut { get; private set; }

        // Pens: enemies behind walls (his point 3)
        internal static ConfigEntry<bool> NoSight { get; private set; }
        internal static ConfigEntry<bool> LetGo { get; private set; }
        internal static ConfigEntry<float> LetGoAfter { get; private set; }
        internal static ConfigEntry<WallKind> Walls { get; private set; }

        // Pens: wandering (his point 4)
        internal static ConfigEntry<bool> Wander { get; private set; }
        internal static ConfigEntry<float> WallMargin { get; private set; }
        internal static ConfigEntry<int> Tries { get; private set; }
        internal static ConfigEntry<float> Stuck { get; private set; }
        internal static ConfigEntry<bool> FarHome { get; private set; }
        internal static ConfigEntry<float> FarHomeTimes { get; private set; }
        internal static ConfigEntry<int> Range { get; private set; }

        // Pens: healing (card A84)
        internal static ConfigEntry<bool> Heal { get; private set; }
        internal static ConfigEntry<int> HealRate { get; private set; }
        internal static ConfigEntry<int> HealReach { get; private set; }
        internal static ConfigEntry<int> HealGaps { get; private set; }
        internal static ConfigEntry<bool> HealNumbers { get; private set; }
        internal static ConfigEntry<bool> HealLog { get; private set; }

        // Feeding from the hand (card A84)
        internal static ConfigEntry<bool> Feed { get; private set; }
        internal static ConfigEntry<FeedWhen> FeedTiming { get; private set; }
        internal static ConfigEntry<TameRefusal> FeedRefused { get; private set; }
        internal static ConfigEntry<bool> FeedWild { get; private set; }
        internal static ConfigEntry<int> FeedHeal { get; private set; }

        // Card B12: tames' deaths and removals written to the log
        internal static ConfigEntry<bool> LogTames { get; private set; }

        // Following (card B25)
        internal static ConfigEntry<bool> Follow { get; private set; }
        internal static ConfigEntry<WhichTames> FollowWhich { get; private set; }
        internal static ConfigEntry<string> FollowList { get; private set; }
        internal static ConfigEntry<bool> FollowSummons { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> FollowSaddleKey { get; private set; }
        internal static ConfigEntry<bool> FollowHover { get; private set; }
        internal static ConfigEntry<bool> FollowFreeOfPens { get; private set; }

        // With the Hoarstone (card B26)
        internal static ConfigEntry<bool> Ground { get; private set; }
        internal static ConfigEntry<WhichTames> GroundWhich { get; private set; }
        internal static ConfigEntry<string> GroundList { get; private set; }
        internal static ConfigEntry<bool> GroundFight { get; private set; }
        internal static ConfigEntry<bool> GroundFollow { get; private set; }
        internal static ConfigEntry<bool> GroundReturn { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> GroundReturnKey { get; private set; }
        internal static ConfigEntry<bool> GroundGuard { get; private set; }
        internal static ConfigEntry<PennedGuard> GroundGuardPenned { get; private set; }
        internal static ConfigEntry<int> GroundPenReach { get; private set; }
        internal static ConfigEntry<bool> GroundGuardAnimals { get; private set; }
        internal static ConfigEntry<int> GroundGuardReach { get; private set; }
        internal static ConfigEntry<bool> GroundOthers { get; private set; }
        internal static ConfigEntry<bool> GroundLog { get; private set; }

        /// <summary>Goes up whenever any setting changes, so whatever was worked out from them (the lists) is worked out
        /// again.</summary>
        internal static int Version { get; private set; }

        private static ModuleSettings bound;
        private static int order;
        private static ConfigEntry<bool> foldUnfolded;
        private static List<ConfigEntryBase> foldSettings;

        internal static void Bind(ModuleSettings settings)
        {
            bound = settings;
            Enabled = settings.Enabled;

            // Not a part of its own: a watch that only writes (card B12), above the parts' fold-outs.
            LogTames = settings.Bind(ModuleSettings.General, "LogTames", true, "Write tames' deaths and removals to the log", 995,
                "Writes a line to the game's log (BepInEx\\LogOutput.log) whenever one of the world's tames dies on your game " +
                "(its kind, name, place and what hit it last), or a tame is removed from the world by any game (and whose). " +
                "It changes nothing in the game; it's there so a tame that goes missing can be traced.");
            order = 990;

            BeginFold(SparePart);
            Spare = Add(SparePart, "SpareBuildings", true, "Tames spare buildings",
                "On: the tames chosen below no longer damage buildings with their bites, stomps and their running trample. " +
                "Only the damage is dropped: a bite still stops at the wall in its way, so tames don't start biting through " +
                "walls. A building is spared if either the tame's owner or the building's owner runs this mod (with a share " +
                "set below, the building's owner). On out of the box.");
            settings.MovedDefault(Spare, false, 1);
            SpareWhich = Add(SparePart, "SpareWhich", WhichTames.EveryTame, "Which tames",
                "EveryTame (the default): every tamed creature (wolves, boars, hens, asksvin, Lox, the staffs' companions). " +
                "TheList: only the tames in the list below.");
            settings.MovedDefault(SpareWhich, WhichTames.TheList, 1);
            SpareList = Add(SparePart, "SpareList", "Lox", "The list",
                "Tames that spare buildings when \"Which tames\" is TheList, separated by commas: each its name as the game " +
                "shows it (Lox), its prefab name (Lox) or its $ token ($enemy_lox). Case and spaces don't matter.");
            SpareSummons = Add(SparePart, "SpareSummons", false, "Summons that aren't tames",
                "Also spare buildings from creatures a staff calls up that the game doesn't count as tamed: the Trollstav's " +
                "troll and the Staff of the Wild's roots.");
            SpareTrample = Add(SparePart, "SpareTrample", true, "Their running trample too",
                "The damage a Lox or a moose does to what it runs into above 5 m/s, ridden or not.");
            SparePlayerBuilt = Add(SparePart, "SparePlayerBuilt", true, "Buildings players built",
                "Every piece a player placed: walls, floors, fences, chests, stations, everything built with the hammer.");
            SpareShips = Add(SparePart, "SpareShips", true, "Ships and carts", "Rafts, karves, longships and carts.");
            SpareWorld = Add(SparePart, "SpareWorld", false, "The world's own buildings",
                "Ruins, fuling villages, dvergr outposts and the other buildings the world was made with.");
            SpareCrops = Add(SparePart, "SpareCrops", true, "Crops and saplings",
                "Growing crops and saplings, and what they grow into (carrots, turnips, barley and the rest, not wild " +
                "berries or mushrooms). On by default.");
            settings.MovedDefault(SpareCrops, false, 1);
            SpareShare = Add(SparePart, "SpareShare", 0, "Share of the damage they still do, %",
                "0: none. Any other share: that much of each hit still lands, worked out once, on the computer that owns " +
                "the building (with a share set, a building whose owner doesn't run this mod takes the whole hit). On a " +
                "world whose world level is raised, the game adds a flat amount to every creature's hit after the share, so a " +
                "small share does more than its percent there.",
                new AcceptableValueRange<int>(0, 100));
            SpareLog = Add(SparePart, "SpareLog", false, "Write each spared hit to the log",
                "For testing: each hit a tame doesn't do, with the tame and the piece, in BepInEx\\LogOutput.log.");
            EndFold();

            BeginFold(FriendlyPart);
            Friendly = Add(FriendlyPart, "FriendlyFire", true, "Area attacks hurt the creatures around them",
                "On: wild creatures' area attacks (stomps, slams, spins, their shockwaves, clouds, fire patches and novas, " +
                "and exploding projectiles' blasts) also hurt the creatures around them, not only their enemies. Never " +
                "from a tame or a staff's summon, and never on a player. While it's on, the game's own friendly fire (Morgen's " +
                "rolls, an asksvin's headbutt) follows the settings below too. On out of the box.");
            settings.MovedDefault(Friendly, false, 1);
            FfAreaAttacks = Add(FriendlyPart, "FfAreaAttacks", true, "The game's area attacks",
                "Stomps, slams and spins (a troll's ground slam, a Lox's stomp, the Abomination's slams).");
            FfAreaObjects = Add(FriendlyPart, "FfAreaObjects", true, "Shockwaves, clouds, fire patches and novas",
                "What an attack leaves behind that damages (Bonemass's cloud, a cultist's flames, a fireball's fire " +
                "patch). Healing and shielding clouds stay friends-only, as in the game.");
            FfBlasts = Add(FriendlyPart, "FfBlasts", true, "Exploding projectiles' blasts",
                "The blast of a fuling shaman's fireball, a blob's burst, a gjall's spit, and of projectiles that burst around " +
                "themselves as they fly (those then hurt friends all along their way). The projectile itself still flies past " +
                "friends (the next setting but one).");
            FfMelee = Add(FriendlyPart, "FfMelee", true, "Wide melee swings too",
                "Melee attacks that sweep through several things (a troll's log swings, Moder's claws), not single bites. A " +
                "friend in a swing's way then stops it there, and when several are hit, each takes less, as the game shares " +
                "out a swing's damage. On by default.");
            settings.MovedDefault(FfMelee, false, 1);
            FfProjectiles = Add(FriendlyPart, "FfProjectiles", false, "Projectiles stop on friends in their way",
                "Arrows, spears and spit hit a friend in their path instead of flying through it, as they hit an enemy. A " +
                "friend in the way then shields what's behind it.");
            FfShare = Add(FriendlyPart, "FfShare", 100, "Share of the damage a friendly hit does, %",
                "100: as much as on an enemy. 0: friendly hits don't land at all. The game's own friendly fire is scaled too " +
                "while this part is on. On a world whose world level is raised, the game adds a flat amount to every " +
                "creature's hit after the share.",
                new AcceptableValueRange<int>(0, 500));
            FfOwnKind = Add(FriendlyPart, "FfOwnKind", true, "Also hurts its own kind",
                "A troll's slam hurts other trolls, a wild Lox's stomp other Lox. Off: only other kinds, and while this part is " +
                "on the game's own friendly fire on its own kind stops too (Morgen's rolls on other Morgen).");
            FfBosses = Add(FriendlyPart, "FfBosses", true, "Bosses' area attacks too",
                "Bosses' area attacks hurt their own summons and the creatures around them (Bonemass's cloud on its " +
                "blobs, the Elder's stomp on its roots, the Queen's blast on her seekers).");
            FfNever = Add(FriendlyPart, "FfNever", string.Empty, "Creatures that never deal it",
                "Creatures whose friendly fire stops altogether while this part is on, the game's own included " +
                "(Morgen's rolls, an asksvin's headbutt), separated by commas: each its name as the game shows it, its " +
                "prefab name or its $ token.");
            FfHurtTames = Add(FriendlyPart, "FfHurtTames", false, "Your tames hurt by wild creatures' friendly fire",
                "Off: while this part is on, your tames are safe from every wild creature's friendly fire, the game's own " +
                "included (a wild Lox's stomp beside your tamed Lox). On: they are hurt as other creatures are.");
            FfFightBack = Add(FriendlyPart, "FfFightBack", false, "Creatures fight back",
                "On: a creature hurt by a friend may fight it back for a while (below). Off: it lets it be, as in the " +
                "game. Tames never take part.");
            FfGrudge = Add(FriendlyPart, "FfGrudge", 30, "How long a grudge lasts, s",
                "With \"Creatures fight back\" on: how long the two may fight each other after the hurt.",
                new AcceptableValueRange<int>(5, 300));
            FfOnlyAll = Add(FriendlyPart, "FfOnlyAll", false, "Only when every player nearby runs the pack",
                "On: creatures deal friendly fire only while every player near them runs this mod with this part on, so " +
                "everyone nearby sees the same fight. Games with it on say hello to each other; nothing is written into the " +
                "world.");
            FfNearby = Add(FriendlyPart, "FfNearby", 100, "How near counts as nearby, m",
                "For the setting above: players within this distance of the creature.",
                new AcceptableValueRange<int>(20, 500));
            FfLog = Add(FriendlyPart, "FfLog", false, "Write each friendly hit to the log",
                "For testing: each friendly hit, with the two creatures and the damage, in BepInEx\\LogOutput.log.");
            EndFold();

            BeginFold(PensPart);
            Pens = Add(PensPart, "Pens", true, "Tames in pens",
                "On: tames stay in their pens (no climbing onto each other or over fences), keep their heads out of " +
                "walls, don't fight enemies through a wall, and wander where they can walk instead of standing at the " +
                "wall. Each of the four has its own switch in the sections below, all on. On out of the box.");
            settings.MovedDefault(Pens, false, 1);
            PensWhich = Add(PensPart, "PensWhich", WhichTames.EveryTame, "Which tames",
                "EveryTame: every tamed creature. TheList: only the tames in the list below.");
            PensList = Add(PensPart, "PensList", "Lox", "The list",
                "Tames looked after when \"Which tames\" is TheList, separated by commas: each its name as the game shows " +
                "it, its prefab name or its $ token.");
            Walls = Add(PensPart, "PensWalls", WallKind.PiecesPlayersBuilt, "What counts as a wall",
                "For all four points below, and for healing in pens. PiecesPlayersBuilt: pieces a player placed (walls, fences, poles, gates, chests). " +
                "AnyBuildingPiece: the world's own buildings too (ruins, villages).");
            PensRidden = Add(PensPart, "PensRidden", false, "Also while ridden",
                "On: a ridden tame can't climb onto creatures or building pieces either. Off: the rider steers, and the " +
                "game's own rules hold.");
            PensLog = Add(PensPart, "PensLog", false, "Write what it does to the log",
                "For testing: what this part does to each tame, in BepInEx\\LogOutput.log: each kind of thing at once, the " +
                "same kind again at most every 10 s (some come many times a second).");
            EndFold();

            BeginFold(KeptInPart);
            NoStacking = Add(KeptInPart, "PensNoStacking", true, "No standing on creatures",
                "A tame never counts another creature as ground: it can't walk up a neighbour's flank or rest on its back, " +
                "so no stacks to climb out on.");
            Nudge = Add(KeptInPart, "PensNudge", 5f, "Nudge off a creature, m/s",
                "A tame still on another creature after a second is pushed off sideways this fast. 0: no nudge. 5 by default.",
                new AcceptableValueRange<float>(0f, 5f));
            settings.MovedDefault(Nudge, 1f, 1);
            NoClimbing = Add(KeptInPart, "PensNoClimbing", true, "No climbing onto building pieces",
                "A tame can't walk up onto a fence, a wall or a chest by its edge. A floor at its feet, stairs and ramps " +
                "still carry it.");
            Step = Add(KeptInPart, "PensStep", 0f, "Highest step onto a building piece, m",
                "The highest edge of a building piece a tame may still step onto, above the bottom of its body (a slope's " +
                "rise doesn't count). 0: each tame's own step, as its paths are " +
                "planned (0.6 m for the Lox, 0.3 m for wolves, boars, hens, asksvin and moose). Any other number: that, for " +
                "every tame.",
                new AcceptableValueRange<float>(0f, 1.5f));
            Births = Add(KeptInPart, "PensBirths", true, "Births land inside",
                "A newborn or an egg lands on its parent's side of a fence or wall, not inside it or beyond it.");
            GrowUps = Add(KeptInPart, "PensGrowUps", true, "Grown-ups don't start in walls",
                "A calf growing into an adult that would start inside a fence or wall is moved out of it, toward the side " +
                "the calf stood on.");
            EndFold();

            BeginFold(HeadsPart);
            Heads = Add(HeadsPart, "PensHeads", true, "Keep heads out of walls",
                "A tame walking about stops before its head reaches into a wall, and with \"Wander only where they can walk\" " +
                "on, picks places to stand where its head stays clear. Fighting, following and riding aren't changed.");
            HeadMargin = Add(HeadsPart, "PensHeadMargin", 0.3f, "Margin ahead of the head, m",
                "How far ahead of the tip of its head a wall must stay.", new AcceptableValueRange<float>(0f, 2f));
            EaseOut = Add(HeadsPart, "PensEaseOut", true, "Walk away when the head is in a wall",
                "A tame standing with its head in a wall (pushed there, or a wall built through it) walks away from it.");
            EndFold();

            BeginFold(WallsPart);
            NoSight = Add(WallsPart, "PensNoSight", true, "Don't see or attack through walls",
                "A tame doesn't see an enemy behind a wall (measured from its body, not its eyes, which a wolf pushes " +
                "through the wall it presses), so it doesn't attack across it: no more biting the pen's wall because a " +
                "greydwarf stands outside. The young (calves, cubs, piglets, chicks) don't fight; letting go, below, covers " +
                "them.");
            LetGo = Add(WallsPart, "PensLetGo", true, "Let go of enemies they can't reach",
                "A tame lets go of an enemy behind a wall that it has no way to walk to, and takes the nearest one it can " +
                "reach instead. Once a wild creature breaks the wall or the gate opens, there's a way, and it goes for it.");
            LetGoAfter = Add(WallsPart, "PensLetGoAfter", 1f, "Let go after, s",
                "How long an enemy must stay out of reach before a tame lets go of it.", new AcceptableValueRange<float>(0f, 10f));
            EndFold();

            BeginFold(WanderPart);
            Wander = Add(WanderPart, "PensWander", true, "Wander only where they can walk",
                "A tame wandering about picks only places it can walk all the way to, a little way from the walls, with its " +
                "head clear when it gets there: less standing at the pen's wall. If nothing fits, it stands where it is " +
                "until its next pick.");
            WallMargin = Add(WanderPart, "PensMargin", 0.5f, "Keep this far from walls, m",
                "How far from the walls its body must stay at a place it picks (a Lox's middle then stays 2 m from the " +
                "wall).",
                new AcceptableValueRange<float>(0f, 5f));
            Tries = Add(WanderPart, "PensTries", 6, "Tries before standing still",
                "How many places a tame tries at each pick, two at each distance: two at the game's distance, two at half, " +
                "two at a quarter, and so on; then it stands still until its next pick.", new AcceptableValueRange<int>(1, 12));
            Stuck = Add(WanderPart, "PensStuck", 2f, "End a stuck walk after, s",
                "A walk that gets nowhere (less than 0.2 m) for this long ends there. 0: never.",
                new AcceptableValueRange<float>(0f, 10f));
            FarHome = Add(WanderPart, "PensFarHome", true, "Ignore a far-away spawn point",
                "A tame caught near a spawner (boars from runestones and abandoned farms) keeps running back toward it, so " +
                "it stands at the wall nearest its old home. On: a spawn point farther than below is ignored, and a nearer " +
                "one no longer centres its wandering (it wanders around itself). Nothing is written to the tame: switched " +
                "off, the game's rule is back. A wolf told to stay keeps its spot.");
            FarHomeTimes = Add(WanderPart, "PensFarHomeTimes", 2f, "… farther than × the wander range",
                "A spawn point farther than this many times the tame's own wander range (the game's, not scaled by \"How far " +
                "tames wander\") is ignored. 2: where the game itself starts running back.", new AcceptableValueRange<float>(1f, 5f));
            Range = Add(WanderPart, "PensRange", 100, "How far tames wander, %",
                "With \"Wander only where they can walk\" on: the game's own wander range (a Lox's 10 m), scaled. Smaller " +
                "pens look calmer with less.",
                new AcceptableValueRange<int>(25, 200));
            EndFold();

            BeginFold(HealPart);
            Heal = Add(HealPart, "PensHeal", true, "Tames heal faster in pens",
                "On: a tame inside a pen (walls round it, below) gets its health back much faster than in the game, hungry " +
                "or not, with nothing else needed. It works whether \"Tames in pens\" above is on or off, and for every tame. " +
                "Only the game looking after the tame heals it (with friends, one player's game at a time), so it never " +
                "heals twice; that game must run House Trained with this on. On out of the box.");
            HealRate = Add(HealPart, "PensHealRate", 10, "Healing in a pen, % a minute",
                "How much of its full health a tame in a pen gets back each minute, on top of the game's own healing (the " +
                "game heals a fed tame fully in an hour, under 2% a minute, and a hungry one not at all). 10: a tame hurt " +
                "to its last breath is whole again in 10 minutes.",
                new AcceptableValueRange<int>(1, 100));
            HealReach = Add(HealPart, "PensHealReach", 40, "How far its pen's walls may be, m",
                "A tame counts as in a pen when walls stand round it within this distance, looked for in 16 directions at " +
                "three heights above the ground, following its slopes (up to 1.5 m, so fences with gaps between their " +
                "rails count). What counts as a wall is the " +
                "setting \"What counts as a wall\" under \"Tames in pens\". A house counts as a pen too.",
                new AcceptableValueRange<int>(5, 100));
            HealGaps = Add(HealPart, "PensHealGaps", 2, "Open directions allowed",
                "How many of the 16 directions may find no wall (an open gate, a gap between two fences) for a tame to " +
                "still count as in a pen. 0: walls all round.",
                new AcceptableValueRange<int>(0, 8));
            HealNumbers = Add(HealPart, "PensHealNumbers", true, "Show the healing numbers",
                "The numbers the game shows over a tame as it heals, as over a fed tame. Off: it heals quietly.");
            HealLog = Add(HealPart, "PensHealLog", false, "Write healing to the log",
                "For testing: each hurt tame found in a pen or out of one, with how many directions are walled, in " +
                "BepInEx\\LogOutput.log.");
            EndFold();

            BeginFold(FeedPart);
            Feed = Add(FeedPart, "FeedHand", true, "Feed a tame from the hand",
                "On: using a food your tame eats on it (its hotbar key while you look at it or its saddle, as a saddle is put on) feeds " +
                "it one. One leaves your inventory and the creature eats it as if it had found it on the ground: it's no " +
                "longer hungry, it's soothed, and the game's own healing runs again (with what it held back while the tame " +
                "was hungry, as below). A Lox eats Cloudberries, Barley " +
                "and Flax. When a friend's game looks after the creature, that game feeds it, if it runs House Trained; " +
                "otherwise nothing is taken and you're told. On out of the box.");
            FeedTiming = Add(FeedPart, "FeedWhen", FeedWhen.OnlyWhenHungry, "When",
                "OnlyWhenHungry (the default): only a hungry creature takes food from your hand, as it only eats from the " +
                "ground when hungry (what a tame that isn't does: the next setting). AnyTime: it takes food " +
                "whenever you hold it out, and is fed for as long again from then.");
            FeedRefused = Add(FeedPart, "FeedRefused", TameRefusal.KeepItAndSayWhy, "When a tame won't eat",
                "What happens when you hold food out to one of your tames that won't take it (it isn't hungry). " +
                "KeepItAndSayWhy (the default): you keep the food and are told why. EatItYourself: the game goes on as " +
                "without the mod, so you eat it yourself, as when you eat while looking at it.");
            FeedWild = Add(FeedPart, "FeedWild", false, "Also wild creatures being tamed",
                "On: a calm wild creature that can be tamed takes food from your hand too (when hungry, or any time by " +
                "\"When\"), which counts toward taming it as eating from the ground does. A wild creature that won't eat " +
                "(frightened, fighting, or not hungry) never takes your food: you eat it yourself, as in the game. Off (the " +
                "default): only your tames, so eating while you look at a wild boar works as always.");
            FeedHeal = Add(FeedPart, "FeedHeal", 20, "A meal from the hand heals, % of full health",
                "This much of its full health comes back at once with each meal from your hand. 0: as in the game. The game's " +
                "own slow healing stops while a tame is hungry, and once it eats, from the " +
                "ground or your hand, all it missed comes back at its next heal, a moment later: a tame hungry for long heals " +
                "a lot at once, up to full.",
                new AcceptableValueRange<int>(0, 100));
            EndFold();

            BeginFold(FollowPart);
            Follow = Add(FollowPart, "FollowEveryTame", true, "Every tame follows and stays",
                "On: E on any of your tames (a boar, a Lox, a hen, an asksvin, a moose) makes it follow you, and E again makes it " +
                "stay, as the game lets you do with wolves: the game's own command, kept with the creature, so it follows you " +
                "on a friend's game too. Point at the creature itself: on a saddle, E rides, as in the game (or set a key " +
                "for the saddle below). Off: only wolves follow, as in the game. On out of the box.");
            FollowWhich = Add(FollowPart, "FollowWhich", WhichTames.EveryTame, "Which tames",
                "EveryTame (the default): every tamed creature. TheList: only the tames in the list below (wolves always " +
                "follow, as in the game).");
            FollowList = Add(FollowPart, "FollowList", "Lox", "The list",
                "Tames that follow when \"Which tames\" is TheList, separated by commas: each its name as the game shows it, " +
                "its prefab name or its $ token.");
            FollowSummons = Add(FollowPart, "FollowSummons", false, "The staffs' summons too",
                "The creatures blood magic calls up (the Dead Raiser's skeletons, the spirit callers' wolves, boars, bears and " +
                "moose) follow their summoner by themselves, and the game sends one away when it's left too far behind. On: E " +
                "tells them to stay too, and one told to stay waits there until you tell it to follow again (the game sends " +
                "a summon away, and counts it toward your summons' limit, only while it follows). Off: E pets them, as in " +
                "the game.");
            FollowSaddleKey = Add(FollowPart, "FollowSaddleKey", KeyboardShortcut.Empty, "Follow or stay, at a saddle",
                "A key that makes a saddled tame follow or stay while you point at its saddle (where E rides). None out of the " +
                "box: point at the creature itself, beside or in front of the saddle, and press E.");
            FollowHover = Add(FollowPart, "FollowHover", true, "Say Follow or Stay on a tame",
                "A tame's own text says what E does: [E] Follow, or [E] Stay while it follows, in place of the game's [E] Pet " +
                "(which the game shows on wolves too). The pet still happens. Off: the game's text.");
            FollowFreeOfPens = Add(FollowPart, "FollowFreeOfPens", true, "Pens' rules leave followers alone",
                "While a tame follows you, \"Tames in pens\" leaves it alone (its climbing, head, wall and wandering rules), so " +
                "it can follow you up a step or into a house. Told to stay, it's under them again.");
            EndFold();

            BeginFold(GroundPart, GroundTitle);
            Ground = Add(GroundPart, "Ground", true, "Tames keep to a Hoarstone's ground",
                "Works only while the Hoarstone mod is switched on (in miikeskii's QoL Pack, or its own release): with it off " +
                "or not installed, nothing in this part does anything. On: a tame on the ground of a settlement with a " +
                "Hoarstone (the shape Hoarstone keeps monsters from spawning in) stays on it: it wanders only inside, and " +
                "walks back in when it finds itself outside. It leaves only as the settings below allow. A tame told to stay " +
                "outside stays where it was told, as in the game. The game that looks after a tame decides for it, so that " +
                "game must run House Trained and Hoarstone. On out of the box.");
            GroundWhich = Add(GroundPart, "GroundWhich", WhichTames.EveryTame, "Which tames",
                "EveryTame (the default): every tamed creature but the staffs' summons. TheList: only the tames in the list " +
                "below.");
            GroundList = Add(GroundPart, "GroundList", "Lox", "The list",
                "Tames kept to the ground when \"Which tames\" is TheList, separated by commas: each its name as the game " +
                "shows it, its prefab name or its $ token.");
            GroundFight = Add(GroundPart, "GroundFight", true, "… they may leave it to fight (and come back)",
                "On: a tame may chase an enemy off the ground, as far as the game lets it chase, and walks back in once the " +
                "fight is over. Off: it lets go of an enemy off the ground at the edge, unless it was hurt in the last few " +
                "seconds.");
            GroundFollow = Add(GroundPart, "GroundFollow", true, "… they may leave it while following you",
                "On: a tame following you goes wherever you go; told to stay out there, it stays. Off: it follows you only " +
                "to the ground's edge, and waits there until you come back in.");
            GroundReturn = Add(GroundPart, "GroundReturn", true, "\"Return to base\"",
                "On a tame off the ground, the key below sends it home: it stops following and walks back to the ground it " +
                "came from (or, if it never was on one, the nearest Hoarstone's), and keeps to it again. Its hover text shows the key only while " +
                "it's off the ground.");
            GroundReturnKey = Add(GroundPart, "GroundReturnKey", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt), "\"Return to base\" key",
                "Pressed while pointing at a tame off the ground (or at its saddle). While it's held there, the game's own E " +
                "does nothing.");
            GroundGuard = Add(GroundPart, "GroundGuard", true, "Tames guard the ground",
                "On: a tame on the ground notices an enemy anywhere on it, not only one it sees or hears, and goes for it if " +
                "it can walk there. A tame in a pen goes only as the next setting says.");
            GroundGuardPenned = Add(GroundPart, "GroundGuardPenned", PennedGuard.ThroughAnOpenGate, "Tames in a pen guard it",
                "ThroughAnOpenGate (the default): a tame in a pen goes for an enemy only while a door or gate of its pen is " +
                "open. Never: tames in pens stay out of it. Always: whenever it can walk there. A pen is what \"Pens: " +
                "healing\" counts as one, but nearer (next setting): walls round the tame, with its open directions allowed.");
            GroundPenReach = Add(GroundPart, "GroundPenReach", 20, "How near a pen's walls are, m",
                "For the setting above: a tame counts as in a pen when walls stand round it within this distance (the walls " +
                "of a whole base, farther off, don't make a pen), and its pen's doors and gates are looked for as near.",
                new AcceptableValueRange<int>(5, 60));
            GroundGuardAnimals = Add(GroundPart, "GroundGuardAnimals", false, "… against harmless animals too",
                "Off: deer, hares and the other animals that never fight are left alone on the ground. On: every creature " +
                "the game lets tames fight.");
            GroundGuardReach = Add(GroundPart, "GroundGuardReach", 0, "How far they go for an enemy, m",
                "0: anywhere on the ground. Any other: only an enemy at most this far from the tame.",
                new AcceptableValueRange<int>(0, 300));
            GroundOthers = Add(GroundPart, "GroundOthers", false, "Also settlements without a Hoarstone",
                "On: the ground of every settlement Hoarstone counts keeps tames too, as Hoarstone's own \"Where\" does when " +
                "set to EverySettlement. Off: only settlements with a Hoarstone.");
            GroundLog = Add(GroundPart, "GroundLog", false, "Write what it does to the log",
                "For testing: what this part does to each tame, in BepInEx\\LogOutput.log, the same kind of thing again at most " +
                "every 10 s.");
            EndFold();

            // The Pick buttons in F1 (card B03): the five lists picked from pictures of the world's creatures (their
            // trophies), names read as Names reads them and new ones written by prefab name.
            PickerSettings.Bind(settings);
            Lists.Attach(SpareList, CreatureList("The tames that spare buildings while \"Which tames\" is TheList.", true));
            Lists.Attach(FfNever, CreatureList("Creatures whose attacks never hurt other creatures.", false));
            Lists.Attach(PensList, CreatureList("The tames kept in pens while \"Which tames\" is TheList.", true));
            Lists.Attach(FollowList, CreatureList("The tames that follow you while \"Which tames\" is TheList.", true));
            Lists.Attach(GroundList, CreatureList("The tames kept to the ground while \"Which tames\" is TheList.", true));
        }

        private static ListSpec CreatureList(string says, bool tamesOnly) => new ListSpec
        {
            Module = Module.Name,
            Kinds = new[] { ThingKind.Creatures },
            Format = new ListFormat { Separators = ",;\n\r", Joiner = ", ", Normal = Names.Normal },
            ShownNames = true,
            Holds = tamesOnly ? thing => thing.Tame : (Func<Thing, bool>)null,
            Says = says,
            Warn = text => Module.Log?.LogWarning(text),
        };

        private static ConfigEntry<T> Add<T>(string part, string key, T value, string name, string description, AcceptableValueBase range = null)
        {
            ConfigEntry<T> entry = bound.Bind(part, key, value, name, order--, description, range);
            entry.SettingChanged += (sender, args) => Version++;
            foldSettings?.Add(entry);
            return entry;
        }

        // Each part is a fold-out: a title row, then its settings, all under the module's one heading. The title row is a
        // setting of the part's own ("Unfolded"), which remembers whether it is open.
        // The title row shows the part's name, or a longer title (With the Hoarstone's says it needs Hoarstone on, his
        // words of 2026-10-11, card B26); the settings file keeps the part's name as its section.
        private static void BeginFold(string part, Func<string> title = null)
        {
            var settings = new List<ConfigEntryBase>();
            foldUnfolded = bound.Bind(part, "Unfolded", false, part, order--,
                "Opens and closes this part of the settings in the window. It changes nothing else.", null,
                title != null ? FoldDrawer.For(title, settings) : FoldDrawer.For(part, settings));
            foldSettings = settings;
        }

        /// <summary>With the Hoarstone's title row, as drawn now: it works only while Hoarstone is on, and whether it is.</summary>
        internal static string GroundTitle() => GroundRules.Title(HoarstoneLink.Now);

        private static void EndFold()
        {
            Folds.Add(foldUnfolded, foldSettings, bound.SectionOf(ModuleSettings.General));
            foldUnfolded = null;
            foldSettings = null;
        }
    }
}
