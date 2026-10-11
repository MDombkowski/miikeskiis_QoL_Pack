using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Terraformer
{
    /// <summary>
    /// The game methods Terraformer hooks, and the private members it uses, looked up once at startup and checked by name
    /// and type (the installed game, 1.0.16; research C and D). A missing one leaves the module off with its name in the
    /// log, before anything is patched.
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // Patch targets.
        internal static MethodInfo UpdatePlacement { get; private set; }
        internal static MethodInfo UpdatePlacementGhost { get; private set; }
        internal static MethodInfo TryPlacePiece { get; private set; }
        internal static MethodInfo CanRotatePiece { get; private set; }
        internal static MethodInfo UpdateRegisters { get; private set; }
        internal static MethodInfo TerrainOpAwake { get; private set; }
        internal static MethodInfo Serialize { get; private set; }
        internal static MethodInfo Deserialize { get; private set; }
        internal static MethodInfo ApplyOperation { get; private set; }
        internal static MethodInfo RpcApplyOperation { get; private set; }
        internal static MethodInfo DoOperation { get; private set; }
        internal static MethodInfo InternalDoOperation { get; private set; }
        internal static MethodInfo UpdateCamera { get; private set; }
        internal static MethodInfo GetCameraPosition { get; private set; }

        // Player's private members.
        internal static AccessTools.FieldRef<Player, int> PlaceRotation { get; private set; }
        internal static AccessTools.FieldRef<Player, GameObject> PlacementGhost { get; private set; }
        internal static AccessTools.FieldRef<Player, Player.PlacementStatus> PlacementStatus { get; private set; }
        private static Action<Player, bool> setPlacementGhostValid;
        private static Action<Player> updateKnownRecipesList;
        private static Action<Player, bool> updatePlacementGhost;

        // A zone's terrain compiler's private members (research D, section 1).
        internal static AccessTools.FieldRef<TerrainComp, int> Width { get; private set; }
        internal static AccessTools.FieldRef<TerrainComp, float[]> LevelDelta { get; private set; }
        internal static AccessTools.FieldRef<TerrainComp, float[]> SmoothDelta { get; private set; }
        internal static AccessTools.FieldRef<TerrainComp, bool[]> ModifiedHeight { get; private set; }
        internal static AccessTools.FieldRef<TerrainComp, Color[]> PaintMask { get; private set; }
        internal static AccessTools.FieldRef<TerrainComp, bool[]> ModifiedPaint { get; private set; }
        internal static AccessTools.FieldRef<TerrainComp, Heightmap> HeightmapOf { get; private set; }
        internal static AccessTools.FieldRef<TerrainComp, int> Operations { get; private set; }
        internal static AccessTools.FieldRef<TerrainComp, Vector3> LastOpPoint { get; private set; }
        internal static AccessTools.FieldRef<TerrainComp, float> LastOpRadius { get; private set; }
        private static Action<TerrainComp, bool> save;

        // A zone's heights as drawn (Heightmap), and the camera's distance (GameCamera).
        internal static AccessTools.FieldRef<Heightmap, List<float>> Heights { get; private set; }
        internal static AccessTools.FieldRef<GameCamera, float> CameraDistance { get; private set; }

        // A zone's paint-only save is skipped while the paint's checksum is the one last saved (TerrainComp.Save).
        internal static AccessTools.FieldRef<TerrainComp, int> LastHash { get; private set; }
        private static Func<TerrainComp, int> paintMaskHash;

        // Every place of the world loaded now, each saying whether building is allowed around it (Location).
        private static FieldInfo allLocationsField;
        private static List<Location> allLocations;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            UpdatePlacement = Method(typeof(Player), "UpdatePlacement", missing, typeof(bool), typeof(float));
            UpdatePlacementGhost = Method(typeof(Player), "UpdatePlacementGhost", missing, typeof(bool));
            TryPlacePiece = Method(typeof(Player), nameof(Player.TryPlacePiece), missing, typeof(Piece));
            CanRotatePiece = Method(typeof(Player), nameof(Player.CanRotatePiece), missing);
            UpdateRegisters = Method(typeof(ObjectDB), "UpdateRegisters", missing);
            TerrainOpAwake = Method(typeof(TerrainOp), "Awake", missing);
            Serialize = Method(typeof(TerrainOp.Settings), nameof(TerrainOp.Settings.Serialize), missing, typeof(ZPackage), typeof(GameObject));
            Deserialize = typeof(TerrainOp.Settings).GetMethod(nameof(TerrainOp.Settings.Deserialize), AnyStatic, null, new[] { typeof(ZPackage) }, null);
            if (Deserialize == null || Deserialize.ReturnType != typeof(TerrainOp.Settings))
            {
                Deserialize = null;
                missing.Add("TerrainOp.Settings.Deserialize");
            }
            ApplyOperation = Method(typeof(TerrainComp), nameof(TerrainComp.ApplyOperation), missing, typeof(TerrainOp));
            RpcApplyOperation = Method(typeof(TerrainComp), "RPC_ApplyOperation", missing, typeof(long), typeof(ZPackage));
            DoOperation = Method(typeof(TerrainComp), "DoOperation", missing, typeof(Vector3), typeof(Vector3), typeof(TerrainOp.Settings));
            InternalDoOperation = Method(typeof(TerrainComp), "InternalDoOperation", missing, typeof(Vector3), typeof(Vector3), typeof(TerrainOp.Settings));
            UpdateCamera = Method(typeof(GameCamera), "UpdateCamera", missing, typeof(float));
            GetCameraPosition = Method(typeof(GameCamera), "GetCameraPosition", missing, typeof(float), typeof(Vector3).MakeByRefType(), typeof(Quaternion).MakeByRefType());

            PlaceRotation = Field<Player, int>("m_placeRotation", missing);
            PlacementGhost = Field<Player, GameObject>("m_placementGhost", missing);
            PlacementStatus = Field<Player, Player.PlacementStatus>("m_placementStatus", missing);
            setPlacementGhostValid = Delegate<Action<Player, bool>>(typeof(Player), "SetPlacementGhostValid", missing, typeof(void), typeof(bool));
            updateKnownRecipesList = Delegate<Action<Player>>(typeof(Player), "UpdateKnownRecipesList", missing, typeof(void));
            updatePlacementGhost = Delegate<Action<Player, bool>>(typeof(Player), "UpdatePlacementGhost", missing, typeof(void), typeof(bool));

            Width = Field<TerrainComp, int>("m_width", missing);
            LevelDelta = Field<TerrainComp, float[]>("m_levelDelta", missing);
            SmoothDelta = Field<TerrainComp, float[]>("m_smoothDelta", missing);
            ModifiedHeight = Field<TerrainComp, bool[]>("m_modifiedHeight", missing);
            PaintMask = Field<TerrainComp, Color[]>("m_paintMask", missing);
            ModifiedPaint = Field<TerrainComp, bool[]>("m_modifiedPaint", missing);
            HeightmapOf = Field<TerrainComp, Heightmap>("m_hmap", missing);
            Operations = Field<TerrainComp, int>("m_operations", missing);
            LastOpPoint = Field<TerrainComp, Vector3>("m_lastOpPoint", missing);
            LastOpRadius = Field<TerrainComp, float>("m_lastOpRadius", missing);
            save = Delegate<Action<TerrainComp, bool>>(typeof(TerrainComp), "Save", missing, typeof(void), typeof(bool));
            Heights = Field<Heightmap, List<float>>("m_heights", missing);
            CameraDistance = Field<GameCamera, float>("m_distance", missing);
            LastHash = Field<TerrainComp, int>("m_lastHash", missing);
            paintMaskHash = Delegate<Func<TerrainComp, int>>(typeof(TerrainComp), "ComputePaintMaskHash", missing, typeof(int));

            allLocationsField = typeof(Location).GetField("s_allLocations", AnyStatic);
            if (allLocationsField == null || allLocationsField.FieldType != typeof(List<Location>))
            {
                allLocationsField = null;
                missing.Add("Location.s_allLocations");
            }
        }

        internal static void SetPlacementGhostValid(Player player, bool valid) => setPlacementGhostValid(player, valid);

        internal static void UpdateKnownRecipesList(Player player) => updateKnownRecipesList(player);

        /// <summary>Places the ghost as the game's click is about to (Player.UpdatePlacementGhost, patched: Ghost.After runs).</summary>
        internal static void PlaceGhost(Player player, bool flashGuardStone) => updatePlacementGhost(player, flashGuardStone);

        /// <summary>The zone's record saved as the game saves it after a stroke (TerrainComp.Save, heights and paint).</summary>
        internal static void Save(TerrainComp compiler) => save(compiler, false);

        /// <summary>The zone's paint checksum as the game's own save computes it.</summary>
        internal static int PaintMaskHash(TerrainComp compiler) => paintMaskHash(compiler);

        /// <summary>The game's list of the places of the world loaded now (the one list, made once; read at first use, in
        /// the game, rather than at startup).</summary>
        internal static List<Location> AllLocations =>
            allLocations ??= allLocationsField != null ? allLocationsField.GetValue(null) as List<Location> : null;

        private static MethodInfo Method(Type type, string name, List<string> missing, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, AnyInstance, null, parameters, null);
            if (method == null)
            {
                missing.Add($"{type.Name}.{name}");
            }
            return method;
        }

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
            return (TDelegate)System.Delegate.CreateDelegate(typeof(TDelegate), method);
        }
    }
}
