using System.Collections.Generic;
using QoLMods.Chests;

namespace AddToCart
{
    /// <summary>One row of the materials list: an item the cart needs, and where it would come from.</summary>
    internal sealed class MaterialLine
    {
        /// <summary>The item's key (ChestStock: a name for any quality, or name@quality).</summary>
        internal string Item;

        /// <summary>What the whole cart needs of it: what its builds and crafts need, plus what it takes as it is.</summary>
        internal int Need;

        /// <summary>What he carries now.</summary>
        internal int Carrying;

        /// <summary>What a fetch would take from the chests: the preview's promise.</summary>
        internal int FromChests;

        /// <summary>What the chests can't cover (for builds and crafts, with "Take only what I'm short", after what he carries).</summary>
        internal int Short;
    }

    /// <summary>What the cart needs of one item, in two parts.</summary>
    internal struct Need
    {
        /// <summary>For builds, crafts and processing: "Take only what I'm short" counts what he carries against it.</summary>
        internal int Make;

        /// <summary>Take items, taken as they are: exactly this many, whatever he carries.</summary>
        internal int Take;
    }

    /// <summary>
    /// The cart's arithmetic, kept apart from the window so it can be tested outside the game. "Available" is what the
    /// chests may give (after leaving one where that is on, reserved chests left out); "carrying" is what his inventory
    /// holds. Both are by item key.
    /// </summary>
    internal static class CartMath
    {
        /// <summary>What the cart needs of each item, in the order the items first appear in it.</summary>
        internal static List<KeyValuePair<string, Need>> Needs(IEnumerable<KeyValuePair<Entry, int>> cart)
        {
            var order = new List<string>();
            var sums = new Dictionary<string, Need>();
            foreach (KeyValuePair<Entry, int> line in cart)
            {
                foreach (Cost cost in line.Key.Costs)
                {
                    if (!sums.TryGetValue(cost.Item, out Need sum))
                    {
                        order.Add(cost.Item);
                    }
                    if (line.Key.Literal)
                    {
                        sum.Take += cost.Amount * line.Value;
                    }
                    else
                    {
                        sum.Make += cost.Amount * line.Value;
                    }
                    sums[cost.Item] = sum;
                }
            }
            var needs = new List<KeyValuePair<string, Need>>();
            foreach (string item in order)
            {
                needs.Add(new KeyValuePair<string, Need>(item, sums[item]));
            }
            return needs;
        }

        /// <summary>What the cart needs of each item, looked up by item.</summary>
        internal static Dictionary<string, Need> NeedsByItem(IEnumerable<KeyValuePair<Entry, int>> cart)
        {
            var needs = new Dictionary<string, Need>();
            foreach (KeyValuePair<string, Need> need in Needs(cart))
            {
                needs[need.Key] = need.Value;
            }
            return needs;
        }

        /// <summary>How much of an item he wants from the chests: the Take part as it is, and the Make part less what he
        /// carries if "Take only what I'm short" is on.</summary>
        internal static int Wanted(Need need, int carrying, bool onlyWhatImShort) =>
            need.Take + (onlyWhatImShort ? (need.Make > carrying ? need.Make - carrying : 0) : need.Make);

        /// <summary>
        /// What the chests can give each item of the cart, with overlaps settled. An item named by its level
        /// ("name@quality", a Take line for gear or fish) and the same item by name (a recipe's ingredient, any level)
        /// draw on the same stacks: the level lines take theirs first, and the name keeps only what's left.
        /// </summary>
        internal static Dictionary<string, int> Settled(IDictionary<string, Need> needs, IDictionary<string, int> carrying,
                                                        IDictionary<string, int> available, bool onlyWhatImShort)
        {
            var settled = new Dictionary<string, int>();
            if (available != null)
            {
                foreach (KeyValuePair<string, int> each in available)
                {
                    settled[each.Key] = each.Value;
                }
            }
            foreach (KeyValuePair<string, Need> need in needs)
            {
                ChestStock.Parse(need.Key, out string name, out int quality);
                if (quality < 0)
                {
                    continue;
                }
                int wanted = Wanted(need.Value, Get(carrying, need.Key), onlyWhatImShort);
                int claimed = wanted < Get(available, need.Key) ? wanted : Get(available, need.Key);
                if (claimed > 0 && settled.TryGetValue(name, out int left))
                {
                    settled[name] = left > claimed ? left - claimed : 0;
                }
            }
            return settled;
        }

        /// <summary>The materials list: for each item, what's needed, carried, taken from the chests, and short.</summary>
        internal static List<MaterialLine> Materials(IEnumerable<KeyValuePair<Entry, int>> cart, IDictionary<string, int> carrying,
                                                     IDictionary<string, int> available, bool onlyWhatImShort)
        {
            var lines = new List<MaterialLine>();
            List<KeyValuePair<string, Need>> needs = Needs(cart);
            var byItem = new Dictionary<string, Need>();
            foreach (KeyValuePair<string, Need> need in needs)
            {
                byItem[need.Key] = need.Value;
            }
            available = Settled(byItem, carrying, available, onlyWhatImShort);
            foreach (KeyValuePair<string, Need> need in needs)
            {
                int carried = Get(carrying, need.Key);
                int wanted = Wanted(need.Value, carried, onlyWhatImShort);
                int fromChests = wanted < Get(available, need.Key) ? wanted : Get(available, need.Key);
                lines.Add(new MaterialLine
                {
                    Item = need.Key,
                    Need = need.Value.Make + need.Value.Take,
                    Carrying = carried,
                    FromChests = fromChests,
                    Short = wanted - fromChests,
                });
            }
            return lines;
        }

        /// <summary>
        /// What would be short if one more of <paramref name="entry"/> went into the cart as it stands: empty when the
        /// chests (and, for builds and crafts with "Take only what I'm short", his inventory) cover it all. An entry
        /// greys out when this isn't empty, and a click on it asks "Add all available?", naming these.
        /// </summary>
        internal static List<KeyValuePair<string, int>> ShortForOneMore(Entry entry, IEnumerable<KeyValuePair<Entry, int>> cart,
                                                                        IDictionary<string, int> carrying, IDictionary<string, int> available,
                                                                        bool onlyWhatImShort)
        {
            Dictionary<string, Need> needs = NeedsByItem(cart);
            return ShortForOneMore(entry, needs, carrying, Settled(needs, carrying, available, onlyWhatImShort), onlyWhatImShort);
        }

        /// <summary>The same, from the cart's needs (NeedsByItem) and what the chests can give with overlaps settled
        /// (Settled), both worked out once: for checking many entries against one cart.</summary>
        internal static List<KeyValuePair<string, int>> ShortForOneMore(Entry entry, IDictionary<string, Need> needs,
                                                                        IDictionary<string, int> carrying, IDictionary<string, int> settled,
                                                                        bool onlyWhatImShort)
        {
            IDictionary<string, int> available = settled;
            var shortages = new List<KeyValuePair<string, int>>();
            foreach (Cost cost in entry.Costs)
            {
                needs.TryGetValue(cost.Item, out Need need);
                if (entry.Literal)
                {
                    need.Take += cost.Amount;
                }
                else
                {
                    need.Make += cost.Amount;
                }
                int missing = Wanted(need, Get(carrying, cost.Item), onlyWhatImShort) - Get(available, cost.Item);
                if (missing > 0)
                {
                    shortages.Add(new KeyValuePair<string, int>(cost.Item, missing));
                }
            }
            return shortages;
        }

        /// <summary>
        /// How many more of <paramref name="entry"/> the cart could take before anything of it runs short: on Take, what
        /// the chests hold beyond the cart; on Build and Craft, how many more could be made from the chests (and what he
        /// carries, with "Take only what I'm short"). 0 when not even one more.
        /// </summary>
        internal static int MostMore(Entry entry, IDictionary<string, Need> needs, IDictionary<string, int> carrying,
                                     IDictionary<string, int> available, bool onlyWhatImShort) =>
            MostMoreSettled(entry, needs, carrying, Settled(needs, carrying, available, onlyWhatImShort), onlyWhatImShort);

        /// <summary>The same, from what the chests can give with overlaps settled (Settled), worked out once: for
        /// many entries against one cart (the Count sort).</summary>
        internal static int MostMoreSettled(Entry entry, IDictionary<string, Need> needs, IDictionary<string, int> carrying,
                                            IDictionary<string, int> available, bool onlyWhatImShort)
        {
            int most = int.MaxValue;
            foreach (Cost cost in entry.Costs)
            {
                if (cost.Amount <= 0)
                {
                    continue;
                }
                needs.TryGetValue(cost.Item, out Need need);
                int room;
                if (entry.Literal || !onlyWhatImShort)
                {
                    // Every one more takes its full amount from the chests.
                    room = Get(available, cost.Item) - Wanted(need, Get(carrying, cost.Item), onlyWhatImShort);
                }
                else if (need.Take > Get(available, cost.Item))
                {
                    room = -1;   // the Take part alone is already short: carrying never covers it
                }
                else
                {
                    // What he carries covers the Make part first: the chests cover the Take part and the rest.
                    room = Get(available, cost.Item) - need.Take + Get(carrying, cost.Item) - need.Make;
                }
                int count = room >= 0 ? room / cost.Amount : 0;
                if (count < most)
                {
                    most = count;
                }
            }
            return most == int.MaxValue ? 0 : most;
        }

        /// <summary>What a fetch should ask the chests for: each item's promised amount (never more).</summary>
        internal static Dictionary<string, int> Order(IEnumerable<MaterialLine> materials)
        {
            var order = new Dictionary<string, int>();
            foreach (MaterialLine line in materials)
            {
                if (line.FromChests > 0)
                {
                    order[line.Item] = line.FromChests;
                }
            }
            return order;
        }

        private static int Get(IDictionary<string, int> amounts, string item) =>
            amounts != null && amounts.TryGetValue(item, out int amount) ? amount : 0;
    }
}
