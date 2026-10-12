using System;
using System.Collections.Generic;
using System.Globalization;
using QoLMods.Chests;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// The catch-up's clock in the game (card A93, his addition 1: only time with players on the server counts). Which
    /// clock, in order: on a server running miikeskii's Server-Side QoL Pack with its Topper-Upper (Server), the server
    /// pack's (the spans of world time the server stood empty, "clock.gaps", complete from "clock.since": every player
    /// counts, with the pack or without); alone or hosting (this game is the server), the world's own time, which runs
    /// only while the world does; on a server without it, this game's own time online in that world, which errs toward
    /// less (time when only others were on goes uncounted). The last is kept with the character (the game saves its
    /// custom data with it), one key per world by the world's id, which a client knows from the server's first word
    /// (ZNet.GetWorldUID): spans of world time, the newest kept while they reach back over the longest cap, at most 64.
    /// Noted every 10 s while he's in a world on a server and the mod is on, whatever the catch-up's settings say, so
    /// turning the catch-up on later finds the time already counted. Switched off, nothing is written (review F6): the
    /// session's span then starts when it's switched on, erring toward less.
    /// </summary>
    internal static class PlayTime
    {
        /// <summary>The server pack's module that keeps the clock.</summary>
        internal const string ServerModule = "TopperUpperServer";

        /// <summary>The character's key for one world's spans: this and the world's id.</summary>
        internal const string KeyPrefix = "modprojects.topperupper.online.";

        /// <summary>How often the spans are noted, in seconds.</summary>
        internal const float NoteEvery = 10f;

        /// <summary>The most spans kept per world.</summary>
        internal const int MostSpans = 64;

        private static readonly Dictionary<string, List<Span>> Remembered = new Dictionary<string, List<Span>>(StringComparer.Ordinal);
        private static ZNet world;
        private static string key;
        private static double sessionStart = double.NaN;
        private static List<Span> kept = new List<Span>();
        private static Player notedOn;
        private static float notedAt = float.NegativeInfinity;
        private static bool toldBad;

        /// <summary>Every frame, from the module's Update (on or off): on a server, notes this game's time online in this
        /// world into the character's custom data, every 10 s.</summary>
        internal static void Tick()
        {
            ZNet net = ZNet.instance;
            Player player = Player.m_localPlayer;
            if (net != world)
            {
                world = net;
                key = null;
                sessionStart = double.NaN;
                kept = new List<Span>();
                notedOn = null;
            }
            if (!Module.IsOn)
            {
                sessionStart = double.NaN;   // switched off, nothing is written (MODULES.md rule 5): the time it's off goes uncounted
                return;
            }
            if (net == null || net.IsServer() || player == null)
            {
                return;
            }
            if (key == null)
            {
                key = KeyOf(net);
                if (key == null)
                {
                    return;   // the world's id isn't known yet
                }
            }
            double now = net.GetTimeSeconds();
            if (double.IsNaN(sessionStart))
            {
                sessionStart = now;
                kept = Remembered.TryGetValue(key, out List<Span> before) ? new List<Span>(before) : new List<Span>();
            }
            if (player != notedOn)
            {
                // A new character body (arriving, or after dying): what its saved data holds joins what's kept.
                notedOn = player;
                notedAt = float.NegativeInfinity;
                if (player.m_customData != null && player.m_customData.TryGetValue(key, out string text))
                {
                    if (PlayClock.TryParse(text, out List<Span> saved))
                    {
                        kept.AddRange(saved);
                    }
                    else if (!toldBad)
                    {
                        toldBad = true;
                        Module.Warn("Topper-Upper's note of your time online in this world couldn't be read; it starts again from now.");
                    }
                }
            }
            if (Time.realtimeSinceStartup - notedAt < NoteEvery)
            {
                return;
            }
            notedAt = Time.realtimeSinceStartup;
            kept = PlayClock.Online(kept, sessionStart, now, CatchUpChoice.MostCap, MostSpans);
            Remembered[key] = kept;
            if (player.m_customData != null)
            {
                player.m_customData[key] = PlayClock.Format(kept);
            }
        }

        /// <summary>
        /// The time with players on between two world times, in seconds, by the clock that applies here (see the class), and
        /// in <paramref name="clock"/> which clock that was, in words for the log.
        /// </summary>
        internal static double Played(double from, double to, out string clock)
        {
            ZNet net = ZNet.instance;
            if (net == null || net.IsServer())
            {
                clock = "the world's own time (alone or hosting)";
                return Math.Max(0.0, to - from);
            }
            List<Span> own = Own(to);
            if (ServerPack.Runs(ServerModule) &&
                PlayClock.TryParse(ServerPack.Value(ServerModule, "clock.gaps"), out List<Span> gaps) &&
                PlayClock.TryNumber(ServerPack.Value(ServerModule, "clock.since"), out double since))
            {
                clock = "the server pack's clock";
                return PlayClock.WithGaps(from, to, gaps, since, own);
            }
            clock = "your own time online in this world";
            return PlayClock.Overlap(from, to, own);
        }

        // This game's spans in this world, the session's up to now included.
        private static List<Span> Own(double now) =>
            double.IsNaN(sessionStart) ? new List<Span>(kept) : PlayClock.Online(kept, sessionStart, now, CatchUpChoice.MostCap, MostSpans);

        // The character's key for this world, or null while its id isn't known.
        private static string KeyOf(ZNet net)
        {
            try
            {
                return KeyPrefix + net.GetWorldUID().ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
