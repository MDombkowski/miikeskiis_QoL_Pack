using System;
using HarmonyLib;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>
    /// Placing a Hoarstone places the game's own Ward (Player.PlacePiece is given the Ward instead of the menu entry; the
    /// cost was already checked and is taken from the entry, the Hoarstone's), and marks it right after the game names
    /// its placer (PrivateArea.Setup). This swap and mark run whether or not the mod is switched on: the entry must never
    /// place an object of its own kind, which a host without the mod would delete.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    internal static class PlacePatch
    {
        /// <summary>True between a Hoarstone entry's placing and the end of that call.</summary>
        internal static bool Placing;

        private static void Prefix(ref Piece piece)
        {
            if (piece != null && Stone.Entry != null && piece.gameObject == Stone.Entry && Stone.WardPiece != null)
            {
                piece = Stone.WardPiece;
                Placing = true;
            }
        }

        private static Exception Finalizer(Exception __exception)
        {
            Placing = false;
            return __exception;
        }
    }

    /// <summary>The new Ward's marks: a Hoarstone, switched off, and noted at once.</summary>
    [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Setup))]
    internal static class MarkPatch
    {
        private static void Postfix(PrivateArea __instance)
        {
            if (!PlacePatch.Placing)
            {
                return;
            }
            try
            {
                ZNetView view = GameHooks.WardView(__instance);
                if (view == null || !view.IsValid())
                {
                    return;
                }
                ZDO zdo = view.GetZDO();
                zdo.Set(Records.MarkHash, true);
                zdo.Set(ZDOVars.s_enabled, false);
                Piece entry = Stone.Entry != null ? Stone.Entry.GetComponent<Piece>() : null;
                if (entry != null)
                {
                    zdo.Set(Records.PaidHash, Stone.TextOf(entry.m_resources));   // what was just paid: given back as it was
                }
                Records.Note(zdo);
            }
            catch (Exception error)
            {
                Module.Fail("marking a Hoarstone", error);
            }
        }
    }

    /// <summary>A Hoarstone's hover text, in games with this mod: its own (no Ward's circle), unless it acts as a Ward
    /// now, when the Ward's text shows under the Hoarstone's name. Pointing at it shows its settlement's outline, with
    /// "Pointing at it shows its settlement" on.</summary>
    [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.GetHoverText))]
    internal static class HoverPatch
    {
        private static bool Prefix(PrivateArea __instance, ref string __result, out bool __state)
        {
            __state = false;
            if (!Module.IsOn)
            {
                return true;
            }
            try
            {
                if (!Stone.IsMarked(__instance))
                {
                    return true;
                }
                __state = true;
                if (Settings.PointingShows.Value)
                {
                    Outlines.Point(__instance.transform.position);
                }
                if (Stone.ActsAsWard(__instance))
                {
                    return true;
                }
                __result = Stone.HoverText(__instance);
                return false;
            }
            catch (Exception error)
            {
                Module.Fail("showing a Hoarstone's text", error);
                return true;
            }
        }

        private static void Postfix(bool __state, ref string __result)
        {
            if (!__state || string.IsNullOrEmpty(__result) || Localization.instance == null)
            {
                return;
            }
            string ward = Localization.instance.Localize("$piece_guardstone");
            if (__result.StartsWith(ward, StringComparison.Ordinal) && !__result.StartsWith("Hoarstone", StringComparison.Ordinal))
            {
                __result = "Hoarstone" + __result.Substring(ward.Length);
            }
        }
    }

    /// <summary>A Hoarstone's name where the game asks for it (the crosshair's name, messages).</summary>
    [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.GetHoverName))]
    internal static class HoverNamePatch
    {
        private static void Postfix(PrivateArea __instance, ref string __result)
        {
            if (!Module.IsOn)
            {
                return;
            }
            try
            {
                if (Stone.IsMarked(__instance))
                {
                    __result = "Hoarstone";
                }
            }
            catch (Exception error)
            {
                Module.Fail("naming a Hoarstone", error);
            }
        }
    }

    /// <summary>E on a Hoarstone that is no ward shows or hides its settlement's outline; while it acts as a Ward, E is
    /// the Ward's own.</summary>
    [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Interact))]
    internal static class InteractPatch
    {
        private static bool Prefix(PrivateArea __instance, bool hold, ref bool __result)
        {
            if (!Module.IsOn)
            {
                return true;
            }
            try
            {
                if (!Stone.IsMarked(__instance) || Stone.ActsAsWard(__instance))
                {
                    return true;
                }
                __result = !hold && Outlines.ToggleAt(__instance);
                return false;
            }
            catch (Exception error)
            {
                Module.Fail("using a Hoarstone", error);
                return true;
            }
        }
    }

    /// <summary>
    /// Taking a Hoarstone down gives back what it cost when it was placed (written on its record by the mark; review
    /// round 1: not whatever the cost setting says by then). The drop is made by the game that owns the record (the
    /// game's RPC_Remove acts only there), so this game makes itself the owner just before it asks (WearNTear.Remove),
    /// and its own drop then swaps the Ward's materials for the Hoarstone's (Piece.DropResources). Where a game without
    /// this mod owns it (a friend taking it down while their game looks after it, or damage there), a Ward's materials
    /// drop, as A88 says. Both run while the mod is installed, switched on or not (and after an error: the module keeps
    /// its patches), so a Hoarstone's materials are never lost to the switch.
    /// </summary>
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Remove))]
    internal static class RemovePatch
    {
        private static void Prefix(WearNTear __instance)
        {
            try
            {
                ZNetView view = __instance.GetComponent<ZNetView>();
                if (Stone.IsMarked(view) && !view.IsOwner())
                {
                    view.ClaimOwnership();
                }
            }
            catch (Exception error)
            {
                Module.Fail("taking down a Hoarstone", error);
            }
        }
    }

    [HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
    internal static class DropPatch
    {
        private static void Prefix(Piece __instance, out Piece.Requirement[] __state)
        {
            __state = null;
            try
            {
                ZNetView view = __instance.GetComponent<ZNetView>();
                if (Stone.IsMarked(view))
                {
                    Piece.Requirement[] cost = Stone.PaidFor(view.GetZDO());
                    if (cost.Length > 0)
                    {
                        __state = __instance.m_resources;
                        __instance.m_resources = cost;
                    }
                }
            }
            catch (Exception error)
            {
                Module.Fail("dropping a Hoarstone's materials", error);
            }
        }

        private static Exception Finalizer(Piece __instance, Piece.Requirement[] __state, Exception __exception)
        {
            if (__state != null)
            {
                __instance.m_resources = __state;
            }
            return __exception;
        }
    }

    /// <summary>A piece that loads is noted at once, so a piece just built or just reached counts without waiting for the
    /// next pass over the records.</summary>
    [HarmonyPatch(typeof(Piece), "Awake")]
    internal static class PieceAwakePatch
    {
        private static void Postfix(Piece __instance)
        {
            if (!Module.IsOn)
            {
                return;
            }
            try
            {
                ZNetView view = __instance.GetComponent<ZNetView>();
                if (view != null && view.IsValid())
                {
                    Records.Note(view.GetZDO());
                }
            }
            catch (Exception error)
            {
                Module.Fail("noting a piece", error);
            }
        }
    }
}
