using System;
using System.Collections.Generic;
using System.Reflection;

namespace ReserveStock
{
    /// <summary>
    /// The game's private members this mod uses, looked up once at startup. Checked against the game's InventoryGui
    /// (m_currentContainer, OnStackAll) of 2026-09-25 (Valheim 1.0.16).
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo currentContainer;

        /// <summary>InventoryGui.OnStackAll(): the chest window's Place stacks button. Null if the game has changed: then the
        /// setting that blocks Place stacks at a reserved chest can't stop that button (holding E it still stops).</summary>
        internal static MethodInfo OnStackAll { get; private set; }

        /// <summary>Whether the chest window's open chest can be read: without it, the window has no button.</summary>
        internal static bool CanSeeOpenChest => currentContainer != null;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            currentContainer = typeof(InventoryGui).GetField("m_currentContainer", AnyInstance);
            if (currentContainer == null || currentContainer.FieldType != typeof(Container))
            {
                currentContainer = null;
                missingOptional.Add("InventoryGui.m_currentContainer (the chest window's button; the key still works)");
            }

            OnStackAll = typeof(InventoryGui).GetMethod("OnStackAll", AnyInstance, null, Type.EmptyTypes, null);
            if (OnStackAll == null)
            {
                missingOptional.Add("InventoryGui.OnStackAll (with \"Place stacks leaves reserved chests alone too\" on, the button still works at a reserved chest)");
            }
        }

        /// <summary>The chest open in the inventory window, or null.</summary>
        internal static Container OpenChest(InventoryGui gui) =>
            currentContainer != null && gui != null ? currentContainer.GetValue(gui) as Container : null;
    }
}
