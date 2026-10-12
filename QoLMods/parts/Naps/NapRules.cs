// The naps, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Naps\. Every mod that uses it carries a copy in
// its own parts\Naps\ folder, kept identical by QoLMods\tools\sync-kit.ps1 (and the server pack's), so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md. The part uses neither kit, so a module
// of miikeskii's Server-Side QoL Pack can use it too; this file uses nothing of the game's either, so a scratch harness
// can run it as it is (NapSkip.cs beside it is the part's one patch on the game).
using System;
using System.Globalization;

namespace QoLMods.Naps
{
    /// <summary>Where a sleep in the afternoon goes, on the server (Nap Time (Server)'s setting "Afternoon"). The names are
    /// written in its settings file, so they are permanent.</summary>
    internal enum AfternoonRule
    {
        /// <summary>Each player picks at the bed: a nap only if every player in bed chose one, else the game's own sleep to
        /// the next morning.</summary>
        EachPlayerPicks,

        /// <summary>Every daytime sleep is a nap, to sunset.</summary>
        Sunset,

        /// <summary>The game's own: an afternoon sleep goes past the night to the next morning; only the morning naps.</summary>
        NextMorning,
    }

    /// <summary>What one key at the bed does in the afternoon (Nap Time's two settings). The names are permanent.</summary>
    internal enum BedChoice
    {
        /// <summary>The game's own sleep, past the night to the next morning.</summary>
        SleepToMorning,

        /// <summary>A nap, to sunset.</summary>
        NapUntilDusk,
    }

    /// <summary>What the server serves: the nap window in the sky's hours, the afternoon rule, and whether the latest
    /// sleep is a nap.</summary>
    internal sealed class Served : IEquatable<Served>
    {
        internal Served(double from, double until, AfternoonRule afternoon, double napTo = 0)
        {
            From = NapRules.CleanHour(from, NapRules.Sunrise);
            Until = NapRules.CleanHour(until, NapRules.Sunset);
            Afternoon = Enum.IsDefined(typeof(AfternoonRule), afternoon) ? afternoon : AfternoonRule.EachPlayerPicks;
            NapTo = double.IsNaN(napTo) || double.IsInfinity(napTo) || napTo < 0 ? 0 : napTo;
        }

        /// <summary>The window's start, in the sky's hours (6 to 18).</summary>
        internal double From { get; }

        /// <summary>The window's end, in the sky's hours (6 to 18): a nap may start before it.</summary>
        internal double Until { get; }

        internal AfternoonRule Afternoon { get; }

        /// <summary>The sunset (world time) the latest sleep runs to when it is a nap; 0 when the latest sleep is the game's
        /// own, or none has been since the server started. Set as a nap starts, cleared as the game's own sleep starts, so
        /// a player's game hears it (the handshake answers again within a second of a change) before the wake, which comes
        /// at least 2 s after any sleep starts (Game.UpdateSleeping runs every 2 s).</summary>
        internal double NapTo { get; }

        /// <summary>Reads what the server says, from the handshake's answer (or the server module in this same game):
        /// <paramref name="value"/> gives a key's text, or null. A key missing, or a text that isn't a number, is its
        /// default; an afternoon rule this copy doesn't know is EachPlayerPicks.</summary>
        internal static Served Read(Func<string, string> value) =>
            new Served(NapRules.Number(value(NapRules.FromKey), NapRules.Sunrise),
                       NapRules.Number(value(NapRules.UntilKey), NapRules.Sunset),
                       NapRules.ReadRule(value(NapRules.AfternoonKey)),
                       NapRules.Number(value(NapRules.NapToKey), 0));

        /// <summary>Writes it into the handshake's answer: the hours and the nap's sunset as numbers, the rule as its word.</summary>
        internal void Write(Action<string, double> number, Action<string, string> text)
        {
            number(NapRules.FromKey, From);
            number(NapRules.UntilKey, Until);
            text(NapRules.AfternoonKey, NapRules.RuleText(Afternoon));
            number(NapRules.NapToKey, NapTo);
        }

        public bool Equals(Served other) =>
            other != null && other.From == From && other.Until == Until && other.Afternoon == Afternoon && other.NapTo == NapTo;

        public override bool Equals(object obj) => Equals(obj as Served);

        public override int GetHashCode() =>
            From.GetHashCode() ^ (Until.GetHashCode() * 7) ^ ((int)Afternoon * 31) ^ (NapTo.GetHashCode() * 13);

        public override string ToString() =>
            $"naps from {NapRules.HourText(From)} until {NapRules.HourText(Until)}, the afternoon: {Afternoon}" +
            (NapTo > 0 ? $", napping until world time {NapTo.ToString("0", CultureInfo.InvariantCulture)}" : string.Empty);
    }

    /// <summary>What a bed's hover text shows for its keys: the game's own when not Changed.</summary>
    internal struct BedHint
    {
        /// <summary>Whether anything differs from the game's own "[E] Sleep".</summary>
        internal bool Changed;

        /// <summary>Whether the use key (E) naps.</summary>
        internal bool UseKeyNaps;

        /// <summary>Whether the second key gets a line of its own (only where it does something else than E).</summary>
        internal bool ShowSecond;

        /// <summary>Whether the second key naps.</summary>
        internal bool SecondKeyNaps;
    }

    /// <summary>
    /// Nap Time's rules (card B24, his answers of 2026-10-11), the same for the server's skip (Nap Time (Server)) and every
    /// player's bed (Nap Time), on plain numbers. Times of day are the sky's hours: the game's sky fraction × 24, so 6 is
    /// sunrise, 12 noon and 18 sunset, whatever the lengths of the day and the night (EnvMan's sky puts sunrise at 0.25 and
    /// sunset at 0.75 of its fraction; the afternoon, EnvMan.IsAfternoon, is 0.5 to 0.75).
    /// </summary>
    internal static class NapRules
    {
        /// <summary>The server module's id, whose answer in the handshake the players' games read.</summary>
        internal const string ServerModuleId = "NapTimeServer";

        /// <summary>The handshake's keys, in Nap Time (Server)'s answer.</summary>
        internal const string FromKey = "from";
        internal const string UntilKey = "until";
        internal const string AfternoonKey = "afternoon";
        internal const string NapToKey = "napto";

        /// <summary>The bed's mark: a bool in a player's character record (written by that player's own game, which owns it), true when
        /// the lie-down is a nap. The server reads it beside the game's own s_inBed. Permanent.</summary>
        internal const string MarkKey = "modprojects.naptime.nap";

        /// <summary>The sky's hours of sunrise, noon and sunset.</summary>
        internal const double Sunrise = 6.0;
        internal const double Noon = 12.0;
        internal const double Sunset = 18.0;

        /// <summary>The sky's fraction at sunset.</summary>
        internal const float SunsetFraction = 0.75f;

        /// <summary>How long the game's own skip takes, in seconds (EnvMan.c_TimeSkipDuration).</summary>
        internal const double SkipSeconds = 12.0;

        /// <summary>The server's own wait after a sleep before the next may start, in seconds (Game.UpdateSleeping).</summary>
        internal const double SleepAgainAfter = 10.0;

        /// <summary>The sky's hour at a sky fraction.</summary>
        internal static double Hour(float skyFraction) => skyFraction * 24.0;

        /// <summary>An hour as the settings keep it: between sunrise and sunset, or <paramref name="fallback"/> for anything
        /// that isn't a finite number.</summary>
        internal static double CleanHour(double hour, double fallback)
        {
            if (double.IsNaN(hour) || double.IsInfinity(hour))
            {
                return fallback;
            }
            return Math.Min(Sunset, Math.Max(Sunrise, hour));
        }

        /// <summary>Whether a nap may start at <paramref name="hour"/>: in the day (from sunrise to before sunset) and inside
        /// the window (from <paramref name="from"/> to before <paramref name="until"/>). A window that ends before it starts
        /// allows none.</summary>
        internal static bool InWindow(double hour, double from, double until) =>
            hour >= Sunrise && hour < Sunset && hour >= from && hour < until;

        /// <summary>The game's afternoon: from noon on (EnvMan.IsAfternoon).</summary>
        internal static bool IsAfternoon(double hour) => hour >= Noon;

        /// <summary>
        /// The server's decision, once the game's own rule says everyone is trying to sleep
        /// (<paramref name="everyoneInBed"/>): a nap to sunset (true), or the game's own (false: in the afternoon or at night
        /// its sleep to the next morning; in the morning nothing). In the morning inside the window, a nap; in the afternoon
        /// inside it, by <paramref name="rule"/>, where EachPlayerPicks asks <paramref name="everyoneInBedNaps"/> (every
        /// player in bed marked the lie-down a nap), called only then.
        /// </summary>
        internal static bool ServerNaps(double hour, Served served, bool everyoneInBed, Func<bool> everyoneInBedNaps)
        {
            if (!everyoneInBed || served == null || !InWindow(hour, served.From, served.Until))
            {
                return false;
            }
            if (!IsAfternoon(hour))
            {
                return true;
            }
            switch (served.Afternoon)
            {
                case AfternoonRule.Sunset:
                    return true;
                case AfternoonRule.NextMorning:
                    return false;
                default:
                    return everyoneInBedNaps();
            }
        }

        /// <summary>
        /// Whether a player's lie-down at <paramref name="hour"/> is a nap (the bed's mark): in the morning inside the
        /// window, always; in the afternoon inside it, by the server's rule, and with EachPlayerPicks by the key used
        /// (<paramref name="secondKey"/>) and what the player's settings make each key do; at night or outside the window, never.
        /// </summary>
        internal static bool BedNaps(double hour, Served served, bool secondKey, BedChoice useKeyAfternoon, BedChoice secondKeyAfternoon)
        {
            if (served == null || !InWindow(hour, served.From, served.Until))
            {
                return false;
            }
            if (!IsAfternoon(hour))
            {
                return true;
            }
            switch (served.Afternoon)
            {
                case AfternoonRule.Sunset:
                    return true;
                case AfternoonRule.NextMorning:
                    return false;
                default:
                    return (secondKey ? secondKeyAfternoon : useKeyAfternoon) == BedChoice.NapUntilDusk;
            }
        }

        /// <summary>
        /// Whether a player's bed lets them lie down where the game's own says no: in the morning inside the window, once the
        /// game's own wait since they last woke (<paramref name="wait"/> seconds after <paramref name="wokeAt"/>, both world
        /// time) has passed, as the game counts it for the night. The afternoon is the game's own already.
        /// </summary>
        internal static bool BedOpens(double hour, Served served, double now, double wokeAt, double wait) =>
            served != null && InWindow(hour, served.From, served.Until) && !IsAfternoon(hour) && now > wokeAt + wait;

        /// <summary>What the bed's hover text says for its keys at <paramref name="hour"/> (see BedHint).</summary>
        internal static BedHint Hint(double hour, Served served, BedChoice useKeyAfternoon, BedChoice secondKeyAfternoon, bool hasSecondKey)
        {
            var hint = new BedHint();
            if (served == null || !InWindow(hour, served.From, served.Until))
            {
                return hint;
            }
            bool picks = IsAfternoon(hour) && served.Afternoon == AfternoonRule.EachPlayerPicks;
            hint.UseKeyNaps = BedNaps(hour, served, false, useKeyAfternoon, secondKeyAfternoon);
            hint.SecondKeyNaps = BedNaps(hour, served, true, useKeyAfternoon, secondKeyAfternoon);
            hint.ShowSecond = picks && hasSecondKey && hint.SecondKeyNaps != hint.UseKeyNaps;
            hint.Changed = hint.UseKeyNaps || hint.ShowSecond;
            return hint;
        }

        /// <summary>Whether a wake at sky fraction <paramref name="skyFraction"/> (worked out from the world's clock, not the
        /// smoothed sky) is at dusk, not in the morning: a skip lands on sunset or on a morning's start, and a player's
        /// clock is at most the server's last 2 s of the skip behind it at the wake (a sixth of a nap's way, which never
        /// reaches back past noon). A night skip across a long afternoon can leave it short of midnight, so this alone
        /// doesn't tell a nap: WokeFromNap asks the server too.</summary>
        internal static bool WokeAtDusk(float skyFraction) => skyFraction >= 0.5f && skyFraction < 0.875f;

        /// <summary>Whether a wake ends a nap: the server said the latest sleep is a nap (<see cref="Served.NapTo"/>), and
        /// this game's clock is at dusk.</summary>
        internal static bool WokeFromNap(Served served, float skyFraction) => served != null && served.NapTo > 0 && WokeAtDusk(skyFraction);

        /// <summary>The raw fraction of a cycle of <paramref name="length"/> seconds at world time <paramref name="seconds"/>,
        /// exactly as EnvMan.FixedUpdate works it out (the time in milliseconds modulo the cycle's, then in float, clamped
        /// to [0, 1]); 0 with no cycle.</summary>
        internal static float RawFraction(double seconds, long length)
        {
            if (length <= 0)
            {
                return 0f;
            }
            double milliseconds = seconds * 1000.0;
            long cycle = length * 1000;
            float raw = (float)(milliseconds % (double)cycle / 1000.0) / (float)length;
            return raw < 0f ? 0f : raw > 1f ? 1f : raw;
        }

        /// <summary>
        /// The raw fraction of the cycle (0 at midnight) at which the sky first reaches sunset, by the live game's own
        /// mapping from raw to sky (<paramref name="rescale"/>: EnvMan.RescaleDayFraction, with whatever patches it, World
        /// Settings' included), found by halving between noon's 0.5 and 1; NaN where the mapping never reaches sunset there.
        /// </summary>
        internal static double SunsetRaw(Func<float, float> rescale)
        {
            if (!(rescale(1f) >= SunsetFraction))
            {
                return double.NaN;
            }
            double low = 0.5;
            double high = 1.0;
            if (rescale((float)low) >= SunsetFraction)
            {
                return low;
            }
            for (int i = 0; i < 64 && high - low > 1e-9; i++)
            {
                double middle = (low + high) / 2.0;
                if (rescale((float)middle) >= SunsetFraction)
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                }
            }
            return high;
        }

        /// <summary>This cycle's sunset, in world seconds, while it is still after world time <paramref name="now"/>, in a
        /// cycle of <paramref name="length"/> seconds whose sunset lies at raw fraction <paramref name="sunsetRaw"/>; NaN
        /// once it has passed (never the next day's: a nap asked for a moment after sunset must not skip a night and a
        /// day), or when either can't be used.</summary>
        internal static double SunsetAhead(double now, long length, double sunsetRaw)
        {
            if (length <= 0 || double.IsNaN(sunsetRaw) || sunsetRaw <= 0 || sunsetRaw >= 1 || double.IsNaN(now) || double.IsInfinity(now))
            {
                return double.NaN;
            }
            double start = Math.Floor(now / length) * length;
            double sunset = start + sunsetRaw * length;
            if (sunset <= now)
            {
                return double.NaN;
            }
            return sunset;
        }

        /// <summary>The skip's speed to <paramref name="target"/>: as the game's own, the distance over 12 s, and never slower
        /// than the clock itself (a nap begun just before sunset).</summary>
        internal static double SkipSpeed(double now, double target) => Math.Max(1.0, (target - now) / SkipSeconds);

        /// <summary>The afternoon rule as the handshake says it.</summary>
        internal static string RuleText(AfternoonRule rule)
        {
            switch (rule)
            {
                case AfternoonRule.Sunset:
                    return "sunset";
                case AfternoonRule.NextMorning:
                    return "morning";
                default:
                    return "picks";
            }
        }

        /// <summary>The afternoon rule from the handshake's word; anything else (a newer server's rule) is EachPlayerPicks.</summary>
        internal static AfternoonRule ReadRule(string text)
        {
            switch (text)
            {
                case "sunset":
                    return AfternoonRule.Sunset;
                case "morning":
                    return AfternoonRule.NextMorning;
                default:
                    return AfternoonRule.EachPlayerPicks;
            }
        }

        /// <summary>A number from the handshake's text (the invariant culture), or <paramref name="fallback"/>.</summary>
        internal static double Number(string text, double fallback)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 32)
            {
                return fallback;
            }
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double read) ? read : fallback;
        }

        /// <summary>"6:00", "13:30", for the log.</summary>
        internal static string HourText(double hour)
        {
            int minutes = (int)Math.Round(hour * 60.0);
            return $"{minutes / 60}:{minutes % 60:00}";
        }
    }
}
