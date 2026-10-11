using System;
using System.Reflection;
using HarmonyLib;

// Loadout Stands' patches (job 30), applied by the kit with the rest of Outfitter. Each checks the stands' switch first and
// changes nothing while they're off (MODULES.md rule 5). Each class has a Cleanup (Bar None's way, MODULES.md rule 7): if
// its patch can't be applied (a game update renamed the method, or another mod's broken patch on it), only the stands are
// lost for the session (or, for the hover text, only its lines), never the rest of Outfitter.
namespace Outfitter.Stands
{
    /// <summary>The stand's lines in its hover text, after every other mod's (MyLittleUI rewrites the stand's own). An
    /// error here costs only the hover lines, for the session (Stands.HoverFailed), never the rest of Outfitter (review
    /// round 1, m6).</summary>
    [HarmonyPatch(typeof(Switch), nameof(Switch.GetHoverText))]
    internal static class StandHoverText
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Switch __instance, ref string __result)
        {
            if (!Stands.Active || Stands.HoverLost || __instance == null || __result == null)
            {
                return;
            }
            try
            {
                // Every other switch (a smelter, a kiln, a map table) costs one look and nothing more; the slot's own text
                // is localized only for a stand with a name of its own (review round 1 of Session 2, n2).
                IStand stand = Stands.StandOf(__instance);
                if (stand != null)
                {
                    __result = StandHover.Add(stand, __result, slot: __instance);
                }
            }
            catch (Exception error)
            {
                Stands.HoverFailed(error);
            }
        }

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.Patching("the armour stand's hover text (Switch.GetHoverText)", hoverOnly: true, original, ex, harmony);
    }

    /// <summary>
    /// While one of the stands' keys is held (Left Alt + E, Left Alt + Left Shift + E), the game's own E on an armour stand
    /// doesn't also take everything off it: that press is the stands' (StandInput reads it), as Reserve Stock's key is on
    /// chests. Before other mods' prefixes, which step aside when an earlier one has stopped the game's own.
    /// </summary>
    [HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
    internal static class StandKeysStopTheGamesE
    {
        private static bool warned;

        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Switch __instance, Humanoid character, ref bool __result)
        {
            try
            {
                if (!Stands.Active || character == null || character != Player.m_localPlayer || Stands.StandOf(__instance) == null ||
                    !StandInput.AnyHeld())
                {
                    return true;
                }
                __result = false;
                return false;
            }
            catch (Exception error)
            {
                if (!warned)
                {
                    warned = true;
                    Module.Warn($"Pressing E at an armour stand couldn't be looked at (said once): {error.Message}");
                }
                return true;
            }
        }

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.Patching("pressing E at an armour stand (Switch.Interact)", hoverOnly: false, original, ex, harmony);
    }

    /// <summary>
    /// Another computer's request for a stand this computer holds (StandClaims) is dropped, as the game drops one that
    /// reaches a computer no longer owning it: a request to take it over (RPC_RequestOwn), to take everything off it
    /// (RPC_DropItemByName), to drop one place (RPC_DropItem) or to destroy one place's item (RPC_DestroyAttachment). Every
    /// other request goes to the game as usual. Positional arguments, so a renamed parameter can't stop the patch applying.
    /// </summary>
    [HarmonyPatch(typeof(ArmorStand), "RPC_RequestOwn")]
    internal static class StandHoldsOwnership
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ArmorStand __instance, long __0) => !StandClaims.Drop(__instance, __0);

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.Patching("holding an armour stand (ArmorStand.RPC_RequestOwn)", hoverOnly: false, original, ex, harmony);
    }

    [HarmonyPatch(typeof(ArmorStand), "RPC_DropItemByName")]
    internal static class StandHoldsAgainstTakeAll
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ArmorStand __instance, long __0) => !StandClaims.Drop(__instance, __0);

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.Patching("holding an armour stand (ArmorStand.RPC_DropItemByName)", hoverOnly: false, original, ex, harmony);
    }

    [HarmonyPatch(typeof(ArmorStand), "RPC_DropItem")]
    internal static class StandHoldsAgainstDrop
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ArmorStand __instance, long __0) => !StandClaims.Drop(__instance, __0);

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.Patching("holding an armour stand (ArmorStand.RPC_DropItem)", hoverOnly: false, original, ex, harmony);
    }

    [HarmonyPatch(typeof(ArmorStand), "RPC_DestroyAttachment")]
    internal static class StandHoldsAgainstDestroy
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ArmorStand __instance, long __0) => !StandClaims.Drop(__instance, __0);

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.Patching("holding an armour stand (ArmorStand.RPC_DestroyAttachment)", hoverOnly: false, original, ex, harmony);
    }

    // ---- A stand shows what its loadout lacks (job 30, Session 3: GameShow), drawn in this game only. Each class's
    // Cleanup loses only this drawing (Stands.ShowLost), never the rest of the stands or Outfitter. Positional arguments,
    // so a renamed parameter can't stop a patch applying. ----

    /// <summary>Before an armour stand's figure draws what its record says: what to draw besides it this frame.</summary>
    [HarmonyPatch(typeof(VisEquipment), "UpdateEquipmentVisuals")]
    internal static class StandShowFrame
    {
        private static void Prefix(VisEquipment __instance) => GameShow.Begin(__instance);

        private static void Postfix() => GameShow.End();

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.PatchingShow("an armour stand's figure (VisEquipment.UpdateEquipmentVisuals)", original, ex, harmony);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetChestEquipped")]
    internal static class StandShowChest
    {
        private static void Prefix(ref int __0) => GameShow.Body(VisSlot.Chest, ref __0);

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.PatchingShow("an armour stand's chest piece (VisEquipment.SetChestEquipped)", original, ex, harmony);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetLegEquipped")]
    internal static class StandShowLegs
    {
        private static void Prefix(ref int __0) => GameShow.Body(VisSlot.Legs, ref __0);

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.PatchingShow("an armour stand's legs (VisEquipment.SetLegEquipped)", original, ex, harmony);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetHelmetEquipped")]
    internal static class StandShowHelmet
    {
        private static void Prefix(ref int __0) => GameShow.Body(VisSlot.Helmet, ref __0);

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.PatchingShow("an armour stand's helmet (VisEquipment.SetHelmetEquipped)", original, ex, harmony);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetUtilityEquipped")]
    internal static class StandShowBelt
    {
        private static void Prefix(ref int __0) => GameShow.Body(VisSlot.Utility, ref __0);

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.PatchingShow("an armour stand's belt (VisEquipment.SetUtilityEquipped)", original, ex, harmony);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetShoulderEquipped")]
    internal static class StandShowCape
    {
        private static void Prefix(ref int __0, ref int __1, ref int __2) => GameShow.Styled(VisSlot.Shoulder, ref __0, ref __1, ref __2);

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.PatchingShow("an armour stand's cape (VisEquipment.SetShoulderEquipped)", original, ex, harmony);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetBackEquipped")]
    internal static class StandShowBack
    {
        private static void Prefix(ref int __0, ref int __1, ref int __2, ref int __3, ref int __4) =>
            GameShow.Back(ref __0, ref __1, ref __2, ref __3, ref __4);

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.PatchingShow("an armour stand's shield and weapon (VisEquipment.SetBackEquipped)", original, ex, harmony);
    }

    /// <summary>After the game sets a stand's supports by what it holds: those a shown piece needs too.</summary>
    [HarmonyPatch(typeof(ArmorStand), "UpdateSupports")]
    internal static class StandShowSupports
    {
        private static void Postfix(ArmorStand __instance) => GameShow.Supports(__instance);

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) =>
            Stands.PatchingShow("an armour stand's supports (ArmorStand.UpdateSupports)", original, ex, harmony);
    }
}
