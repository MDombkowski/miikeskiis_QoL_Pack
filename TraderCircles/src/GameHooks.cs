using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace TraderCircles
{
    /// <summary>
    /// The game methods this mod hooks and calls, looked up once at startup. Checked against the game's Game
    /// (RPC_DiscoverClosestLocation, RPC_DiscoverLocationResponse), Minimap (WorldToMapPoint, MapPointToLocalGuiPos),
    /// ZoneSystem (m_locations, GetLocationIcon, GetLocationIcons) and WorldGenerator (WorldAngle, IsAshlands,
    /// IsDeepnorth, maxMarshDistance).
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>The routed RPC a vegvisir sends to ask the server where the places of one kind are. Every server
        /// registers it in Game.Start.</summary>
        internal const string AskRpc = "RPC_DiscoverClosestLocation";

        /// <summary>The routed RPC that carries each answer back; every game registers it in Game.Start.</summary>
        private const string AnswerRpc = "RPC_DiscoverLocationResponse";

        private static FieldInfo routedFunctions;

        private delegate void WorldToMapPointMethod(Minimap map, Vector3 point, out float mapX, out float mapY);

        private static WorldToMapPointMethod worldToMapPoint;
        private static Func<Minimap, float, float, RawImage, Vector2> mapPointToLocalGuiPos;
        private static FieldInfo maxMarshDistance;

        /// <summary>Game.RPC_DiscoverLocationResponse(long, string, int, Vector3, bool): where the server's answer lands
        /// on this player's game, which would turn it into a saved pin. ReplyCatchPatch goes in front of it.</summary>
        internal static MethodInfo LocationResponse { get; private set; }

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            // Where the game keeps its routed RPCs' handlers: needed to confirm the answer reaches the patched handler.
            routedFunctions = typeof(ZRoutedRpc).GetField("m_functions", AnyInstance);
            if (routedFunctions == null || !typeof(System.Collections.IDictionary).IsAssignableFrom(routedFunctions.FieldType))
            {
                routedFunctions = null;
                missing.Add("ZRoutedRpc.m_functions");
            }

            LocationResponse = typeof(Game).GetMethod(AnswerRpc, AnyInstance, null,
                new[] { typeof(long), typeof(string), typeof(int), typeof(Vector3), typeof(bool) }, null);
            if (LocationResponse == null)
            {
                missing.Add("Game.RPC_DiscoverLocationResponse");
            }

            // The question: the server's handler must still take (sender, name, point, pin name, pin type, show map,
            // discover all). Only the server runs it, but every game has the same code.
            MethodInfo ask = typeof(Game).GetMethod(AskRpc, AnyInstance, null,
                new[] { typeof(long), typeof(string), typeof(Vector3), typeof(string), typeof(int), typeof(bool), typeof(bool) }, null);
            if (ask == null)
            {
                missing.Add("Game." + AskRpc);
            }

            worldToMapPoint = FindDelegate<WorldToMapPointMethod>(typeof(Minimap),
                "WorldToMapPoint", new[] { typeof(Vector3), typeof(float).MakeByRefType(), typeof(float).MakeByRefType() }, missing);
            mapPointToLocalGuiPos = FindDelegate<Func<Minimap, float, float, RawImage, Vector2>>(typeof(Minimap),
                "MapPointToLocalGuiPos", new[] { typeof(float), typeof(float), typeof(RawImage) }, missing);

            // Public members this mod calls directly: checked here, so that a game update that changed them switches the
            // mod off with a clear warning rather than failing on first use.
            RequireStatic(typeof(WorldGenerator), nameof(WorldGenerator.WorldAngle), typeof(float), missing);
            RequireStatic(typeof(WorldGenerator), nameof(WorldGenerator.IsAshlands), typeof(bool), missing);
            RequireStatic(typeof(WorldGenerator), nameof(WorldGenerator.IsDeepnorth), typeof(bool), missing);
            if (typeof(ZoneSystem).GetMethod(nameof(ZoneSystem.GetLocationIcon), new[] { typeof(string), typeof(Vector3).MakeByRefType() }) == null)
            {
                missing.Add("ZoneSystem.GetLocationIcon");
            }
            if (typeof(ZoneSystem).GetMethod(nameof(ZoneSystem.GetLocationIcons), new[] { typeof(Dictionary<Vector3, string>) }) == null)
            {
                missing.Add("ZoneSystem.GetLocationIcons");
            }

            maxMarshDistance = typeof(WorldGenerator).GetField("maxMarshDistance", AnyInstance);
            if (maxMarshDistance == null || maxMarshDistance.FieldType != typeof(float))
            {
                maxMarshDistance = null;
                missingOptional.Add("WorldGenerator.maxMarshDistance (the swamps' outer limit is then taken as 6000 m, or 8000 m in the oldest worlds)");
            }
        }

        /// <summary>Whether this mod's catch sits in front of the game's handler for the server's answer right now, and the
        /// game still hands the answer to that very handler. The mod asks the server only while both hold: otherwise every
        /// answer would become a saved pin on the map.</summary>
        internal static bool CatchInPlace()
        {
            if (LocationResponse == null)
            {
                return false;
            }
            Patches patches = Harmony.GetPatchInfo(LocationResponse);
            if (patches == null || !patches.Prefixes.Any(patch => patch.PatchMethod != null && patch.PatchMethod.DeclaringType == typeof(ReplyCatchPatch)))
            {
                return false;
            }
            return AnswerGoesTo(LocationResponse);
        }

        // The routed RPC's own record: ZRoutedRpc.m_functions maps the answer's name (by its stable hash) to the handler
        // registered in Game.Start, kept in the record's m_action. If the game ever registered another handler under that
        // name, the catch would sit on a method no answer reaches.
        private static bool AnswerGoesTo(MethodInfo handler)
        {
            if (ZRoutedRpc.instance == null || routedFunctions == null)
            {
                return false;
            }
            if (!(routedFunctions.GetValue(ZRoutedRpc.instance) is System.Collections.IDictionary functions))
            {
                return false;
            }
            object record = functions[AnswerRpc.GetStableHashCode()];
            FieldInfo action = record?.GetType().GetField("m_action", AnyInstance);
            return action?.GetValue(record) is Delegate registered && registered.Method != null &&
                   registered.Method.MethodHandle == handler.MethodHandle;
        }

        /// <summary>Where a world position lies on the map texture, from 0 to 1 across the whole map.</summary>
        internal static void WorldToMapPoint(Minimap map, Vector3 point, out float mapX, out float mapY) => worldToMapPoint(map, point, out mapX, out mapY);

        /// <summary>Where a point of the map texture lies inside a map image, in interface units measured from the
        /// image's bottom-left corner. The game places its pins with this.</summary>
        internal static Vector2 MapPointToLocalGuiPos(Minimap map, float mapX, float mapY, RawImage image) => mapPointToLocalGuiPos(map, mapX, mapY, image);

        /// <summary>How far from the world's centre swamps can reach in this world: 6000 m, or 8000 m in worlds made with
        /// the oldest world generator.</summary>
        internal static float MaxMarshDistance(WorldGenerator generator)
        {
            if (maxMarshDistance != null)
            {
                return (float)maxMarshDistance.GetValue(generator);
            }
            // The game's own rule (WorldGenerator.VersionSetup): 8000 m for world-generator version 1 and older.
            return generator.m_world != null && generator.m_world.m_worldGenVersion <= 1 ? 8000f : 6000f;
        }

        private static void RequireStatic(Type type, string name, Type returns, List<string> missing)
        {
            MethodInfo method = type.GetMethod(name, AnyStatic, null, new[] { typeof(float), typeof(float) }, null);
            if (method == null || method.ReturnType != returns)
            {
                missing.Add($"{type.Name}.{name}");
            }
        }

        // An "open" delegate to one of a class's private methods: the object it runs on is passed as the first argument.
        private static T FindDelegate<T>(Type type, string name, Type[] parameters, List<string> missing) where T : Delegate
        {
            MethodInfo method = type.GetMethod(name, AnyInstance, null, parameters, null);
            T call = method == null ? null : (T)Delegate.CreateDelegate(typeof(T), method, throwOnBindFailure: false);
            if (call == null)
            {
                missing.Add($"{type.Name}.{name}");
            }
            return call;
        }
    }
}
