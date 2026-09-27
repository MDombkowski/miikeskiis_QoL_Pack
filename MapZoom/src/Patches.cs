using System;
using System.Reflection;
using HarmonyLib;

// The Harmony patches. Both targets were looked up and checked in GameHooks before anything was patched. Each body
// either only reads or records a few values, or runs inside Module.Guard, so an error here can't reach the game.
// The kit applies them in the order they are declared here: the wheel's patch comes first, so the game's call to it
// inside UpdateMap is already patched when UpdateMap's own patch is compiled (it can't be inlined past the patch).
namespace MapZoom
{
    /// <summary>While this mod has zoomed for this frame's wheel, the game's own read of the wheel inside UpdateMap
    /// gets 0, so the map isn't zoomed twice. At any other moment the wheel reads as usual.</summary>
    [HarmonyPatch]
    internal static class ScrollWheelPatch
    {
        private static MethodBase TargetMethod() => GameHooks.ScrollWheel;

        private static void Postfix(ref float __result)
        {
            if (WheelZoom.Swallowing)   // only ever set while the mod is on (UpdateMapPatch)
            {
                __result = 0f;
            }
        }
    }

    /// <summary>Just before the game updates the map each frame, do this frame's wheel zoom (WheelZoom). Afterwards,
    /// always, even if the game's own code failed, stop hiding the wheel and note the view the game has just drawn.</summary>
    [HarmonyPatch]
    internal static class UpdateMapPatch
    {
        private static MethodBase TargetMethod() => GameHooks.UpdateMap;

        private static void Prefix(Minimap __instance, Player player, bool takeInput)
        {
            if (!Module.IsOn)
            {
                return;
            }
            Module.Guard("zooming the map", () => WheelZoom.BeforeUpdateMap(__instance, player, takeInput));
        }

        private static Exception Finalizer(Minimap __instance, Exception __exception)
        {
            WheelZoom.AfterUpdateMap(__instance);
            return __exception;
        }
    }
}
