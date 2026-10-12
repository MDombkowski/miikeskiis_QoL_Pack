using BepInEx.Configuration;
using TopperUpper.Logic;

namespace TopperUpper
{
    /// <summary>
    /// The catch-up's settings (card A93, 0.2.0): the part "Catch-up", its own band of orders (300 down to 290), bound
    /// between "Linking and products" and "Loose items". Keys are permanent from 0.2.0. Off out of the box (the pack's
    /// rule for something new that changes what a player already sees: MODULES.md, rule 4); a server running
    /// miikeskii's Server-Side QoL Pack can set it for everyone.
    /// </summary>
    internal static partial class Settings
    {
        internal const string CatchUpPart = "Catch-up";

        /// <summary>The catch-up's band of orders (its title row and settings), highest first.</summary>
        internal const int CatchUpOrderTop = 300;

        internal static ConfigEntry<bool> CatchUp { get; private set; }
        internal static ConfigEntry<float> CatchUpMost { get; private set; }
        internal static ConfigEntry<WindChoice> CatchUpWind { get; private set; }
        internal static ConfigEntry<bool> CatchUpSmelters { get; private set; }
        internal static ConfigEntry<bool> CatchUpFermenter { get; private set; }
        internal static ConfigEntry<bool> FollowServerPack { get; private set; }
        internal static ConfigEntry<CatchUpSay> CatchUpSayMade { get; private set; }
        internal static ConfigEntry<bool> LogCatchUps { get; private set; }

        private static void BindCatchUp()
        {
            BeginFold(CatchUpPart, CatchUpOrderTop);
            CatchUp = Add(CatchUpPart, "CatchUp", false, "Catch up while away",
                "When a base's stations load again after you were away, they catch up at once on the time players were on the " +
                "server since they last ran: they're fed from the chests within \"Input radius around a station\" (Stations) " +
                "of them by your rules for all that time, as if the " +
                "base had stayed loaded, and what they made goes straight into the chests (their linked outputs, or those within " +
                "\"Output radius around a station\") as stacks, with one line per station. " +
                "Only stations \"Feed stations\" feeds, and only by the game that looks after them; never fires and torches. " +
                "Off out of the box. On a server running miikeskii's Server-Side QoL Pack, the server can switch it on or off " +
                "for everyone (\"Follow the server pack's settings\").");
            CatchUpMost = Add(CatchUpPart, "CatchUpMost", 24f, "Most time made up (hours)",
                "The most time a station catches up on at once, in hours of play. Only time with players on the server counts: " +
                "a night with the server empty never does (in single player, only time you're in the world). Whatever this " +
                "says, the game itself always lets a station finish what was already in it, up to an hour.",
                new AcceptableValueRange<float>(1f, 168f));
            CatchUpWind = Add(CatchUpPart, "CatchUpWind", WindChoice.DepartureWindSpeed, "Windmill's wind",
                "The wind a windmill catches up with. DepartureWindSpeed: the wind when you left it (noted on the windmill " +
                "while your game looked after it; one never noted uses the wind now). ArrivalWindSpeed: the wind when you " +
                "come back. BiomeAverage: the average wind of the biome it stands in, from the game's weathers there and how " +
                "often each comes.");
            CatchUpSmelters = Add(CatchUpPart, "CatchUpSmelters", true, "Smelters, kilns and the like",
                "The smelter, the blast furnace, the charcoal kiln, the windmill, the spinning wheel, the eitr refinery and the " +
                "Frigid Kiln catch up.");
            CatchUpFermenter = Add(CatchUpPart, "CatchUpFermenter", true, "Fermenter",
                "A fermenter that was ready is tapped, filled and fermented again as often as the time allows (40 minutes a " +
                "batch). Only with \"Tap the fermenter when it's ready\" on.");
            FollowServerPack = Add(CatchUpPart, "FollowServerPack", true, "Follow the server pack's settings",
                "On a server running miikeskii's Server-Side QoL Pack whose catch-up is set for everyone (or for nobody), its " +
                "settings decide here instead of the five above. Off: yours always decide. Without the server pack, yours " +
                "decide.");
            CatchUpSayMade = Add(CatchUpPart, "CatchUpSay", CatchUpSay.OverTheStation, "Say what was made",
                "OverTheStation: one line floating over each station that caught up (\"Smelter caught up 3 h 12 min: Copper " +
                "x96\"), only on your screen; a station farther than the game shows such text gets a line at the top left " +
                "instead. InTheLogOnly: the line goes to the game's log only. Nothing: quietly.");
            LogCatchUps = Add(CatchUpPart, "LogCatchUps", false, "Write catch-ups to the log",
                "For testing: a line in BepInEx\\LogOutput.log for each station that catches up (the time counted and from " +
                "which clock, the wind, what was taken and made, where it went), and for each that doesn't, why.");
            EndFold();
        }
    }
}
