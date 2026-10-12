using System.Collections.Generic;
using System.Globalization;
using QoLMods.DayCycle;
using QoLMods.Serving;
using UnityEngine;

namespace WorldSettings
{
    /// <summary>
    /// The day in a world this game runs itself, alone or with the game's Start Server (card B29, his pick of 2026-10-12:
    /// the QoL Pack does it). There the game decides the sky and the sleep in this game, so World Settings sets the cycle
    /// itself, by its "Your own world" settings, as World Settings (Server) does on a server: the day and night only where
    /// the sleep's skip can follow them (DayCycle.SkipReady), and nothing while no friend could hear it (this copy doesn't
    /// answer their games: another of the pack's mods installed on its own does), so the host's sleep never parts from
    /// their sky. Friends who join with the pack hear it under World Settings (Server)'s id (the part's HostPack). With a
    /// plugin of the server pack's kind in this game, that plugin decides, and its own copy of the day cycle sets the sky.
    /// </summary>
    internal static class Own
    {
        /// <summary>Whether World Settings serves the day itself now: on, in a world this player's game serves (never a
        /// dedicated server), with no plugin of the server pack's kind here.</summary>
        internal static bool Serving => Module.IsOn && ServerHalves.OwnWorld && !ServerHalves.KitHere;

        /// <summary>The cycle this game's own sky follows here, or null for the game's own: the settings', while it serves
        /// and its friends' games can hear it; the day and night the game's own where the skip can't follow them.</summary>
        internal static Cycle Wanted()
        {
            if (!Serving || !HostPack.AnswersHere)
            {
                settled = null;   // a world served again later takes the settings at once
                return null;
            }
            Cycle wanted = Settled();
            return DayCycle.SkipReady ? wanted : new Cycle(0, 0, wanted.Weather, wanted.Wind);
        }

        /// <summary>How long a changed "Your own world" length must stand still before the sky follows it, in seconds:
        /// dragging a slider changes it every frame, and each change would snap the sky, log a line and answer every
        /// friend's game again (review, minor 4).</summary>
        internal const float SettleSeconds = 1f;

        private static Cycle settled;
        private static Cycle pending;
        private static float pendingSince;

        /// <summary>Forgets the settled lengths while World Settings is off or this game serves no world (from the module's
        /// Update), so switching it on again, or a world served again later, takes the settings at once (second look, B).</summary>
        internal static void Forget() => settled = null;

        /// <summary>Whether a changed length is waiting to stand still.</summary>
        internal static bool Settling => settled != null && !Settings.OwnCycle().Equals(settled);

        // The "Your own world" settings' cycle once it has stood still SettleSeconds; at once when this game starts serving.
        private static Cycle Settled()
        {
            Cycle now = Settings.OwnCycle();
            float clock = Time.realtimeSinceStartup;
            if (settled == null)
            {
                settled = pending = now;
                pendingSince = clock;
            }
            else if (!now.Equals(pending))
            {
                pending = now;
                pendingSince = clock;
            }
            else if (!pending.Equals(settled) && clock - pendingSince >= SettleSeconds)
            {
                settled = pending;
            }
            return settled;
        }

        /// <summary>The cycle told to friends' games: the day and night the sky here actually follows (each side working out
        /// "the game's own" for itself could differ), the weather and wind as set. As World Settings (Server)'s answer.</summary>
        internal static Cycle AnswerCycle()
        {
            Cycle wanted = Wanted() ?? Cycle.GameOwn;
            if (!DayCycle.Ready)
            {
                return Cycle.GameOwn;
            }
            if (wanted.ChangesDay && DayCycle.Night > 0)
            {
                return new Cycle(DayCycle.Day, DayCycle.Night, wanted.Weather, wanted.Wind);
            }
            return wanted;
        }

        /// <summary>The words under the Enabled switch in the settings window: what applies right now (Where).</summary>
        internal static string Line()
        {
            if (!Module.Running)
            {
                return "Not running (the game's log says why): the day, night, weather and wind are the game's own everywhere.";
            }
            if (!Module.IsOn)
            {
                return "Switched off: the day, night, weather and wind are the game's own everywhere.";
            }
            switch (Where.Now(Module.ServerModule))
            {
                case Place.NoWorld:
                    return "In a world, this says whose day you follow there: in a world your own game runs (alone, or the " +
                           "game's Start Server) \"Your own world\" below, and on someone else's server that server's.";
                case Place.Own:
                    Cycle own = Wanted() ?? Cycle.GameOwn;
                    bool dayKept = Settings.OwnCycle().ChangesDay && !DayCycle.SkipReady;
                    string settling = Settling ? " A change applies once it has stood still for a second." : string.Empty;
                    if (!own.ChangesAnything)
                    {
                        return (dayKept
                            ? "Your own world (your game runs it): the day and night stay the game's own here, since the game has " +
                              "changed and a sleep couldn't follow them; the weather and wind are the game's own too."
                            : "Your own world (your game runs it): the day, night, weather and wind are the game's own; " +
                              "\"Your own world\" below changes them.") + settling;
                    }
                    string kept = dayKept
                        ? " The day and night stay the game's own: the game has changed, and a sleep couldn't follow them."
                        : string.Empty;
                    return $"Your own world (your game runs it): {Describe(own)}, by \"Your own world\" below. Friends who join " +
                           "with the pack follow it." + kept + settling;
                case Place.OwnUnheard:
                    return "Your own world: the day, night, weather and wind stay the game's own, since another of the pack's mods " +
                           "installed on its own answers your friends' games here and they couldn't follow them. Install " +
                           "miikeskii's QoL Pack in place of the separate mods.";
                case Place.OwnServerHalf:
                    string here = $"Your own world: {ServerHalves.Names} {ServerHalves.Are} in your game and " +
                                  $"decide{(ServerHalves.Are == "is" ? "s" : string.Empty)} here";
                    return ServerHalves.Find("WorldSettingsServer.Module") == null
                        ? here + ", without World Settings (Server), so the day is the game's own (\"Your own world\" below waits)."
                        : here + ": the day follows its World Settings (Server) (the game's own while that's off), not \"Your own " +
                          "world\" below.";
                case Place.Asking:
                    return "Asking this server whether it sets the day...";
                case Place.ServerRuns:
                    Cycle served = FromServer.Cycle();
                    return served != null && served.ChangesAnything
                        ? $"This server sets the day: your sky follows it ({Describe(served)})."
                        : "This server keeps the game's own day, and so does your sky.";
                case Place.ServerWithout:
                    return "This server doesn't set the day (it's off or missing there): the day here is the game's own.";
                default:
                    return "This server doesn't set the day (no answer from miikeskii's Server-Side QoL Pack): the day here is " +
                           "the game's own.";
            }
        }

        /// <summary>"a day of 30 min and a night of 12 min; the weather and the wind the game's own".</summary>
        internal static string Describe(Cycle cycle)
        {
            var set = new List<string>();
            var own = new List<string>();
            (cycle.Day > 0 ? set : own).Add(cycle.Day > 0 ? "a day of " + Minutes(cycle.Day) : "the day");
            (cycle.Night > 0 ? set : own).Add(cycle.Night > 0 ? "a night of " + Minutes(cycle.Night) : "the night");
            (cycle.Weather > 0 ? set : own).Add(cycle.Weather > 0 ? "each weather " + Minutes(cycle.Weather) : "the weather");
            (cycle.Wind > 0 ? set : own).Add(cycle.Wind > 0 ? "the wind holding " + Minutes(cycle.Wind) : "the wind");
            string text = Join(set);
            if (own.Count > 0)
            {
                text += "; " + Join(own) + " the game's own";
            }
            return text;
        }

        private static string Join(List<string> parts) =>
            parts.Count <= 1 ? string.Concat(parts) : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];

        private static string Minutes(double seconds) => (seconds / 60.0).ToString("0.#", CultureInfo.InvariantCulture) + " min";
    }
}
