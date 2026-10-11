using System.Collections.Generic;

namespace TopperUpper
{
    /// <summary>One kind of light or station the settings name: its prefab, its setting's key (permanent), the name the
    /// settings show, and whether it's fed out of the box.</summary>
    internal sealed class Kind
    {
        internal readonly string Prefab;
        internal readonly string Key;
        internal readonly string Name;
        internal readonly bool Default;
        internal readonly string Note;

        internal Kind(string prefab, string key, string name, bool on, string note = null)
        {
            Prefab = prefab;
            Key = key;
            Name = name;
            Default = on;
            Note = note;
        }
    }

    /// <summary>
    /// The kinds of light and station Topper-Upper knows by name (Valheim 1.0.17's own prefabs; the research's tables in
    /// 1.2 and 1.3, read from the installed game's data). Each has a switch of its own. A player-built light of another
    /// kind (another mod's) follows "Other kinds of light"; another mod's station, "Other mods' stations".
    /// </summary>
    internal static class Kinds
    {
        /// <summary>The lights and fires, in the order the settings list them (the research's table 1.2). The hot tub is a
        /// station in the game's code (a smelter with nothing to make), fed as a light.</summary>
        internal static readonly Kind[] Lights =
        {
            new Kind("piece_walltorch", "Sconce", "Sconce", true),
            new Kind("piece_groundtorch", "StandingIronTorch", "Standing Iron Torch", true),
            new Kind("piece_groundtorch_wood", "StandingWoodTorch", "Standing Wood Torch", true),
            new Kind("piece_groundtorch_green", "GreenTorch", "Standing Green-burning Iron Torch", true),
            new Kind("piece_groundtorch_blue", "BlueTorch", "Standing Blue-burning Iron Torch", true),
            new Kind("piece_brazierfloor01", "StandingBrazier", "Standing Brazier", true),
            new Kind("piece_brazierfloor02", "BlueBrazier", "Blue Standing Brazier", true),
            new Kind("piece_brazierceiling01", "HangingBrazier", "Hanging Brazier", true),
            new Kind("fire_pit", "Campfire", "Campfire", true),
            new Kind("fire_pit_iron", "IronFirePit", "Iron Fire Pit", true),
            new Kind("hearth", "Hearth", "Hearth", true),
            new Kind("bonfire", "Bonfire", "Bonfire", true),
            new Kind("piece_jackoturnip", "JackOTurnip", "Jack-o-turnip", true),
            new Kind("piece_snowlantern", "SnowLantern", "Snow Lantern", true),
            new Kind("piece_bathtub", "HotTub", "Hot Tub", true, "It burns a wood every 5,000 s, bathed in or not."),
        };

        /// <summary>The resin candle: the game won't let a hand refill it; its own switch, off.</summary>
        internal const string Candle = "Candle_resin";

        /// <summary>The hot tub's prefab: a smelter fed as a light.</summary>
        internal const string HotTub = "piece_bathtub";

        /// <summary>The stations, in the order the settings list them (the research's 1.3 to 1.7 and its scope, section 7).</summary>
        internal static readonly Kind[] Stations =
        {
            new Kind("smelter", "Smelter", "Smelter", true),
            new Kind("blastfurnace", "BlastFurnace", "Blast Furnace", true),
            new Kind("charcoal_kiln", "CharcoalKiln", "Charcoal Kiln", true),
            new Kind("windmill", "Windmill", "Windmill", true),
            new Kind("piece_spinningwheel", "SpinningWheel", "Spinning Wheel", true),
            new Kind("eitrrefinery", "EitrRefinery", "Eitr Refinery", true),
            new Kind("piece_FrostKiln", "FrigidKiln", "Frigid Kiln", true),
            new Kind("fermenter", "Fermenter", "Fermenter", true),
            new Kind("piece_shieldgenerator", "ShieldGenerator", "Shield Generator", true),
            new Kind("piece_FrostFoundry", "FrostFoundry", "Frost Foundry (its fuel)", true, "Only its fuel: it burns Liquid Frost only while casting."),
            new Kind("piece_oven", "StoneOven", "Stone Oven (its fuel)", false, "Only its fuel: it burns wood even while empty."),
            new Kind("piece_turret", "Ballista", "Ballista (missiles)", false),
            new Kind("piece_beehive", "Beehive", "Beehive (harvest the honey)", false),
            new Kind("piece_birdnest", "BirdsNest", "Birds' Nest (harvest the feathers)", false),
            new Kind("piece_sapcollector", "SapExtractor", "Sap Extractor (harvest the sap)", false),
        };

        /// <summary>Stations never fed, whatever a setting says: a siege engine (the research's scope).</summary>
        internal static readonly HashSet<string> Never = new HashSet<string> { "BatteringRam" };

        /// <summary>A light kind by its prefab, or null.</summary>
        internal static Kind Light(string prefab) => Find(Lights, prefab);

        /// <summary>A station kind by its prefab, or null.</summary>
        internal static Kind Station(string prefab) => Find(Stations, prefab);

        private static Kind Find(Kind[] kinds, string prefab)
        {
            foreach (Kind kind in kinds)
            {
                if (kind.Prefab == prefab)
                {
                    return kind;
                }
            }
            return null;
        }
    }
}
