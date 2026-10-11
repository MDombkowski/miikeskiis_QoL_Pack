using System;
using System.Collections.Generic;
using ItemType = ItemDrop.ItemData.ItemType;

namespace Outfitter.Loadouts
{
    /// <summary>
    /// Copy cells and Paste cells (his words, 2026-10-06: "a button to 'Copy Cells' when cells are selected, and then if
    /// editing a loadout (and cells are copied to clipboard) there would be a 'Paste Cells' button that puts the cells into
    /// the inventory I'm editing. If the cell locations they were copied in aren't available in the one being pasted into,
    /// it should just put them wherever there is space. If no space is available, it should tell the player"; job 30,
    /// Session 3). Outfitter's own clipboard of loadout items, each with the place it was copied from, kept until the game
    /// closes (the system clipboard stays Copy code's). From his inventory, each item as Save takes it (Capture: the exact
    /// copy, worn as he wears it); from a loadout, its items as they are. Pasting returns a changed copy of the loadout (the
    /// window saves it), or null with why, all or nothing; it never touches a real item. Pure, so the scratch harness
    /// reaches it.
    /// </summary>
    internal static class CellClipboard
    {
        private static readonly List<LoadoutItem> Copied = new List<LoadoutItem>();

        /// <summary>Goes up at every change, for the window to redraw.</summary>
        internal static int Version { get; private set; }

        /// <summary>How many items are copied.</summary>
        internal static int Count => Copied.Count;

        /// <summary>Where they were copied from, in words ("your inventory", a loadout's name).</summary>
        internal static string From { get; private set; } = string.Empty;

        /// <summary>Copies of what's on the clipboard (a paste can't change it).</summary>
        internal static List<LoadoutItem> Items()
        {
            var items = new List<LoadoutItem>(Copied.Count);
            foreach (LoadoutItem item in Copied)
            {
                items.Add(item.Clone());
            }
            return items;
        }

        /// <summary>Puts these items on the clipboard (copies of them), in place of what was there.</summary>
        internal static void Set(IEnumerable<LoadoutItem> items, string from)
        {
            Copied.Clear();
            foreach (LoadoutItem item in items ?? new LoadoutItem[0])
            {
                if (item != null && Copied.Count < Loadout.MaxItems)
                {
                    Copied.Add(item.Clone());
                }
            }
            From = from ?? string.Empty;
            Version++;
        }

        /// <summary>Empties it.</summary>
        internal static void Clear()
        {
            if (Copied.Count > 0 || From.Length > 0)
            {
                Copied.Clear();
                From = string.Empty;
                Version++;
            }
        }

        /// <summary>The loadout's items in these cells (x, y as his inventory counts them), in the loadout's order.</summary>
        internal static List<LoadoutItem> FromLoadout(Loadout loadout, GridShape grid, ICollection<Vector2i> cells)
        {
            var items = new List<LoadoutItem>();
            if (loadout == null || grid == null || cells == null)
            {
                return items;
            }
            foreach (LoadoutItem item in loadout.Items)
            {
                if (Places.CellFor(item, grid, out int x, out int y) && cells.Contains(new Vector2i(x, y)))
                {
                    items.Add(item.Clone());
                }
            }
            return items;
        }

        /// <summary>
        /// Pastes <paramref name="items"/> into the loadout: first each into the cell it was copied from, when that cell
        /// is in this grid, free in the loadout and takes the item (one of EquipmentAndQuickSlots' gear slots only its own
        /// kind, and only while the loadout wears nothing in that place outside the gear slots, as FirstFree); then the rest, each into the first free cell, as a drawer item clicked in goes (weapons and tools from the
        /// top row down, everything else from the bottom row up; worn gear into a free gear slot of its kind first). A piece
        /// copied worn stays worn only where nothing of the loadout is worn in its place (the hands by the game's rules), and
        /// in a gear slot it's worn; otherwise it's carried. All or nothing: when they don't all find a cell (or the loadout
        /// would hold more than it may), nothing changes and <paramref name="why"/> says how many more free cells it
        /// needs. <paramref name="template"/> is the game's item for a prefab (null: this game lacks it, and it then
        /// goes only into his rows, carried).
        /// </summary>
        internal static Loadout Paste(Loadout loadout, GridShape grid, IReadOnlyList<LoadoutItem> items, Func<string, ItemDrop.ItemData> template,
                                      out string why)
        {
            why = null;
            if (loadout == null || grid == null)
            {
                why = "Pick a loadout on the left first.";
                return null;
            }
            if (items == null || items.Count == 0)
            {
                why = "Nothing is copied: highlight cells and press Copy cells first.";
                return null;
            }
            if (loadout.Items.Count + items.Count > Loadout.MaxItems)
            {
                why = $"{loadout.Name} would hold {loadout.Items.Count + items.Count} items: a loadout holds at most {Loadout.MaxItems}.";
                return null;
            }
            int slots = loadout.FoodSlots;
            foreach (LoadoutItem item in items)
            {
                if (item?.Food != null)
                {
                    slots++;
                }
            }
            if (slots > Loadout.MaxFoodSlots)
            {
                why = $"{loadout.Name} would hold {slots} food slots: a loadout holds at most {Loadout.MaxFoodSlots}.";
                return null;
            }
            Loadout changed = loadout.Clone();
            var placed = new LoadoutItem[items.Count];

            // First, each into its own cell where that's free and takes it.
            for (int i = 0; i < items.Count; i++)
            {
                LoadoutItem copy = items[i]?.Clone();
                if (copy == null)
                {
                    continue;
                }
                // A gear slot wears its piece: not while the loadout wears that place in a row already (review round 1, m8).
                if (Places.CellFor(copy, grid, out int x, out int y) && grid.Usable(x, y) && LoadoutEdit.At(changed, grid, x, y) == null &&
                    LoadoutEdit.Takes(grid, x, y, TypeOf(copy, template)) &&
                    !LoadoutEdit.WornOutsideSlots(changed, grid, LoadoutEdit.SlotWears(grid, x, y)))
                {
                    Add(changed, grid, copy, x, y, template, copy.Worn != WornAs.None);
                    placed[i] = copy;
                }
            }

            // Then the rest, each into the first free cell.
            int left = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (placed[i] != null || items[i] == null)
                {
                    continue;
                }
                LoadoutItem copy = items[i].Clone();
                ItemDrop.ItemData found = template?.Invoke(copy.Prefab);
                bool toSlot = copy.Worn != WornAs.None && found != null;
                if (FirstFree(changed, grid, found, toSlot, out int x, out int y))
                {
                    Add(changed, grid, copy, x, y, template, copy.Worn != WornAs.None);
                    placed[i] = copy;
                }
                else
                {
                    left++;
                }
            }
            if (left > 0)
            {
                // What didn't find a cell once the rest had theirs: that many more free cells (review round 1, n1).
                why = $"No room in {loadout.Name}: pasting needs {left} more free cell{(left == 1 ? "" : "s")}. Nothing changed.";
                return null;
            }
            return changed;
        }

        /// <summary>
        /// Puts one item as it is (a cell of his inventory dragged in: its maker and other mods' data kept) into a cell of
        /// the loadout, in place of what was there. Worn, with <paramref name="wear"/>, only where nothing of the loadout is
        /// worn in its place (the hands by the game's rules); in one of EquipmentAndQuickSlots' gear slots always (and only
        /// its own kind goes there). Null with why when the cell can't take it.
        /// </summary>
        internal static Loadout PutExact(Loadout loadout, GridShape grid, int x, int y, LoadoutItem item, Func<string, ItemDrop.ItemData> template,
                                         bool wear, out string why)
        {
            why = null;
            if (loadout == null || grid == null || item == null)
            {
                return null;
            }
            ItemType type = TypeOf(item, template);
            if (!LoadoutEdit.Takes(grid, x, y, type))
            {
                why = grid.SlotAt(x, y) != null ? $"The {grid.SlotAt(x, y)} slot doesn't take that." : "There's no cell there.";
                return null;
            }
            Loadout changed = loadout.Clone();
            LoadoutItem there = LoadoutEdit.At(changed, grid, x, y);
            if (there != null)
            {
                changed.Items.Remove(there);
            }
            if (changed.Items.Count >= Loadout.MaxItems)
            {
                why = $"A loadout holds at most {Loadout.MaxItems} items.";
                return null;
            }
            Add(changed, grid, item.Clone(), x, y, template, wear);
            return changed;
        }

        // Adds a pasted item at a cell: its place there, and worn only where that's free (in a gear slot always).
        private static void Add(Loadout changed, GridShape grid, LoadoutItem copy, int x, int y, Func<string, ItemDrop.ItemData> template,
                                bool wanted)
        {
            Places.Of(x, y, grid, copy);
            copy.Worn = WornAs.None;
            WornAs slotWears = LoadoutEdit.SlotWears(grid, x, y);
            ItemDrop.ItemData found = template?.Invoke(copy.Prefab);
            if (slotWears != WornAs.None)
            {
                foreach (LoadoutItem each in changed.Items)
                {
                    if (each.Worn == slotWears && !LoadoutEdit.InGearSlotHere(each, grid))   // a quick slot is no gear slot (n15)
                    {
                        each.Worn = WornAs.None;   // a gear slot's piece is the one worn there, as an edit into the slot makes it
                    }
                }
                copy.Worn = slotWears;
            }
            else if (wanted && found != null)
            {
                copy.Worn = LoadoutEdit.FreeWear(changed, found.m_shared.m_itemType, out LoadoutItem torchMovesLeft);
                if (copy.Worn != WornAs.None && torchMovesLeft != null)
                {
                    torchMovesLeft.Worn = WornAs.LeftHand;   // the game moves a torch in the right hand to the left for a weapon
                }
            }
            changed.Items.Add(copy);
        }

        // The first free cell for an item, as LoadoutEdit's (an item this game lacks: only his rows, from the bottom up).
        private static bool FirstFree(Loadout changed, GridShape grid, ItemDrop.ItemData found, bool toSlot, out int x, out int y)
        {
            if (found != null)
            {
                return LoadoutEdit.FirstFree(changed, grid, found, toSlot, out x, out y);
            }
            x = y = -1;
            for (int row = grid.Rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < grid.Width; column++)
                {
                    if (LoadoutEdit.At(changed, grid, column, row) == null)
                    {
                        x = column;
                        y = row;
                        return true;
                    }
                }
            }
            return false;
        }

        private static ItemType TypeOf(LoadoutItem item, Func<string, ItemDrop.ItemData> template)
        {
            ItemDrop.ItemData found = template?.Invoke(item.Prefab);
            return found != null ? found.m_shared.m_itemType : ItemType.Misc;
        }
    }
}
