// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;

namespace QoLMods.Chests
{
    /// <summary>
    /// Plans a quick stack from counts alone (card A52): which of his stacks go to which chest, and how much of each. It
    /// knows nothing of Unity, so the scratch test harness checks it against the game's own adding. The game stacks items
    /// together only with the same name, quality and world level, up to the item's stack size, and adds a stack by growing
    /// those part-stacks first and then putting the rest into one empty cell (Inventory.AddItem); the plan follows the same
    /// rules, so every put it plans fits.
    /// </summary>
    internal static class StackPlanner
    {
        /// <summary>One of his stacks, as the plan sees it.</summary>
        internal sealed class Item
        {
            internal string Name;
            internal int Quality;
            internal int WorldLevel;
            internal int Amount;

            /// <summary>The item's stack size (1 for gear, which never shares a cell).</summary>
            internal int MaxStack = 1;
        }

        /// <summary>One chest, as the plan sees it: its cells and the stacks in them.</summary>
        internal sealed class Chest
        {
            /// <summary>How many cells it has (width times height).</summary>
            internal int Cells;

            /// <summary>Its stacks: each takes a cell.</summary>
            internal readonly List<Item> Stacks = new List<Item>();
        }

        /// <summary>A put the plan makes: that much of one of his stacks into one chest, into its part-stacks only
        /// (TopUpOnly) or wherever the game's adding puts it (part-stacks first, then one empty cell).</summary>
        internal sealed class Put
        {
            internal int Item;
            internal int Chest;
            internal int Amount;
            internal bool TopUpOnly;
        }

        /// <summary>
        /// The plan, in the order to carry it out. <paramref name="candidates"/> gives, for each item (by its index), the
        /// chests (by index) that may take it, best first (StackRouting). With <paramref name="topUpFirst"/>, every item
        /// first tops up part-stacks in all its chests, in that order, and only then does anything go into an empty cell;
        /// without it, chest by chest: each chest's part-stacks, then an empty cell there, before the next chest. Each of his
        /// stacks is at most one stack size, so it needs at most one new cell.
        /// </summary>
        internal static List<Put> Plan(IList<Item> items, IList<Chest> chests, Func<int, IList<int>> candidates, bool topUpFirst) =>
            Plan(items, chests, i => new[] { candidates(i) }, topUpFirst);

        /// <summary>
        /// The plan, with each item's chests in tiers (StackRouting.Tiers: a tier for each step of the caller's order, by
        /// default the set-up chests that take it, the more specific and the fuller first, then the rest): the first tier of
        /// every item is filled as the plan above fills its chests, then the second with what is left, and so on. So with
        /// part-stacks first, a set-up chest's empty cells are used before another chest's part-stack is topped up. One tier
        /// each is the plan above.
        /// </summary>
        internal static List<Put> Plan(IList<Item> items, IList<Chest> chests, Func<int, IList<IList<int>>> tiers, bool topUpFirst)
        {
            var model = new List<Room>();
            foreach (Chest chest in chests)
            {
                model.Add(new Room(chest));
            }
            var left = new int[items.Count];
            int deepest = 0;
            for (int i = 0; i < items.Count; i++)
            {
                left[i] = Math.Max(0, items[i].Amount);
                deepest = Math.Max(deepest, tiers(i).Count);
            }
            var plan = new List<Put>();
            for (int tier = 0; tier < deepest; tier++)
            {
                int at = tier;
                PlanTier(plan, model, items, left, i => at < tiers(i).Count ? tiers(i)[at] : (IList<int>)Array.Empty<int>(), topUpFirst);
            }
            return plan;
        }

        // One tier of every item's chests, as the summary of the first Plan says.
        private static void PlanTier(List<Put> plan, List<Room> model, IList<Item> items, int[] left, Func<int, IList<int>> candidates, bool topUpFirst)
        {
            if (topUpFirst)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    foreach (int c in candidates(i))
                    {
                        if (left[i] <= 0)
                        {
                            break;
                        }
                        TopUp(plan, model[c], items[i], i, c, left);
                    }
                }
                for (int i = 0; i < items.Count; i++)
                {
                    foreach (int c in candidates(i))
                    {
                        if (left[i] <= 0)
                        {
                            break;
                        }
                        // A stack an earlier one of his just started in this chest has room too, and the game's adding
                        // tops it up before it takes a new cell.
                        TopUp(plan, model[c], items[i], i, c, left);
                        IntoEmptyCell(plan, model[c], items[i], i, c, left);
                    }
                }
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    foreach (int c in candidates(i))
                    {
                        if (left[i] <= 0)
                        {
                            break;
                        }
                        int before = left[i];
                        TopUp(plan, model[c], items[i], i, c, left);
                        IntoEmptyCell(plan, model[c], items[i], i, c, left);
                        // Topping up and a new cell in the same chest are one put, as the game's adding does both.
                        Merge(plan, i, c, before - left[i]);
                    }
                }
            }
        }

        // Into the chest's part-stacks of the same item, as far as they have room.
        private static void TopUp(List<Put> plan, Room room, Item item, int i, int c, int[] left)
        {
            int space = room.FreeSpace(item);
            int amount = Math.Min(left[i], space);
            if (amount <= 0)
            {
                return;
            }
            room.Fill(item, amount);
            left[i] -= amount;
            plan.Add(new Put { Item = i, Chest = c, Amount = amount, TopUpOnly = true });
        }

        // The rest of one of his stacks into one empty cell of the chest (the game's adding tops up first, which the plan has
        // already done wherever there was room, so this lands in the cell): a new stack, whose room is left for the next.
        private static void IntoEmptyCell(List<Put> plan, Room room, Item item, int i, int c, int[] left)
        {
            if (left[i] <= 0 || room.EmptyCells <= 0)
            {
                return;
            }
            int amount = Math.Min(left[i], Math.Max(1, item.MaxStack));
            room.NewStack(item, amount);
            left[i] -= amount;
            plan.Add(new Put { Item = i, Chest = c, Amount = amount, TopUpOnly = false });
        }

        // Without "part-stacks first", a top-up and a new cell in the same chest become one put that isn't top-up only.
        private static void Merge(List<Put> plan, int i, int c, int moved)
        {
            int count = 0;
            for (int k = plan.Count - 1; k >= 0 && plan[k].Item == i && plan[k].Chest == c; k--)
            {
                count++;
            }
            if (count <= 1 || moved <= 0)
            {
                return;
            }
            plan.RemoveRange(plan.Count - count, count);
            plan.Add(new Put { Item = i, Chest = c, Amount = moved, TopUpOnly = false });
        }

        // A chest's room as the plan fills it.
        private sealed class Room
        {
            private readonly List<Item> stacks = new List<Item>();

            internal Room(Chest chest)
            {
                foreach (Item stack in chest.Stacks)
                {
                    stacks.Add(new Item { Name = stack.Name, Quality = stack.Quality, WorldLevel = stack.WorldLevel, Amount = stack.Amount, MaxStack = stack.MaxStack });
                }
                EmptyCells = Math.Max(0, chest.Cells - chest.Stacks.Count);
            }

            internal int EmptyCells { get; private set; }

            internal int FreeSpace(Item like)
            {
                if (like.MaxStack <= 1)
                {
                    return 0;
                }
                int space = 0;
                foreach (Item stack in stacks)
                {
                    if (Same(stack, like) && stack.Amount < stack.MaxStack)
                    {
                        space += stack.MaxStack - stack.Amount;
                    }
                }
                return space;
            }

            // The game fills the first part-stack it finds, one at a time (Inventory.FindFreeStackItem): the totals are all
            // that matter here.
            internal void Fill(Item like, int amount)
            {
                foreach (Item stack in stacks)
                {
                    if (amount <= 0)
                    {
                        break;
                    }
                    if (Same(stack, like) && stack.Amount < stack.MaxStack)
                    {
                        int take = Math.Min(amount, stack.MaxStack - stack.Amount);
                        stack.Amount += take;
                        amount -= take;
                    }
                }
            }

            internal void NewStack(Item like, int amount)
            {
                stacks.Add(new Item { Name = like.Name, Quality = like.Quality, WorldLevel = like.WorldLevel, Amount = amount, MaxStack = like.MaxStack });
                EmptyCells--;
            }

            private static bool Same(Item a, Item b) => a.Name == b.Name && a.Quality == b.Quality && a.WorldLevel == b.WorldLevel;
        }
    }
}
