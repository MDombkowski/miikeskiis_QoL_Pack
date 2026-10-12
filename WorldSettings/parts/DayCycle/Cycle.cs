// The day cycle, a part: the one copy to edit is Games\Valheim\QoLMods\parts\DayCycle\. Every mod that uses it carries a
// copy in its own parts\DayCycle\ folder, kept identical by QoLMods\tools\sync-kit.ps1 (and the server pack's), so each
// mod still builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md. It uses neither kit, so a
// module of miikeskii's Server-Side QoL Pack can use it too.
using System;
using System.Globalization;

namespace QoLMods.DayCycle
{
    /// <summary>
    /// The world's cycle as World Settings (Server) sets it (card B23): the length of a day and of a night, how long each
    /// weather lasts and how long the wind holds, all in seconds of world time, each 0 for the game's own. The server's
    /// module makes it from its settings and tells every pack game in the handshake's answer, by the four keys below;
    /// World Settings, the QoL Pack's half, reads it back from there. Plain values, for the scratch harness.
    /// </summary>
    internal sealed class Cycle : IEquatable<Cycle>
    {
        /// <summary>The handshake's keys, in the answer of the server pack's module "WorldSettingsServer".</summary>
        internal const string DayKey = "day";
        internal const string NightKey = "night";
        internal const string WeatherKey = "weather";
        internal const string WindKey = "wind";

        /// <summary>The shortest day, night, weather or wind other than 0 (the game's own), in seconds: one minute.</summary>
        internal const double Shortest = 60.0;

        /// <summary>The longest day or night, in seconds: a whole real day each.</summary>
        internal const double LongestDayOrNight = 86400.0;

        /// <summary>The longest weather or wind, in seconds: ten hours.</summary>
        internal const double LongestWeatherOrWind = 36000.0;

        /// <summary>Everything the game's own.</summary>
        internal static readonly Cycle GameOwn = new Cycle(0, 0, 0, 0);

        internal Cycle(double day, double night, double weather, double wind)
        {
            Day = Clean(day, LongestDayOrNight);
            Night = Clean(night, LongestDayOrNight);
            Weather = Clean(weather, LongestWeatherOrWind);
            Wind = Clean(wind, LongestWeatherOrWind);
        }

        /// <summary>Seconds of day, or 0 for the game's own.</summary>
        internal double Day { get; }

        /// <summary>Seconds of night, or 0 for the game's own.</summary>
        internal double Night { get; }

        /// <summary>Seconds each weather lasts, or 0 for the game's own.</summary>
        internal double Weather { get; }

        /// <summary>Seconds the wind holds (its longest period), or 0 for the game's own.</summary>
        internal double Wind { get; }

        /// <summary>Whether the length of the day or of the night is changed (the cycle and its split).</summary>
        internal bool ChangesDay => Day > 0 || Night > 0;

        /// <summary>Whether anything at all is changed.</summary>
        internal bool ChangesAnything => ChangesDay || Weather > 0 || Wind > 0;

        /// <summary>
        /// A value as the cycle keeps it: 0 for the game's own (also anything not a finite number, or not above 0), else at
        /// least Shortest and at most <paramref name="longest"/>. What a server says is read through here, so no value a
        /// game is told can stop its sky.
        /// </summary>
        internal static double Clean(double seconds, double longest)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0)
            {
                return 0;
            }
            return Math.Min(longest, Math.Max(Shortest, seconds));
        }

        /// <summary>Reads a cycle from the handshake's answer: <paramref name="value"/> gives a key's text, or null. A key
        /// missing, or a text that isn't a number, is the game's own.</summary>
        internal static Cycle Read(Func<string, string> value)
        {
            return new Cycle(Number(value(DayKey)), Number(value(NightKey)), Number(value(WeatherKey)), Number(value(WindKey)));
        }

        /// <summary>Writes the cycle into the handshake's answer, each value in seconds.</summary>
        internal void Write(Action<string, double> add)
        {
            add(DayKey, Day);
            add(NightKey, Night);
            add(WeatherKey, Weather);
            add(WindKey, Wind);
        }

        private static double Number(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 32)
            {
                return 0;
            }
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double read) ? read : 0;
        }

        public bool Equals(Cycle other) =>
            other != null && other.Day == Day && other.Night == Night && other.Weather == Weather && other.Wind == Wind;

        public override bool Equals(object obj) => Equals(obj as Cycle);

        public override int GetHashCode() => Day.GetHashCode() ^ (Night.GetHashCode() * 7) ^ (Weather.GetHashCode() * 13) ^ (Wind.GetHashCode() * 31);

        /// <summary>"day 1800 s, night 600 s, weather the game's own, wind the game's own", for the log.</summary>
        public override string ToString() =>
            $"day {Seconds(Day)}, night {Seconds(Night)}, weather {Seconds(Weather)}, wind {Seconds(Wind)}";

        private static string Seconds(double value) =>
            value > 0 ? (value / 60.0).ToString("0.##", CultureInfo.InvariantCulture) + " min" : "the game's own";
    }

    /// <summary>
    /// The cycle's arithmetic, as the game's EnvMan does it but with a night of any length (research part A §7). The game
    /// counts a cycle of L seconds from midnight: the raw fraction (t mod L) ÷ L, and the night is the same share either
    /// side of midnight (15 % each in the game: 0.15 and 0.85 of the cycle). RescaleDayFraction maps the raw fraction onto
    /// the sky's fraction, where 0.25 is sunrise and 0.75 sunset, which everything else in the game reads. Plain code, for
    /// the scratch harness.
    /// </summary>
    internal static class CycleMath
    {
        /// <summary>The game's own night as a share of its cycle (its RescaleDayFraction's 0.15 either side of midnight).</summary>
        internal const double GameNightShare = 0.3;

        /// <summary>How long a sleep's skip takes, in seconds (EnvMan.c_TimeSkipDuration).</summary>
        internal const double SkipSeconds = 12.0;

        /// <summary>The game's own day in a cycle of <paramref name="length"/> seconds.</summary>
        internal static double GameDay(double length) => length * (1.0 - GameNightShare);

        /// <summary>The game's own night in a cycle of <paramref name="length"/> seconds.</summary>
        internal static double GameNight(double length) => length * GameNightShare;

        /// <summary>
        /// The raw fraction of the cycle (0 at midnight) mapped onto the sky's fraction, with <paramref name="night"/>
        /// seconds of night in a cycle of <paramref name="length"/>: the night's half before and after midnight onto 0 to
        /// 0.25 and 0.75 to 1, the day between onto 0.25 to 0.75. With a night of 30 % of the cycle it is the game's own
        /// RescaleDayFraction, branch for branch.
        /// </summary>
        internal static float Rescale(float fraction, double length, double night)
        {
            double half = HalfNight(length, night);
            double f = fraction;
            if (f >= half && f <= 1.0 - half)
            {
                return (float)(0.25 + (f - half) / (1.0 - 2.0 * half) * 0.5);
            }
            if (f < 0.5)
            {
                return (float)(f / half * 0.25);
            }
            return (float)(0.75 + (f - (1.0 - half)) / half * 0.25);
        }

        /// <summary>Half the night as a share of the cycle, kept inside (0, 0.5) so no division can fail: 0.15 in the
        /// game. The limits lie beyond anything the settings allow (a minute against a whole day either way is a share of
        /// 1/1441), so every day and night set is drawn exactly as long as set.</summary>
        internal static double HalfNight(double length, double night)
        {
            if (!(length > 0) || !(night > 0))
            {
                return GameNightShare / 2.0;
            }
            return Math.Min(0.4999, Math.Max(0.0001, night / (2.0 * length)));
        }

        /// <summary>The raw fraction of the cycle at world time <paramref name="seconds"/>, as EnvMan works it out (to the
        /// millisecond).</summary>
        internal static float RawFraction(double seconds, long length)
        {
            if (length <= 0)
            {
                return 0f;
            }
            double ms = seconds * 1000.0;
            double cycle = length * 1000.0;
            double into = ms % cycle;
            if (into < 0)
            {
                into += cycle;
            }
            float raw = (float)(into / 1000.0) / length;
            return raw < 0f ? 0f : raw > 1f ? 1f : raw;
        }

        /// <summary>The sky's fraction at world time <paramref name="seconds"/>: where the sun stands, 0.25 sunrise, 0.75
        /// sunset.</summary>
        internal static float SkyFraction(double seconds, long length, double night) =>
            Rescale(RawFraction(seconds, length), length, night);

        /// <summary>Half the night in seconds, as the sky has it (HalfNight's share of the cycle, so a sleep wakes exactly
        /// at the sky's sunrise even at the most lopsided day and night).</summary>
        internal static double HalfNightSeconds(double length, double night) => HalfNight(length, night) * length;

        /// <summary>When day <paramref name="day"/>'s morning begins, in world seconds: the end of the night's half after
        /// midnight (the game's GetMorningStartSec, in double).</summary>
        internal static double MorningStart(long day, double length, double night) => day * length + HalfNightSeconds(length, night);

        /// <summary>
        /// Where a sleep skips to from world time <paramref name="now"/>: the first morning that begins after now (the
        /// game's SkipToMorning: from the afternoon, past the whole night to the next morning; in the night's half after
        /// midnight, to that same night's morning).
        /// </summary>
        internal static double SkipTarget(double now, double length, double night)
        {
            double half = HalfNightSeconds(length, night);
            long day = (long)Math.Floor((now - half) / length);
            return (day + 1) * length + half;
        }
    }
}
