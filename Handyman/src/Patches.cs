using System.Reflection;
using HarmonyLib;

// The Harmony patch. Its target was looked up and checked in GameHooks before anything was patched. Its body runs
// inside Module.Guard (Hammer.BeforePlayerUpdate), so an error there can't reach the game.
namespace Handyman
{
    /// <summary>Just before the game's Player.Update reads the Hide key: note the tool's piece, read the key, and take
    /// the game's press for this frame when the mod acts on it. Only for the local player; while the module is off it
    /// changes nothing and only forgets a press it was waiting on.</summary>
    [HarmonyPatch]
    internal static class PlayerUpdatePatch
    {
        private static MethodBase TargetMethod() => GameHooks.PlayerUpdate;

        private static void Prefix(Player __instance)
        {
            if (!ReferenceEquals(__instance, Player.m_localPlayer))
            {
                return;
            }
            if (!Module.IsOn)
            {
                Hammer.Idle();
                return;
            }
            Hammer.BeforePlayerUpdate(__instance);
        }
    }
}
