using System;
using System.Reflection;
using HarmonyLib;
using Valheim.UI;

// The Harmony patches. Their targets were looked up and checked in GameHooks before anything was patched. Each checks
// the switch first and changes nothing while the mod is off.
namespace PowerWheel
{
    /// <summary>Just before the game's Player.Update reads the power key: read the keys, take the game's press when the mod
    /// acts on it, and use the power on a tap or open the wheel on a hold (Keys, inside Module.Guard). Only for the local
    /// player; while the module is off it changes nothing and only forgets a press it was waiting on.</summary>
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
            PatchCheck.Ran();
            if (!Module.IsOn)
            {
                Keys.Reset();
                return;
            }
            Keys.BeforePlayerUpdate(__instance);
        }
    }

    /// <summary>
    /// A boss stone's check of its trophy (BossStone.SetActivated, at the stone's start and every second after, in every
    /// game that has the start temple loaded): a stone holding its trophy is remembered (Trophies), for "What counts as
    /// earned" at TrophyAtStone (card B06). A nicety: its first error stops only the noting, with one warning, and a
    /// patch that can't be applied loses only it (Cleanup); the wheel works on, the rule then counting powers taken at
    /// their stones.
    /// </summary>
    [HarmonyPatch]
    internal static class TrophyOnStone
    {
        private static bool lost;

        private static bool Prepare() => GameHooks.BossStoneSetActivated != null;

        private static MethodBase TargetMethod() => GameHooks.BossStoneSetActivated;

        private static void Postfix(BossStone __instance, bool active)
        {
            if (!active || lost || !Module.IsOn)
            {
                return;
            }
            try
            {
                Trophies.SeenOn(__instance);
            }
            catch (Exception error)
            {
                lost = true;
                Module.Log.LogWarning("Trophies on the boss stones are no longer noted this session (\"What counts as earned\" at " +
                                      $"TrophyAtStone counts what was noted, and powers taken at their stones).\n{error}");
            }
        }

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony)
        {
            if (ex == null)
            {
                return null;
            }
            try
            {
                if (original != null && harmony != null)
                {
                    harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
                }
            }
            catch (Exception)
            {
                // Taking the record back can fail the same way the patch did; only the noting is lost either way.
            }
            try
            {
                Module.Log.LogWarning("Trophies on the boss stones can't be noted: the patch on BossStone.SetActivated couldn't be applied " +
                                      $"(\"What counts as earned\" at TrophyAtStone counts powers taken at their stones). The wheel works on.\n{ex}");
            }
            catch (Exception)
            {
                // A log listener's own error mustn't stop the patching of the rest.
            }
            return null;
        }
    }

    /// <summary>
    /// No drop hints under the power wheel: the game shows its "drop" key hints for any page but its main one
    /// (RadialBase.ShowThrowHint, read by KeyHintsRadial), and a power can't be dropped. A nicety: if this patch can't be
    /// applied (another mod's broken patch on the same getter, say), only the hints are lost, never the wheel (its
    /// Cleanup keeps the failure to this class; Bar None's Broken.Patching is the model).
    /// </summary>
    [HarmonyPatch]
    internal static class NoDropHintsOnThePowerWheel
    {
        private static bool Prepare() => GameHooks.ShowThrowHint != null;

        private static MethodBase TargetMethod() => GameHooks.ShowThrowHint;

        private static void Postfix(RadialBase __instance, ref bool __result)
        {
            if (__result && Module.IsOn && Wheel.IsOurs(__instance))
            {
                __result = false;
            }
        }

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony)
        {
            if (ex == null)
            {
                return null;
            }
            try
            {
                if (original != null && harmony != null)
                {
                    harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
                }
            }
            catch (Exception)
            {
                // Taking the record back can fail the same way the patch did; only the hints are lost either way.
            }
            try
            {
                Module.Log.LogWarning("The game's drop hints may show under the power wheel: their patch couldn't be applied " +
                                      $"(another mod's patch on the same method may have failed first). The wheel works on.\n{ex}");
            }
            catch (Exception)
            {
                // A log listener's own error mustn't stop the patching of the rest.
            }
            return null;
        }
    }
}
