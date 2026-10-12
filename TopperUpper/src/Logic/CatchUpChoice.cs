using System;
using System.Collections.Generic;
using System.Globalization;

namespace TopperUpper.Logic
{
    /// <summary>The wind a windmill catches up with (his addition 2 on card A93; his default, Departure).</summary>
    internal enum WindChoice
    {
        /// <summary>The wind when the players left it: noted on the windmill while its game looked after it.</summary>
        DepartureWindSpeed,

        /// <summary>The wind when they come back: the wind now.</summary>
        ArrivalWindSpeed,

        /// <summary>The average wind of the biome it stands in.</summary>
        BiomeAverage,
    }

    /// <summary>How a catch-up is said.</summary>
    internal enum CatchUpSay
    {
        OverTheStation,
        InTheLogOnly,
        Nothing,
    }

    /// <summary>The catch-up's rules as they stand for this game: his own settings, or the server pack's for everyone.</summary>
    internal sealed class CatchUpRules
    {
        internal bool On;
        internal double CapSeconds;
        internal WindChoice Wind;
        internal bool Smelters;
        internal bool Fermenter;

        /// <summary>The server pack's settings decided (it said "everyone" or "nobody", and he follows it).</summary>
        internal bool FromServer;
    }

    /// <summary>
    /// Who decides the catch-up (card A93, his addition 3: the server pack sets it for everyone, but it must work without
    /// one), and its words, on plain values. The server pack's Topper-Upper (Server) answers "catchup" (everyone, nobody
    /// or each), "catchup.cap" (seconds of play), "catchup.wind" (Departure, Arrival or BiomeAverage), "catchup.smelters"
    /// and "catchup.fermenter" ("1" or "0"). When it runs and says everyone or nobody, and "Follow the server pack's
    /// settings" is on, those decide here (each one it doesn't say, or says in a way this version can't read, stays his
    /// own); otherwise his own settings do.
    /// </summary>
    internal static class CatchUpChoice
    {
        /// <summary>The longest cap anyone may set, in seconds: a week of play.</summary>
        internal const double MostCap = 7 * 86400.0;

        internal static CatchUpRules Decide(CatchUpRules own, bool follow, bool serverRuns, Func<string, string> server)
        {
            if (!follow || !serverRuns || server == null)
            {
                return own;
            }
            string mode = server("catchup")?.Trim();
            if (string.Equals(mode, "nobody", StringComparison.OrdinalIgnoreCase))
            {
                return new CatchUpRules { On = false, CapSeconds = own.CapSeconds, Wind = own.Wind, Smelters = own.Smelters, Fermenter = own.Fermenter, FromServer = true };
            }
            if (!string.Equals(mode, "everyone", StringComparison.OrdinalIgnoreCase))
            {
                return own;   // "each", nothing, or a word this version doesn't know: each player's own settings
            }
            return new CatchUpRules
            {
                On = true,
                CapSeconds = TryCap(server("catchup.cap"), out double cap) ? cap : own.CapSeconds,
                Wind = TryWind(server("catchup.wind"), out WindChoice wind) ? wind : own.Wind,
                Smelters = TryFlag(server("catchup.smelters"), out bool smelters) ? smelters : own.Smelters,
                Fermenter = TryFlag(server("catchup.fermenter"), out bool fermenter) ? fermenter : own.Fermenter,
                FromServer = true,
            };
        }

        /// <summary>"Departure", "Arrival" or "BiomeAverage" (any case; the setting's own names too).</summary>
        internal static bool TryWind(string text, out WindChoice wind)
        {
            wind = WindChoice.DepartureWindSpeed;
            switch (text?.Trim().ToLowerInvariant())
            {
                case "departure":
                case "departurewindspeed":
                    wind = WindChoice.DepartureWindSpeed;
                    return true;
                case "arrival":
                case "arrivalwindspeed":
                    wind = WindChoice.ArrivalWindSpeed;
                    return true;
                case "biomeaverage":
                    wind = WindChoice.BiomeAverage;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>"1" or "0".</summary>
        internal static bool TryFlag(string text, out bool on)
        {
            on = false;
            switch (text?.Trim())
            {
                case "1":
                    on = true;
                    return true;
                case "0":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>A cap in seconds of play, in the invariant culture: not negative, at most MostCap.</summary>
        internal static bool TryCap(string text, out double seconds)
        {
            if (!PlayClock.TryNumber(text, out seconds) || seconds < 0.0)
            {
                seconds = 0.0;
                return false;
            }
            seconds = Math.Min(seconds, MostCap);
            return true;
        }

        /// <summary>"1" or "0", as the server pack's keys are said.</summary>
        internal static string Flag(bool on) => on ? "1" : "0";

        /// <summary>A time for the line over a station: "3 h 12 min", "45 min", "under a minute".</summary>
        internal static string Duration(double seconds)
        {
            long minutes = (long)Math.Floor(Math.Max(0.0, seconds) / 60.0);
            if (minutes <= 0)
            {
                return "under a minute";
            }
            long hours = minutes / 60;
            long rest = minutes % 60;
            if (hours == 0)
            {
                return $"{rest} min";
            }
            return rest == 0 ? $"{hours} h" : $"{hours} h {rest} min";
        }

        /// <summary>The line for a station (review: the time it made up with players on, never the game's own hour):
        /// "Smelter caught up 3 h 12 min: Copper x96"; with under a minute of play to make up (the game's own catch-up of
        /// what was in it, done by the catch-up), "Smelter finished what was in it: Copper x10".</summary>
        internal static string Line(string station, int playedSeconds, string made) =>
            playedSeconds >= 60 ? $"{station} caught up {Duration(playedSeconds)}: {made}" : $"{station} finished what was in it: {made}";

        /// <summary>What was made, for the line ("Copper x96, Iron x30"): the most first, at most four kinds, then "and n
        /// more"; names through <paramref name="nameOf"/>.</summary>
        internal static string Made(IDictionary<string, int> made, Func<string, string> nameOf)
        {
            var sorted = new List<KeyValuePair<string, int>>();
            if (made != null)
            {
                foreach (KeyValuePair<string, int> each in made)
                {
                    if (each.Value > 0)
                    {
                        sorted.Add(each);
                    }
                }
            }
            if (sorted.Count == 0)
            {
                return "nothing made";
            }
            sorted.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            var parts = new List<string>();
            for (int i = 0; i < sorted.Count && i < 4; i++)
            {
                parts.Add($"{(nameOf != null ? nameOf(sorted[i].Key) : sorted[i].Key)} x{sorted[i].Value}");
            }
            string text = string.Join(", ", parts);
            return sorted.Count > 4 ? $"{text} and {sorted.Count - 4} more" : text;
        }

        /// <summary>A product's stacks: as few as can be, each at most <paramref name="maxStack"/> ("never single items").</summary>
        internal static List<int> Stacks(int amount, int maxStack)
        {
            var stacks = new List<int>();
            int most = Math.Max(1, maxStack);
            while (amount > 0)
            {
                int part = Math.Min(amount, most);
                stacks.Add(part);
                amount -= part;
            }
            return stacks;
        }
    }
}
