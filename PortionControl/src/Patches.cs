using System.Reflection;
using HarmonyLib;

// The Harmony patches. Both targets were looked up and checked in GameHooks before anything was patched. Each body asks
// Module.IsOn first and runs its work inside Module.Guard, so an error here can't reach the game, and while the mod is
// off the game's own code runs untouched.
namespace PortionControl
{
    /// <summary>As the game opens the split window on a stack, show this mod's buttons for that stack.</summary>
    [HarmonyPatch]
    internal static class ShowSplitDialogPatch
    {
        private static MethodBase TargetMethod() => GameHooks.ShowSplitDialog;

        // Not applied at all while BetterSplitDialog or Abacus has the window: they patch the same methods, and a patch
        // that failed there would switch off the chest window's buttons too (review round 1).
        private static bool Prepare() => !Module.SplitWindowStandsAside;

        // __0 and __1 by position: the game's second parameter is spelt "fromIventory".
        private static void Postfix(InventoryGui __instance, ItemDrop.ItemData __0, Inventory __1)
        {
            if (!Module.IsOn || Module.SplitWindowStandsAside)
            {
                return;
            }
            Module.Guard("setting up the split window", () => Window.Opened(__instance, __0, __1));
        }
    }

    /// <summary>While the amount box has the keyboard, the game's own reading of the window's keys (digits, arrows,
    /// Enter) waits, so a digit isn't read twice; Enter then takes the typed amount. Otherwise the game reads them as
    /// usual.</summary>
    [HarmonyPatch]
    internal static class UpdateSplitDialogPatch
    {
        private static MethodBase TargetMethod() => GameHooks.UpdateSplitDialog;

        private static bool Prepare() => !Module.SplitWindowStandsAside;

        private static bool Prefix(InventoryGui __instance)
        {
            if (!Module.IsOn || Module.SplitWindowStandsAside)
            {
                return true;
            }
            bool runGame = true;
            Module.Guard("reading the split window's keys", () => runGame = Window.BeforeGameKeys(__instance));
            return runGame;
        }
    }
}
