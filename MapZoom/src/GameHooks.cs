using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MapZoom
{
    /// <summary>
    /// The game methods this mod hooks, and the private members it uses, looked up once at startup.
    /// Checked against the game's Minimap (UpdateMap, ScreenToWorldPoint, m_mapOffset) and ZInput (GetMouseScrollWheel).
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo mapOffset;
        private static Func<Minimap, Vector3, Vector3> screenToWorldPoint;

        /// <summary>Minimap.UpdateMap(Player, float, bool): where the game reads the wheel and zooms the big map.</summary>
        internal static MethodInfo UpdateMap { get; private set; }

        /// <summary>ZInput.GetMouseScrollWheel(): the wheel as the game and other mods read it.</summary>
        internal static MethodInfo ScrollWheel { get; private set; }

        /// <summary>Whether the two members zooming toward the pointer needs were found. Without them the wheel zooms
        /// around the middle of the view, at this mod's speed.</summary>
        internal static bool CanZoomTowardPointer => mapOffset != null && screenToWorldPoint != null;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            UpdateMap = typeof(Minimap).GetMethod("UpdateMap", AnyInstance, null, new[] { typeof(Player), typeof(float), typeof(bool) }, null);
            if (UpdateMap == null)
            {
                missing.Add("Minimap.UpdateMap");
            }

            ScrollWheel = typeof(ZInput).GetMethod(nameof(ZInput.GetMouseScrollWheel), BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (ScrollWheel == null || ScrollWheel.ReturnType != typeof(float))
            {
                ScrollWheel = null;
                missing.Add("ZInput.GetMouseScrollWheel");
            }

            const string withoutPointer = "the wheel then zooms around the middle of the view";
            mapOffset = typeof(Minimap).GetField("m_mapOffset", AnyInstance);
            if (mapOffset == null || mapOffset.FieldType != typeof(Vector3))
            {
                mapOffset = null;
                missingOptional.Add($"Minimap.m_mapOffset ({withoutPointer})");
            }

            MethodInfo toWorld = typeof(Minimap).GetMethod("ScreenToWorldPoint", AnyInstance, null, new[] { typeof(Vector3) }, null);
            if (toWorld == null || toWorld.ReturnType != typeof(Vector3))
            {
                missingOptional.Add($"Minimap.ScreenToWorldPoint ({withoutPointer})");
            }
            else
            {
                screenToWorldPoint = (Func<Minimap, Vector3, Vector3>)Delegate.CreateDelegate(typeof(Func<Minimap, Vector3, Vector3>), toWorld);
            }
        }

        /// <summary>The world point shown at a screen position on the big map, in the view the map shows right now.
        /// The game returns (0, 0, 0) when the position can't be placed on the map.</summary>
        internal static Vector3 ScreenToWorldPoint(Minimap map, Vector3 screenPoint) => screenToWorldPoint(map, screenPoint);

        /// <summary>Where the big map's middle is, measured from the player. The game centres the map on the player's
        /// position plus this, every frame, later in UpdateMap.</summary>
        internal static Vector3 GetMapOffset(Minimap map) => (Vector3)mapOffset.GetValue(map);

        internal static void SetMapOffset(Minimap map, Vector3 offset) => mapOffset.SetValue(map, offset);
    }
}
