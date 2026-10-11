using System;
using System.Collections.Generic;

namespace PortionControl
{
    /// <summary>
    /// Max weight's number (card A49): the most of a chest's stack he can carry without being encumbered, and its mirror,
    /// the fewest of his own stack to put down to stop being encumbered. Worked out with the game's own sums, so every
    /// effect and mod that changes an item's weight or his limit counts:
    /// - the limit is Player.GetMaxCarryWeight() (300, plus Megingjord, meads and powers, times the world's setting);
    /// - the game calls him encumbered only when his total is strictly above it (Player.IsEncumbered), so exactly at the
    ///   limit is fine;
    /// - his total is the game's Inventory.GetTotalWeight(), which it keeps as a float sum of every stack's GetWeight(),
    ///   added in the inventory's own order (Inventory.UpdateTotalWeight).
    /// A candidate is checked by redoing that sum exactly as the game will once the items land, for every way they could
    /// land (a new stack, or merged into each of his stacks of the same kind with room), and the number steps down by one
    /// until the worst of them keeps him at or under the limit: with fractional weights (coins weigh 0.1) the last step
    /// can round over. No Unity here, so a console harness can check it against the game's own classes.
    /// </summary>
    internal static class Weights
    {
        /// <summary>A float field to add into, as the game adds into its own field (Inventory.m_totalWeight), so each step
        /// is rounded to a float the same way.</summary>
        private sealed class Sum
        {
            internal float Value;
        }

        /// <summary>
        /// The total the game will work out for <paramref name="items"/> (an inventory's own list, in its order) once
        /// <paramref name="changed"/> holds <paramref name="changedStack"/> (0: gone), and <paramref name="added"/>, if
        /// given, has landed as a new stack of <paramref name="addedStack"/> at the end of the list, where the game adds a
        /// new stack.
        /// </summary>
        internal static float TotalAfter(IList<ItemDrop.ItemData> items, ItemDrop.ItemData changed, int changedStack,
                                         ItemDrop.ItemData added, int addedStack)
        {
            var sum = new Sum();
            foreach (ItemDrop.ItemData item in items)
            {
                if (item == changed)
                {
                    if (changedStack > 0)
                    {
                        sum.Value += item.GetWeight(changedStack);
                    }
                    continue;
                }
                sum.Value += item.GetWeight();
            }
            if (added != null && addedStack > 0)
            {
                sum.Value += added.GetWeight(addedStack);
            }
            return sum.Value;
        }

        /// <summary>
        /// How far the game's own total (<paramref name="gameTotal"/>, Inventory.GetTotalWeight()) is from the sum redone
        /// here: 0 when they agree, as they do in the unmodded game. A mod that changes the total itself (rather than an
        /// item's weight, which the sum here already includes) shows as an offset, added to every total worked out here.
        /// </summary>
        internal static float Offset(IList<ItemDrop.ItemData> items, float gameTotal) =>
            gameTotal - TotalAfter(items, null, 0, null, 0);

        /// <summary>A small disagreement between the two sums is rounding, not a mod: the answer then keeps that much in
        /// hand instead of trusting either.</summary>
        private const float RoundingOnly = 0.01f;

        /// <summary>
        /// The most of <paramref name="theirs"/> (a stack in a chest) he can take and still be at or under
        /// <paramref name="limit"/> less <paramref name="leaveFree"/>, whichever way the items land in
        /// <paramref name="mine"/> (his inventory's own list). 0 when he is already there or over.
        /// </summary>
        internal static int MaxTake(IList<ItemDrop.ItemData> mine, float gameTotal, ItemDrop.ItemData theirs, float limit, float leaveFree)
        {
            if (theirs == null || theirs.m_stack < 1)
            {
                return 0;
            }
            float target = limit - Math.Max(0f, leaveFree);
            float offset = Offset(mine, gameTotal);
            float slack = Math.Abs(offset) < RoundingOnly ? Math.Abs(offset) : 0f;
            if (Math.Abs(offset) < RoundingOnly)
            {
                offset = 0f;
            }
            float now = TotalAfter(mine, null, 0, null, 0) + offset;
            if (now + slack > target)
            {
                return 0;
            }
            int stack = theirs.m_stack;
            float one = theirs.GetWeight(1);
            int start = stack;
            if (one > 0f)
            {
                double estimate = Math.Floor((target - now) / (double)one) + 1d;
                if (!double.IsNaN(estimate) && estimate < stack)
                {
                    start = Math.Max(0, (int)estimate);
                }
            }
            for (int take = start; take > 0; take--)
            {
                if (WorstAfterTaking(mine, theirs, take) + offset + slack <= target)
                {
                    return take;
                }
            }
            return 0;
        }

        /// <summary>The highest total the game could work out once <paramref name="take"/> of <paramref name="theirs"/>
        /// has landed in his inventory: as a new stack, or merged into any stack of his of the same kind that has room (up
        /// to that room; the rest stays in the chest).</summary>
        internal static float WorstAfterTaking(IList<ItemDrop.ItemData> mine, ItemDrop.ItemData theirs, int take)
        {
            float worst = TotalAfter(mine, null, 0, theirs, take);
            foreach (ItemDrop.ItemData stack in mine)
            {
                int room = stack.m_shared.m_maxStackSize - stack.m_stack;
                if (room <= 0 || !stack.IsSameType(theirs))
                {
                    continue;
                }
                int merged = Math.Min(room, take);
                worst = Math.Max(worst, TotalAfter(mine, stack, stack.m_stack + merged, null, 0));
            }
            return worst;
        }

        /// <summary>
        /// The total the game will work out once <paramref name="take"/> of <paramref name="theirs"/> has been moved
        /// straight across the way Mover.Across moves it: onto his stacks of the same kind with room, in his list's order,
        /// then the rest as a new stack at the end of the list. Spread over several stacks it is summed in a different
        /// order than any single landing, so a weight not exact in binary (0.1) can round one step apart (review round 2).
        /// </summary>
        internal static float TotalAfterSpread(IList<ItemDrop.ItemData> mine, ItemDrop.ItemData theirs, int take)
        {
            var sum = new Sum();
            int left = take;
            foreach (ItemDrop.ItemData item in mine)
            {
                int room = item.m_shared.m_maxStackSize - item.m_stack;
                if (left > 0 && room > 0 && item.IsSameType(theirs))
                {
                    int merged = Math.Min(room, left);
                    left -= merged;
                    sum.Value += item.GetWeight(item.m_stack + merged);
                    continue;
                }
                sum.Value += item.GetWeight();
            }
            if (left > 0)
            {
                sum.Value += theirs.GetWeight(left);
            }
            return sum.Value;
        }

        /// <summary>
        /// For straight across: <paramref name="take"/> (Max weight's number), stepped down until spreading it the way
        /// Mover.Across does keeps him at or under <paramref name="limit"/> less <paramref name="leaveFree"/>, with the
        /// same allowance for a mod's change to his total as MaxTake.
        /// </summary>
        internal static int FitSpread(IList<ItemDrop.ItemData> mine, float gameTotal, ItemDrop.ItemData theirs, int take, float limit, float leaveFree)
        {
            if (theirs == null)
            {
                return 0;
            }
            float target = limit - Math.Max(0f, leaveFree);
            float offset = Offset(mine, gameTotal);
            float slack = 0f;
            if (Math.Abs(offset) < RoundingOnly)
            {
                slack = Math.Abs(offset);
                offset = 0f;
            }
            while (take > 0 && TotalAfterSpread(mine, theirs, take) + offset + slack > target)
            {
                take--;
            }
            return take;
        }

        /// <summary>
        /// The chest window's Max weight (A54), one stack at a time: the most of <paramref name="theirs"/>, up to
        /// <paramref name="cap"/>, that spread the way Mover.Across spreads it keeps him at or under
        /// <paramref name="limit"/> less <paramref name="leaveFree"/>. Starts from the same estimate as MaxTake (a step
        /// above what the weights say) and steps down with FitSpread, so the answer is the most that fits. 0 when he is
        /// already there or over.
        /// </summary>
        internal static int MostSpread(IList<ItemDrop.ItemData> mine, float gameTotal, ItemDrop.ItemData theirs, int cap, float limit, float leaveFree)
        {
            if (theirs == null || cap < 1 || theirs.m_stack < 1)
            {
                return 0;
            }
            float target = limit - Math.Max(0f, leaveFree);
            float offset = Offset(mine, gameTotal);
            float slack = Math.Abs(offset) < RoundingOnly ? Math.Abs(offset) : 0f;
            if (Math.Abs(offset) < RoundingOnly)
            {
                offset = 0f;
            }
            float now = TotalAfter(mine, null, 0, null, 0) + offset;
            if (now + slack > target)
            {
                return 0;
            }
            int start = Math.Min(cap, theirs.m_stack);
            float one = theirs.GetWeight(1);
            if (one > 0f)
            {
                double estimate = Math.Floor((target - now) / (double)one) + 1d;
                if (!double.IsNaN(estimate) && estimate < start)
                {
                    start = Math.Max(0, (int)estimate);
                }
            }
            return FitSpread(mine, gameTotal, theirs, start, limit, leaveFree);
        }

        /// <summary>
        /// The fewest of <paramref name="ours"/> (a stack in his own inventory, <paramref name="mine"/>) to put down to be
        /// at or under <paramref name="limit"/> less <paramref name="leaveFree"/>. 0 when he already is, and 0 when even
        /// the whole stack isn't enough (the button then can't do what it says).
        /// </summary>
        internal static int JustEnough(IList<ItemDrop.ItemData> mine, float gameTotal, ItemDrop.ItemData ours, float limit, float leaveFree)
        {
            if (ours == null || ours.m_stack < 1 || !mine.Contains(ours))
            {
                return 0;
            }
            float target = limit - Math.Max(0f, leaveFree);
            float offset = Offset(mine, gameTotal);
            float slack = Math.Abs(offset) < RoundingOnly ? Math.Abs(offset) : 0f;
            if (Math.Abs(offset) < RoundingOnly)
            {
                offset = 0f;
            }
            float now = TotalAfter(mine, null, 0, null, 0) + offset;
            if (now + slack <= target)
            {
                return 0;
            }
            int stack = ours.m_stack;
            float one = ours.GetWeight(1);
            int start = 1;
            if (one > 0f)
            {
                double estimate = Math.Ceiling((now - target) / (double)one) - 1d;
                if (!double.IsNaN(estimate) && estimate > 1d)
                {
                    start = (int)Math.Min(stack, estimate);
                }
            }
            // Step back first, in case the estimate overshot, so the answer is the fewest.
            while (start > 1 && Fine(mine, ours, stack - (start - 1), offset, slack, target))
            {
                start--;
            }
            for (int put = start; put <= stack; put++)
            {
                if (Fine(mine, ours, stack - put, offset, slack, target))
                {
                    return put;
                }
            }
            return 0;
        }

        private static bool Fine(IList<ItemDrop.ItemData> mine, ItemDrop.ItemData ours, int left, float offset, float slack, float target) =>
            TotalAfter(mine, ours, left, null, 0) + offset + slack <= target;
    }
}
