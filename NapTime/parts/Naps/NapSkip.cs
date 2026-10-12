// The naps, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Naps\. Every mod that uses it carries a copy in
// its own parts\Naps\ folder, kept identical by QoLMods\tools\sync-kit.ps1 (and the server pack's), so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md. This file is the part's one patch on
// the game; NapRules.cs beside it uses nothing of the game's, so a scratch harness can run that as it is.
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace QoLMods.Naps
{
    /// <summary>
    /// The nap itself, where the world is served (moved here from Nap Time (Server)'s own Naps for card B29, unchanged in
    /// what it does, so Nap Time can serve naps too in a world its own game serves). Every 2 s the game asks whether
    /// everyone is trying to sleep (Game.UpdateSleeping); in the afternoon and at night it then skips to the next morning.
    /// This prefix comes first: in the daytime, inside the nap window, with everyone in bed by the game's own rule (its
    /// EverybodyIsTryingToSleep, called, so a mod that changes "everyone" counts here too), and in the afternoon by the
    /// afternoon rule (with EachPlayerPicks: every player in bed marked the lie-down a nap), it starts the game's own skip,
    /// to this day's sunset instead of the next morning, and tells everyone the sleep has started (the game's own
    /// SleepStart). From there the game's own code runs on unchanged: it waits for the skip to end, then wakes everyone
    /// (SleepStop). Anything else, and any error, leaves the game's own decision to run. The time of day is read from the
    /// world's clock (as the target is), never the game's smoothed sky, which trails the clock by seconds: on the smoothed
    /// sky a nap asked a moment after sunset would still look like the afternoon and aim at the next day's sunset.
    ///
    /// One module per plugin serves the naps (Nap Time (Server) in the server pack; Nap Time in the QoL Pack), and hands
    /// the part what it serves now (Rules: null while it serves nothing), its Guard and its log line. They are single
    /// slots: a second module of the same plugin setting them would replace the first, so a module that adds naps to a
    /// plugin which already serves them shares that module's Rules instead. Two plugins carrying
    /// the part in one game (a player hosting with both packs) each patch once, under their own Harmony ID; the one whose
    /// Rules give null passes straight through.
    /// </summary>
    internal static class NapSkip
    {
        /// <summary>What the using module serves now (the nap window and the afternoon rule), or null while it serves
        /// nothing (not active, switched off, not serving the world, or standing aside). Asked at each of the game's turns.</summary>
        internal static Func<Served> Rules;

        /// <summary>Runs the skip's work under the using module's Guard: an error switches that module off, and its Rules
        /// then give null. Without one, the work runs as it is, inside the prefix's own catch.</summary>
        internal static Action<string, Action> Guard;

        /// <summary>The using module's log line.</summary>
        internal static Action<string> Say;

        private static bool resolved;
        private static readonly List<string> missingAtResolve = new List<string>();
        private static Harmony harmony;

        private static AccessTools.FieldRef<Game, bool> sleeping;
        private static AccessTools.FieldRef<Game, double> lastSleepTime;
        private static Func<Game, bool> everybodyIsTryingToSleep;
        private static Func<EnvMan, float, float> rescaleDayFraction;
        private static AccessTools.FieldRef<EnvMan, bool> skipTime;
        private static AccessTools.FieldRef<EnvMan, double> skipToTime;
        private static AccessTools.FieldRef<EnvMan, double> timeSkipSpeed;
        private static int markHash;
        private static bool napping;
        private static ZNet watched;

        /// <summary>The sunset the latest sleep runs to when it is a nap, 0 when it is the game's own (Served.NapTo, which
        /// every pack game hears, for its "Good evening").</summary>
        internal static double NapTo { get; private set; }

        /// <summary>Whether the part found everything and patched the game.</summary>
        internal static bool Ready => resolved && missingAtResolve.Count == 0;

        /// <summary>
        /// Every frame, from the using module's Update: a sleep this part didn't start is the game's own, so NapTo goes back
        /// to 0 as it starts (every pack game hears it within a second, long before that sleep's wake); a nap's NapTo stays
        /// after its wake until the next sleep. A new world served starts afresh.
        /// </summary>
        internal static void Watch()
        {
            ZNet net = ZNet.instance;
            if (!ReferenceEquals(net, watched))
            {
                watched = net;
                NapTo = 0;
                napping = false;
            }
            Game game = Game.instance;
            if (game == null || sleeping == null)
            {
                return;
            }
            if (!sleeping(game))
            {
                napping = false;
            }
            else if (!napping)
            {
                NapTo = 0;
            }
        }

        /// <summary>
        /// Finds the game's members the nap reaches for, and patches Game.UpdateSleeping the first time a using module asks
        /// (once per plugin, under modprojects.&lt;plugin&gt;.naps; never removed by a module's host). Adds to
        /// <paramref name="missing"/> what the game lacks; then nothing is patched, and no nap ever starts.
        /// </summary>
        internal static void Resolve(List<string> missing)
        {
            if (!resolved)
            {
                resolved = true;
                try
                {
                    ResolveOnce();
                }
                catch (Exception error)
                {
                    missingAtResolve.Add($"the nap ({error.Message})");
                }
            }
            missing.AddRange(missingAtResolve);
        }

        private static void ResolveOnce()
        {
            var update = AccessTools.Method(typeof(Game), "UpdateSleeping", Type.EmptyTypes);
            if (update == null)
            {
                missingAtResolve.Add("Game.UpdateSleeping");
            }
            sleeping = Field<Game, bool>("m_sleeping");
            lastSleepTime = Field<Game, double>("m_lastSleepTime");
            skipTime = Field<EnvMan, bool>("m_skipTime");
            skipToTime = Field<EnvMan, double>("m_skipToTime");
            timeSkipSpeed = Field<EnvMan, double>("m_timeSkipSpeed");
            var everybody = AccessTools.Method(typeof(Game), "EverybodyIsTryingToSleep", Type.EmptyTypes);
            if (everybody == null || everybody.ReturnType != typeof(bool))
            {
                missingAtResolve.Add("Game.EverybodyIsTryingToSleep");
            }
            else
            {
                everybodyIsTryingToSleep = AccessTools.MethodDelegate<Func<Game, bool>>(everybody);
            }
            var rescale = AccessTools.Method(typeof(EnvMan), "RescaleDayFraction", new[] { typeof(float) });
            if (rescale == null || rescale.ReturnType != typeof(float))
            {
                missingAtResolve.Add("EnvMan.RescaleDayFraction");
            }
            else
            {
                rescaleDayFraction = AccessTools.MethodDelegate<Func<EnvMan, float, float>>(rescale);
            }
            markHash = NapRules.MarkKey.GetStableHashCode();
            if (missingAtResolve.Count > 0)
            {
                return;
            }
            string plugin = typeof(NapSkip).Assembly.GetName().Name.ToLowerInvariant();
            harmony = new Harmony($"modprojects.{plugin}.naps");
            try
            {
                harmony.Patch(update, prefix: new HarmonyMethod(AccessTools.Method(typeof(NapSkipPatches), nameof(NapSkipPatches.UpdateSleepingPrefix))));
            }
            catch (Exception error)
            {
                missingAtResolve.Add($"a patch on Game.UpdateSleeping ({error.Message})");
                try
                {
                    harmony.UnpatchSelf();
                }
                catch (Exception)
                {
                    // nothing more to undo
                }
            }
        }

        private static AccessTools.FieldRef<T, TField> Field<T, TField>(string name)
        {
            if (AccessTools.Field(typeof(T), name)?.FieldType != typeof(TField))
            {
                missingAtResolve.Add(typeof(T).Name + "." + name);
                return null;
            }
            return AccessTools.FieldRefAccess<T, TField>(name);
        }

        /// <summary>Called by the prefix: true when a nap was started (the game's own decision is then skipped this time).</summary>
        internal static bool TryStart(Game game)
        {
            Served served = null;
            if (Ready && game != null)
            {
                try
                {
                    served = Rules?.Invoke();
                }
                catch (Exception error)
                {
                    served = null;   // the using module can't say now: the game's own decides this time
                    NapSkipPatches.TellOnce($"what's served couldn't be read ({error.GetType().Name}: {error.Message})");
                }
            }
            if (served == null)
            {
                return false;
            }
            bool started = false;
            Action work = () => started = Start(game, served);
            if (Guard != null)
            {
                Guard("starting a nap", work);
            }
            else
            {
                work();
            }
            return started;
        }

        private static bool Start(Game game, Served served)
        {
            ZNet net = ZNet.instance;
            EnvMan env = EnvMan.instance;
            if (net == null || env == null || !net.IsServer() || sleeping(game) || env.IsTimeSkipping())
            {
                return false;
            }
            double now = net.GetTimeSeconds();
            if (now - lastSleepTime(game) < NapRules.SleepAgainAfter)
            {
                return false;
            }
            // The time of day by the clock, through the game's own mapping (patched or not), as the target below is.
            double hour = NapRules.Hour(rescaleDayFraction(env, NapRules.RawFraction(now, env.m_dayLengthSec)));
            if (!NapRules.InWindow(hour, served.From, served.Until))
            {
                return false;   // the cheap test first: outside the window the game's own decides, as ever
            }
            if (!NapRules.ServerNaps(hour, served, everybodyIsTryingToSleep(game), EveryoneInBedNaps))
            {
                return false;
            }
            double sunsetRaw = NapRules.SunsetRaw(fraction => rescaleDayFraction(env, fraction));
            double target = NapRules.SunsetAhead(now, env.m_dayLengthSec, sunsetRaw);
            if (double.IsNaN(target) || target <= now)
            {
                return false;   // this day's sunset has passed (a float step at most): the game's own sleep to the morning
            }
            // The game's own skip (EnvMan.SkipToMorning's three fields), aimed at sunset; EnvMan.UpdateTimeSkip runs it.
            skipTime(env) = true;
            skipToTime(env) = target;
            timeSkipSpeed(env) = NapRules.SkipSpeed(now, target);
            // Then as the game's own UpdateSleeping goes on after its skip.
            NapTo = target;
            napping = true;
            sleeping(game) = true;
            ZRoutedRpc.instance.InvokeRoutedRPC(0L, "SleepStart");
            Say?.Invoke($"Everyone is in bed at {NapRules.HourText(hour)} of the day: napping until sunset (world time {now:0} to {target:0}).");
            return true;
        }

        // Every player in bed marked the lie-down a nap (each player's own game writes the mark into the character's
        // record, which it owns; a player without Nap Time never does).
        private static bool EveryoneInBedNaps()
        {
            foreach (ZDO zdo in ZNet.instance.GetAllCharacterZDOS())
            {
                if (zdo != null && zdo.GetBool(ZDOVars.s_inBed) && !zdo.GetBool(markHash))
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>The part's patch, applied by NapSkip.Resolve (not by a module's host, so no host ever removes it). Not a
    /// [HarmonyPatch] class, so no host picks it up. It does nothing while the using module serves nothing, and can't throw
    /// into the game: a failure lets the game's own decision run.</summary>
    internal static class NapSkipPatches
    {
        private static bool told;

        /// <summary>Before the game decides sleeping (every 2 s, where the world is served): a nap, if one is due.</summary>
        internal static bool UpdateSleepingPrefix(Game __instance)
        {
            try
            {
                return !NapSkip.TryStart(__instance);
            }
            catch (Exception error)
            {
                TellOnce($"{error.GetType().Name}: {error.Message}");
                return true;
            }
        }

        /// <summary>A warning, once a session, that a nap couldn't be decided, under this plugin's own name.</summary>
        internal static void TellOnce(string why)
        {
            if (told)
            {
                return;
            }
            told = true;
            ZLog.LogWarning($"[{typeof(NapSkip).Assembly.GetName().Name}, naps] Couldn't decide a nap, so the game's own sleep decides: {why}. Said once.");
        }
    }
}
