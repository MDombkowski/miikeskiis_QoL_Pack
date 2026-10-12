// The day cycle, a part: the one copy to edit is Games\Valheim\QoLMods\parts\DayCycle\. Every mod that uses it carries a
// copy in its own parts\DayCycle\ folder, kept identical by QoLMods\tools\sync-kit.ps1 (and the server pack's), so each
// mod still builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md. It uses neither kit, so a
// module of miikeskii's Server-Side QoL Pack can use it too.
using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;

namespace QoLMods.DayCycle
{
    /// <summary>
    /// Makes the game's EnvMan follow a Cycle (card B23; research part A §7): the length of its cycle (EnvMan.m_dayLengthSec,
    /// the day and the night together), the split between day and night (RescaleDayFraction, and where a morning begins:
    /// GetMorningStartSec and SkipToMorning), how long each weather lasts (m_environmentDuration) and how long the wind holds
    /// (m_windPeriodDuration). Everything else in the game reads the sky's fraction and EnvMan's flags, so it follows by
    /// itself.
    ///
    /// A using module calls Follow every frame with the cycle it wants (World Settings (Server) on the server, from its
    /// settings; World Settings in a player's game, from the server's answer), or null for the game's own. A plugin's copy
    /// follows one cycle: every module of one plugin that calls Follow must pass the same one. Each copy of the
    /// part (one per plugin) remembers the EnvMan's own values before it first changes them, puts them back when its cycle
    /// is the game's own again, and writes nothing at all while it has changed nothing, so two copies in one game (a player
    /// hosting with both packs installed) never fight over it. EnvMan lives in the world's scene, so a new world starts
    /// afresh. When the cycle changes, the sky is set straight to the new time of day rather than swept there (the game's
    /// smoothing would take it the shorter way round, which can be backwards through the evening).
    /// </summary>
    internal static class DayCycle
    {
        /// <summary>The length of the cycle this copy put in place, in seconds (0 while it changes nothing): what its patches
        /// work with.</summary>
        internal static long Length { get; private set; }

        /// <summary>The seconds of night this copy put in place (0 while it doesn't change the day): its patches act only
        /// while this is above 0.</summary>
        internal static double Night { get; private set; }

        private static bool resolved;
        private static readonly List<string> missingAtResolve = new List<string>();
        private static Harmony harmony;

        private static AccessTools.FieldRef<EnvMan, float> smoothFraction;
        private static AccessTools.FieldRef<EnvMan, bool> skipTime;
        private static AccessTools.FieldRef<EnvMan, double> skipToTime;
        private static AccessTools.FieldRef<EnvMan, double> timeSkipSpeed;

        private static EnvMan changed;      // the EnvMan this copy has changed, or null
        private static Cycle applied;       // what it put in place there
        private static long ownLength;      // that EnvMan's own values, put back when the cycle is the game's own again
        private static long ownWeather;
        private static long ownWind;

        /// <summary>
        /// Finds the game's members the part reaches for, and patches EnvMan the first time any using module asks (once per
        /// plugin, under modprojects.&lt;plugin&gt;.daycycle; never removed by a module's host). Adds to
        /// <paramref name="missing"/> what the game lacks; then nothing is patched, and Follow does nothing.
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
                    // Nothing patched and nothing followed: the module using the part goes on without it.
                    missingAtResolve.Add($"the day cycle ({error.Message})");
                }
            }
            missing.AddRange(missingAtResolve);
        }

        private static void ResolveOnce()
        {
            var rescale = AccessTools.Method(typeof(EnvMan), "RescaleDayFraction", new[] { typeof(float) });
            var skip = AccessTools.Method(typeof(EnvMan), "SkipToMorning", Type.EmptyTypes);
            var morning = AccessTools.Method(typeof(EnvMan), "GetMorningStartSec", new[] { typeof(int) });
            if (rescale == null) missingAtResolve.Add("EnvMan.RescaleDayFraction");
            if (morning == null) missingAtResolve.Add("EnvMan.GetMorningStartSec");
            foreach (string field in new[] { "m_dayLengthSec", "m_environmentDuration", "m_windPeriodDuration" })
            {
                if (AccessTools.Field(typeof(EnvMan), field)?.FieldType != typeof(long))
                {
                    missingAtResolve.Add("EnvMan." + field);
                }
            }
            smoothFraction = Ref<float>("m_smoothDayFraction", required: true);
            // The skip's fields are used only where sleeping is decided (the server): without them SkipToMorning isn't
            // patched (SkipReady is false), and a module deciding sleep there keeps the game's own day and night, since the
            // game's own skip would aim at the wrong morning. A player's game never needs them.
            skipTime = Ref<bool>("m_skipTime", required: false);
            skipToTime = Ref<double>("m_skipToTime", required: false);
            timeSkipSpeed = Ref<double>("m_timeSkipSpeed", required: false);
            skipReady = skip != null && skipTime != null && skipToTime != null && timeSkipSpeed != null;
            if (missingAtResolve.Count > 0)
            {
                return;
            }
            string plugin = typeof(DayCycle).Assembly.GetName().Name.ToLowerInvariant();
            harmony = new Harmony($"modprojects.{plugin}.daycycle");
            try
            {
                harmony.Patch(rescale, prefix: new HarmonyMethod(AccessTools.Method(typeof(DayCyclePatches), nameof(DayCyclePatches.RescalePrefix))));
                harmony.Patch(morning, prefix: new HarmonyMethod(AccessTools.Method(typeof(DayCyclePatches), nameof(DayCyclePatches.MorningStartPrefix))));
                if (skipReady)
                {
                    harmony.Patch(skip, prefix: new HarmonyMethod(AccessTools.Method(typeof(DayCyclePatches), nameof(DayCyclePatches.SkipToMorningPrefix))));
                }
            }
            catch (Exception error)
            {
                // A patch that can't apply leaves none of them in place, and the part waits, doing nothing: the rest of
                // the module using it goes on.
                missingAtResolve.Add($"a patch on EnvMan ({error.Message})");
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

        private static AccessTools.FieldRef<EnvMan, T> Ref<T>(string field, bool required)
        {
            if (AccessTools.Field(typeof(EnvMan), field)?.FieldType != typeof(T))
            {
                if (required)
                {
                    missingAtResolve.Add("EnvMan." + field);
                }
                return null;
            }
            return AccessTools.FieldRefAccess<EnvMan, T>(field);
        }

        private static bool skipReady;

        /// <summary>Whether the part found everything and patched the game.</summary>
        internal static bool Ready => resolved && missingAtResolve.Count == 0;

        /// <summary>Whether a sleep's skip follows the cycle too (the part is ready and the game's skip fields were found):
        /// where sleeping is decided, the day may change only while this is true.</summary>
        internal static bool SkipReady => Ready && skipReady;

        /// <summary>The seconds of day this copy put in place (0 while it doesn't change the day).</summary>
        internal static double Day => Length > 0 && Night > 0 ? Length - Night : 0;

        /// <summary>
        /// Every frame, from a using module's Update: makes the live EnvMan follow <paramref name="wanted"/> (null, or
        /// nothing changed, for the game's own). <paramref name="log"/> gets one line whenever what this copy put in place
        /// changes. Safe to call from several modules of one plugin each frame with the same cycle.
        /// </summary>
        internal static void Follow(Cycle wanted, Action<string> log)
        {
            if (!Ready)
            {
                return;
            }
            EnvMan env = EnvMan.instance;
            // Plain reference checks: a destroyed EnvMan counts as null to Unity's own comparison, which would hide that it
            // is gone.
            if (!ReferenceEquals(changed, null) && !ReferenceEquals(env, changed))
            {
                Forget();   // the world this copy changed is gone with its scene (or another world loaded)
            }
            if (env == null)
            {
                return;
            }
            bool wantsAny = wanted != null && wanted.ChangesAnything;
            if (ReferenceEquals(changed, null))
            {
                if (!wantsAny)
                {
                    return;   // nothing changed here: never touch an EnvMan another copy may be changing
                }
                changed = env;
                ownLength = env.m_dayLengthSec;
                ownWeather = env.m_environmentDuration;
                ownWind = env.m_windPeriodDuration;
            }
            if (!wantsAny)
            {
                env.m_dayLengthSec = ownLength;
                env.m_environmentDuration = ownWeather;
                env.m_windPeriodDuration = ownWind;
                bool dayWasChanged = applied != null && applied.ChangesDay;
                Forget();
                if (dayWasChanged)
                {
                    Snap(env, ownLength, CycleMath.GameNight(ownLength));
                }
                log?.Invoke("The day, night, weather and wind are the game's own again.");
                return;
            }
            if (wanted.Equals(applied))
            {
                return;
            }
            double day = wanted.Day > 0 ? wanted.Day : CycleMath.GameDay(ownLength);
            double night = wanted.Night > 0 ? wanted.Night : CycleMath.GameNight(ownLength);
            long length = Math.Max(1L, (long)Math.Round(day + night));
            bool dayMoves = applied == null ? wanted.ChangesDay : applied.Day != wanted.Day || applied.Night != wanted.Night;
            if (wanted.ChangesDay)
            {
                env.m_dayLengthSec = length;
                Length = length;
                Night = night;
            }
            else
            {
                env.m_dayLengthSec = ownLength;
                Length = 0;
                Night = 0;
            }
            env.m_environmentDuration = wanted.Weather > 0 ? (long)Math.Round(wanted.Weather) : ownWeather;
            env.m_windPeriodDuration = wanted.Wind > 0 ? (long)Math.Round(wanted.Wind) : ownWind;
            applied = wanted;
            if (dayMoves)
            {
                Snap(env, env.m_dayLengthSec, wanted.ChangesDay ? night : CycleMath.GameNight(env.m_dayLengthSec));
            }
            log?.Invoke($"The world's cycle is now: {wanted} (a cycle of {Minutes(env.m_dayLengthSec)}, " +
                        $"weather {Minutes(env.m_environmentDuration)}, wind {Minutes(env.m_windPeriodDuration)}).");
        }

        /// <summary>Forgets what this copy changed, without touching the game (the EnvMan is gone, or was just put back).</summary>
        private static void Forget()
        {
            changed = null;
            applied = null;
            Length = 0;
            Night = 0;
        }

        // Sets the sky straight to the time of day it stands at now, so it doesn't sweep there.
        private static void Snap(EnvMan env, long length, double night)
        {
            ZNet net = ZNet.instance;
            if (net == null || smoothFraction == null)
            {
                return;
            }
            smoothFraction(env) = CycleMath.SkyFraction(net.GetTimeSeconds(), length, night);
        }

        private static string Minutes(long seconds) => (seconds / 60.0).ToString("0.##", CultureInfo.InvariantCulture) + " min";

        /// <summary>
        /// SkipToMorning with this copy's night (only while it changes the day; the server's game, where sleeping is decided):
        /// the skip runs to the first morning after now, as fast as the game's own (over 12 s), and logs the game's own line.
        /// </summary>
        internal static void StartSkip(EnvMan env)
        {
            double now = ZNet.instance.GetTimeSeconds();
            double target = CycleMath.SkipTarget(now, Length, Night);
            skipTime(env) = true;
            skipToTime(env) = target;
            timeSkipSpeed(env) = (target - now) / CycleMath.SkipSeconds;
            ZLog.Log($"Time {now}, cycle {Length} s with {Night} s of night, nextm:{target}  skipspeed:{(target - now) / CycleMath.SkipSeconds}");
        }
    }

    /// <summary>
    /// The part's patches on EnvMan, applied by DayCycle.Resolve (not by a module's host, so no host ever removes them). Not
    /// [HarmonyPatch] classes, so no host picks them up. Each does nothing while its copy of the part doesn't change the day
    /// (DayCycle.Night is 0), and none can throw into the game: a failure lets the game's own code run.
    /// </summary>
    internal static class DayCyclePatches
    {
        /// <summary>Prefix on EnvMan.RescaleDayFraction (its one argument, the raw fraction, taken by position so a renamed
        /// parameter can't stop the patch): the raw fraction onto the sky's with this copy's night.</summary>
        internal static bool RescalePrefix(float __0, ref float __result)
        {
            if (DayCycle.Night <= 0 || DayCycle.Length <= 0)
            {
                return true;
            }
            __result = CycleMath.Rescale(__0, DayCycle.Length, DayCycle.Night);
            return false;
        }

        /// <summary>Prefix on EnvMan.GetMorningStartSec (its one argument, the day, by position): where that day's morning
        /// begins.</summary>
        internal static bool MorningStartPrefix(int __0, ref double __result)
        {
            if (DayCycle.Night <= 0 || DayCycle.Length <= 0)
            {
                return true;
            }
            __result = CycleMath.MorningStart(__0, DayCycle.Length, DayCycle.Night);
            return false;
        }

        /// <summary>Prefix on EnvMan.SkipToMorning (run where the world is served, when everyone sleeps).</summary>
        internal static bool SkipToMorningPrefix(EnvMan __instance)
        {
            if (DayCycle.Night <= 0 || DayCycle.Length <= 0 || ZNet.instance == null)
            {
                return true;
            }
            try
            {
                DayCycle.StartSkip(__instance);
                return false;
            }
            catch (Exception error)
            {
                ZLog.LogWarning($"[World Settings] Couldn't start the sleep's skip with the world's own night; the game's own skip runs instead: {error.Message}");
                return true;
            }
        }
    }
}
