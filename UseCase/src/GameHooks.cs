using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace UseCase
{
    /// <summary>
    /// The game methods this mod hooks, and the private members it uses, looked up once at startup with their types
    /// checked (Valheim 1.0.16, decompiled 2026-09-30). A game update that renames one switches the mod off with a line
    /// saying which (MODULES.md rule 8), or, for the parts it can do without, leaves only that part out.
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>UITooltip.LateUpdate(): where the game shows and places its tooltip, every frame.</summary>
        internal static MethodInfo TooltipLateUpdate { get; private set; }

        /// <summary>InventoryGrid.CreateItemTooltip(ItemData, UITooltip): the inventory's and chests' item tooltips.</summary>
        internal static MethodInfo CreateItemTooltip { get; private set; }

        /// <summary>InventoryGui.SetupRequirement(Transform, Piece.Requirement, Player, bool, int, int): the cost icons of
        /// the crafting panel and the build panel.</summary>
        internal static MethodInfo SetupRequirement { get; private set; }

        /// <summary>StoreGui.FillList(): the trader's list; null when it can't be found (the trader's list then has no panel).</summary>
        internal static MethodInfo FillList { get; private set; }

        /// <summary>OfferingBowl.Start() and Door.Awake(): an altar or a locked door loaded near him (optional).</summary>
        internal static MethodInfo BowlStart { get; private set; }

        internal static MethodInfo DoorAwake { get; private set; }

        private static AccessTools.FieldRef<UITooltip> current;
        private static AccessTools.FieldRef<GameObject> tooltip;
        internal static AccessTools.FieldRef<StoreGui, List<GameObject>> StoreItems { get; private set; }
        internal static AccessTools.FieldRef<StoreGui, Trader> StoreTrader { get; private set; }

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            TooltipLateUpdate = Method(typeof(UITooltip), "LateUpdate", AnyInstance, Type.EmptyTypes, missing);
            current = Static<UITooltip, UITooltip>("m_current", missing);
            tooltip = Static<UITooltip, GameObject>("m_tooltip", missing);
            CreateItemTooltip = Method(typeof(InventoryGrid), "CreateItemTooltip", AnyInstance, new[] { typeof(ItemDrop.ItemData), typeof(UITooltip) }, missing);
            SetupRequirement = Method(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement), AnyStatic,
                new[] { typeof(Transform), typeof(Piece.Requirement), typeof(Player), typeof(bool), typeof(int), typeof(int) }, missing);
            if (SetupRequirement != null && SetupRequirement.ReturnType != typeof(bool))
            {
                missing.Add("InventoryGui.SetupRequirement (it no longer answers true or false)");
                SetupRequirement = null;
            }

            var store = new List<string>();
            FillList = Method(typeof(StoreGui), "FillList", AnyInstance, Type.EmptyTypes, store);
            StoreItems = Field<StoreGui, List<GameObject>>("m_itemList", store);
            StoreTrader = Field<StoreGui, Trader>("m_trader", store);
            if (store.Count > 0)
            {
                FillList = null;
                missingOptional.Add($"{string.Join(", ", store)} (the trader's list then has no panel)");
            }

            var places = new List<string>();
            BowlStart = Method(typeof(OfferingBowl), "Start", AnyInstance, Type.EmptyTypes, places);
            DoorAwake = Method(typeof(Door), "Awake", AnyInstance, Type.EmptyTypes, places);
            if (places.Count > 0)
            {
                missingOptional.Add($"{string.Join(", ", places)} (altars and locked doors are then never seen, so they stay \"???\" " +
                                    "while hidden until seen)");
            }
        }

        /// <summary>The UITooltip whose tooltip shows now, or null.</summary>
        internal static UITooltip Current() => current != null ? current() : null;

        /// <summary>The tooltip object the game shows now, or null.</summary>
        internal static GameObject Tooltip() => tooltip != null ? tooltip() : null;

        private static MethodInfo Method(Type type, string name, BindingFlags flags, Type[] parameters, List<string> missing)
        {
            MethodInfo method = type.GetMethod(name, flags, null, parameters, null);
            if (method == null)
            {
                missing.Add($"{type.Name}.{name}");
            }
            return method;
        }

        private static AccessTools.FieldRef<F> Static<T, F>(string name, List<string> missing)
        {
            FieldInfo field = typeof(T).GetField(name, AnyStatic);
            if (field == null || field.FieldType != typeof(F))
            {
                missing.Add($"{typeof(T).Name}.{name}");
                return null;
            }
            return AccessTools.StaticFieldRefAccess<F>(field);
        }

        private static AccessTools.FieldRef<T, F> Field<T, F>(string name, List<string> missing)
        {
            FieldInfo field = typeof(T).GetField(name, AnyInstance);
            if (field == null || field.FieldType != typeof(F))
            {
                missing.Add($"{typeof(T).Name}.{name}");
                return null;
            }
            return AccessTools.FieldRefAccess<T, F>(field);
        }
    }
}
