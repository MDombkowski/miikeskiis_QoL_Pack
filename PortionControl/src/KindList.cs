using System;
using System.Collections.Generic;
using System.Linq;
using QoLMods.Catalogue;
using UnityEngine;

namespace PortionControl
{
    /// <summary>One item that stacks, as the lists in F1 show it.</summary>
    internal sealed class KindItem
    {
        internal string SharedName;
        internal string Prefab;
        internal string Name;
        internal string Family;
        internal string Member;
        internal int StackSize;
        internal string Search;
    }

    /// <summary>A category or a category's member, as the list in F1 shows it, with how many items that stack are in it.</summary>
    internal sealed class KindCategory
    {
        internal string Key;
        internal string Label;
        internal bool IsMember;
        internal int Count;
    }

    /// <summary>
    /// Everything the lists of "Amounts by item, category and stack size" show, read from the game's own item list
    /// (ObjectDB) through the catalogue part, once per item list: every item that stacks (a stack size above 1: the split
    /// window never opens on one that doesn't), one per shared name (the catalogue's canonical prefab), named in his
    /// language, placed in the pack's families and members as Add to Cart places them; the categories that hold any of
    /// them, in the pack's order; and every stack size they use. Empty until the game has read its items (the main menu).
    /// </summary>
    internal static class KindList
    {
        private static ObjectDB readFor;
        private static int readCount = -1;
        private static bool readInWorld;

        internal static readonly List<KindItem> Items = new List<KindItem>();
        internal static readonly List<KindCategory> Categories = new List<KindCategory>();
        internal static readonly List<KeyValuePair<int, int>> StackSizes = new List<KeyValuePair<int, int>>();

        private static readonly HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Goes up each time the lists are read again, so a list drawn from them knows to rebuild.</summary>
        internal static int Version { get; private set; }

        /// <summary>Whether an item of this shared name is in the game's list (one that stacks).</summary>
        internal static bool Knows(string sharedName) => sharedName != null && known.Contains(sharedName);

        /// <summary>True once the lists are read; reads them again when the game's item list is a new one (a new world),
        /// or once a world is loaded after the main menu (the catalogue places trophies by biome only then).</summary>
        internal static bool Ready()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0)
            {
                return false;
            }
            bool inWorld = ZNetScene.instance != null;
            if (db != readFor || db.m_items.Count != readCount || (inWorld && !readInWorld))
            {
                Read(db);
                readFor = db;
                readCount = db.m_items.Count;
                readInWorld = inWorld;
            }
            return Items.Count > 0;
        }

        private static void Read(ObjectDB db)
        {
            Version++;
            Items.Clear();
            Categories.Clear();
            StackSizes.Clear();
            known.Clear();
            var seen = new HashSet<string>();
            foreach (GameObject prefab in db.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                ItemDrop.ItemData.SharedData shared = drop != null ? drop.m_itemData?.m_shared : null;
                if (shared == null || shared.m_maxStackSize <= 1 || string.IsNullOrEmpty(shared.m_name) || !seen.Add(shared.m_name))
                {
                    continue;
                }
                GameObject canonical = GameFacts.Canonical(shared.m_name) ?? prefab;
                ItemHome home = GameFacts.HomeOf(shared.m_name) ?? ItemHome.Unknown;
                string name = GameFacts.Localize(shared.m_name);
                if (string.IsNullOrEmpty(name) || name.StartsWith("$", StringComparison.Ordinal))
                {
                    name = canonical.name;
                }
                Items.Add(new KindItem
                {
                    SharedName = shared.m_name,
                    Prefab = canonical.name,
                    Name = name,
                    Family = home.Family,
                    Member = home.Section,
                    StackSize = shared.m_maxStackSize,
                    Search = (name + " " + canonical.name + " " + shared.m_name).ToLowerInvariant(),
                });
            }
            Items.Sort(Compare);
            foreach (KindItem item in Items)
            {
                known.Add(item.SharedName);
            }

            foreach (IGrouping<string, KindItem> family in Items.GroupBy(item => item.Family))
            {
                Categories.Add(new KindCategory { Key = family.Key, Label = family.Key, Count = family.Count() });
                foreach (IGrouping<string, KindItem> member in family.Where(item => !string.IsNullOrEmpty(item.Member)).GroupBy(item => item.Member))
                {
                    Categories.Add(new KindCategory
                    {
                        Key = AmountRules.CategoryKey(family.Key, member.Key),
                        Label = member.Key,
                        IsMember = true,
                        Count = member.Count(),
                    });
                }
            }
            foreach (IGrouping<int, KindItem> size in Items.GroupBy(item => item.StackSize).OrderBy(group => group.Key))
            {
                StackSizes.Add(new KeyValuePair<int, int>(size.Key, size.Count()));
            }
        }

        // The pack's order: its families in Taxonomy's order, each family's members in its order (others after, by name),
        // then the items by name.
        private static int Compare(KindItem a, KindItem b)
        {
            int byFamily = Rank(Taxonomy.Families, a.Family).CompareTo(Rank(Taxonomy.Families, b.Family));
            if (byFamily != 0)
            {
                return byFamily;
            }
            string[] members = Taxonomy.Members.TryGetValue(a.Family, out string[] listed) ? listed : Array.Empty<string>();
            int byMember = Rank(members, a.Member).CompareTo(Rank(members, b.Member));
            if (byMember != 0)
            {
                return byMember;
            }
            int byMemberName = string.Compare(a.Member, b.Member, StringComparison.CurrentCultureIgnoreCase);
            if (byMemberName != 0)
            {
                return byMemberName;
            }
            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        private static int Rank(string[] order, string name)
        {
            int at = Array.IndexOf(order, name);
            return at >= 0 ? at : order.Length;
        }

        /// <summary>A stack's category and member, for the split window (the catalogue's home for its shared name).</summary>
        internal static (string Family, string Member) HomeOf(ItemDrop.ItemData item)
        {
            ItemHome home = item != null ? GameFacts.HomeOf(item.m_shared.m_name) : null;
            return home != null ? (home.Family, home.Section) : (null, null);
        }
    }
}
