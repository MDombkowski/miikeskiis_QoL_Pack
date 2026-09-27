using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace XPortalMapPicker
{
    /// <summary>
    /// The game methods this mod hooks, and the private ones it calls, looked up once at startup.
    /// Checked against the game's current Minimap and Game classes.
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private delegate void WorldToMapPointMethod(Minimap map, Vector3 point, out float mapX, out float mapY);

        private static WorldToMapPointMethod worldToMapPoint;
        private static Func<Minimap, float, float, RawImage, Vector2> mapPointToLocalGuiPos;
        private static Func<Minimap, Vector3, bool> isExplored;
        private static FieldInfo playerPins;

        /// <summary>Minimap.OnMapLeftClick: a short left click on the big map (the game ticks the nearest pin).</summary>
        internal static MethodInfo MapLeftClick { get; private set; }

        /// <summary>Minimap.RemovePinUnderPointer: what a right click on the big map runs (the game deletes the
        /// nearest pin). The game wires it to the map in Minimap.Start; it has no OnMapRightClick method.</summary>
        internal static MethodInfo MapRightClick { get; private set; }

        /// <summary>Minimap.OnMapDblClick: a double click (the game opens the box for naming a new pin).</summary>
        internal static MethodInfo MapDoubleClick { get; private set; }

        /// <summary>Game.Shutdown: runs on logout and on quitting, before the character and its map are saved. Optional.</summary>
        internal static MethodInfo WorldShutdown { get; private set; }

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            MapLeftClick = FindMethod(nameof(Minimap.OnMapLeftClick), Type.EmptyTypes, missing);
            MapRightClick = FindMethod("RemovePinUnderPointer", Type.EmptyTypes, missing);
            MapDoubleClick = FindMethod(nameof(Minimap.OnMapDblClick), Type.EmptyTypes, missing);

            worldToMapPoint = FindDelegate<WorldToMapPointMethod>(
                "WorldToMapPoint", new[] { typeof(Vector3), typeof(float).MakeByRefType(), typeof(float).MakeByRefType() }, missing);
            mapPointToLocalGuiPos = FindDelegate<Func<Minimap, float, float, RawImage, Vector2>>(
                "MapPointToLocalGuiPos", new[] { typeof(float), typeof(float), typeof(RawImage) }, missing);
            isExplored = FindDelegate<Func<Minimap, Vector3, bool>>("IsExplored", new[] { typeof(Vector3) }, missing);

            WorldShutdown = typeof(Game).GetMethod("Shutdown", AnyInstance, null, new[] { typeof(bool) }, null);
            if (WorldShutdown == null)
            {
                missingOptional.Add("Game.Shutdown (picking then isn't ended tidily at logout; nothing of it is saved either way)");
            }

            playerPins = typeof(Minimap).GetField("m_playerPins", AnyInstance);
            if (playerPins == null || playerPins.FieldType != typeof(List<Minimap.PinData>))
            {
                playerPins = null;
                missingOptional.Add("Minimap.m_playerPins (the other players then stay hidden on the clean map while picking)");
            }
        }

        /// <summary>Minimap.m_playerPins: the pins of the other players whose position is public, which the game moves
        /// every frame and draws as their live icons. Null if the game no longer has them. Optional.</summary>
        internal static List<Minimap.PinData> PlayerPins(Minimap map) => playerPins == null ? null : playerPins.GetValue(map) as List<Minimap.PinData>;

        /// <summary>Where a world position lies on the map texture, from 0 to 1 across the whole map.</summary>
        internal static void WorldToMapPoint(Minimap map, Vector3 point, out float mapX, out float mapY) => worldToMapPoint(map, point, out mapX, out mapY);

        /// <summary>Where a point of the map texture lies inside a map image, in interface units measured from the
        /// image's bottom-left corner. The game places its big-map pins and the player's marker with this.</summary>
        internal static Vector2 MapPointToLocalGuiPos(Minimap map, float mapX, float mapY, RawImage image) => mapPointToLocalGuiPos(map, mapX, mapY, image);

        /// <summary>Whether the map shows the ground at a world position: explored by the player, or shared at a cartography table.</summary>
        internal static bool IsExplored(Minimap map, Vector3 point) => isExplored(map, point);

        private static MethodInfo FindMethod(string name, Type[] parameters, List<string> missing)
        {
            MethodInfo method = typeof(Minimap).GetMethod(name, AnyInstance, null, parameters, null);
            if (method == null)
            {
                missing.Add($"Minimap.{name}");
            }
            return method;
        }

        // An "open" delegate to one of Minimap's private methods: the map it runs on is passed as the first argument.
        private static T FindDelegate<T>(string name, Type[] parameters, List<string> missing) where T : Delegate
        {
            MethodInfo method = typeof(Minimap).GetMethod(name, AnyInstance, null, parameters, null);
            T call = method == null ? null : (T)Delegate.CreateDelegate(typeof(T), method, throwOnBindFailure: false);
            if (call == null)
            {
                missing.Add($"Minimap.{name}");
            }
            return call;
        }
    }
}
