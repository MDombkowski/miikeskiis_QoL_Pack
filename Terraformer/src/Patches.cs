using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// The Harmony patches. Every target was looked up and checked in GameHooks before anything was patched. Each body that
// does real work runs inside Module.Guard, so an error there switches Terraformer off instead of reaching the game.
// The ones that serve strokes reaching his game (the registry, reading a stroke's numbers, lending them, applying a
// square) work whether Terraformer is switched on or off, as a stroke already on its way must still apply; the rest do
// nothing while it's off.
namespace Terraformer
{
    /// <summary>Right after the game's own placement input: Terraformer's keys and wheel (Controls).</summary>
    [HarmonyPatch]
    internal static class UpdatePlacementPatch
    {
        private static MethodBase TargetMethod() => GameHooks.UpdatePlacement;

        private static void Prefix(Player __instance, out int __state)
        {
            __state = GameHooks.PlaceRotation(__instance);
        }

        private static void Postfix(Player __instance, bool takeInput, int __state)
        {
            if (!Module.IsOn || __instance != Player.m_localPlayer)
            {
                return;
            }
            Module.Guard("reading the terrain tools' keys", () => Controls.Frame(__instance, takeInput, __state));
        }
    }

    /// <summary>After the game has placed the ghost: a square's snap and turn, the whole-stroke ward check, the marks.</summary>
    [HarmonyPatch]
    internal static class UpdatePlacementGhostPatch
    {
        private static MethodBase TargetMethod() => GameHooks.UpdatePlacementGhost;

        private static void Postfix(Player __instance, bool flashGuardStone)
        {
            if (__instance != Player.m_localPlayer)
            {
                return;
            }
            if (!Module.IsOn)
            {
                Marks.Hide();
                Module.Guard("putting a terrain tool's marker back", () => Ghost.Unscale(__instance));
                return;
            }
            Module.Guard("placing a terrain tool's ghost", () => Ghost.After(__instance, flashGuardStone));
        }
    }

    /// <summary>His click: his own stroke, for exactly the click's length. A click another mod already refused is left
    /// alone.</summary>
    [HarmonyPatch]
    internal static class TryPlacePiecePatch
    {
        private static MethodBase TargetMethod() => GameHooks.TryPlacePiece;

        private static bool Prefix(Player __instance, Piece piece, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal || !Module.IsOn)
            {
                return true;
            }
            bool place = true;
            Module.Guard("starting a terrain stroke", () => place = Strokes.Begin(__instance, piece));
            if (!place)
            {
                __result = false;
            }
            return place;
        }

        private static Exception Finalizer(Exception __exception)
        {
            Strokes.End();
            return __exception;
        }
    }

    /// <summary>While a Terraformer key is held with a terrain tool, the camera doesn't zoom with the wheel: the game's
    /// camera zooms only while the piece in hand can't turn, and this is the one place it asks.</summary>
    [HarmonyPatch]
    internal static class CanRotatePiecePatch
    {
        private static MethodBase TargetMethod() => GameHooks.CanRotatePiece;

        private static void Postfix(Player __instance, ref bool __result)
        {
            if (__result || !Module.IsOn)
            {
                return;
            }
            bool adjusting = false;
            Module.Guard("keeping the camera still", () => adjusting = Controls.Adjusting(__instance));
            if (adjusting)
            {
                __result = true;
            }
        }
    }

    /// <summary>
    /// While a Terraformer key is held with a terrain tool, the camera never zooms with the wheel (his first test,
    /// 2026-09-30). The game zooms not only while the piece in hand can't turn (the postfix above) but also whenever the
    /// aim reaches nothing within building range, whatever the piece (GameCamera.UpdateCamera): the "sometimes" he saw,
    /// aiming a big tool far off. So the camera's distance, as it was before its own update, is put back:
    /// - **before the camera is placed from it** (GameCamera.GetCameraPosition, which the update calls after zooming), so
    ///   not even one frame is drawn zoomed (review round 1);
    /// - **after the update**, for the camera's other paths (seated, riding), which don't place it from the distance.
    /// </summary>
    internal static class CameraHold
    {
        internal static bool Held;
        internal static float Distance;
    }

    [HarmonyPatch]
    internal static class UpdateCameraPatch
    {
        private static MethodBase TargetMethod() => GameHooks.UpdateCamera;

        private static void Prefix(GameCamera __instance)
        {
            CameraHold.Held = false;
            Player player = Player.m_localPlayer;
            if (!Module.IsOn || player == null)
            {
                return;
            }
            bool adjusting = false;
            Module.Guard("keeping the camera still", () => adjusting = Controls.Adjusting(player));
            CameraHold.Held = adjusting;
            CameraHold.Distance = GameHooks.CameraDistance(__instance);
        }

        private static void Postfix(GameCamera __instance)
        {
            if (CameraHold.Held)
            {
                GameHooks.CameraDistance(__instance) = CameraHold.Distance;
                CameraHold.Held = false;
            }
        }
    }

    [HarmonyPatch]
    internal static class GetCameraPositionPatch
    {
        private static MethodBase TargetMethod() => GameHooks.GetCameraPosition;

        private static void Prefix(GameCamera __instance)
        {
            if (CameraHold.Held)
            {
                GameHooks.CameraDistance(__instance) = CameraHold.Distance;
            }
        }
    }

    /// <summary>After the game (re)built its list of terrain strokes: Terraformer's tools made once, then registered.</summary>
    [HarmonyPatch]
    internal static class UpdateRegistersPatch
    {
        private static MethodBase TargetMethod() => GameHooks.UpdateRegisters;

        private static void Postfix(ObjectDB __instance)
        {
            Module.Guard("registering the terrain tools", () => Pieces.AfterRegistry(__instance));
        }
    }

    /// <summary>A placed stroke's own copy, before it picks its zones: widened to reach every zone it changes.</summary>
    [HarmonyPatch]
    internal static class TerrainOpAwakePatch
    {
        private static MethodBase TargetMethod() => GameHooks.TerrainOpAwake;

        private static void Prefix(TerrainOp __instance)
        {
            if (Strokes.Current == null)
            {
                return;
            }
            Module.Guard("widening a terrain stroke", () => Strokes.Widen(__instance));
        }
    }

    /// <summary>His stroke's numbers, packed after the game's own data.</summary>
    [HarmonyPatch]
    internal static class SerializePatch
    {
        private static MethodBase TargetMethod() => GameHooks.Serialize;

        private static void Postfix(ZPackage pkg, GameObject prefab)
        {
            if (Strokes.Current == null)
            {
                return;
            }
            Module.Guard("packing a terrain stroke", () => Strokes.Write(pkg, prefab));
        }
    }

    /// <summary>A stroke's numbers, read back where it's applied, if it carries Terraformer's.</summary>
    [HarmonyPatch]
    internal static class DeserializePatch
    {
        private static MethodBase TargetMethod() => GameHooks.Deserialize;

        private static void Postfix(ZPackage pkg, TerrainOp.Settings __result)
        {
            Module.Guard("unpacking a terrain stroke", () => Strokes.Read(pkg, __result));
        }
    }

    /// <summary>Before his stroke is sent to a zone: the zone's ground record is claimed for his game.</summary>
    [HarmonyPatch]
    internal static class ApplyOperationPatch
    {
        private static MethodBase TargetMethod() => GameHooks.ApplyOperation;

        private static void Prefix(TerrainComp __instance, TerrainOp modifier)
        {
            if (Strokes.Current == null)
            {
                return;
            }
            Module.Guard("claiming the ground for a terrain stroke", () => Strokes.Claim(__instance, modifier));
        }
    }

    /// <summary>After a stroke has been applied or dropped: its numbers are forgotten.</summary>
    [HarmonyPatch]
    internal static class RpcApplyOperationPatch
    {
        private static MethodBase TargetMethod() => GameHooks.RpcApplyOperation;

        private static Exception Finalizer(Exception __exception)
        {
            Strokes.Done();
            return __exception;
        }
    }

    /// <summary>A round stroke with Terraformer's numbers: they're lent to its tool's settings for exactly this one
    /// application, and always put back.</summary>
    [HarmonyPatch]
    internal static class DoOperationPatch
    {
        private static MethodBase TargetMethod() => GameHooks.DoOperation;

        private static void Prefix(TerrainComp __instance, TerrainOp.Settings modifier, out uint __state)
        {
            __state = Revision(__instance);
            if (Strokes.Arriving == null)
            {
                return;
            }
            Module.Guard("lending a terrain stroke its size", () => Strokes.Lend(modifier));
        }

        /// <summary>His own stroke, once the game has saved the zone: what was saved, for undo (Undo.Saved). Whether the
        /// save really wrote is read from the zone record's revision: a paint-only save whose checksum didn't move is
        /// skipped, and then nothing was saved (the last look).</summary>
        private static void Postfix(TerrainComp __instance, TerrainOp.Settings modifier, uint __state)
        {
            if (Strokes.IsCurrent(modifier))
            {
                bool wrote = Revision(__instance) != __state;
                Module.Guard("noting what the game saved of a stroke, for undo", () => Undo.Saved(__instance, wrote));
            }
        }

        private static uint Revision(TerrainComp compiler)
        {
            ZNetView view = compiler != null ? compiler.GetComponent<ZNetView>() : null;
            ZDO zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            return zdo != null ? zdo.DataRevision : 0u;
        }

        private static Exception Finalizer(Exception __exception)
        {
            Strokes.Restore();
            return __exception;
        }
    }

    /// <summary>
    /// Where a stroke is worked out in a zone, just before the game saves the zone:
    /// - **His own stroke, while his click places it,** has the zones' points round it noted before it's worked out, for
    ///   undo; they're compared once the click is over (Undo).
    /// - **A square stroke, or Flatten,** is applied by Terraformer's own code in place of the game's steps. Which strokes
    ///   are its own work is decided before any work, so the game's steps never run on one, even after an error part way.
    /// - **A paint-only stroke Terraformer watches** (Strokes.WatchesSave) has the zone's paint noted before and compared
    ///   after (the postfix runs whether or not the game's steps did), so a repaint the game's checksum can't see is
    ///   still saved.
    /// </summary>
    [HarmonyPatch]
    internal static class InternalDoOperationPatch
    {
        private static MethodBase TargetMethod() => GameHooks.InternalDoOperation;

        private static bool Prefix(TerrainComp __instance, Vector3 pos, Vector3 rot, TerrainOp.Settings modifier, out ulong __state)
        {
            ulong before = 0;
            Module.Guard("noting a zone's paint before a stroke", () => before = Strokes.PaintBefore(__instance, modifier, pos));
            __state = before;
            if (Strokes.IsCurrent(modifier))
            {
                Module.Guard("noting the ground round a stroke, for undo", () => Strokes.Working(__instance, pos, rot));
            }
            if (!Strokes.IsOwnWork(modifier))
            {
                return true;
            }
            Module.Guard("applying a terrain stroke of Terraformer's own", () => Strokes.ApplyOwn(__instance, pos, rot, modifier));
            return false;
        }

        private static void Postfix(TerrainComp __instance, ulong __state)
        {
            if (__state == 0)
            {
                return;
            }
            Module.Guard("making sure a repainted zone is saved", () => Strokes.SaveIfRepainted(__instance, __state));
        }
    }
}
