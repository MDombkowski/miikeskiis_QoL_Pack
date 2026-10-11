using System;
using System.Collections.Generic;
using Outfitter.Loadouts;

namespace Outfitter.Equip
{
    /// <summary>What a plan chose for one food slot: the food (its prefab and the game's template for it), or none, and
    /// then why.</summary>
    internal sealed class FoodChoice
    {
        internal string Prefab = string.Empty;

        /// <summary>The game's item for the food; null when nothing fills the slot.</summary>
        internal ItemDrop.ItemData Template;

        /// <summary>Why nothing fills it (null when something does).</summary>
        internal string Why;
    }

    /// <summary>One kind of food the slots could take (the game's "one of each": its $ name), and how many an equip
    /// could bring of it.</summary>
    internal sealed class FoodKind
    {
        /// <summary>Its $ name: the game lets him eat one of each (Player.CanEat, 1.0.17).</summary>
        internal string Name = string.Empty;

        /// <summary>The prefab a slot asks for (of a $ name two prefabs share, the first by name).</summary>
        internal string Prefab = string.Empty;

        internal ItemDrop.ItemData Template;

        /// <summary>How many he carries.</summary>
        internal int Carried;

        /// <summary>How many the chests could give this equip (as a want of it takes them: EquipPlanner.PlanStackable).</summary>
        internal int InChests;

        /// <summary>A food the loadout names itself (an item of the same $ name): never a slot's.</summary>
        internal bool Named;

        internal int Total => Carried + InChests;
    }

    /// <summary>
    /// Filling a loadout's food slots at an equip (card A34, decision 72; his words, 2026-10-06), before the plan makes its
    /// wants (EquipPlanner.Plan): each slot gets one kind of food that meets its rule, from what he carries and what the
    /// chests nearby could give this equip, by the rules a want takes by ("Take the loadout's items from", one left in each
    /// chest by "Leave one of each item in chests", the world's level). No food fills two slots, nor a slot and a food the
    /// loadout names itself (by its $ name: the game lets him eat one of each). The choice: the most slots filled; then the
    /// most filled to their whole count (as many as the slot asks, up to one stack), where a slot putting its stat first
    /// (FoodRule.First, 0.5.0) counts as filled by any food with at least "Food slots by stat: at least" nearby (or its
    /// count, if fewer); then each slot, in the loadout's order,
    /// its most-preferred food among those that still let the rest be filled as well; ties (foods a slot likes as much) to
    /// the choice giving the most slots a food he already carries, then by name. It reads totals (what he carries and what
    /// the chests could give together), never where they sit, so the moves of the equip it plans, which only shift food
    /// between him and the chests, leave the same choice: a second press finds nothing to do (LESSONS_LEARNED, 36-B). Pure
    /// apart from the game's plain item classes, so the scratch harness reaches it.
    /// </summary>
    internal static class FoodPick
    {
        /// <summary>Whether an item (the game's template for it) is a food: a consumable the game eats as food, which is
        /// one giving Health (Player.ConsumeItem and CanConsumeItem, 1.0.17, ask only m_food; review round 1, n4).</summary>
        internal static bool IsFood(ItemDrop.ItemData template)
        {
            ItemDrop.ItemData.SharedData shared = template?.m_shared;
            return shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable && shared.m_food > 0f;
        }

        /// <summary>A food's stat (All: the three together).</summary>
        internal static float Stat(ItemDrop.ItemData template, FoodStat stat)
        {
            ItemDrop.ItemData.SharedData shared = template.m_shared;
            switch (stat)
            {
                case FoodStat.Health:
                    return shared.m_food;
                case FoodStat.Stamina:
                    return shared.m_foodStamina;
                case FoodStat.Eitr:
                    return shared.m_foodEitr;
                default:
                    return shared.m_food + shared.m_foodStamina + shared.m_foodEitr;
            }
        }

        /// <summary>Whether a food meets a slot's rule: it is a food, and by stats it is in every range given (none given:
        /// any food), from a list its prefab is ticked.</summary>
        internal static bool Meets(FoodRule rule, string prefab, ItemDrop.ItemData template)
        {
            if (rule == null || !IsFood(template))
            {
                return false;
            }
            if (rule.Mode == FoodMode.FromList)
            {
                return prefab != null && rule.Foods.Contains(prefab);
            }
            return rule.Health.Holds(template.m_shared.m_food) && rule.Stamina.Holds(template.m_shared.m_foodStamina) &&
                   rule.Eitr.Holds(template.m_shared.m_foodEitr);
        }

        /// <summary>A food's worth to a slot: the stat its rule prefers, higher first for Most, lower first for Least
        /// (the sign: bigger is always better here).</summary>
        internal static double Worth(FoodRule rule, ItemDrop.ItemData template)
        {
            double value = Stat(template, rule.Of);
            return rule.Prefer == FoodPrefer.Least ? -value : value;
        }

        /// <summary>How many a slot brings of a food: its count, at most one stack.</summary>
        internal static int Target(LoadoutItem slot, ItemDrop.ItemData template) =>
            Math.Max(1, Math.Min(slot.Count, Math.Max(1, template.m_shared.m_maxStackSize)));

        /// <summary>How many of a food nearby fill a slot well enough to count it as "whole" (his words, 2026-10-07: "the slot
        /// prioritizes the stats than the amounts ... prioritize the stat as long as at least X are available"): its whole
        /// count (<see cref="Target"/>) when it puts the amount first; when it puts its stat first, <paramref name="statAtLeast"/>
        /// (at least 1), or its whole count if that's fewer.</summary>
        internal static int Enough(LoadoutItem slot, ItemDrop.ItemData template, int statAtLeast)
        {
            int target = Target(slot, template);
            return slot.Food != null && slot.Food.First == FoodFirst.Stat ? Math.Min(target, Math.Max(1, statAtLeast)) : target;
        }

        /// <summary>Why a slot no food fills is empty, in words: an empty list; a list of only what this game has no food
        /// for; foods nearby meet it but each is the loadout's own (an item of it, or another slot's); or none nearby
        /// meets it.</summary>
        internal static string WhyNone(FoodRule rule, IEnumerable<FoodKind> kinds, Func<string, ItemDrop.ItemData> templateOf)
        {
            if (rule.Mode == FoodMode.FromList)
            {
                if (rule.Foods.Count == 0)
                {
                    return "no food is ticked for it";
                }
                bool any = false;
                foreach (string prefab in rule.Foods)
                {
                    any |= IsFood(templateOf?.Invoke(prefab));
                }
                if (!any)
                {
                    return "none of its foods is in your game";
                }
            }
            foreach (FoodKind kind in kinds)
            {
                if (kind.Total > 0 && Meets(rule, kind.Prefab, kind.Template))
                {
                    return "every food nearby that meets it is already in this loadout";
                }
            }
            return "no food nearby meets it";
        }

        /// <summary>
        /// Each food slot's food, by the loadout's order: what the plan's input says when it carries the equip's earlier
        /// choice (PlanInput.FoodsChosen: an equip's later plans keep its first), else chosen now from what he carries and
        /// the chests in the input. The first <see cref="Loadout.MaxFoodSlots"/> slots are filled; any beyond (a loadout
        /// never holds more) get nothing.
        /// </summary>
        internal static Dictionary<LoadoutItem, FoodChoice> Choose(PlanInput input)
        {
            var choices = new Dictionary<LoadoutItem, FoodChoice>();
            var slots = new List<LoadoutItem>();
            foreach (LoadoutItem item in input.Loadout.Items)
            {
                if (item.Food != null)
                {
                    slots.Add(item);
                }
            }
            if (slots.Count == 0)
            {
                return choices;
            }
            if (input.FoodsChosen != null)
            {
                foreach (LoadoutItem slot in slots)
                {
                    choices[slot] = input.FoodsChosen.TryGetValue(slot, out FoodChoice chosen) && chosen != null
                        ? chosen
                        : new FoodChoice { Why = "no food nearby meets it" };
                }
                return choices;
            }
            List<FoodKind> all = Kinds(input);
            var offered = all.FindAll(kind => !kind.Named);   // (none of a kind nearby: Assign gives it no slot)
            var filled = slots.Count > Loadout.MaxFoodSlots ? slots.GetRange(0, Loadout.MaxFoodSlots) : slots;
            FoodKind[] picked = Assign(filled, offered, input.Rules?.FoodStatAtLeast ?? 1);
            for (int i = 0; i < slots.Count; i++)
            {
                FoodKind kind = i < picked.Length ? picked[i] : null;
                choices[slots[i]] = kind != null
                    ? new FoodChoice { Prefab = kind.Prefab, Template = kind.Template }
                    : new FoodChoice
                    {
                        Why = i < Loadout.MaxFoodSlots ? WhyNone(slots[i].Food, all, input.Template) : $"a loadout fills at most {Loadout.MaxFoodSlots} food slots",
                    };
            }
            return choices;
        }

        /// <summary>
        /// Every kind of food (by $ name) he carries or the chests in the input hold, with what he carries of it (every
        /// stack of its prefab, as a want of it counts his: EquipPlanner.SameKind and Matching.Answers) and what the chests
        /// could give this equip (each chest that may give it by "Take the loadout's items from", its stacks at the world's
        /// level, less one by "Leave one of each item in chests": as PlanStackable takes), in order of $ name. A kind the
        /// loadout names itself (an item of the same $ name) is marked <see cref="FoodKind.Named"/>. Reading an item's
        /// prefab asks the engine: once for each kind's shared data.
        /// </summary>
        internal static List<FoodKind> Kinds(PlanInput input)
        {
            var named = new HashSet<string>(StringComparer.Ordinal);
            foreach (LoadoutItem item in input.Loadout.Items)
            {
                ItemDrop.ItemData template = item.Food == null ? input.Template(item.Prefab) : null;
                if (template != null)
                {
                    named.Add(template.m_shared.m_name);
                }
            }
            var prefabs = new Dictionary<ItemDrop.ItemData.SharedData, string>();
            var byName = new Dictionary<string, FoodKind>(StringComparer.Ordinal);
            void See(ItemDrop.ItemData item)
            {
                // The item's own shared data first, which costs nothing (the game's is its template's).
                if (item == null || item.m_stack <= 0 || !IsFood(item))
                {
                    return;
                }
                if (!prefabs.TryGetValue(item.m_shared, out string prefab))
                {
                    prefabs[item.m_shared] = prefab = input.PrefabOf(item);
                }
                ItemDrop.ItemData template = prefab != null ? input.Template(prefab) : null;
                if (!IsFood(template) || template.m_shared.m_name != item.m_shared.m_name)
                {
                    return;
                }
                string name = template.m_shared.m_name;
                if (!byName.TryGetValue(name, out FoodKind kind) || string.CompareOrdinal(prefab, kind.Prefab) < 0)
                {
                    byName[name] = new FoodKind { Name = name, Prefab = prefab, Template = template, Named = named.Contains(name) };
                }
            }
            foreach (ItemDrop.ItemData item in input.Inventory.GetAllItems())
            {
                See(item);
            }
            foreach (ChestView chest in input.Chests)
            {
                if (chest?.Contents == null)
                {
                    continue;
                }
                foreach (ItemDrop.ItemData item in chest.Contents.GetAllItems())
                {
                    See(item);
                }
            }
            var kinds = new List<FoodKind>(byName.Values);
            kinds.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            var probe = new LoadoutItem();   // a slot as the want asks: quality 1, style 0, no maker, no data
            foreach (FoodKind kind in kinds)
            {
                Want want = EquipPlanner.SlotWant(probe, kind.Prefab, kind.Template);
                foreach (ItemDrop.ItemData item in input.Inventory.GetAllItems())
                {
                    if (item.m_stack > 0 && EquipPlanner.SameKind(input, item, want) && Matching.Answers(item, probe, input.Rules))
                    {
                        kind.Carried += item.m_stack;
                    }
                }
                foreach (ChestView chest in input.Chests)
                {
                    if (chest?.Contents == null || !EquipPlanner.Allowed(input.Rules, chest, kind.Template))
                    {
                        continue;
                    }
                    int count = EquipPlanner.CountKey(chest.Contents, want, input.Rules.WorldLevel);
                    if (input.Rules.LeaveOne && count > 0)
                    {
                        count--;
                    }
                    kind.InChests += count;
                }
            }
            return kinds;
        }

        /// <summary>
        /// The choice itself, on the slots (in the loadout's order) and the foods on offer: for each slot, its food or
        /// null. The most slots filled, then the most filled whole (<see cref="Enough"/>: the food's total reaches its whole
        /// count, or for a slot putting its stat first <paramref name="statAtLeast"/>),
        /// then slot by slot, in order, the best worth it can have (<see cref="Worth"/>) that still lets the rest reach
        /// that; then, among foods of equal worth, the most slots given a food he already carries, then slot by slot the
        /// first by $ name. Each step asks a matching of the slots to the foods for its best (an assignment solved
        /// exactly: Best), so the result is the best of every assignment, by the order above. Ties are settled last, over
        /// every slot, rather than slot by slot: a food fetched for a later slot counts as carried at the next press, and
        /// settled slot by slot it could then win an earlier slot's tie and swap the two (the scratch harness's random
        /// worlds, 40-B).
        /// </summary>
        internal static FoodKind[] Assign(IList<LoadoutItem> slots, IList<FoodKind> kinds, int statAtLeast = 1)
        {
            int n = slots.Count, m = kinds.Count;
            var picked = new FoodKind[n];
            if (n == 0 || m == 0)
            {
                return picked;
            }
            // The edges: a food that meets a slot (and has some nearby). First weight: one more slot filled outweighs every
            // whole count together (n + 1 apiece), then one for a slot filled whole. Second weight, for the ties: the same,
            // then one for a slot given a food he carries. No edge: -1.
            long big = n + 1;
            var first = new long[n, m];
            var second = new long[n, m];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    FoodKind kind = kinds[j];
                    if (!Meets(slots[i].Food, kind.Prefab, kind.Template) || kind.Total <= 0)
                    {
                        first[i, j] = second[i, j] = -1;
                        continue;
                    }
                    long whole = kind.Total >= Enough(slots[i], kind.Template, statAtLeast) ? 1 : 0;
                    first[i, j] = big + whole;
                    second[i, j] = big * big + big * whole + (kind.Carried > 0 ? 1 : 0);
                }
            }

            // Each slot in the loadout's order: the best worth among its foods that still lets the most slots be filled, and
            // the most whole (the slots before it held to theirs, each to a worth that kept that best): every food of that
            // worth (its group), or none.
            var group = new List<int>[n];   // null: not decided yet (any food that meets it)
            var taken = new bool[m];
            long best = Best(first, group, 0, n, taken);
            for (int i = 0; i < n; i++)
            {
                group[i] = new List<int>();
                foreach (List<int> level in Levels(slots[i].Food, kinds, first, i))
                {
                    group[i] = level;
                    if (Best(first, group, 0, n, taken) == best)
                    {
                        break;
                    }
                    group[i] = new List<int>();   // none: no food of this worth keeps the best
                }
            }

            // Ties, inside each slot's group: the most slots given a food he already carries; then each slot, in order, the
            // first food by $ name that still lets the rest reach that. Settled after every slot's preference, so a food
            // another slot fetched (which then counts as carried) never turns an earlier slot's tie at the next press.
            for (int i = 0; i < n; i++)
            {
                if (group[i].Count == 0)
                {
                    continue;
                }
                long now = Best(second, group, i, n, taken);
                var byName = new List<int>(group[i]);
                byName.Sort((a, b) => string.CompareOrdinal(kinds[a].Name, kinds[b].Name));
                foreach (int j in byName)
                {
                    if (taken[j])
                    {
                        continue;
                    }
                    taken[j] = true;
                    if (second[i, j] + Best(second, group, i + 1, n, taken) == now)
                    {
                        picked[i] = kinds[j];
                        break;
                    }
                    taken[j] = false;
                }
            }
            return picked;
        }

        // A slot's foods grouped by their worth to it, the best worth first.
        private static List<List<int>> Levels(FoodRule rule, IList<FoodKind> kinds, long[,] weight, int slot)
        {
            var levels = new List<List<int>>();
            var worths = new List<double>();
            for (int j = 0; j < kinds.Count; j++)
            {
                if (weight[slot, j] < 0)
                {
                    continue;
                }
                double worth = Worth(rule, kinds[j].Template);
                int at = worths.IndexOf(worth);
                if (at < 0)
                {
                    worths.Add(worth);
                    levels.Add(new List<int>());
                    at = levels.Count - 1;
                }
                levels[at].Add(j);
            }
            var order = new List<int>();
            for (int k = 0; k < levels.Count; k++)
            {
                order.Add(k);
            }
            order.Sort((a, b) => worths[b].CompareTo(worths[a]));
            return order.ConvertAll(k => levels[k]);
        }

        /// <summary>What <see cref="Best"/> answers when the slots held to a group can't all be filled.</summary>
        internal const long Infeasible = long.MinValue / 4;

        /// <summary>The best total weight of slots <paramref name="from"/> to n-1 matched to the foods not taken, each
        /// food to one slot at most: a slot with no group (null) to any food with an edge or none; a slot held to a group
        /// to one of its foods (an empty group: none). The assignment problem, solved exactly (the Hungarian method, each
        /// slot with a "none" of its own); <see cref="Infeasible"/> when a slot held to a group can't be given one of its
        /// foods.</summary>
        internal static long Best(long[,] weight, List<int>[] group, int from, int n, bool[] taken)
        {
            int rows = n - from;
            if (rows <= 0)
            {
                return 0;
            }
            var columns = new List<int>();
            for (int j = 0; j < taken.Length; j++)
            {
                if (!taken[j])
                {
                    columns.Add(j);
                }
            }
            int cols = columns.Count + rows;   // the foods left, then a "none" for each slot
            const long Forbidden = 1L << 40;
            var cost = new long[rows + 1, cols + 1];
            for (int r = 1; r <= rows; r++)
            {
                List<int> held = group?[from + r - 1];
                for (int c = 1; c <= cols; c++)
                {
                    if (c > columns.Count)
                    {
                        // Its own "none", unless it is held to a group of foods.
                        cost[r, c] = c - columns.Count == r && (held == null || held.Count == 0) ? 0 : Forbidden;
                        continue;
                    }
                    int food = columns[c - 1];
                    long w = weight[from + r - 1, food];
                    cost[r, c] = w < 0 || (held != null && !held.Contains(food)) ? Forbidden : -w;
                }
            }
            int[] match = Hungarian(cost, rows, cols);
            long total = 0;
            for (int r = 1; r <= rows; r++)
            {
                int c = match[r];
                if (c < 1 || cost[r, c] >= Forbidden)
                {
                    return Infeasible;
                }
                if (c <= columns.Count)
                {
                    total -= cost[r, c];
                }
            }
            return total;
        }

        // The least-cost assignment of every row 1..rows to its own column of 1..cols (rows <= cols), by potentials (the
        // Hungarian method in its O(rows² · cols) form); the column of each row.
        private static int[] Hungarian(long[,] cost, int rows, int cols)
        {
            const long Infinite = long.MaxValue / 4;
            var u = new long[rows + 1];
            var v = new long[cols + 1];
            var rowOf = new int[cols + 1];
            var way = new int[cols + 1];
            for (int i = 1; i <= rows; i++)
            {
                rowOf[0] = i;
                int j0 = 0;
                var least = new long[cols + 1];
                var used = new bool[cols + 1];
                for (int j = 0; j <= cols; j++)
                {
                    least[j] = Infinite;
                }
                do
                {
                    used[j0] = true;
                    int i0 = rowOf[j0], j1 = 0;
                    long delta = Infinite;
                    for (int j = 1; j <= cols; j++)
                    {
                        if (used[j])
                        {
                            continue;
                        }
                        long reduced = cost[i0, j] - u[i0] - v[j];
                        if (reduced < least[j])
                        {
                            least[j] = reduced;
                            way[j] = j0;
                        }
                        if (least[j] < delta)
                        {
                            delta = least[j];
                            j1 = j;
                        }
                    }
                    for (int j = 0; j <= cols; j++)
                    {
                        if (used[j])
                        {
                            u[rowOf[j]] += delta;
                            v[j] -= delta;
                        }
                        else
                        {
                            least[j] -= delta;
                        }
                    }
                    j0 = j1;
                }
                while (rowOf[j0] != 0);
                do
                {
                    int j1 = way[j0];
                    rowOf[j0] = rowOf[j1];
                    j0 = j1;
                }
                while (j0 != 0);
            }
            var match = new int[rows + 1];
            for (int j = 1; j <= cols; j++)
            {
                if (rowOf[j] != 0)
                {
                    match[rowOf[j]] = j;
                }
            }
            return match;
        }
    }
}
