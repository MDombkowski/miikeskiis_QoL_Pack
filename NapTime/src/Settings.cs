using BepInEx.Configuration;
using QoLMods.Kit;
using QoLMods.Naps;
using UnityEngine;

namespace NapTime
{
    /// <summary>
    /// Nap Time's settings (card B24; his picks of 2026-10-11): what each of the bed's two keys does in the afternoon, the
    /// second key, and its messages. When a nap may start, and whether each player picks in the afternoon at all, are the
    /// server's (Nap Time (Server)'s settings); in a world this game runs itself, the four "Your own world" settings (card
    /// B29, 2026-10-12: Nap Time (Server)'s three, by the same names and defaults, and a switch). Every setting is read
    /// where it is needed, so a change applies at once.
    /// </summary>
    internal static class Settings
    {
        internal static ConfigEntry<BedChoice> UseKeyAfternoon { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> NapKey { get; private set; }
        internal static ConfigEntry<BedChoice> NapKeyAfternoon { get; private set; }
        internal static ConfigEntry<bool> Messages { get; private set; }
        internal static ConfigEntry<bool> OwnWorld { get; private set; }
        internal static ConfigEntry<float> OwnFrom { get; private set; }
        internal static ConfigEntry<float> OwnUntil { get; private set; }
        internal static ConfigEntry<AfternoonRule> OwnAfternoon { get; private set; }

        private const string PicksNote =
            " Only where the afternoon lets each player pick (a server's Nap Time (Server), or \"Your own world: in the " +
            "afternoon\" below; both do out of the box): everyone naps until dusk only if everyone in bed chose the nap, else " +
            "everyone sleeps to the morning. In the morning any lie-down is a nap; at night, a sleep to the morning.";

        private const string OwnNote =
            " Only in a world your own game runs (playing alone, or the game's Start Server). On someone else's server, that " +
            "server decides; with miikeskii's Server-Side QoL Pack also in your game, its Nap Time (Server) decides instead.";

        private const string HoursNote =
            " In the sky's hours: 6 is sunrise, 12 noon and 18 sunset, whatever the length of the day and the night.";

        internal static void Bind(ModuleSettings settings)
        {
            UseKeyAfternoon = settings.Bind(ModuleSettings.General, "UseKeyAfternoon", BedChoice.SleepToMorning,
                "E in the afternoon", 50,
                "What lying down with the game's use key (E) does in the afternoon: SleepToMorning, as the game does (past " +
                "the night to the next morning), or NapUntilDusk." + PicksNote);
            NapKey = settings.Bind(ModuleSettings.General, "NapKey", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt),
                "Second key at your bed", 40,
                "Lying down with this key does what \"Second key in the afternoon\" says. Hold its modifier as you press the " +
                "use key. None: no second key.");
            NapKeyAfternoon = settings.Bind(ModuleSettings.General, "NapKeyAfternoon", BedChoice.NapUntilDusk,
                "Second key in the afternoon", 30,
                "What lying down with the second key does in the afternoon: NapUntilDusk, or SleepToMorning as the game " +
                "does." + PicksNote);
            Messages = settings.Bind(ModuleSettings.General, "Messages", true, "Messages", 20,
                "A short line when you lie down for a nap, and \"Good evening\" in place of the game's \"Good morning\" when a " +
                "nap ends at dusk.");

            OwnWorld = settings.Bind(ModuleSettings.General, "OwnWorldNaps", true, "Naps in your own world", 18,
                "When everyone is in bed in the daytime, the clock runs on to sunset and everyone wakes at dusk, by the three " +
                "settings below. Friends who join with the pack can lie down by day too; one without it can't, so a nap waits " +
                "for them as the night does. Off: your beds there are the game's own." + OwnNote);
            OwnFrom = settings.Bind(ModuleSettings.General, "OwnNapFrom", 6f, "Your own world: naps from", 16,
                "The time of day from which everyone in bed may nap until sunset." + HoursNote + OwnNote,
                new AcceptableValueRange<float>(6f, 18f));
            OwnUntil = settings.Bind(ModuleSettings.General, "OwnNapUntil", 18f, "Your own world: naps until", 14,
                "The time of day before which a nap may start. A window that ends where or before it starts allows no nap." +
                HoursNote + OwnNote,
                new AcceptableValueRange<float>(6f, 18f));
            OwnAfternoon = settings.Bind(ModuleSettings.General, "OwnAfternoon", AfternoonRule.EachPlayerPicks,
                "Your own world: in the afternoon, a sleep goes to", 12,
                "From noon to sunset the game already lets everyone sleep, past the night to the next morning. " +
                "EachPlayerPicks: each player picks at the bed (E and the second key, as set above), and everyone naps until " +
                "dusk only if everyone in bed chose the nap. Sunset: every sleep in the afternoon is a nap until dusk. " +
                "NextMorning: the game's own; only the morning naps." + OwnNote);
        }

        /// <summary>What this game serves in a world it runs itself, from the "Your own world" settings, with the latest
        /// nap's sunset (0: none).</summary>
        internal static Served OwnServed(double napTo = 0) => new Served(OwnFrom.Value, OwnUntil.Value, OwnAfternoon.Value, napTo);
    }
}
