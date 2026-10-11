using System;
using System.Collections.Generic;
using System.Linq;

namespace StoreStash
{
    /// <summary>One stack in the part of a grid being sorted, as the sort sees it: plain values only, so the scratch test
    /// harness can plan without the game. <see cref="Item"/> carries the game's own item through, untouched.</summary>
    internal sealed class SortEntry
    {
        /// <summary>The game's item (ItemDrop.ItemData); the plan never looks at it.</summary>
        internal object Item;

        /// <summary>Where it is now.</summary>
        internal int X;

        internal int Y;

        /// <summary>How many it holds now.</summary>
        internal int Stack;

        /// <summary>How many one stack of it may hold (1 for gear).</summary>
        internal int MaxStack = 1;

        /// <summary>What it may merge with: stacks with the same key are the same item (name, quality, world level and
        /// the rest), so moving amounts between them changes nothing but the amounts. Null: never merged.</summary>
        internal string MergeKey;

        /// <summary>Kept exactly as it is: its cell, its amount (the hotbar, what he wears, his favourites, the stack in
        /// his hand). Other stacks flow around it.</summary>
        internal bool Pinned;

        /// <summary>Pinned, but its amount may merge with the other pinned stacks like it that may too (his favourites
        /// kept in place, "Merge favorite stacks", card A82): its cell stays, its amount may change, and it may be emptied
        /// into one of them, freeing its cell. Only with a MergeKey.</summary>
        internal bool MergeInPlace;

        /// <summary>Its place in reading order before the sort (row by row, left to right): the last word between two
        /// stacks the order calls equal, so a sorted grid sorted again stays as it is.</summary>
        internal int Index;

        /// <summary>What the order compares (SortOrder).</summary>
        internal SortKey Key;

        /// <summary>The plan: its new cell and amount. An amount of 0 means it was merged into others and leaves the
        /// grid.</summary>
        internal int NewX;

        internal int NewY;

        internal int NewStack;

        internal bool Removed => NewStack == 0;

        internal bool Changed => NewX != X || NewY != Y || NewStack != Stack;
    }

    /// <summary>What a plan did, for the caller to say and to decide whether anything needs saving.</summary>
    internal sealed class SortOutcome
    {
        /// <summary>Stacks now in another cell.</summary>
        internal int Moved;

        /// <summary>Stacks merged into others and gone.</summary>
        internal int Merged;

        /// <summary>Why nothing was planned, if nothing could be (a check failed); null when the plan stands.</summary>
        internal string Refused;

        internal bool AnyChange => Refused == null && (Moved > 0 || Merged > 0 || AmountsChanged);

        /// <summary>Stacks whose amount changed without moving (topped up by a merge).</summary>
        internal bool AmountsChanged;
    }

    /// <summary>
    /// Plans a sort of one part of a grid (card A53): the rows from <c>top</c> up to <c>bottom</c>, the whole width. Pinned
    /// stacks keep their cells and amounts, except that those marked MergeInPlace merge among themselves (card A82: his
    /// favourites, each staying in its cell); the rest are merged where they are the same item (if asked), put in the order
    /// given, and laid into the free cells row by row, left to right, from the first free cell (or ending at the last, so
    /// the empty cells are at the top). Nothing is created: a merge only moves amounts between stacks of one item and
    /// empties the ones no longer needed. Plain arithmetic, no game or engine, so the harness checks it on every case.
    /// </summary>
    internal static class SortPlan
    {
        internal static SortOutcome Plan(List<SortEntry> entries, int width, int top, int bottom, bool merge, bool fromBottom,
                                         Comparison<SortEntry> order)
        {
            var outcome = new SortOutcome();
            foreach (SortEntry entry in entries)
            {
                entry.NewX = entry.X;
                entry.NewY = entry.Y;
                entry.NewStack = entry.Stack;
            }
            if (width <= 0 || bottom <= top)
            {
                outcome.Refused = "nothing to sort";
                return outcome;
            }

            List<SortEntry> movable = entries.Where(entry => !entry.Pinned).ToList();
            if (merge)
            {
                Merge(movable);
                MergeInPlace(entries.Where(entry => entry.Pinned && entry.MergeInPlace).ToList());
            }
            List<SortEntry> placed = movable.Where(entry => !entry.Removed).ToList();
            placed.Sort((a, b) =>
            {
                int byOrder = order(a, b);
                return byOrder != 0 ? byOrder : a.Index.CompareTo(b.Index);
            });

            // A pinned stack emptied by a merge in place leaves its cell to the rest.
            var taken = new HashSet<int>(entries.Where(entry => entry.Pinned && !entry.Removed).Select(entry => Cell(entry.X, entry.Y, width)));
            var free = new List<(int X, int Y)>();
            for (int y = top; y < bottom; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!taken.Contains(Cell(x, y, width)))
                    {
                        free.Add((x, y));
                    }
                }
            }
            if (placed.Count > free.Count)
            {
                // Can't happen when every stack came from these rows (a merge only lowers the count); refused, never
                // squeezed, if a caller ever hands in more.
                outcome.Refused = $"{placed.Count} stacks for {free.Count} free cells";
                Reset(entries);
                return outcome;
            }
            int offset = fromBottom ? free.Count - placed.Count : 0;
            for (int i = 0; i < placed.Count; i++)
            {
                placed[i].NewX = free[offset + i].X;
                placed[i].NewY = free[offset + i].Y;
            }

            string broken = Check(entries, width, top, bottom);
            if (broken != null)
            {
                outcome.Refused = broken;
                Reset(entries);
                return outcome;
            }
            foreach (SortEntry entry in entries)
            {
                if (entry.Removed)
                {
                    outcome.Merged++;
                }
                else if (entry.NewX != entry.X || entry.NewY != entry.Y)
                {
                    outcome.Moved++;
                }
                else if (entry.NewStack != entry.Stack)
                {
                    outcome.AmountsChanged = true;
                }
            }
            return outcome;
        }

        /// <summary>
        /// Merges each item's part-stacks: the item's whole amount goes into as few stacks as its stack size allows, kept
        /// in the stacks that were fullest (then the first in reading order), the others emptied. An item whose amount
        /// already needs more stacks than it has (a stack over its size, from another mod) is left as it is: a merge never
        /// makes a stack.
        /// </summary>
        private static void Merge(List<SortEntry> movable)
        {
            foreach (IGrouping<string, SortEntry> group in movable.Where(entry => entry.MergeKey != null && entry.MaxStack > 1).GroupBy(entry => entry.MergeKey))
            {
                Fill(group.OrderByDescending(entry => entry.Stack).ThenBy(entry => entry.Index).ToList());
            }
        }

        /// <summary>
        /// Merges each item's stacks that are kept in place but may merge (MergeInPlace: his favourites, card A82): the
        /// item's whole amount goes into as few of them as its stack size allows, the first in reading order first (top
        /// left first, so a hotbar sorted with the rest keeps its stack), each staying in its cell; the others are emptied
        /// and leave, their cells free for the rest. Never with the stacks that move, and an item whose amount already
        /// needs more stacks than it has is left as it is, as Merge leaves one.
        /// </summary>
        private static void MergeInPlace(List<SortEntry> kept)
        {
            foreach (IGrouping<string, SortEntry> group in kept.Where(entry => entry.MergeKey != null && entry.MaxStack > 1).GroupBy(entry => entry.MergeKey))
            {
                Fill(group.OrderBy(entry => entry.Index).ToList());
            }
        }

        // One item's stacks, in the order they're filled: the amount into as few of them as fit, the rest emptied; left as
        // they are when fewer than two, or when the amount already needs more stacks than there are.
        private static void Fill(List<SortEntry> stacks)
        {
            if (stacks.Count < 2)
            {
                return;
            }
            int size = stacks.Min(entry => entry.MaxStack);
            long total = stacks.Sum(entry => (long)entry.Stack);
            long needed = (total + size - 1) / size;
            if (needed > stacks.Count || stacks.Any(entry => entry.Stack <= 0))
            {
                return;
            }
            long left = total;
            for (int i = 0; i < stacks.Count; i++)
            {
                int amount = (int)Math.Min(size, left);
                stacks[i].NewStack = amount;
                left -= amount;
            }
        }

        /// <summary>
        /// Whether a plan is safe to carry out, or what's wrong with it: every stack kept inside the rows sorted and never
        /// two in one cell; pinned stacks untouched (a MergeInPlace one may change its amount or be emptied, never move); no
        /// amount above the stack size unless it was already; and every item's amount the same as before (by merge key, or by
        /// the stack itself). Run on every plan before it's carried out, and by the harness.
        /// </summary>
        internal static string Check(List<SortEntry> entries, int width, int top, int bottom)
        {
            var cells = new HashSet<int>();
            var before = new Dictionary<string, long>();
            var after = new Dictionary<string, long>();
            for (int i = 0; i < entries.Count; i++)
            {
                SortEntry entry = entries[i];
                string key = entry.MergeKey ?? "#" + i;
                before[key] = (before.TryGetValue(key, out long had) ? had : 0) + entry.Stack;
                if (entry.Pinned && (entry.NewX != entry.X || entry.NewY != entry.Y || (entry.NewStack != entry.Stack && !(entry.MergeInPlace && entry.MergeKey != null))))
                {
                    return "a kept stack would have changed";
                }
                if (entry.NewStack < 0 || (entry.NewStack > entry.MaxStack && entry.NewStack != entry.Stack))
                {
                    return "an amount would have gone past its stack size";
                }
                if (entry.Removed)
                {
                    if (entry.MergeKey == null || (entry.Pinned && !entry.MergeInPlace))
                    {
                        return "a stack that can't merge would have gone";
                    }
                    continue;
                }
                after[key] = (after.TryGetValue(key, out long has) ? has : 0) + entry.NewStack;
                if (entry.NewX < 0 || entry.NewX >= width || entry.NewY < top || entry.NewY >= bottom)
                {
                    return "a stack would have left the rows being sorted";
                }
                if (!cells.Add(Cell(entry.NewX, entry.NewY, width)))
                {
                    return "two stacks would have shared a cell";
                }
            }
            foreach (KeyValuePair<string, long> item in before)
            {
                if (!after.TryGetValue(item.Key, out long now) || now != item.Value)
                {
                    return "an item's amount would have changed";
                }
            }
            return null;
        }

        private static void Reset(List<SortEntry> entries)
        {
            foreach (SortEntry entry in entries)
            {
                entry.NewX = entry.X;
                entry.NewY = entry.Y;
                entry.NewStack = entry.Stack;
            }
        }

        private static int Cell(int x, int y, int width) => y * width + x;
    }
}
