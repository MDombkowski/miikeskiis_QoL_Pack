using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QoLMods.Catalogue;

namespace StoreStash
{
    /// <summary>How one sort goes, as the settings say when it's asked for.</summary>
    internal sealed class SortRules
    {
        internal SortOrderChoice Order = SortOrderChoice.PacksOrder;

        /// <summary>The families' line for the pack's order (empty: his order).</summary>
        internal string Families = string.Empty;

        internal bool Merge = true;

        internal bool FromBottom;

        internal static SortRules FromSettings() => new SortRules
        {
            Order = SortSettings.Order.Value,
            Families = SortSettings.Families.Value,
            Merge = SortSettings.Merge.Value,
            FromBottom = SortSettings.From.Value == SortFrom.Bottom,
        };
    }

    /// <summary>
    /// Sorts part of a game inventory (card A53): the rows from <c>top</c> up to <c>bottom</c>, every column, never a stack
    /// the caller pins, by SortPlan, in one frame. The plan is checked before anything changes (SortPlan.Check); only then
    /// are the stacks moved (their cells), topped up or emptied (their amounts), the emptied ones taken out of the
    /// inventory, and the game told once that the inventory changed, as its own moves tell it. Nothing is ever added: every
    /// stack is the game's own object, carrying everything it carried. A plan that changes nothing changes nothing, so a
    /// sorted chest isn't saved again.
    /// </summary>
    internal static class Sorter
    {
        /// <summary>Where the catalogue puts an item, by its shared name. Only the scratch test harness sets another.</summary>
        internal static Func<string, ItemHome> HomeOf = GameFacts.HomeOf;

        /// <summary>An item's biome, as a label. Only the scratch test harness sets another.</summary>
        internal static Func<string, string> BiomeOf = Biomes.LabelOf;

        /// <summary>The name he reads. Only the scratch test harness sets another.</summary>
        internal static Func<string, string> Localize = GameFacts.Localize;

        /// <summary>Tells the game an inventory changed. Only the scratch test harness sets another.</summary>
        internal static Action<Inventory> Changed = SortHooks.Changed;

        /// <param name="pinned">Stacks kept exactly as they are: their cells and amounts.</param>
        /// <param name="unmerged">Stacks that may move but never merge (what he wears or holds, when that isn't kept in
        /// place): the game holds on to an equipped stack itself (a thrown bomb is taken from the stack in his hand without
        /// asking the inventory), so it must never be emptied into another.</param>
        /// <param name="mergeInPlace">Pinned stacks that may still merge among themselves, each staying in its cell (his
        /// favourites, "Merge favorite stacks", card A82; SortEntry.MergeInPlace). Never one that is unmerged.</param>
        internal static SortOutcome Sort(Inventory inventory, int top, int bottom, Func<ItemDrop.ItemData, bool> pinned, SortRules rules,
                                         Func<ItemDrop.ItemData, bool> unmerged = null, Func<ItemDrop.ItemData, bool> mergeInPlace = null)
        {
            int width = inventory.GetWidth();
            List<SortEntry> entries = Entries(inventory, width, top, bottom, pinned, rules, unmerged, mergeInPlace);
            SortOutcome outcome = SortPlan.Plan(entries, width, top, bottom, rules.Merge, rules.FromBottom, SortOrder.For(rules.Order));
            if (outcome.AnyChange)
            {
                Apply(inventory, entries);
            }
            return outcome;
        }

        /// <summary>The stacks in those rows, in reading order, with their keys.</summary>
        internal static List<SortEntry> Entries(Inventory inventory, int width, int top, int bottom, Func<ItemDrop.ItemData, bool> pinned, SortRules rules,
                                                Func<ItemDrop.ItemData, bool> unmerged = null, Func<ItemDrop.ItemData, bool> mergeInPlace = null)
        {
            Orders orders = rules.Order == SortOrderChoice.PacksOrder ? SortOrder.OrdersFrom(rules.Families) : null;
            var names = new Dictionary<string, string>();
            var entries = new List<SortEntry>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item?.m_shared == null || item.m_gridPos.x < 0 || item.m_gridPos.x >= width || item.m_gridPos.y < top || item.m_gridPos.y >= bottom)
                {
                    continue;   // another row's (the hotbar kept, EquipmentAndQuickSlots' slots): never touched
                }
                bool isPinned = pinned != null && pinned(item);
                entries.Add(new SortEntry
                {
                    Item = item,
                    X = item.m_gridPos.x,
                    Y = item.m_gridPos.y,
                    Stack = item.m_stack,
                    MaxStack = Math.Max(1, item.m_shared.m_maxStackSize),
                    MergeKey = unmerged != null && unmerged(item) ? null : MergeKeyOf(item),
                    Pinned = isPinned,
                    MergeInPlace = isPinned && mergeInPlace != null && mergeInPlace(item),
                    Key = KeyOf(item, orders, names),
                });
            }
            List<SortEntry> reading = entries.OrderBy(entry => entry.Y).ThenBy(entry => entry.X).ToList();
            for (int i = 0; i < reading.Count; i++)
            {
                reading[i].Index = i;
            }
            return entries;
        }

        /// <summary>
        /// What an item may merge with: stackable items of the same kind, quality, world level and variant, with the same
        /// mark of a cheated item and the same data other mods keep on it. The game itself stacks by kind, quality and
        /// world level alone; the rest is kept apart so a merge never drops what another mod wrote. Null: never merged
        /// (gear, anything worn down).
        /// </summary>
        internal static string MergeKeyOf(ItemDrop.ItemData item)
        {
            if (item.m_shared.m_maxStackSize <= 1 || item.m_shared.m_useDurability)
            {
                return null;
            }
            var key = new StringBuilder(item.m_shared.m_name);
            key.Append('\u0001').Append(item.m_quality).Append('\u0001').Append(item.m_worldLevel).Append('\u0001').Append(item.m_variant)
               .Append('\u0001').Append(item.m_cheated ? '1' : '0');
            if (item.m_customData != null && item.m_customData.Count > 0)
            {
                foreach (KeyValuePair<string, string> pair in item.m_customData.OrderBy(each => each.Key, StringComparer.Ordinal))
                {
                    key.Append('\u0002').Append(pair.Key).Append('\u0003').Append(pair.Value);
                }
            }
            return key.ToString();
        }

        /// <summary>The keys an order compares, worked out once per stack. The pack's order's places only when that order
        /// is chosen (they need the catalogue).</summary>
        internal static SortKey KeyOf(ItemDrop.ItemData item, Orders orders, Dictionary<string, string> names)
        {
            string shared = item.m_shared.m_name ?? string.Empty;
            if (!names.TryGetValue(shared, out string name))
            {
                name = Localize(shared) ?? shared;
                names[shared] = name;
            }
            var key = new SortKey
            {
                Type = (int)item.m_shared.m_itemType,
                Name = name,
                Token = shared,
                Quality = item.m_quality,
                WorldLevel = item.m_worldLevel,
                Durability = item.m_durability,
                UnitWeight = item.GetNonStackedWeight(),
                Value = item.m_shared.m_value,
            };
            if (orders != null)
            {
                SortOrder.PlacePacks(key, HomeOf(shared), BiomeOf(shared), orders);
            }
            return key;
        }

        // Carries out a checked plan: the emptied stacks leave the inventory, holding nothing (so a reference to one left
        // anywhere can't give anything, as the game's own RemoveItem by name leaves an emptied stack at 0), the rest take
        // their cells and amounts, and the game hears once that the inventory changed.
        private static void Apply(Inventory inventory, List<SortEntry> entries)
        {
            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            foreach (SortEntry entry in entries)
            {
                var item = (ItemDrop.ItemData)entry.Item;
                if (entry.Removed)
                {
                    item.m_stack = 0;
                    items.Remove(item);
                }
                else
                {
                    item.m_stack = entry.NewStack;
                    item.m_gridPos = new Vector2i(entry.NewX, entry.NewY);
                }
            }
            Changed(inventory);
        }
    }
}
