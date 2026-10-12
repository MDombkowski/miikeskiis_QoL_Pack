using System.Reflection;
using HarmonyLib;

// The Harmony patches. Both targets were looked up and checked in GameHooks before anything was patched. Each body only
// notes the piece for the Tracker, which does the work four times a second inside Module.Guard (Module.Update), so an
// error can't reach the game from here.
namespace ZFighter
{
    /// <summary>After the game has woken a piece, note it, so the Tracker takes it in at its next round (once the game
    /// is done setting the piece up). Nothing is noted while the mod is off: switched on, it finds every loaded piece
    /// itself.</summary>
    [HarmonyPatch]
    internal static class AwakePatch
    {
        private static MethodBase TargetMethod() => GameHooks.Awake;

        private static void Postfix(WearNTear __instance)
        {
            if (Module.IsOn)
            {
                Tracker.Woke(__instance);
            }
        }
    }

    /// <summary>When a piece is destroyed (removed, broken, or its area unloaded), let go of it at the next round, so
    /// the pieces laid into it are given new levels.</summary>
    [HarmonyPatch]
    internal static class OnDestroyPatch
    {
        private static MethodBase TargetMethod() => GameHooks.OnDestroy;

        private static void Postfix(WearNTear __instance)
        {
            if (Module.IsOn)
            {
                Tracker.Destroyed(__instance);
            }
        }
    }
}
