using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Hoarstone
{
    /// <summary>
    /// The game's members this mod reaches that aren't public, and the private methods its patches go on, each looked up
    /// once at the start, so a game update that changed one switches the mod off with a clear message rather than failing
    /// in play (read from the game, 1.0.17, decompiled fresh by session 55-B: research
    /// 2026-10-10_hoarstone-what-the-game-settles).
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private delegate void WorldToMapPointMethod(Minimap map, Vector3 point, out float mapX, out float mapY);

        private static FieldInfo objectsBySector;
        private static FieldInfo allWards;
        private static FieldInfo spawnSystems;
        private static Action<Player> updateKnownRecipesList;
        private static WorldToMapPointMethod worldToMapPoint;
        private static Func<Minimap, float, float, RawImage, Vector2> mapPointToLocalGuiPos;

        internal static AccessTools.FieldRef<CreatureSpawner, ZNetView> SpawnerView { get; private set; }
        internal static AccessTools.FieldRef<CreatureSpawner, bool> SpawnerCheckedLocation { get; private set; }
        internal static AccessTools.FieldRef<CreatureSpawner, Location> SpawnerLocation { get; private set; }
        internal static AccessTools.FieldRef<PrivateArea, ZNetView> WardView { get; private set; }

        /// <summary>The piece being placed, as the game shows it (Player.m_placementGhost); null where it can't be read,
        /// and then only the outline shown while placing a Hoarstone is lost.</summary>
        internal static AccessTools.FieldRef<Player, GameObject> PlacementGhost { get; private set; }

        /// <summary>The map's drawing helpers, which only the outline on the map needs.</summary>
        internal static bool MapReady => worldToMapPoint != null && mapPointToLocalGuiPos != null;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            objectsBySector = typeof(ZDOMan).GetField("m_objectsBySector", AnyInstance);
            if (objectsBySector == null || objectsBySector.FieldType != typeof(List<ZDO>[]))
            {
                missing.Add("ZDOMan.m_objectsBySector");
                objectsBySector = null;
            }
            allWards = typeof(PrivateArea).GetField("m_allAreas", AnyStatic);
            if (allWards == null || allWards.FieldType != typeof(List<PrivateArea>))
            {
                missing.Add("PrivateArea.m_allAreas");
                allWards = null;
            }
            SpawnerView = Field<CreatureSpawner, ZNetView>("m_nview", missing);
            SpawnerCheckedLocation = Field<CreatureSpawner, bool>("m_checkedLocation", missing);
            SpawnerLocation = Field<CreatureSpawner, Location>("m_location", missing);
            spawnSystems = typeof(SpawnSystem).GetField("m_instances", AnyStatic);
            if (spawnSystems == null || spawnSystems.FieldType != typeof(List<SpawnSystem>))
            {
                missing.Add("SpawnSystem.m_instances");
                spawnSystems = null;
            }
            WardView = Field<PrivateArea, ZNetView>("m_nview", missing);
            updateKnownRecipesList = Delegate<Action<Player>>(typeof(Player), "UpdateKnownRecipesList", missing, typeof(void));

            // The methods the patches go on, checked here so a changed one is named (the patch itself would only fail).
            Require(typeof(SpawnSystem), "IsSpawnPointGood", typeof(bool), missing, typeof(SpawnSystem.SpawnData), typeof(Vector3).MakeByRefType());
            Require(typeof(CreatureSpawner), "UpdateSpawner", typeof(void), missing);
            Require(typeof(SpawnArea), "FindSpawnPoint", typeof(bool), missing, typeof(GameObject), typeof(Vector3).MakeByRefType());
            Require(typeof(Player), "PlacePiece", typeof(void), missing, typeof(Piece), typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool));
            Require(typeof(PrivateArea), "Setup", typeof(void), missing, typeof(string));
            Require(typeof(PrivateArea), "GetHoverText", typeof(string), missing);
            Require(typeof(PrivateArea), "GetHoverName", typeof(string), missing);
            Require(typeof(PrivateArea), "Interact", typeof(bool), missing, typeof(Humanoid), typeof(bool), typeof(bool));
            Require(typeof(Piece), "DropResources", typeof(void), missing, typeof(HitData));
            Require(typeof(WearNTear), "Remove", typeof(void), missing, typeof(bool));
            Require(typeof(Piece), "Awake", typeof(void), missing);

            PlacementGhost = Field<Player, GameObject>("m_placementGhost", missingOptional);

            // The map's outline only: without these the outline shows on the ground alone.
            var mapMissing = new List<string>();
            worldToMapPoint = Delegate<WorldToMapPointMethod>(typeof(Minimap), "WorldToMapPoint", mapMissing, typeof(void),
                typeof(Vector3), typeof(float).MakeByRefType(), typeof(float).MakeByRefType());
            mapPointToLocalGuiPos = Delegate<Func<Minimap, float, float, RawImage, Vector2>>(typeof(Minimap), "MapPointToLocalGuiPos",
                mapMissing, typeof(Vector2), typeof(float), typeof(float), typeof(RawImage));
            missingOptional.AddRange(mapMissing);
        }

        /// <summary>The game's records by sector (ZDOMan's private list), or null before a world is loaded.</summary>
        internal static List<ZDO>[] Sectors(ZDOMan man) => man == null ? null : (List<ZDO>[])objectsBySector.GetValue(man);

        /// <summary>Every spawn system (zone controller) loaded in this game (SpawnSystem's private list); never null.</summary>
        internal static List<SpawnSystem> SpawnSystems() => (List<SpawnSystem>)spawnSystems.GetValue(null) ?? new List<SpawnSystem>();

        /// <summary>Every Ward loaded in this game (PrivateArea's private list); never null.</summary>
        internal static List<PrivateArea> Wards() => (List<PrivateArea>)allWards.GetValue(null) ?? new List<PrivateArea>();

        /// <summary>What the game does when a tool's pieces change: learn the pieces newly known.</summary>
        internal static void UpdateKnownRecipesList(Player player) => updateKnownRecipesList(player);

        /// <summary>A world point on the map's texture (Minimap.WorldToMapPoint).</summary>
        internal static void WorldToMapPoint(Minimap map, Vector3 point, out float mapX, out float mapY) => worldToMapPoint(map, point, out mapX, out mapY);

        /// <summary>Where a point of the map's texture lies inside a map image, measured from its bottom-left corner.</summary>
        internal static Vector2 MapPointToLocalGuiPos(Minimap map, float mapX, float mapY, RawImage image) => mapPointToLocalGuiPos(map, mapX, mapY, image);

        private static AccessTools.FieldRef<TOwner, TField> Field<TOwner, TField>(string name, List<string> missing)
        {
            FieldInfo field = typeof(TOwner).GetField(name, AnyInstance);
            if (field == null || field.FieldType != typeof(TField))
            {
                missing.Add($"{typeof(TOwner).Name}.{name}");
                return null;
            }
            return AccessTools.FieldRefAccess<TOwner, TField>(field);
        }

        private static TDelegate Delegate<TDelegate>(Type type, string name, List<string> missing, Type returns, params Type[] parameters)
            where TDelegate : Delegate
        {
            MethodInfo method = type.GetMethod(name, AnyInstance, null, parameters, null);
            if (method == null || method.ReturnType != returns)
            {
                missing.Add($"{type.Name}.{name}");
                return null;
            }
            var call = (TDelegate)System.Delegate.CreateDelegate(typeof(TDelegate), method, throwOnBindFailure: false);
            if (call == null)
            {
                missing.Add($"{type.Name}.{name}");
            }
            return call;
        }

        private static void Require(Type type, string name, Type returns, List<string> missing, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, AnyInstance | BindingFlags.Static, null, parameters, null);
            if (method == null || method.ReturnType != returns)
            {
                missing.Add($"{type.Name}.{name}");
            }
        }
    }
}
