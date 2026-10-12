using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QuickFix
{
    /// <summary>
    /// The game's private members Quick Fix reads, looked up by name once at start (Resolve), so a game update that
    /// renames one leaves the mod switched off with a word in the log instead of failing mid-game. Read in Valheim 1.0.17
    /// (the research set's part A and C).
    /// </summary>
    internal static class GameHooks
    {
        private static FieldInfo allStations;
        private static FieldInfo allExtensions;

        private static AccessTools.FieldRef<Player, Player.PlacementStatus> placementStatus;
        private static AccessTools.FieldRef<Player, GameObject> placementGhost;
        private static AccessTools.FieldRef<Player, float> actionQueuePause;
        private static MethodInfo doInteractAnimation;
        private static Func<InventoryGui, ItemDrop.ItemData, bool> canRepair;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            allStations = Static<List<CraftingStation>>(typeof(CraftingStation), "m_allStations", missing);
            allExtensions = Static<List<StationExtension>>(typeof(StationExtension), "m_allExtensions", missing);
            placementStatus = Field<Player.PlacementStatus>("m_placementStatus", missing);
            placementGhost = Field<GameObject>("m_placementGhost", missing);
            actionQueuePause = Field<float>("m_actionQueuePause", missing);

            doInteractAnimation = typeof(Humanoid).GetMethod("DoInteractAnimation", BindingFlags.Instance | BindingFlags.NonPublic,
                                                             null, new[] { typeof(GameObject) }, null);
            if (doInteractAnimation == null)
            {
                missingOptional.Add("the game's interact gesture at a Grinding Wheel or Smith's Anvil (Humanoid.DoInteractAnimation)");
            }

            MethodInfo canRepairMethod = typeof(InventoryGui).GetMethod("CanRepair", BindingFlags.Instance | BindingFlags.NonPublic,
                                                                        null, new[] { typeof(ItemDrop.ItemData) }, null);
            if (canRepairMethod == null || canRepairMethod.ReturnType != typeof(bool))
            {
                missing.Add("the game's repair rule (InventoryGui.CanRepair)");
            }
            else
            {
                canRepair = (Func<InventoryGui, ItemDrop.ItemData, bool>)Delegate.CreateDelegate(
                    typeof(Func<InventoryGui, ItemDrop.ItemData, bool>), canRepairMethod);
            }
        }

        /// <summary>Every crafting station loaded (CraftingStation.m_allStations, private static), read each time it's
        /// asked; null if it can't be read.</summary>
        internal static List<CraftingStation> AllStations() => allStations?.GetValue(null) as List<CraftingStation>;

        /// <summary>Every station extension loaded, the Grinding Wheel and Smith's Anvil among them
        /// (StationExtension.m_allExtensions, private static).</summary>
        internal static List<StationExtension> AllExtensions() => allExtensions?.GetValue(null) as List<StationExtension>;

        internal static Player.PlacementStatus PlacementStatus(Player player) => placementStatus(player);

        internal static void SetPlacementStatus(Player player, Player.PlacementStatus status) => placementStatus(player) = status;

        internal static GameObject PlacementGhost(Player player) => placementGhost(player);

        /// <summary>The game's short pause after each equip or reload (Player.m_actionQueuePause).</summary>
        internal static float ActionQueuePause(Player player) => actionQueuePause(player);

        /// <summary>The game's own gesture at a thing used: the player turns to it and the "interact" motion plays.</summary>
        internal static void InteractGesture(Player player, GameObject thing)
        {
            doInteractAnimation?.Invoke(player, new object[] { thing });
        }

        /// <summary>The game's own repair rule at the station whose window is open now (InventoryGui.CanRepair).</summary>
        internal static bool GameCanRepair(ItemDrop.ItemData item)
        {
            return canRepair != null && InventoryGui.instance != null && canRepair(InventoryGui.instance, item);
        }

        private static FieldInfo Static<T>(Type type, string name, List<string> missing)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null && typeof(T).IsAssignableFrom(field.FieldType))
            {
                return field;
            }
            missing.Add($"the game's {type.Name}.{name}");
            return null;
        }

        private static AccessTools.FieldRef<Player, T> Field<T>(string name, List<string> missing)
        {
            FieldInfo field = typeof(Player).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.FieldType != typeof(T))
            {
                missing.Add($"the game's Player.{name}");
                return null;
            }
            return AccessTools.FieldRefAccess<Player, T>(field);
        }
    }
}
