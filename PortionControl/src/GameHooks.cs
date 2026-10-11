using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PortionControl
{
    /// <summary>
    /// The game methods this mod hooks, and the private members it uses, looked up once at startup and checked against
    /// the installed game (1.0.16, decompiled 2026-09-30): InventoryGui's split window (ShowSplitDialog,
    /// UpdateSplitDialog, OnSplitOk, OnSplitCancel, m_splitItem, m_splitInventory, m_currentContainer, SetupDragItem),
    /// SplitDialog's parts (m_panel, m_splitAmount, m_splitOkButton), and Inventory's own empty-cell search
    /// (FindEmptySlot, TopFirst).
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo splitItem;
        private static FieldInfo splitInventory;
        private static FieldInfo currentContainer;
        private static FieldInfo panel;
        private static FieldInfo amountText;
        private static FieldInfo okButton;
        private static Action<InventoryGui> splitOk;
        private static Action<InventoryGui> splitCancel;
        private static Func<Inventory, bool, Vector2i> findEmptySlot;
        private static Func<Inventory, ItemDrop.ItemData, bool> topFirst;
        private static Action<InventoryGui, ItemDrop.ItemData, Inventory, int> setupDragItem;

        /// <summary>InventoryGui.ShowSplitDialog(ItemData, Inventory): where the game opens the window on a stack.</summary>
        internal static MethodInfo ShowSplitDialog { get; private set; }

        /// <summary>InventoryGui.UpdateSplitDialog(): where the game reads the window's keys, every frame it shows.</summary>
        internal static MethodInfo UpdateSplitDialog { get; private set; }

        /// <summary>Whether the split window can move items itself: Max weight's "straight across" and a right-click's
        /// split (the open chest, the game's Cancel, and its empty-cell search). The chest window's buttons ask
        /// CanUseChest.</summary>
        internal static bool CanMoveAcross => currentContainer != null && splitCancel != null && findEmptySlot != null;

        /// <summary>Whether the chest window's buttons can work: the open chest and the game's empty-cell search.</summary>
        internal static bool CanUseChest => currentContainer != null && findEmptySlot != null;

        /// <param name="splitWindowStandsAside">True while BetterSplitDialog or Abacus has the split window: its members
        /// are then only optional, so a game update that renames one can't switch off the chest window's buttons (review
        /// round 2).</param>
        internal static void Resolve(List<string> missing, List<string> missingOptional, bool splitWindowStandsAside)
        {
            Type gui = typeof(InventoryGui);
            var split = new List<string>();
            ShowSplitDialog = gui.GetMethod("ShowSplitDialog", AnyInstance, null, new[] { typeof(ItemDrop.ItemData), typeof(Inventory) }, null);
            if (ShowSplitDialog == null)
            {
                split.Add("InventoryGui.ShowSplitDialog");
            }
            UpdateSplitDialog = gui.GetMethod("UpdateSplitDialog", AnyInstance, null, Type.EmptyTypes, null);
            if (UpdateSplitDialog == null)
            {
                split.Add("InventoryGui.UpdateSplitDialog");
            }
            splitOk = Action(gui, "OnSplitOk", split);
            splitItem = Field(gui, "m_splitItem", typeof(ItemDrop.ItemData), split);
            splitInventory = Field(gui, "m_splitInventory", typeof(Inventory), split);

            Type dialog = typeof(SplitDialog);
            panel = Field(dialog, "m_panel", typeof(RectTransform), split);
            amountText = Field(dialog, "m_splitAmount", typeof(TMP_Text), split);
            okButton = Field(dialog, "m_splitOkButton", typeof(Button), split);
            if (splitWindowStandsAside)
            {
                foreach (string name in split)
                {
                    missingOptional.Add($"{name} (the split window is another mod's anyway)");
                }
            }
            else
            {
                missing.AddRange(split);
            }

            // Max weight's "straight across" only; without them its amount goes on the cursor.
            var optional = new List<string>();
            splitCancel = Action(gui, "OnSplitCancel", optional);
            currentContainer = Field(gui, "m_currentContainer", typeof(Container), optional);
            MethodInfo empty = typeof(Inventory).GetMethod("FindEmptySlot", AnyInstance, null, new[] { typeof(bool) }, null);
            if (empty != null && empty.ReturnType == typeof(Vector2i))
            {
                findEmptySlot = (Func<Inventory, bool, Vector2i>)Delegate.CreateDelegate(typeof(Func<Inventory, bool, Vector2i>), empty);
            }
            else
            {
                optional.Add("Inventory.FindEmptySlot");
            }
            MethodInfo top = typeof(Inventory).GetMethod("TopFirst", AnyInstance, null, new[] { typeof(ItemDrop.ItemData) }, null);
            if (top != null && top.ReturnType == typeof(bool))
            {
                topFirst = (Func<Inventory, ItemDrop.ItemData, bool>)Delegate.CreateDelegate(typeof(Func<Inventory, ItemDrop.ItemData, bool>), top);
            }
            foreach (string name in optional)
            {
                string lost = splitWindowStandsAside
                    ? (name == "InventoryGui.OnSplitCancel" ? null : "the chest window's buttons then don't show")
                    : name == "InventoryGui.OnSplitCancel"
                        ? "Max weight's amount then goes on the cursor, and a right-click splits nothing"
                        : "Max weight's amount then goes on the cursor, a right-click splits nothing, and the chest window's buttons don't show";
                if (lost != null)
                {
                    missingOptional.Add($"{name} ({lost})");
                }
            }
            MethodInfo drag = gui.GetMethod("SetupDragItem", AnyInstance, null,
                                            new[] { typeof(ItemDrop.ItemData), typeof(Inventory), typeof(int) }, null);
            if (drag != null && drag.ReturnType == typeof(void))
            {
                setupDragItem = (Action<InventoryGui, ItemDrop.ItemData, Inventory, int>)Delegate.CreateDelegate(
                    typeof(Action<InventoryGui, ItemDrop.ItemData, Inventory, int>), drag);
            }
            else
            {
                missingOptional.Add("InventoryGui.SetupDragItem (the chest window's buttons then leave a stack on the cursor " +
                                    "where it is, as they find it)");
            }
        }

        private static FieldInfo Field(Type type, string name, Type fieldType, List<string> missing)
        {
            FieldInfo field = type.GetField(name, AnyInstance);
            if (field == null || !fieldType.IsAssignableFrom(field.FieldType))
            {
                missing.Add($"{type.Name}.{name}");
                return null;
            }
            return field;
        }

        private static Action<InventoryGui> Action(Type type, string name, List<string> missing)
        {
            MethodInfo method = type.GetMethod(name, AnyInstance, null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(void))
            {
                missing.Add($"{type.Name}.{name}");
                return null;
            }
            return (Action<InventoryGui>)Delegate.CreateDelegate(typeof(Action<InventoryGui>), method);
        }

        /// <summary>The stack the open window splits, or null.</summary>
        internal static ItemDrop.ItemData SplitItem(InventoryGui gui) => (ItemDrop.ItemData)splitItem.GetValue(gui);

        /// <summary>The inventory that stack is in (his, or a chest's), or null.</summary>
        internal static Inventory SplitInventory(InventoryGui gui) => (Inventory)splitInventory.GetValue(gui);

        /// <summary>The chest open beside his inventory, or null.</summary>
        internal static Container CurrentContainer(InventoryGui gui) => currentContainer != null ? (Container)currentContainer.GetValue(gui) : null;

        /// <summary>Whether this mod may take from or put into the chest open beside his inventory: as long as the game's own
        /// Take all can be clicked. The game never greys it; the pack's chest service does at a friend's personal chest he
        /// lets others only look at (card A80), and every move of this mod's into or out of the open chest asks this (its
        /// -1 and Max weight, a right-click's split into stacks, Max weight straight across).</summary>
        internal static bool ChestTouchable(InventoryGui gui) => gui == null || gui.m_takeAllButton == null || gui.m_takeAllButton.interactable;

        /// <summary>The game's own OK: puts the slider's amount on the cursor and closes the window.</summary>
        internal static void SplitOk(InventoryGui gui) => splitOk(gui);

        /// <summary>Lets go of what is on the cursor, as the game's own Take all does first.</summary>
        internal static void ClearCursor(InventoryGui gui) => setupDragItem?.Invoke(gui, null, null, 1);

        /// <summary>The game's own Cancel: forgets the stack and closes the window.</summary>
        internal static void SplitCancel(InventoryGui gui) => splitCancel(gui);

        internal static RectTransform Panel(SplitDialog dialog) => (RectTransform)panel.GetValue(dialog);

        internal static TMP_Text AmountText(SplitDialog dialog) => (TMP_Text)amountText.GetValue(dialog);

        internal static Button OkButton(SplitDialog dialog) => (Button)okButton.GetValue(dialog);

        /// <summary>The empty cell the game's own pick-up would use for this item in this inventory (x below 0: none).</summary>
        internal static Vector2i EmptyCell(Inventory inventory, ItemDrop.ItemData item)
        {
            bool top = topFirst != null ? topFirst(inventory, item) : Mover.TopFirst(item);
            return findEmptySlot(inventory, top);
        }
    }
}
