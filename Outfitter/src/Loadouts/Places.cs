using System.Collections.Generic;

namespace Outfitter.Loadouts
{
    /// <summary>One of EquipmentAndQuickSlots' slots, as its interface describes it.</summary>
    internal struct SlotCell
    {
        internal int X;
        internal int Y;
        internal bool Quick;
        internal bool Equipment;
    }

    /// <summary>
    /// The shape of his inventory now: its width, how many rows he sees (the game's own, 4 to 6 with Haldor's Pockets, or
    /// EquipmentAndQuickSlots' visible rows while it runs), its whole height (with EquipmentAndQuickSlots, three hidden
    /// rows more), and EquipmentAndQuickSlots' active slots by name.
    /// </summary>
    internal sealed class GridShape
    {
        internal int Width = 8;

        /// <summary>The rows he sees; row 0 is the hotbar.</summary>
        internal int Rows = 4;

        /// <summary>Every row of the inventory, the hidden ones too.</summary>
        internal int Height = 4;

        /// <summary>EquipmentAndQuickSlots' active slots, by name; empty without it.</summary>
        internal readonly Dictionary<string, SlotCell> Slots = new Dictionary<string, SlotCell>();

        /// <summary>The active slot at a cell, or null.</summary>
        internal string SlotAt(int x, int y)
        {
            foreach (KeyValuePair<string, SlotCell> slot in Slots)
            {
                if (slot.Value.X == x && slot.Value.Y == y)
                {
                    return slot.Key;
                }
            }
            return null;
        }

        /// <summary>Whether a cell is one Outfitter may put an item in: in the visible rows, or an active slot.</summary>
        internal bool Usable(int x, int y) => x >= 0 && x < Width && y >= 0 && (y < Rows || (y < Height && SlotAt(x, y) != null));
    }

    /// <summary>
    /// Where a loadout's items sit (his words, 2026-09-29: "how many rows from the bottom the slot is ... with the exception
    /// being the top row"; research section 3). Row 0 is the hotbar and stays the hotbar; rows 1 to R−1 are counted up
    /// from the bottom row, so a new row at the bottom (Haldor's Pockets) keeps them in place; EquipmentAndQuickSlots'
    /// rows (from R on) by the slot's name, which survives its slots moving down with a new row. Pure, so the scratch
    /// harness reaches it.
    /// </summary>
    internal static class Places
    {
        /// <summary>The place of the cell (x, y) in a grid of this shape.</summary>
        internal static void Of(int x, int y, GridShape grid, LoadoutItem into)
        {
            into.Cell = CellKind.None;
            into.Column = 0;
            into.RowsUp = 0;
            into.SlotId = string.Empty;
            if (x < 0 || x >= grid.Width || y < 0)
            {
                return;
            }
            if (y == 0)
            {
                into.Cell = CellKind.Hotbar;
                into.Column = x;
            }
            else if (y < grid.Rows)
            {
                into.Cell = CellKind.FromBottom;
                into.Column = x;
                into.RowsUp = grid.Rows - 1 - y;
            }
            else
            {
                string slot = grid.SlotAt(x, y);
                if (slot != null)
                {
                    into.Cell = CellKind.Slot;
                    into.SlotId = slot;
                }
            }
        }

        /// <summary>The cell an item's place is in a grid of this shape; false when it has no fixed cell there (no place;
        /// saved with more rows than he has now, so it would reach the hotbar or above; a slot that isn't active, or
        /// EquipmentAndQuickSlots not running): it then goes to a free cell.</summary>
        internal static bool CellFor(LoadoutItem item, GridShape grid, out int x, out int y)
        {
            x = -1;
            y = -1;
            switch (item.Cell)
            {
                case CellKind.Hotbar:
                    if (item.Column >= 0 && item.Column < grid.Width)
                    {
                        x = item.Column;
                        y = 0;
                        return true;
                    }
                    return false;
                case CellKind.FromBottom:
                    int row = grid.Rows - 1 - item.RowsUp;
                    if (row >= 1 && item.Column >= 0 && item.Column < grid.Width)
                    {
                        x = item.Column;
                        y = row;
                        return true;
                    }
                    return false;
                case CellKind.Slot:
                    if (grid.Slots.TryGetValue(item.SlotId, out SlotCell slot))
                    {
                        x = slot.X;
                        y = slot.Y;
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>Whether an item's place is one of EquipmentAndQuickSlots' quick slots (by its name: Quick1 …).</summary>
        internal static bool IsQuickSlot(LoadoutItem item) =>
            item.Cell == CellKind.Slot && item.SlotId.StartsWith("Quick", System.StringComparison.Ordinal);
    }
}
