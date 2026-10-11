using System.Reflection;
using HarmonyLib;
using QoLMods.Chests;
using UnityEngine;

namespace ReserveStock
{
    /// <summary>
    /// Place stacks by hand at a reserved chest: allowed by default, since it's a choice made at that one chest (card
    /// A12). With the setting "Place stacks leaves reserved chests alone too" on, it does nothing there and says why:
    /// the window's button (the game's own, and HexQuickStackStorage's, which replaces it) and holding E on the chest
    /// (Container.StackAll, before any request is sent, so it stops the game's and Hex's alike).
    /// </summary>
    internal static class PlaceStacks
    {
        private static int toldIn = -1;

        /// <summary>True if Place stacks must do nothing at this chest: the mod on, its setting on, and the chest reserved.</summary>
        internal static bool Blocks(Container chest, bool setting) =>
            setting && Module.IsOn && chest != null && ChestReserve.IsReserved(chest);

        /// <summary>True if Place stacks must do nothing at this chest; says why, once a frame (the game's button and
        /// Hex's both ask in the same click).</summary>
        internal static bool Blocked(Container chest)
        {
            if (!Blocks(chest, Settings.BlockPlaceStacks.Value))
            {
                return false;
            }
            if (Time.frameCount != toldIn)
            {
                toldIn = Time.frameCount;
                Messages.Center("This chest is reserved: Place stacks leaves it alone (Reserve Stock's setting).");
            }
            return true;
        }

        /// <summary>Runs the test inside the module's Guard: an error lets Place stacks go ahead, as without the mod.</summary>
        internal static bool Allows(Container chest)
        {
            bool blocked = false;
            Module.Guard("keeping Place stacks out of a reserved chest", () => blocked = Blocked(chest));
            return !blocked;
        }
    }

    /// <summary>The chest window's Place stacks button (InventoryGui.OnStackAll), found by name at start.</summary>
    [HarmonyPatch]
    internal static class PlaceStacksButton
    {
        private static bool Prepare() => GameHooks.OnStackAll != null;

        private static MethodBase TargetMethod() => GameHooks.OnStackAll;

        // First, so the game's own is stopped; HexQuickStackStorage's prefix runs even so (it doesn't look), and is
        // stopped inside it (HexPlaceStacks).
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(InventoryGui __instance) => PlaceStacks.Allows(GameHooks.OpenChest(__instance));
    }

    /// <summary>Holding E on a chest, or with its window open: Container.StackAll asks the chest's owner, and the answer
    /// stacks (the game's, or HexQuickStackStorage's in its place). Stopped before the asking.</summary>
    [HarmonyPatch(typeof(Container), nameof(Container.StackAll))]
    internal static class PlaceStacksByHoldingE
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Container __instance) => PlaceStacks.Allows(__instance);
    }

    /// <summary>HexQuickStackStorage's own Place stacks on one chest, QuickStackService.QuickStack(Player, Container). Its
    /// prefix on the button runs whatever an earlier one said, so another player's personal chest he may only look at
    /// (card A80, ChestAccess) is kept from it here too, with the chest service's own words.</summary>
    [HarmonyPatch]
    internal static class HexPlaceStacks
    {
        private static bool Prepare() => Hex.QuickStackOne != null;

        private static MethodBase TargetMethod() => Hex.QuickStackOne;

        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Container __1) => PlaceStacks.Allows(__1) && Touchable(__1);

        // True unless the mod is on and the chest is another player's that he may only look at: then false, said.
        private static bool Touchable(Container chest)
        {
            bool allowed = true;
            Module.Guard("keeping Place stacks out of another player's personal chest", () =>
            {
                if (Module.IsOn && chest != null && !ChestAccess.MayTouch(chest))
                {
                    allowed = false;
                    Messages.Center($"{ChestMine.Whose(chest)} personal chest:\nlook, don't touch.");
                }
            });
            return allowed;
        }
    }
}
