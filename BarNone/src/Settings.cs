using BepInEx.Configuration;
using QoLMods.Kit;
using QoLMods.ListPicker;
using UnityEngine;

namespace BarNone
{
    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.barnone.cfg, one section per part
    /// (General, Reach, Smoke, Projectiles, Sky, Items); inside miikeskii's QoL Pack, in the bundle's file, all in the
    /// section "Bar None", each named after its part. Every one takes effect at once, also when changed in game with a
    /// configuration manager: the mod reads them when it needs them.
    /// </summary>
    internal static class Settings
    {
        internal const string PartReach = "Reach";
        internal const string PartSmoke = "Smoke";
        internal const string PartProjectiles = "Projectiles";
        internal const string PartSky = "Sky";
        internal const string PartItems = "Items";

        /// <summary>His list (the cage floors under both of the Cage Floor 1x1's names, the cage walls, the dvergr metal
        /// wall, the crystal wall, the carved darkwood divider, the roundpole fence) and the three ashwood decorative walls
        /// (the card, A50).</summary>
        internal const string ReachDefault =
            "iron_floor_1x1,iron_floor_1x1_v2,iron_floor_2x2,iron_wall_1x1,iron_wall_2x2,piece_dvergr_metal_wall_2x2," +
            "crystal_wall_1x1,darkwood_decowall,wood_fence,ashwood_decowall_2x2,ashwood_decowall_divider,ashwood_decowall_tree";

        /// <summary>JereKuusela's eight (Smoke_Collision's defaults) plus the Cage Floor 1x1 the hammer builds today, the
        /// dvergr metal wall, the roundpole fence and gate, and the three ashwood decorative walls. Not the crystal wall:
        /// glass keeps smoke in.</summary>
        internal const string SmokeDefault =
            "iron_floor_1x1,iron_floor_1x1_v2,iron_floor_2x2,iron_wall_1x1,iron_wall_2x2,iron_grate,darkwood_decowall," +
            "wood_stair,wood_stepladder,piece_dvergr_metal_wall_2x2,wood_fence,wood_fence_gate,ashwood_decowall_2x2," +
            "ashwood_decowall_divider,ashwood_decowall_tree";

        /// <summary>The cage floors (both 1x1 names) and walls, and the iron gate (Projectile_Collision's five, and the
        /// new Cage Floor 1x1).</summary>
        internal const string ProjectilesDefault =
            "iron_floor_1x1,iron_floor_1x1_v2,iron_floor_2x2,iron_wall_1x1,iron_wall_2x2,iron_grate";

        /// <summary>The reach list, plus the iron gate and the roundpole gate.</summary>
        internal const string SkyDefault = ReachDefault + ",iron_grate,wood_fence_gate";

        /// <summary>His "hay and cage floors" (2026-09-30): the cage floors under both of the Cage Floor 1x1's names and the
        /// 2x2. The Straw rug, the game's only hay-like piece, already lets items through: its collider is on the
        /// piece_nonsolid layer, which the item layer doesn't collide with (the game's collision matrix, 1.0.16).</summary>
        internal const string ItemsDefault = "iron_floor_1x1,iron_floor_1x1_v2,iron_floor_2x2";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<bool> ReachThrough { get; private set; }

        internal static ConfigEntry<string> ReachPieces { get; private set; }

        internal static ConfigEntry<bool> ReachCreatures { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> ReachKey { get; private set; }

        internal static ConfigEntry<bool> SmokeThrough { get; private set; }

        internal static ConfigEntry<string> SmokePieces { get; private set; }

        internal static ConfigEntry<bool> FiresUnderPieces { get; private set; }

        internal static ConfigEntry<float> FireClearSpace { get; private set; }

        internal static ConfigEntry<bool> ProjectilesThrough { get; private set; }

        internal static ConfigEntry<string> ProjectilePieces { get; private set; }

        internal static ConfigEntry<bool> PlayersShots { get; private set; }

        internal static ConfigEntry<bool> BallistasShots { get; private set; }

        internal static ConfigEntry<bool> TamesShots { get; private set; }

        internal static ConfigEntry<bool> EnemiesShots { get; private set; }

        internal static ConfigEntry<float> LargestProjectile { get; private set; }

        internal static ConfigEntry<bool> LogHits { get; private set; }

        internal static ConfigEntry<bool> SkyThrough { get; private set; }

        internal static ConfigEntry<string> SkyPieces { get; private set; }

        internal static ConfigEntry<bool> SkyThroughLeaky { get; private set; }

        internal static ConfigEntry<bool> SkyGrowSpace { get; private set; }

        internal static ConfigEntry<bool> SkyBeehives { get; private set; }

        internal static ConfigEntry<bool> SkyWindmills { get; private set; }

        internal static ConfigEntry<bool> SkyWisps { get; private set; }

        internal static ConfigEntry<bool> ItemsThrough { get; private set; }

        internal static ConfigEntry<string> ItemPieces { get; private set; }

        internal static ConfigEntry<string> ExceptItems { get; private set; }

        internal static ConfigEntry<bool> LogItems { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings of a section by
            // their Order, highest first; in the bundle every part shares the one section, so the orders keep the parts
            // together: reach, smoke, projectiles, sky, items.
            Enabled = settings.Enabled;

            // Reach: on out of the box since card A63 (2026-10-06), his own settings made the pack's defaults; until then off
            // (MODULES.md rule 4). A file still at an old default moves once (the kit's MovedDefault).
            ReachThrough = settings.Bind(PartReach, "ReachThrough", true,
                "Reach: through see-through pieces", 99,
                "On (the default): what's behind a listed piece (a cage wall, a crystal wall …) can be used as if the piece weren't there, " +
                "within your usual reach: add fuel to a fire, open a chest, pick a crop, pick up an item. Anything you can " +
                "use itself, such as a gate or a shutter, is never reached through, so it still opens. Off: the game's own " +
                "reach, which stops at the first thing in the way.");
            settings.MovedDefault(ReachThrough, false, 1);

            ReachPieces = settings.Bind(PartReach, "ReachPieces", ReachDefault,
                "Reach: pieces", 98,
                "The pieces you can reach through, by their prefab names (the names in the game's data), separated by " +
                "commas. The Cage Floor 1x1 has two: iron_floor_1x1 for older builds, iron_floor_1x1_v2 for new ones.");

            ReachCreatures = settings.Bind(PartReach, "ReachCreatures", true,
                "Reach: creatures behind them", 97,
                "On (the default): a creature behind a listed piece can be reached too, to pet or command a tame through cage bars, and its " +
                "name and health show. Off: a creature behind them stays out of reach, as in the game.");
            settings.MovedDefault(ReachCreatures, false, 1);

            ReachKey = settings.Bind(PartReach, "ReachKey", KeyboardShortcut.Empty,
                "Reach: only while holding", 96,
                "A key to hold for reaching through listed pieces: while one is set, you reach through them only while " +
                "holding it. Empty: always.");

            // Smoke: on out of the box since card A63 (until then off, rule 4); it stands aside while Smoke_Collision is installed.
            SmokeThrough = settings.Bind(PartSmoke, "SmokeThrough", true,
                "Smoke: passes through pieces", 89,
                "On (the default): smoke rises through the listed pieces instead of piling up under them, so a fire, a smelter or a kiln " +
                "under a cage floor doesn't choke on its own smoke. The smoke then fills whatever is above and smokes " +
                "whoever is there. Each game moves its own smoke, so a friend without this sees it pile up as usual. Off: " +
                "smoke stops at every piece, as in the game.");
            settings.MovedDefault(SmokeThrough, false, 1);

            SmokePieces = settings.Bind(PartSmoke, "SmokePieces", SmokeDefault,
                "Smoke: pieces", 88,
                "The pieces smoke passes through, by their prefab names, separated by commas. Glass (the crystal wall) isn't " +
                "one: it keeps smoke in.");

            FiresUnderPieces = settings.Bind(PartSmoke, "FiresUnderPieces", true,
                "Smoke: fires burn under listed pieces", 87,
                "A fire goes out while something solid is right above its flames (the game's test, over the clear space " +
                "below). On: listed pieces don't count there, so a fire right under a cage floor keeps burning. Only while " +
                "\"Smoke: passes through pieces\" is on. Off: every piece counts, as in the game.");

            FireClearSpace = settings.Bind(PartSmoke, "FireClearSpace", 0.5f,
                "Smoke: clear space above fires (m)", 86,
                "How much clear space a fire needs above its flames, in metres, before a piece puts it out; the game's is " +
                "0.5, and 0 turns the test off. Ground over a fire and its own trapped smoke still put it out. Only while " +
                "\"Smoke: passes through pieces\" is on.",
                new AcceptableValueRange<float>(0f, 3f));

            // Projectiles: on out of the box since card A63 (until then off, rule 4); it stands aside while Projectile_Collision
            // is installed.
            ProjectilesThrough = settings.Bind(PartProjectiles, "ProjectilesThrough", true,
                "Projectiles: pass through pieces", 79,
                "On (the default): projectiles fly through the listed pieces, for the shooters switched on below and up to the size set. " +
                "Each shot is flown by its shooter's game, so a friend's arrows pass only if their game has this on too. " +
                "Off: projectiles hit every piece, as in the game.");
            settings.MovedDefault(ProjectilesThrough, false, 1);

            ProjectilePieces = settings.Bind(PartProjectiles, "ProjectilePieces", ProjectilesDefault,
                "Projectiles: pieces", 78,
                "The pieces projectiles fly through, by their prefab names, separated by commas. \"Projectiles: write each " +
                "hit to the log\" shows the name of anything you shoot at.");

            PlayersShots = settings.Bind(PartProjectiles, "PlayersShots", true,
                "Projectiles: players' shots", 77,
                "Players' arrows, bolts, spears, thrown weapons and staffs' shots pass through: yours, and friends' on " +
                "their own games.");

            BallistasShots = settings.Bind(PartProjectiles, "BallistasShots", false,
                "Projectiles: ballistas' and catapults' shots", 76,
                "Ballistas' and catapults' shots pass through (they have no shooter of their own).");

            TamesShots = settings.Bind(PartProjectiles, "TamesShots", false,
                "Projectiles: tames' shots", 75,
                "Tamed creatures' shots pass through.");

            EnemiesShots = settings.Bind(PartProjectiles, "EnemiesShots", true,
                "Projectiles: enemies' shots", 74,
                "Enemies' shots pass through, and the world's own (hot sparks, a tree's spawn). On out of the box; off " +
                "keeps a cage a shield against them.");
            settings.MovedDefault(EnemiesShots, false, 1);

            LargestProjectile = settings.Bind(PartProjectiles, "LargestProjectile", 0f,
                "Projectiles: largest that passes (m)", 73,
                "Only projectiles up to this size pass, by the radius the game gives each one; 0 lets any size through. " +
                "\"Projectiles: write each hit to the log\" shows each projectile's size.",
                new AcceptableValueRange<float>(0f, 5f));

            LogHits = settings.Bind(PartProjectiles, "LogHits", false,
                "Projectiles: write each hit to the log", 72,
                "On: every hit of a projectile flown by this game is written to the log: the projectile, its size, its " +
                "shooter, what it hit and whether it passed. Useful to find a piece's name for a list, or a projectile's " +
                "size. It works also while \"Projectiles: pass through pieces\" is off, but not while Projectile_Collision " +
                "is installed.");

            // Sky: on for everyone out of the box, his call (2026-09-29, card A50), over MODULES.md rule 4.
            SkyThrough = settings.Bind(PartSky, "SkyThrough", true,
                "Sky: crops and saplings see the sky through pieces", 69,
                "On for everyone: crops and saplings see the sky through the listed pieces, so they grow under a cage " +
                "floor. A crop that can't grow is destroyed when it ripens by whichever game has it then, so keep this on, " +
                "with the same list, for every friend who may be near your fields. Off: a crop under any piece has \"no " +
                "sun\" and dies when it ripens, as in the game.");

            SkyPieces = settings.Bind(PartSky, "SkyPieces", SkyDefault,
                "Sky: pieces", 68,
                "The pieces crops and saplings (and, if switched on below, beehives, windmills and wisp fountains) see the " +
                "sky through, by their prefab names, separated by commas. Keep it the same for every friend near your " +
                "fields.");

            SkyThroughLeaky = settings.Bind(PartSky, "SkyThroughLeaky", false,
                "Sky: also through everything rain passes", 67,
                "On: crops and saplings also see the sky through everything the game lets rain through (it calls it " +
                "leaky): wood floors, walls and beams, chests, tables and more. Friends need the same to keep such crops " +
                "alive.");

            SkyGrowSpace = settings.Bind(PartSky, "SkyGrowSpace", false,
                "Sky: pieces take no growing space", 66,
                "A crop needs a little room around it (half a metre for most, more for tree saplings), and a piece that " +
                "close takes it (\"no space\"). On: listed pieces don't take it. Friends need the same to keep such crops " +
                "alive.");

            SkyBeehives = settings.Bind(PartSky, "SkyBeehives", true,
                "Sky: beehives and birds' nests see through them", 65,
                "On (the default): beehives and birds' nests don't count listed pieces as cover, so bees work inside a cage.");
            settings.MovedDefault(SkyBeehives, false, 1);

            SkyWindmills = settings.Bind(PartSky, "SkyWindmills", true,
                "Sky: windmills see through them", 64,
                "On (the default): windmills don't count listed pieces as cover, so a windmill behind cage walls gets its " +
                "full wind.");
            settings.MovedDefault(SkyWindmills, false, 1);

            SkyWisps = settings.Bind(PartSky, "SkyWisps", true,
                "Sky: Wisp Fountain and Eternal Pyre see through them", 63,
                "On (the default): the Wisp Fountain and the Eternal Pyre don't count listed pieces as cover.");
            settings.MovedDefault(SkyWisps, false, 1);

            // Items: off out of the box (rule 4).
            ItemsThrough = settings.Bind(PartItems, "ItemsThrough", false,
                "Items: fall through pieces", 59,
                "On: loose items fall through the listed pieces instead of landing on them: a creature's drops, the eggs a " +
                "hen lays, what you drop yourself, what a smelter or a kiln puts out, and one already lying on a listed " +
                "piece. Food set out as a piece stays. Each item falls as the game that owns it moves it, so one a friend's " +
                "game owns falls only if their game has this on too, with the same lists. Off: items land on pieces as in " +
                "the game.");

            ItemPieces = settings.Bind(PartItems, "ItemPieces", ItemsDefault,
                "Items: pieces", 58,
                "The pieces items fall through, by their prefab names, separated by commas: the cage floors to start. The " +
                "Straw rug already lets items through in the game, as most rugs do. \"Items: write each touch to the log\" " +
                "shows the name of any piece an item lands on.");

            ExceptItems = settings.Bind(PartItems, "ExceptItems", string.Empty,
                "Items: except these items", 57,
                "Items that never fall through, by their prefab names, separated by commas: the food you drop for your " +
                "animals, so it stays within their reach. List all of it: an animal goes only for the food nearest it, so " +
                "one piece fallen out of reach can keep it hungry, and a hungry animal doesn't breed (a boar eats Carrot, " +
                "Turnip, Onion, Mushroom, Raspberry, Blueberries; a hen Dandelion, Barley, BeechSeeds, BirchSeeds, " +
                "CarrotSeeds, OnionSeeds, TurnipSeeds). \"Items: write each touch to the log\" shows each item's name.");

            LogItems = settings.Bind(PartItems, "LogItems", false,
                "Items: write each touch to the log", 56,
                "On: the first time an item touches a piece, the log names the item and the piece and says whether your " +
                "game lets it fall through. Useful to find names for the two lists. Only while \"Items: fall through " +
                "pieces\" is on.");

            // The Pick buttons in F1 (card B03): each list picked from pictures of the game's things, its text written as
            // PieceList reads it (split at commas, any case).
            PickerSettings.Bind(settings);
            Lists.Attach(ReachPieces, List(ThingKind.Pieces, "The pieces you can reach through."));
            Lists.Attach(SmokePieces, List(ThingKind.Pieces, "The pieces smoke passes through."));
            Lists.Attach(ProjectilePieces, List(ThingKind.Pieces, "The pieces arrows, bolts and other projectiles fly through."));
            Lists.Attach(SkyPieces, List(ThingKind.Pieces, "The pieces crops and saplings see the sky through."));
            Lists.Attach(ItemPieces, List(ThingKind.Pieces, "The pieces items fall through."));
            Lists.Attach(ExceptItems, List(ThingKind.Items, "Items that never fall through: your animals' food."));
        }

        private static ListSpec List(ThingKind kind, string says) => new ListSpec
        {
            Module = Module.Name,
            Kinds = new[] { kind },
            Format = new ListFormat { Separators = ",", Joiner = "," },
            Says = says,
            Warn = text => Module.Log?.LogWarning(text),
        };

        /// <summary>The key is held down now (its main key and every modifier), read through the game's input. An empty
        /// key is never held.</summary>
        internal static bool Held(KeyboardShortcut key)
        {
            if (key.MainKey == KeyCode.None || !ZInput.GetKey(key.MainKey, false))
            {
                return false;
            }
            foreach (KeyCode modifier in key.Modifiers)
            {
                if (!ZInput.GetKey(modifier, false))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
