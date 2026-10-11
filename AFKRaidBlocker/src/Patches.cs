using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

// The Harmony patches. Every target was looked up and checked in GameHooks before anything was patched. Each one does
// nothing unless the mod is on and he is away (Away.HoldsOff, false whenever the mod is off), so patches left in place
// while the mod is off change nothing.
namespace AFKRaidBlocker
{
    /// <summary>
    /// A client's report to the server, every 2 seconds: while he is away, the report sent says what's held off (no base,
    /// or no raids ready), and his game's own report is put back the moment the package is written, whatever happens in
    /// between. On his friend's server this is the whole mod: the server never centres a raid on a player whose report
    /// says so.
    /// </summary>
    [HarmonyPatch]
    internal static class SendReportPatch
    {
        private static MethodBase TargetMethod() => GameHooks.SendReport;

        private static void Prefix(ZNet __instance, out ReportMask __state)
        {
            __state = null;
            if (!Away.HoldsOff || !Module.IsOn)
            {
                return;
            }
            var mask = new ReportMask();
            __state = mask;   // set before any change, so the finalizer puts back whatever Apply got to
            Dictionary<string, string> report = __instance.m_serverSyncedPlayerData;
            HeldOff what = Settings.HeldOff.Value;
            Module.Guard("holding off raids in your report to the server", () => mask.Apply(report, what));
        }

        private static Exception Finalizer(ZNet __instance, ReportMask __state, Exception __exception)
        {
            if (__state != null && __state.Changed)
            {
                try
                {
                    __state.Restore(__instance.m_serverSyncedPlayerData);
                }
                catch (Exception error)
                {
                    Module.Log.LogError($"Couldn't put your report to the server back as it was: {error.GetType().Name}: {error.Message}");
                }
            }
            return __exception;
        }
    }

    /// <summary>
    /// The server's reading of a report before a raid roll. Where his game is the server (single player, or a world he
    /// hosts), it reads his own report straight from his game, never sent: while he is away, that reading says what's
    /// held off. Every other player's report, sent by their own game, is read as it came.
    /// </summary>
    [HarmonyPatch]
    internal static class ReadReportPatch
    {
        private static MethodBase TargetMethod() => GameHooks.ReadReport;

        private static void Postfix(Dictionary<string, string> playerData, ref RandEventSystem.PlayerEventData eventData, ref bool __result)
        {
            if (!__result || !Away.HoldsOff || !Module.IsOn)
            {
                return;
            }
            ZNet net = ZNet.instance;
            if (net == null || !ReferenceEquals(playerData, net.m_serverSyncedPlayerData))
            {
                return;
            }
            RandEventSystem.PlayerEventData reading = eventData;
            bool kept = true;
            Module.Guard("holding off raids in your own report, read by your game as the server", () =>
            {
                kept = ReportMask.Held(true, ref reading.baseValue, Settings.HeldOff.Value);
            });
            eventData = kept ? reading : default;
            __result = kept;
        }
    }

    /// <summary>After the game writes the names over heads: the away mark beside each player who shows one. Skipped
    /// (Prepare) when the names' parts weren't found; its errors switch only the mark off (Mark).</summary>
    [HarmonyPatch]
    internal static class NameLabelsPatch
    {
        private static bool Prepare() => GameHooks.CanShowMarks;

        private static MethodBase TargetMethod() => GameHooks.UpdateHuds;

        private static void Postfix(EnemyHud __instance)
        {
            if (!Module.IsOn || !Settings.SeeMarks.Value)
            {
                return;
            }
            Mark.OnHuds(__instance);
        }
    }
}
