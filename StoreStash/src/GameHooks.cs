using System;
using System.Collections.Generic;
using System.Reflection;

namespace StoreStash
{
    /// <summary>
    /// The game's private members this mod uses, looked up once at startup. Checked against the game's InventoryGui
    /// (m_currentContainer, OnStackAll, SetupDragItem) and Container (RPC_StackResponse) of 2026-09-25 (Valheim 1.0.16).
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo currentContainer;
        private static MethodInfo setupDragItem;

        /// <summary>InventoryGui.OnStackAll(): the chest window's Place stacks button. Null if the game has changed: then
        /// Place stacks stays the game's own.</summary>
        internal static MethodInfo OnStackAll { get; private set; }

        /// <summary>Container.RPC_StackResponse(long, bool): where holding E at a chest stacks, once its owner said yes.
        /// Null if the game has changed: then holding E stays the game's own.</summary>
        internal static MethodInfo StackResponse { get; private set; }

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            currentContainer = typeof(InventoryGui).GetField("m_currentContainer", AnyInstance);
            if (currentContainer == null || currentContainer.FieldType != typeof(Container))
            {
                currentContainer = null;
                missingOptional.Add("InventoryGui.m_currentContainer (Place stacks stays the game's own)");
            }

            OnStackAll = typeof(InventoryGui).GetMethod("OnStackAll", AnyInstance, null, Type.EmptyTypes, null);
            if (OnStackAll == null)
            {
                missingOptional.Add("InventoryGui.OnStackAll (Place stacks stays the game's own)");
            }

            StackResponse = typeof(Container).GetMethod("RPC_StackResponse", AnyInstance, null, new[] { typeof(long), typeof(bool) }, null);
            if (StackResponse == null)
            {
                missingOptional.Add("Container.RPC_StackResponse (holding E stays the game's own)");
            }

            // Optional: without it, Place stacks leaves a stack he's dragging where it lies instead of letting go of it first.
            setupDragItem = typeof(InventoryGui).GetMethod("SetupDragItem", AnyInstance, null,
                new[] { typeof(ItemDrop.ItemData), typeof(Inventory), typeof(int) }, null);
        }

        /// <summary>The chest open in the inventory window, or null.</summary>
        internal static Container OpenChest(InventoryGui gui) =>
            currentContainer != null && gui != null ? currentContainer.GetValue(gui) as Container : null;

        /// <summary>Whether Place stacks can be taken over: the button and the open chest are both found.</summary>
        internal static bool CanTakeOverPlaceStacks => OnStackAll != null && currentContainer != null;

        /// <summary>Lets go of the stack he's dragging, as the game's own Place stacks does first, so it can be stacked too.</summary>
        internal static void LetGoOfDrag(InventoryGui gui)
        {
            if (setupDragItem != null && gui != null)
            {
                setupDragItem.Invoke(gui, new object[] { null, null, 1 });
            }
        }
    }
}
