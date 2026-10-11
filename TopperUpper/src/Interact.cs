using HarmonyLib;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// Keeps the game's own E from also acting while one of Topper-Upper's keys is used (the research's 4.1; LESSONS_LEARNED,
    /// "a key that looks free may be the game's"): E is the game's Use, and a game button fires whatever else is held. So
    /// while Left Alt + E (its light or station key) is held at a light or station it would feed, that light's or station's
    /// own use (adding fuel, putting a candle out, tapping, harvesting) doesn't happen; and while a station is being
    /// linked, E and Left Shift + E on a chest don't open it, as Reserve Stock's key doesn't open a chest. Each prefix runs
    /// early (before Craft from Chests' fill, which steps aside when an earlier prefix stopped the game's own, and before
    /// MyLittleUI's rename on Left Shift + E at a chest) and changes nothing while the mod is off.
    /// </summary>
    internal static class Interact
    {
        /// <summary>Whether the game's own use of this light or station is skipped now: the mod on, the local player's press,
        /// one it would feed, and its key held.</summary>
        internal static bool Skip(MonoBehaviour component, Humanoid user)
        {
            if (!Module.IsOn || user == null || user != Player.m_localPlayer)
            {
                return false;
            }
            bool skip = false;
            // An error here costs only this press's skip (the game's own E then acts), and switches the module off.
            Module.Guard("keeping the game's E from acting while the key is held", () =>
            {
                Fed fed = Registry.Of(component);
                skip = fed != null && Marks.Toggleable(fed) && Keys.Held(fed.IsLight ? Settings.LightKey.Value : Settings.StationKey.Value);
            });
            return skip;
        }

        /// <summary>Whether a chest's opening is skipped now: while a station is being linked, for the local player.</summary>
        internal static bool SkipChest(Humanoid user) => Module.IsOn && user != null && user == Player.m_localPlayer && Links.Linking != null;
    }

    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    internal static class ChestNotOpenedWhileLinking
    {
        [HarmonyPriority(Priority.First)]
        [HarmonyBefore("shudnal.MyLittleUI")]
        private static bool Prefix(Humanoid __0, ref bool __result)
        {
            if (!Interact.SkipChest(__0))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
    internal static class FireNotUsedWhileKeyHeld
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(Fireplace __instance, Humanoid __0, ref bool __result)
        {
            if (!Interact.Skip(__instance, __0))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnAddOre")]
    internal static class SmelterOreNotUsedWhileKeyHeld
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(Smelter __instance, Humanoid __1, ref bool __result)
        {
            if (!Interact.Skip(__instance, __1))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
    internal static class SmelterFuelNotUsedWhileKeyHeld
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(Smelter __instance, Humanoid __1, ref bool __result)
        {
            if (!Interact.Skip(__instance, __1))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnEmpty")]
    internal static class SmelterEmptyNotUsedWhileKeyHeld
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(Smelter __instance, Humanoid __1, ref bool __result)
        {
            if (!Interact.Skip(__instance, __1))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.Interact))]
    internal static class FermenterNotUsedWhileKeyHeld
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(Fermenter __instance, Humanoid __0, ref bool __result)
        {
            if (!Interact.Skip(__instance, __0))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ShieldGenerator), "OnAddFuel")]
    internal static class ShieldNotUsedWhileKeyHeld
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(ShieldGenerator __instance, Humanoid __1, ref bool __result)
        {
            if (!Interact.Skip(__instance, __1))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnAddFuelSwitch")]
    internal static class OvenFuelNotUsedWhileKeyHeld
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(CookingStation __instance, Humanoid __1, ref bool __result)
        {
            if (!Interact.Skip(__instance, __1))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnInteract")]
    internal static class OvenFoodNotUsedWhileKeyHeld
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(CookingStation __instance, Humanoid __0, ref bool __result)
        {
            if (!Interact.Skip(__instance, __0))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Turret), nameof(Turret.Interact))]
    internal static class BallistaNotUsedWhileKeyHeld
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(Turret __instance, Humanoid __0, ref bool __result)
        {
            if (!Interact.Skip(__instance, __0))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Beehive), nameof(Beehive.Interact))]
    internal static class BeehiveNotUsedWhileKeyHeld
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(Beehive __instance, Humanoid __0, ref bool __result)
        {
            if (!Interact.Skip(__instance, __0))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(SapCollector), nameof(SapCollector.Interact))]
    internal static class SapNotUsedWhileKeyHeld
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(SapCollector __instance, Humanoid __0, ref bool __result)
        {
            if (!Interact.Skip(__instance, __0))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
