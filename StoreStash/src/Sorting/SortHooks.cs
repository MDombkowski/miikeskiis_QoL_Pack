using System;
using System.Collections.Generic;
using System.Reflection;

namespace StoreStash
{
    /// <summary>
    /// The game's private members sorting and trash use (card A53), looked up once at startup. Each is optional to the
    /// module: without one, only the feature that needs it stays off (and the log says so once), so a game update that
    /// breaks sorting leaves quick stack working. Checked against the game's Inventory (Changed), InventoryGui
    /// (UpdateContainer, Update, UpdateCraftingPanel, m_dragItem, m_dragInventory, m_dragAmount, m_shownFrames) of
    /// 2026-09-25 (Valheim 1.0.16), decompiled again on 2026-09-30.
    /// </summary>
    internal static class SortHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static Action<Inventory, bool, bool> changed;
        private static FieldInfo dragItem;
        private static FieldInfo dragInventory;
        private static FieldInfo dragAmount;
        private static FieldInfo shownFrames;
        private static MethodInfo updateCraftingPanel;

        /// <summary>InventoryGui.UpdateContainer(Player): where the game shows a chest's window once it's his. Sorting a
        /// chest as it opens hangs there; null without it (then chests aren't sorted as they open).</summary>
        internal static MethodInfo UpdateContainer { get; private set; }

        /// <summary>InventoryGui.Update(): the trash dialog keeps the inventory open while it asks. Null without it (then
        /// Esc closes the inventory along with the dialog).</summary>
        internal static MethodInfo InventoryUpdate { get; private set; }

        /// <summary>Whether sorting can tell the game an inventory changed (so it's saved and redrawn). Without it,
        /// nothing is sorted.</summary>
        internal static bool CanSort => changed != null;

        /// <summary>Whether the trash can can see what he's carrying (to delete just that).</summary>
        internal static bool CanSeeCarried => dragItem != null && dragInventory != null && dragAmount != null;

        internal static void Resolve(List<string> missingOptional)
        {
            MethodInfo change = typeof(Inventory).GetMethod("Changed", AnyInstance, null, new[] { typeof(bool), typeof(bool) }, null);
            if (change != null && change.ReturnType == typeof(void))
            {
                changed = (Action<Inventory, bool, bool>)Delegate.CreateDelegate(typeof(Action<Inventory, bool, bool>), change);
            }
            else
            {
                missingOptional.Add("Inventory.Changed (sorting stays off)");
            }

            UpdateContainer = typeof(InventoryGui).GetMethod("UpdateContainer", AnyInstance, null, new[] { typeof(Player) }, null);
            if (UpdateContainer == null)
            {
                missingOptional.Add("InventoryGui.UpdateContainer (chests aren't sorted as they open)");
            }

            dragItem = Field("m_dragItem", typeof(ItemDrop.ItemData));
            dragInventory = Field("m_dragInventory", typeof(Inventory));
            dragAmount = Field("m_dragAmount", typeof(int));
            if (!CanSeeCarried)
            {
                missingOptional.Add("InventoryGui.m_dragItem, m_dragInventory or m_dragAmount (the trash can can't take a carried item)");
            }

            shownFrames = Field("m_shownFrames", typeof(int));
            InventoryUpdate = typeof(InventoryGui).GetMethod("Update", AnyInstance, null, Type.EmptyTypes, null);
            if (shownFrames == null || InventoryUpdate == null)
            {
                InventoryUpdate = null;
                missingOptional.Add("InventoryGui.Update or m_shownFrames (Esc closes the inventory along with the trash question)");
            }

            // Optional, quietly: without it the crafting list's colours catch up with a deletion a moment later.
            updateCraftingPanel = typeof(InventoryGui).GetMethod("UpdateCraftingPanel", AnyInstance, null, new[] { typeof(bool) }, null);
        }

        private static FieldInfo Field(string name, Type type)
        {
            FieldInfo field = typeof(InventoryGui).GetField(name, AnyInstance);
            return field != null && field.FieldType == type ? field : null;
        }

        /// <summary>Tells the game an inventory changed, as its own moves do: the weight is counted again, and whoever
        /// listens (a chest saves itself if this computer owns it; his character notes new items) hears of it.</summary>
        internal static void Changed(Inventory inventory) => changed(inventory, false, false);

        /// <summary>What he's carrying on the pointer right now (an item picked up from a grid, with how many), or false.</summary>
        internal static bool Carried(InventoryGui gui, out ItemDrop.ItemData item, out Inventory from, out int amount)
        {
            item = null;
            from = null;
            amount = 0;
            if (gui == null || !CanSeeCarried)
            {
                return false;
            }
            item = dragItem.GetValue(gui) as ItemDrop.ItemData;
            from = dragInventory.GetValue(gui) as Inventory;
            amount = (int)dragAmount.GetValue(gui);
            return item != null && from != null;
        }

        /// <summary>Keeps the inventory open this frame: the game closes it on Esc, Tab or E only once it has been shown
        /// for more than a frame (InventoryGui.Update), so this counts it as just shown.</summary>
        internal static void HoldOpen(InventoryGui gui)
        {
            if (shownFrames != null && gui != null)
            {
                shownFrames.SetValue(gui, 0);
            }
        }

        /// <summary>Refreshes the crafting list after items were deleted, as the game does after dropping one.</summary>
        internal static void RefreshCrafting(InventoryGui gui)
        {
            if (updateCraftingPanel != null && gui != null)
            {
                updateCraftingPanel.Invoke(gui, new object[] { false });
            }
        }
    }
}
