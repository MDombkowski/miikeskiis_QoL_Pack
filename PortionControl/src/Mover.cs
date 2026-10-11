using System;
using System.Collections.Generic;
using System.Linq;

namespace PortionControl
{
    /// <summary>
    /// Every move this mod makes itself, each through the game's own drop (Inventory.MoveItemToThis with a cell), the
    /// same call the game makes when a stack on the cursor is dropped on a cell, so the source stack loses exactly what
    /// the target gains:
    /// - Max weight's "straight across" (card A49's setting) and the chest window's Max weight (A54): first onto his
    ///   stacks of the same kind that have room, in the inventory's order (as the game's own pick-up fills them), then
    ///   into the empty cell the game's own search picks (Inventory.FindEmptySlot, which EquipmentAndQuickSlots steers to
    ///   the visible rows first);
    /// - the chest window's -1 (A54): as the game's own Take all (Inventory.MoveAll), but leaving some of chosen stacks;
    /// - a right-click's split into stacks (A17): each share into an empty cell of its own.
    /// What doesn't fit stays where it was. No Unity here, so a console harness can check it against the game's classes.
    /// </summary>
    internal static class Mover
    {
        /// <summary>
        /// Moves up to <paramref name="amount"/> of <paramref name="item"/> from <paramref name="from"/> into
        /// <paramref name="to"/>, and says how many really left the source stack, measured on the stack itself.
        /// <paramref name="emptyCell"/> is the game's own search for an empty cell in <paramref name="to"/> for that item
        /// (x below 0: none).
        /// </summary>
        internal static int Across(Inventory from, ItemDrop.ItemData item, int amount, Inventory to,
                                   Func<Inventory, ItemDrop.ItemData, Vector2i> emptyCell)
        {
            if (from == null || to == null || item == null || from == to || !from.ContainsItem(item))
            {
                return 0;
            }
            int wanted = Math.Min(amount, item.m_stack);
            int moved = 0;
            // A copy of the list: every move changes it.
            foreach (ItemDrop.ItemData stack in to.GetAllItems().ToList())
            {
                if (moved >= wanted || !from.ContainsItem(item))
                {
                    break;
                }
                int room = stack.m_shared.m_maxStackSize - stack.m_stack;
                if (room <= 0 || !stack.IsSameType(item))
                {
                    continue;
                }
                moved += MoveInto(from, item, Math.Min(room, wanted - moved), to, stack.m_gridPos);
            }
            if (moved < wanted && from.ContainsItem(item) && emptyCell != null)
            {
                Vector2i cell = emptyCell(to, item);
                if (cell.x >= 0 && cell.y >= 0 && to.GetItemAt(cell.x, cell.y) == null)
                {
                    moved += MoveInto(from, item, wanted - moved, to, cell);
                }
            }
            return moved;
        }

        /// <summary>One drop onto one cell, measured: what left the source stack (all of it when the game removed the
        /// emptied stack from the source).</summary>
        private static int MoveInto(Inventory from, ItemDrop.ItemData item, int count, Inventory to, Vector2i cell)
        {
            if (count <= 0)
            {
                return 0;
            }
            int before = item.m_stack;
            to.MoveItemToThis(from, item, count, cell.x, cell.y);
            int after = from.ContainsItem(item) ? item.m_stack : 0;
            return Math.Max(0, before - after);
        }

        /// <summary>
        /// The chest window's -1 (A54): takes everything from <paramref name="from"/> into <paramref name="to"/> as the
        /// game's own Take all does (Inventory.MoveAll: each stack into the same cell first, merging with a stack of its
        /// kind there; then what is left of each onto the stacks of its kind with room and into the empty cell the game's
        /// pick-up picks), but leaves <paramref name="keep"/>'s count of each stack it names (one, in each kind's first
        /// stack). Says how many items moved.
        /// </summary>
        internal static int TakeAll(Inventory from, Inventory to, IDictionary<ItemDrop.ItemData, int> keep,
                                    Func<Inventory, ItemDrop.ItemData, Vector2i> emptyCell)
        {
            if (from == null || to == null || from == to)
            {
                return 0;
            }
            int moved = 0;
            var later = new List<ItemDrop.ItemData>();
            // A copy of the list, as the game's MoveAll takes one: every move changes it.
            foreach (ItemDrop.ItemData item in from.GetAllItems().ToList())
            {
                int wanted = Wanted(item, keep);
                if (wanted <= 0)
                {
                    continue;
                }
                moved += MoveInto(from, item, wanted, to, item.m_gridPos);
                if (from.ContainsItem(item))
                {
                    later.Add(item);
                }
            }
            foreach (ItemDrop.ItemData item in later)
            {
                int wanted = from.ContainsItem(item) ? Wanted(item, keep) : 0;
                if (wanted > 0)
                {
                    moved += Across(from, item, wanted, to, emptyCell);
                }
            }
            return moved;
        }

        private static int Wanted(ItemDrop.ItemData item, IDictionary<ItemDrop.ItemData, int> keep) =>
            item.m_stack - (keep != null && keep.TryGetValue(item, out int kept) ? kept : 0);

        /// <summary>
        /// The one of each kind the -1 (and the chest window's Max weight, when it leaves one) keeps: one of the kind's
        /// first stack as he reads the chest (top row first, left to right). A kind is the item's shared name, whatever its
        /// quality or world level; with <paramref name="stackableOnly"/>, kinds that don't stack keep nothing.
        /// </summary>
        internal static Dictionary<ItemDrop.ItemData, int> OneOfEachKind(Inventory chest, bool stackableOnly)
        {
            var keep = new Dictionary<ItemDrop.ItemData, int>();
            var seen = new HashSet<string>();
            foreach (ItemDrop.ItemData item in ReadingOrder(chest))
            {
                if (item.m_stack < 1 || (stackableOnly && item.m_shared.m_maxStackSize <= 1) || !seen.Add(item.m_shared.m_name))
                {
                    continue;
                }
                keep[item] = 1;
            }
            return keep;
        }

        /// <summary>An inventory's stacks as he reads it: the top row first, each row left to right.</summary>
        internal static List<ItemDrop.ItemData> ReadingOrder(Inventory inventory) =>
            inventory.GetAllItems().OrderBy(item => item.m_gridPos.y).ThenBy(item => item.m_gridPos.x).ToList();

        /// <summary>
        /// The chest window's Max weight (A54): takes from <paramref name="chest"/> into <paramref name="mine"/> as much as
        /// keeps him at or under <paramref name="limit"/> less <paramref name="leaveFree"/>, the chest's stacks in reading
        /// order, each as much as fits (going on past one that doesn't fit whole: a lighter one after it may), leaving
        /// <paramref name="keep"/>'s count of each stack it names. Each step is worked out against his total after the
        /// step before (<paramref name="totalOf"/>, the game's own), for the spread Across makes. Says how many moved.
        /// </summary>
        internal static int TakeMaxWeight(Inventory chest, Inventory mine, IDictionary<ItemDrop.ItemData, int> keep, float limit,
                                          float leaveFree, Func<Inventory, ItemDrop.ItemData, Vector2i> emptyCell,
                                          Func<Inventory, float> totalOf)
        {
            if (chest == null || mine == null || chest == mine)
            {
                return 0;
            }
            int moved = 0;
            foreach (ItemDrop.ItemData item in ReadingOrder(chest))
            {
                int wanted = chest.ContainsItem(item) ? Wanted(item, keep) : 0;
                if (wanted <= 0)
                {
                    continue;
                }
                int fits = Weights.MostSpread(mine.GetAllItems(), totalOf(mine), item, wanted, limit, leaveFree);
                if (fits > 0)
                {
                    moved += Across(chest, item, fits, mine, emptyCell);
                }
            }
            return moved;
        }

        /// <summary>
        /// A right-click's split (A17): moves <paramref name="sizes"/>' shares of <paramref name="item"/>, one after
        /// another, each into an empty cell of its own in <paramref name="to"/> (the cell the game's pick-up picks), never
        /// onto a stack already there. <paramref name="to"/> may be <paramref name="from"/> itself: the stack then stays as
        /// the last share, so a share as big as what is left isn't moved. Stops at the first share that has no cell (or
        /// more than is left). Says how many new stacks it made.
        /// </summary>
        internal static int IntoStacks(Inventory from, ItemDrop.ItemData item, IList<int> sizes, Inventory to,
                                       Func<Inventory, ItemDrop.ItemData, Vector2i> emptyCell)
        {
            if (from == null || to == null || item == null || sizes == null || emptyCell == null)
            {
                return 0;
            }
            int made = 0;
            foreach (int size in sizes)
            {
                if (size < 1 || !from.ContainsItem(item) || item.m_stack < size || (from == to && item.m_stack == size))
                {
                    break;
                }
                Vector2i cell = emptyCell(to, item);
                if (cell.x < 0 || cell.y < 0 || to.GetItemAt(cell.x, cell.y) != null)
                {
                    break;
                }
                if (MoveInto(from, item, size, to, cell) != size)
                {
                    break;
                }
                made++;
            }
            return made;
        }

        /// <summary>The kinds of item the game's own pick-up puts in the top rows first (Inventory.TopFirst, private in the
        /// game): weapons, tools, shields, utility items, trinkets and misc. Used only if the game's own can't be found.</summary>
        internal static bool TopFirst(ItemDrop.ItemData item)
        {
            if (item.IsWeapon())
            {
                return true;
            }
            ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
            return type == ItemDrop.ItemData.ItemType.Tool || type == ItemDrop.ItemData.ItemType.Shield ||
                   type == ItemDrop.ItemData.ItemType.Utility || type == ItemDrop.ItemData.ItemType.Misc ||
                   type == ItemDrop.ItemData.ItemType.Trinket;
        }
    }
}
