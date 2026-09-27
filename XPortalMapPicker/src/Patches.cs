using System.Reflection;
using HarmonyLib;

// The Harmony patches. Every target was looked up and checked in XPortalHooks or GameHooks before anything
// was patched. Each body either only reads or sets a flag, or runs inside Module.Guard, so an error here
// can't reach XPortal or the game.
namespace XPortalMapPicker
{
    /// <summary>After XPortal builds its panel, measure it and add the Map button to it.</summary>
    [HarmonyPatch]
    internal static class BuildPanelPatch
    {
        private static MethodBase TargetMethod() => XPortalHooks.BuildPanel;

        private static void Postfix(object __instance) => Module.Guard("adding the Map button", () => MapButton.OnPanelBuilt(__instance));
    }

    /// <summary>After XPortal lays out the destination row, make room for the Map button.</summary>
    [HarmonyPatch]
    internal static class ArrangeDestinationRowPatch
    {
        private static MethodBase TargetMethod() => XPortalHooks.ArrangeDestinationRow;

        private static void Postfix(object __instance) => Module.Guard("placing the Map button", () => MapButton.Arrange(__instance));
    }

    /// <summary>
    /// While picking, the portal markers take the clicks, and a left click on the map itself does nothing: the game's
    /// own left click (ticking the nearest pin) is skipped. Runs before every other prefix. HarmonyX still runs the
    /// others: Cartur's Map Pins' prefix ignores whether the original will run, so a Shift+click on the map can still
    /// open its pin editor when its icon picker is switched on.
    /// </summary>
    [HarmonyPatch]
    internal static class MapLeftClickPatch
    {
        private static MethodBase TargetMethod() => GameHooks.MapLeftClick;

        [HarmonyPriority(Priority.First)]
        private static bool Prefix() => !PickMode.IsPicking;
    }

    /// <summary>While picking, a right click on the map cancels, and the game's own right click (deleting the pin
    /// under the pointer) is skipped. A right click on a marker cancels too (PortalMarker).</summary>
    [HarmonyPatch]
    internal static class MapRightClickPatch
    {
        private static MethodBase TargetMethod() => GameHooks.MapRightClick;

        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            if (!PickMode.IsPicking)
            {
                return true;
            }
            PickMode.OnRightClick();
            return false;
        }
    }

    /// <summary>While picking, a double click must not open the game's box for adding a new pin.</summary>
    [HarmonyPatch]
    internal static class MapDoubleClickPatch
    {
        private static MethodBase TargetMethod() => GameHooks.MapDoubleClick;

        [HarmonyPriority(Priority.First)]
        private static bool Prefix() => !PickMode.IsPicking;
    }

    /// <summary>On logout or quit, end picking before the game saves the character.</summary>
    [HarmonyPatch]
    internal static class WorldShutdownPatch
    {
        private static bool Prepare() => GameHooks.WorldShutdown != null;

        private static MethodBase TargetMethod() => GameHooks.WorldShutdown;

        private static void Prefix() => Module.Guard("ending picking at logout", PickMode.OnWorldClosing);
    }
}
