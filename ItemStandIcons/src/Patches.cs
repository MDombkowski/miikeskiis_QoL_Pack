using System.Reflection;
using HarmonyLib;
using UnityEngine;

// The Harmony patches: each runs after the game has drawn a place's icons, and marks them (Places). Every target was
// looked up and checked in GameHooks before anything was patched; a place whose target wasn't found is skipped (Prepare).
// Each body only reads the game and draws the mod's own marks, inside Module.Guard, so an error here can't reach the game.
// Nothing runs while the marks are off and none shows (Places.Busy). Positional arguments (__0, __1), so a renamed
// parameter can't stop a patch from applying.
namespace ItemStandIcons
{
    [HarmonyPatch]
    internal static class GridPatch
    {
        private static bool Prepare() => GameHooks.GridUpdateGui != null;

        private static MethodBase TargetMethod() => GameHooks.GridUpdateGui;

        [HarmonyPriority(Priority.Low)]
        private static void Postfix(InventoryGrid __instance)
        {
            if (Places.Busy)
            {
                Module.Guard("marking an inventory", () => Places.AfterGrid(__instance));
            }
        }
    }

    [HarmonyPatch]
    internal static class HotbarPatch
    {
        private static bool Prepare() => GameHooks.HotbarUpdateIcons != null;

        private static MethodBase TargetMethod() => GameHooks.HotbarUpdateIcons;

        [HarmonyPriority(Priority.Low)]
        private static void Postfix(HotkeyBar __instance)
        {
            if (Places.Busy)
            {
                Module.Guard("marking the hotbar", () => Places.AfterHotbar(__instance));
            }
        }
    }

    [HarmonyPatch]
    internal static class RecipeListPatch
    {
        private static bool Prepare() => GameHooks.AddRecipeToList != null;

        private static MethodBase TargetMethod() => GameHooks.AddRecipeToList;

        // Always noted, also while the marks are off (noting changes nothing), so they show at once when switched on.
        private static void Postfix(InventoryGui __instance)
        {
            Module.Guard("marking the crafting list", () => Places.AfterAddRecipe(__instance));
        }
    }

    [HarmonyPatch]
    internal static class ChosenRecipePatch
    {
        private static bool Prepare() => GameHooks.UpdateRecipe != null;

        private static MethodBase TargetMethod() => GameHooks.UpdateRecipe;

        private static void Postfix(InventoryGui __instance)
        {
            if (Places.Busy)
            {
                Module.Guard("marking the chosen recipe", () => Places.AfterUpdateRecipe(__instance));
            }
        }
    }

    [HarmonyPatch]
    internal static class RequirementPatch
    {
        private static bool Prepare() => GameHooks.SetupRequirement != null;

        private static MethodBase TargetMethod() => GameHooks.SetupRequirement;

        private static void Postfix(Transform __0, Piece.Requirement __1, bool __result)
        {
            if (Places.Busy)
            {
                Module.Guard("marking an ingredient", () => Places.AfterSetupRequirement(__0, __1, __result));
            }
        }
    }

    [HarmonyPatch]
    internal static class HideRequirementPatch
    {
        private static bool Prepare() => GameHooks.HideRequirement != null;

        private static MethodBase TargetMethod() => GameHooks.HideRequirement;

        private static void Postfix(Transform __0)
        {
            if (Places.Busy)
            {
                Module.Guard("clearing an ingredient", () => Places.AfterHideRequirement(__0));
            }
        }
    }

    [HarmonyPatch]
    internal static class PieceInfoPatch
    {
        private static bool Prepare() => GameHooks.SetupPieceInfo != null && GameHooks.SetupRequirement != null;

        private static MethodBase TargetMethod() => GameHooks.SetupPieceInfo;

        private static void Postfix(Hud __instance, Piece __0)
        {
            if (Places.Busy)
            {
                Module.Guard("clearing the build panel's station row", () => Places.AfterSetupPieceInfo(__instance, __0));
            }
        }
    }

    [HarmonyPatch]
    internal static class TraderPatch
    {
        private static bool Prepare() => GameHooks.FillStoreList != null;

        private static MethodBase TargetMethod() => GameHooks.FillStoreList;

        // Always noted, also while the marks are off (noting changes nothing), so they show at once when switched on.
        private static void Postfix(StoreGui __instance)
        {
            Module.Guard("marking the trader's list", () => Places.AfterFillStore(__instance));
        }
    }
}
