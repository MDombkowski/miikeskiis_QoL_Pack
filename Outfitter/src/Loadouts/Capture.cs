using System;
using System.Collections.Generic;

namespace Outfitter.Loadouts
{
    /// <summary>
    /// Making a loadout from what he has: each item in the cells asked for (or every item), with its place (Places), and
    /// whether he wears it and as what. A weapon the game has put away for a moment (R, swimming, a station …) counts as
    /// held while "Weapons put away count as worn" is on. Items in cells Outfitter doesn't know (an EquipmentAndQuickSlots
    /// slot that isn't switched on, or rows another inventory mod adds) are left out. Nothing is moved or changed.
    /// </summary>
    internal static class Capture
    {
        /// <summary>Everything he carries and wears, each in its place: the stands' "save what I carry and wear".</summary>
        internal static Loadout Everything(Player player, string name) => Cells(player, name, null);

        /// <summary>The items in these cells of his inventory (x, y as the game counts them; null: every cell), each in
        /// its place. A new loadout with a new ID, saved by him; not yet kept (Store.Save keeps it).</summary>
        internal static Loadout Cells(Player player, string name, ICollection<Vector2i> cells)
        {
            GridShape grid = GameHooks.Shape(player);
            ItemDrop.ItemData hiddenRight = GameHooks.HiddenRight(player);
            ItemDrop.ItemData hiddenLeft = GameHooks.HiddenLeft(player);
            var loadout = new Loadout
            {
                Id = Store.NewId(),
                Name = Text.Plain(name, Loadout.MaxName),
                SavedBy = player.GetPlayerID(),
                SavedByName = Text.Plain(player.GetPlayerName(), Loadout.MaxName),
            };
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(player.GetInventory().GetAllItemsInGridOrder()))
            {
                Vector2i at = item.m_gridPos;
                if ((cells != null && !cells.Contains(at)) || !grid.Usable(at.x, at.y) || item.m_stack <= 0)
                {
                    continue;
                }
                string prefab = PrefabOf(item);
                if (prefab == null || loadout.Items.Count >= Loadout.MaxItems)
                {
                    continue;
                }
                var entry = new LoadoutItem
                {
                    Prefab = prefab,
                    Quality = item.m_quality,
                    Variant = item.m_variant,
                    Count = item.m_stack,
                    Worn = WornOf(player, item),
                    CrafterId = item.m_crafterID,
                    CrafterName = item.m_crafterName ?? string.Empty,
                    Data = DataOf(item),
                };
                Places.Of(at.x, at.y, grid, entry);
                if (entry.Worn == WornAs.None && Settings.HiddenAsWorn.Value)
                {
                    if (ReferenceEquals(item, hiddenRight))
                    {
                        entry.Worn = WornAs.RightHand;
                    }
                    else if (ReferenceEquals(item, hiddenLeft))
                    {
                        entry.Worn = WornAs.LeftHand;
                    }
                }
                loadout.Items.Add(entry);
            }
            return loadout;
        }

        /// <summary>What he wears an item as, or None.</summary>
        internal static WornAs WornOf(Player player, ItemDrop.ItemData item)
        {
            if (item == null || !player.IsItemEquiped(item))
            {
                return WornAs.None;
            }
            if (ReferenceEquals(item, player.RightItem))
            {
                return WornAs.RightHand;
            }
            if (ReferenceEquals(item, player.LeftItem))
            {
                return WornAs.LeftHand;
            }
            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Helmet:
                    return WornAs.Helmet;
                case ItemDrop.ItemData.ItemType.Chest:
                    return WornAs.Chest;
                case ItemDrop.ItemData.ItemType.Legs:
                    return WornAs.Legs;
                case ItemDrop.ItemData.ItemType.Shoulder:
                    return WornAs.Cape;
                case ItemDrop.ItemData.ItemType.Utility:
                    return WornAs.Utility;   // EquipmentAndQuickSlots' second and third belts too
                case ItemDrop.ItemData.ItemType.Trinket:
                    return WornAs.Trinket;
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                    return WornAs.Ammo;
                default:
                    return WornAs.None;
            }
        }

        /// <summary>A fingerprint of other mods' data on an item (its m_customData), for "This exact item": empty when it
        /// has none; else 16 hex digits hashed (FNV-1a, 64 bits) from every key and value in key order, so the same data
        /// always gives the same fingerprint. A mod's note of where an item sits isn't part of what the item is, so
        /// EquipmentAndQuickSlots' own keys (every key starting "eaqs": the slot and player it stamps on the items in its
        /// slots at each character save and clears at its next tidy-up, a parked piece's mark, its death notes) are left out
        /// (R3, finding 1). Pure, so the scratch harness reaches it.</summary>
        internal static string DataOf(ItemDrop.ItemData item)
        {
            if (item?.m_customData == null || item.m_customData.Count == 0)
            {
                return string.Empty;
            }
            var keys = new List<string>();
            foreach (string key in item.m_customData.Keys)
            {
                if (key != null && !key.StartsWith("eaqs", StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(key);
                }
            }
            if (keys.Count == 0)
            {
                return string.Empty;
            }
            keys.Sort(StringComparer.Ordinal);
            ulong hash = 14695981039346656037UL;
            void Mix(string text)
            {
                foreach (char c in text ?? string.Empty)
                {
                    hash = (hash ^ c) * 1099511628211UL;
                }
                hash = (hash ^ 0xFFFFUL) * 1099511628211UL;   // ends each piece, so "ab" + "c" isn't "a" + "bc"
            }
            foreach (string key in keys)
            {
                Mix(key);
                Mix(item.m_customData[key]);
            }
            return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>An item's prefab name: the name of the prefab it was made from, or, for an item without one, the
        /// prefab the game knows for its kind; null when neither is known.</summary>
        internal static string PrefabOf(ItemDrop.ItemData item) => item == null ? null : PrefabName(item);

        /// <summary>The prefab name reading behind <see cref="PrefabOf"/>; the scratch harness sets another (a prefab's
        /// name asks the engine).</summary>
        internal static Func<ItemDrop.ItemData, string> PrefabName = GamePrefabName;

        private static string GamePrefabName(ItemDrop.ItemData item)
        {
            if (item.m_dropPrefab != null)
            {
                return item.m_dropPrefab.name;
            }
            return ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item.m_shared)?.name : null;
        }
    }
}
