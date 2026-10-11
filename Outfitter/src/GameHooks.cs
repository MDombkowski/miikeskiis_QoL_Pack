using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Outfitter.Loadouts;
using UnityEngine;

namespace Outfitter
{
    /// <summary>
    /// The game's private members Outfitter reads, looked up once at start (no publicizer: MODULES.md), each checked by
    /// type, and the shape of his inventory now. Read from the decompiled game 1.0.16 on 2026-10-05.
    /// </summary>
    internal static class GameHooks
    {
        // Humanoid's hand items the game has put away for a moment (R, swimming, a station, a bed or chair, the barber,
        // eating): unequipped, but his (Humanoid.HideHandItems).
        private static FieldInfo hiddenRight;
        private static FieldInfo hiddenLeft;

        // InventoryGui's item being dragged: while he holds one, nothing in his inventory may move under it.
        private static FieldInfo dragGo;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            hiddenRight = Field(typeof(Humanoid), "m_hiddenRightItem", typeof(ItemDrop.ItemData), missing);
            hiddenLeft = Field(typeof(Humanoid), "m_hiddenLeftItem", typeof(ItemDrop.ItemData), missing);
            dragGo = Field(typeof(InventoryGui), "m_dragGo", typeof(GameObject), missing);
        }

        private static FieldInfo Field(System.Type type, string name, System.Type fieldType, List<string> missing)
        {
            FieldInfo field = AccessTools.Field(type, name);
            if (field == null || field.FieldType != fieldType)
            {
                missing.Add($"{type.Name}.{name}");
                return null;
            }
            return field;
        }

        /// <summary>The right-hand item the game has put away for a moment, or null.</summary>
        internal static ItemDrop.ItemData HiddenRight(Player player) => player != null ? (ItemDrop.ItemData)hiddenRight.GetValue(player) : null;

        /// <summary>The left-hand item the game has put away for a moment, or null.</summary>
        internal static ItemDrop.ItemData HiddenLeft(Player player) => player != null ? (ItemDrop.ItemData)hiddenLeft.GetValue(player) : null;

        /// <summary>Whether he is holding an item picked up in the inventory window (dragging it).</summary>
        internal static bool Dragging()
        {
            InventoryGui gui = InventoryGui.instance;
            return gui != null && (GameObject)dragGo.GetValue(gui) != null;
        }

        /// <summary>
        /// The shape of his inventory now: 8 wide; the rows he sees (with EquipmentAndQuickSlots, its visible rows;
        /// without it, the game's own row count, the character's "invrows", at most the inventory's height); its whole
        /// height; and EquipmentAndQuickSlots' active slots. Asked only once he has spawned: before that a character's
        /// inventory is 4 rows whatever it has bought.
        /// </summary>
        internal static GridShape Shape(Player player)
        {
            Inventory inventory = player.GetInventory();
            var grid = new GridShape { Width = inventory.GetWidth(), Height = inventory.GetHeight() };
            int rows = Eaqs.Usable ? Eaqs.VisibleRows() : -1;
            if (rows <= 0)
            {
                rows = 4;
                if (player.TryGetUniqueKeyValue(Player.InventoryRowsKey, out string value) && LoadoutText.TryInt(value, out int saved))
                {
                    rows = saved;
                }
            }
            grid.Rows = Mathf.Clamp(rows, 1, grid.Height);
            Eaqs.AddSlots(grid);
            return grid;
        }
    }
}
