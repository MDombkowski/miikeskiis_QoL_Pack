using BepInEx.Configuration;
using QoLMods.DayCycle;
using QoLMods.Kit;

namespace WorldSettings
{
    /// <summary>
    /// World Settings' settings (card B29, 2026-10-12): the day, night, weather and wind of a world this game runs itself
    /// (alone, or the game's Start Server), by World Settings (Server)'s four, with the same ranges and defaults (each 0,
    /// the game's own, until set). On someone else's server its cycle follows that server's, which needs no setting. All in
    /// General, under the module's one heading. Every setting is read where it is needed, so a change applies at once.
    /// </summary>
    internal static class Settings
    {
        internal static ConfigEntry<float> OwnDay { get; private set; }
        internal static ConfigEntry<float> OwnNight { get; private set; }
        internal static ConfigEntry<float> OwnWeather { get; private set; }
        internal static ConfigEntry<float> OwnWind { get; private set; }

        private const string OwnNote =
            " Only in a world your own game runs (playing alone, or the game's Start Server). On someone else's server, that " +
            "server's day decides; with miikeskii's Server-Side QoL Pack also in your game, its World Settings (Server) decides " +
            "instead.";

        internal static void Bind(ModuleSettings settings)
        {
            OwnDay = settings.Bind(ModuleSettings.General, "OwnDayMinutes", 0f, "Your own world: length of the day", 40,
                "Minutes from sunrise to sunset. 0 keeps the game's own (21 minutes). Friends who join with miikeskii's QoL " +
                "Pack see it too; friends without it keep the game's own sky, out of step with your sleep. When it changes, " +
                "the day number and the time of day move too: the day number is the world's age in days." + OwnNote,
                new AcceptableValueRange<float>(0f, 1440f));
            OwnNight = settings.Bind(ModuleSettings.General, "OwnNightMinutes", 0f, "Your own world: length of the night", 30,
                "Minutes from sunset to sunrise. 0 keeps the game's own (9 minutes). As the day: friends with the pack see it, " +
                "and a sleep still skips to the next morning." + OwnNote,
                new AcceptableValueRange<float>(0f, 1440f));
            OwnWeather = settings.Bind(ModuleSettings.General, "OwnWeatherMinutes", 0f, "Your own world: how long each weather lasts", 20,
                "Minutes each weather lasts before the next is picked. 0 keeps the game's own (11.1 minutes). Friends with the " +
                "pack follow it; friends without it see the game's own weather." + OwnNote,
                new AcceptableValueRange<float>(0f, 600f));
            OwnWind = settings.Bind(ModuleSettings.General, "OwnWindMinutes", 0f, "Your own world: how long the wind holds", 10,
                "Minutes the wind's slowest change takes; its quicker turns follow in step. 0 keeps the game's own (16.7 " +
                "minutes). Friends with the pack follow it; friends without it feel the game's own wind." + OwnNote,
                new AcceptableValueRange<float>(0f, 600f));
        }

        /// <summary>The cycle the "Your own world" settings ask for, in seconds (each 0 for the game's own).</summary>
        internal static Cycle OwnCycle() =>
            new Cycle(OwnDay.Value * 60.0, OwnNight.Value * 60.0, OwnWeather.Value * 60.0, OwnWind.Value * 60.0);
    }
}
