using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Grounded
{
    /// <summary>
    /// The game method Grounded hooks, and the private members it uses, looked up once at startup and checked by name
    /// and type against Valheim 1.0.17 (decompiled fresh, 2026-10-09). A missing one that it can't do without leaves the
    /// module off with its name in the log, before anything is patched, and the game places as it always has.
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>The layers the game's aiming ray looks at (Player.Awake), for when its own fields can't be read.</summary>
        private static readonly string[] PlaceLayers = { "Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "vehicle" };

        private static readonly string[] PlaceWaterLayers = { "Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "Water", "vehicle" };

        private static AccessTools.FieldRef<Player, GameObject> placementGhost;
        private static AccessTools.FieldRef<Player, int> placeRayMask;
        private static AccessTools.FieldRef<Player, int> placeWaterRayMask;
        private static AccessTools.FieldRef<Player, int> placeRotation;
        private static AccessTools.FieldRef<Player, float> placeRotationDegrees;
        private static int ownPlaceMask = -1;
        private static int ownPlaceWaterMask = -1;
        private static int waterLayer = int.MinValue;

        /// <summary>Player.PieceRayTest(out point, out normal, out piece, out heightmap, out waterSurface, water): the
        /// game's aiming ray for building, called only by UpdatePlacementGhost, which positions the ghost every frame and
        /// again for the click (TryPlacePiece).</summary>
        internal static MethodInfo PieceRayTest { get; private set; }

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            PieceRayTest = typeof(Player).GetMethod("PieceRayTest", AnyInstance, null,
                new[]
                {
                    typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType(), typeof(Piece).MakeByRefType(),
                    typeof(Heightmap).MakeByRefType(), typeof(Collider).MakeByRefType(), typeof(bool),
                }, null);
            // The patch names the game's parameters, so their names must be the ones it was written for.
            string[] names = { "point", "normal", "piece", "heightmap", "waterSurface", "water" };
            ParameterInfo[] parameters = PieceRayTest?.GetParameters();
            bool namesMatch = parameters != null && parameters.Length == names.Length;
            for (int i = 0; namesMatch && i < names.Length; i++)
            {
                namesMatch = parameters[i].Name == names[i];
            }
            if (PieceRayTest == null || PieceRayTest.ReturnType != typeof(bool) || !namesMatch)
            {
                PieceRayTest = null;
                missing.Add("Player.PieceRayTest");
            }

            placementGhost = Field<GameObject>("m_placementGhost", missing);

            const string ownMasks = "the aiming ray's layers are then the ones the game used in 1.0.17";
            placeRayMask = Field<int>("m_placeRayMask", missingOptional, ownMasks);
            placeWaterRayMask = Field<int>("m_placeWaterRayMask", missingOptional, ownMasks);

            const string ghostTurn = "the rug's turn is then read from the ghost, a frame late as the wheel turns it";
            placeRotation = Field<int>("m_placeRotation", missingOptional, ghostTurn);
            placeRotationDegrees = Field<float>("m_placeRotationDegrees", missingOptional, ghostTurn);
        }

        /// <summary>The ghost of the piece in hand, or null.</summary>
        internal static GameObject PlacementGhost(Player player) => placementGhost(player);

        /// <summary>The layers the game's aiming ray looks at this time: the water variant for a piece that floats or must
        /// stay out of water, as PieceRayTest chooses.</summary>
        internal static int RayMask(Player player, bool water)
        {
            AccessTools.FieldRef<Player, int> field = water ? placeWaterRayMask : placeRayMask;
            if (field != null)
            {
                return field(player);
            }
            if (ownPlaceMask == -1)
            {
                ownPlaceMask = LayerMask.GetMask(PlaceLayers);
                ownPlaceWaterMask = LayerMask.GetMask(PlaceWaterLayers);
            }
            return water ? ownPlaceWaterMask : ownPlaceMask;
        }

        /// <summary>The Water layer, where the ray meets a water surface.</summary>
        internal static int WaterLayer
        {
            get
            {
                if (waterLayer == int.MinValue)
                {
                    waterLayer = LayerMask.NameToLayer("Water");
                }
                return waterLayer;
            }
        }

        /// <summary>The turn the game gives the ghost this frame, in degrees about the vertical: 22.5 times the wheel's
        /// count (Player.UpdatePlacementGhost); else the ghost's own turn, which is last frame's.</summary>
        internal static float PlaceYaw(Player player, GameObject ghost)
        {
            if (placeRotation != null && placeRotationDegrees != null)
            {
                return placeRotationDegrees(player) * placeRotation(player);
            }
            return ghost.transform.eulerAngles.y;
        }

        private static AccessTools.FieldRef<Player, T> Field<T>(string name, List<string> missing, string without = null)
        {
            FieldInfo field = typeof(Player).GetField(name, AnyInstance);
            if (field == null || field.FieldType != typeof(T))
            {
                missing.Add(without == null ? $"Player.{name}" : $"Player.{name} ({without})");
                return null;
            }
            return AccessTools.FieldRefAccess<Player, T>(field);
        }
    }
}
