using System;
using System.Collections.Generic;
using TopperUpper.Logic;

namespace TopperUpper
{
    /// <summary>
    /// His lists as the settings write them (by the game's prefab names, so they hold in every language), read once each
    /// time a setting changes and turned into the names the game counts items by (an item's shared name, "$item_resin"):
    /// the reserves, the maximums, the never-take list, and each station's "takes first" line and the kiln's woods.
    /// A name the game doesn't know, or an entry it can't read, is written to the log once per change, and ignored.
    /// </summary>
    internal static class RuleLists
    {
        private static int readAt = -1;
        private static ObjectDB readWith;
        private static readonly Dictionary<string, ItemDrop> ItemsByPrefab = new Dictionary<string, ItemDrop>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ItemDrop> ItemsByShared = new Dictionary<string, ItemDrop>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> reserves = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> maximums = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> never = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, NameList> firsts = new Dictionary<string, NameList>(StringComparer.Ordinal);
        private static NameList kilnWoods = new NameList();
        private static string lastWarning;

        /// <summary>The reserve of an item, by its shared name, or null for none.</summary>
        internal static int? ReserveOf(string sharedName)
        {
            Refresh();
            return sharedName != null && reserves.TryGetValue(sharedName, out int amount) ? amount : (int?)null;
        }

        /// <summary>The maximum of a product, by its shared name, or null for none.</summary>
        internal static int? MaximumOf(string sharedName)
        {
            Refresh();
            return sharedName != null && maximums.TryGetValue(sharedName, out int amount) ? amount : (int?)null;
        }

        /// <summary>Whether an item (by its shared name) is on the never-take list.</summary>
        internal static bool Never(string sharedName)
        {
            Refresh();
            return sharedName != null && never.Contains(sharedName);
        }

        /// <summary>A station kind's "takes first" line, read (empty for a kind without one).</summary>
        internal static NameList FirstsOf(string stationPrefab)
        {
            Refresh();
            if (stationPrefab == null)
            {
                return Empty;
            }
            if (!firsts.TryGetValue(stationPrefab, out NameList list))
            {
                list = NameList.Parse(Settings.FirstOf(stationPrefab));
                firsts[stationPrefab] = list;
            }
            return list;
        }

        /// <summary>The kiln's woods, read; empty: every wood it burns.</summary>
        internal static NameList KilnWoods
        {
            get
            {
                Refresh();
                return kilnWoods;
            }
        }

        private static readonly NameList Empty = new NameList();

        /// <summary>The item prefab of that name (any case), or null.</summary>
        internal static ItemDrop Item(string prefab)
        {
            Refresh();
            return prefab != null && ItemsByPrefab.TryGetValue(prefab, out ItemDrop item) ? item : null;
        }

        /// <summary>An item prefab by its shared name ("$item_iron"), or null.</summary>
        internal static ItemDrop ItemByShared(string sharedName)
        {
            Refresh();
            return sharedName != null && ItemsByShared.TryGetValue(sharedName, out ItemDrop item) ? item : null;
        }

        private static void Refresh()
        {
            ObjectDB db = ObjectDB.instance;
            if (readAt == Settings.Version && readWith == db)
            {
                return;
            }
            readAt = Settings.Version;
            readWith = db;
            ItemsByPrefab.Clear();
            ItemsByShared.Clear();
            if (db != null && db.m_items != null)
            {
                foreach (UnityEngine.GameObject prefab in db.m_items)
                {
                    ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                    if (item != null && item.m_itemData?.m_shared != null && !ItemsByPrefab.ContainsKey(prefab.name))
                    {
                        ItemsByPrefab[prefab.name] = item;
                        if (!ItemsByShared.ContainsKey(item.m_itemData.m_shared.m_name))
                        {
                            ItemsByShared[item.m_itemData.m_shared.m_name] = item;
                        }
                    }
                }
            }
            var unknown = new List<string>();
            var bad = new List<string>();
            Fill(reserves, Settings.Reserves.Value, unknown, bad);
            Fill(maximums, Settings.Maximums.Value, unknown, bad);
            never.Clear();
            NameList neverList = NameList.Parse(Settings.NeverTake.Value);
            bad.AddRange(neverList.Bad);
            foreach (string name in neverList.Names)
            {
                string shared = SharedOf(name);
                if (shared != null)
                {
                    never.Add(shared);
                }
                else if (db != null)
                {
                    unknown.Add(name);
                }
            }
            firsts.Clear();
            kilnWoods = NameList.Parse(Settings.KilnWoods.Value);
            bad.AddRange(kilnWoods.Bad);
            if (db != null && (unknown.Count > 0 || bad.Count > 0))
            {
                string warning = "Some names in Topper-Upper's lists are ignored: " +
                                 (unknown.Count > 0 ? $"not items the game knows: {string.Join(", ", unknown)}" : string.Empty) +
                                 (unknown.Count > 0 && bad.Count > 0 ? "; " : string.Empty) +
                                 (bad.Count > 0 ? $"not written as item:amount or a prefab name: {string.Join(", ", bad)}" : string.Empty) +
                                 ". Items are named by the game's prefab names (Resin, BlackMetalScrap, OatSeeds).";
                if (warning != lastWarning)
                {
                    lastWarning = warning;   // once per change of what's wrong, not at every setting's change (n10)
                    Module.Warn(warning);
                }
            }
        }

        private static void Fill(Dictionary<string, int> into, string text, List<string> unknown, List<string> bad)
        {
            into.Clear();
            ItemAmounts list = ItemAmounts.Parse(text);
            bad.AddRange(list.Bad);
            foreach (KeyValuePair<string, int> each in list.Amounts)
            {
                string shared = SharedOf(each.Key);
                if (shared != null)
                {
                    into[shared] = each.Value;
                }
                else if (ObjectDB.instance != null)
                {
                    unknown.Add(each.Key);
                }
            }
        }

        private static string SharedOf(string prefab) =>
            prefab != null && ItemsByPrefab.TryGetValue(prefab, out ItemDrop item) ? item.m_itemData.m_shared.m_name : null;
    }
}
