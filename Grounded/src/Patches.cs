using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// The Harmony patch. Its target was looked up and checked in GameHooks (name, parameter names and types, return type)
// before anything was patched. Its work runs inside Module.Guard, so an error there can't reach the game: the first one
// switches the module off, and the game's own aim stands.
namespace Grounded
{
    /// <summary>
    /// After the game's aiming ray for building (Player.PieceRayTest), while Grounded is on, the key works and a piece it
    /// lays is in hand: aim at the floor behind the rugs instead (Placement.Find). The game then does everything else
    /// from that aim (placing the ghost, snapping, every check, the red tint), and the click places the piece there,
    /// since TryPlacePiece positions the ghost through this same ray first. Off, or with nothing to change, the game's
    /// own aim is left as it is.
    ///
    /// The guarded work is one cached delegate reading its inputs from fields (the game calls this only from the main
    /// thread), and KeyState reads the key without allocating, so building allocates nothing each frame while the key
    /// isn't held.
    /// </summary>
    [HarmonyPatch]
    internal static class PieceRayTestPatch
    {
        private static readonly Action Work = FindAim;
        private static Player player;
        private static bool forWater;
        private static Vector3 gamePoint;
        private static Aim aim;

        private static MethodBase TargetMethod() => GameHooks.PieceRayTest;

        private static void Postfix(Player __instance, bool water, ref bool __result, ref Vector3 point, ref Vector3 normal,
                                    ref Piece piece, ref Heightmap heightmap, ref Collider waterSurface)
        {
            if (!Module.IsOn || !__result || __instance != Player.m_localPlayer)
            {
                return;
            }
            player = __instance;
            forWater = water;
            gamePoint = point;
            aim = null;
            Module.Guard("laying a piece on the floor", Work);
            Aim found = aim;
            player = null;
            aim = null;
            if (found == null)
            {
                return;
            }
            point = found.Point;
            normal = found.Normal;
            piece = found.Piece;
            heightmap = found.Heightmap;
            waterSurface = found.WaterSurface;
        }

        private static void FindAim()
        {
            if (KeyState.Active)
            {
                aim = Placement.Find(player, forWater, gamePoint);
            }
        }
    }
}
