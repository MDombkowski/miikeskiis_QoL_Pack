using System;
using System.Reflection;
using HarmonyLib;

// The Harmony patch: a postfix on the game's Minimap.UpdatePins, ordered before Cartur's Map Pins' own postfix there
// (by its Harmony ID, which is its plugin ID). The target was looked up and checked in GameHooks before anything was
// patched. The body runs inside Module.Guard and does nothing while the mod is off, so an error here can't reach the game.
namespace MapViewRules
{
    /// <summary>Before Cartur's: icons and names off by the rules, and sizes (Drawing.Draw).</summary>
    [HarmonyPatch]
    internal static class UpdatePinsPatch
    {
        // The map being drawn, for the one delegate Guard runs: no new closure each pass (the minimap redraws its pins
        // nearly every frame while the player moves).
        private static Minimap drawing;
        private static readonly Action Draw = () => Drawing.Draw(drawing);

        private static MethodBase TargetMethod() => GameHooks.UpdatePins;

        [HarmonyBefore(Cartur.Guid)]
        [HarmonyPriority(Priority.High)]
        private static void Postfix(Minimap __instance)
        {
            if (!Module.IsOn)
            {
                return;
            }
            drawing = __instance;
            try
            {
                Module.Guard("sorting the map's pins", Draw);
            }
            finally
            {
                drawing = null;
            }
        }
    }
}
