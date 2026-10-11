using System;
using UnityEngine;

namespace Outfitter.Loadouts
{
    /// <summary>
    /// The game's side of loadouts and share codes: the game's own item for a prefab name (its template, read only, never
    /// changed), and a code read into a loadout checked against this game: an item this game has gets its quality, style
    /// and count capped to what the item allows; one it lacks (another mod's) stays a line, shown as "not in your game".
    /// A food slot stays as the code says (its foods are names, whether this game has them or not). Never makes an item.
    /// </summary>
    internal static class Codes
    {
        private static readonly System.Random Random = new System.Random();

        /// <summary>The game's own item for a prefab name, or null when this game has none (read it; never change it).</summary>
        internal static ItemDrop.ItemData Template(string prefab) => TemplateOf(prefab);

        /// <summary>The reading behind <see cref="Template"/>; the scratch harness sets another (ObjectDB asks the engine).</summary>
        internal static Func<string, ItemDrop.ItemData> TemplateOf = GameTemplate;

        private static ItemDrop.ItemData GameTemplate(string prefab)
        {
            if (string.IsNullOrEmpty(prefab) || ObjectDB.instance == null)
            {
                return null;
            }
            GameObject found = ObjectDB.instance.GetItemPrefab(prefab);
            if (found == null)
            {
                return null;
            }
            ItemDrop drop = found.GetComponent<ItemDrop>();
            return drop != null ? drop.m_itemData : null;
        }

        /// <summary>A friend's code as a new loadout, its items capped to what this game allows; null, with why, when it
        /// can't be read. Nothing is kept until Store.Save.</summary>
        internal static Loadout Read(string code, out string why)
        {
            Loadout loadout = ShareCode.Read(code, Random, out why);
            if (loadout == null)
            {
                return null;
            }
            loadout.Id = Store.NewId();
            foreach (LoadoutItem item in loadout.Items)
            {
                if (item.Food != null)
                {
                    continue;   // a food slot: a rule, not an unknown item; its foods a list of names (one this game lacks stays one)
                }
                ItemDrop.ItemData template = Template(item.Prefab);
                if (template == null)
                {
                    continue;   // not in this game: kept as a line, never an item
                }
                item.Quality = Mathf.Clamp(item.Quality, 1, Math.Max(1, template.m_shared.m_maxQuality));
                int styles = template.m_shared.m_icons != null ? template.m_shared.m_icons.Length : 1;
                item.Variant = Mathf.Clamp(item.Variant, 0, Math.Max(0, styles - 1));
                item.Count = Mathf.Clamp(item.Count, 1, Math.Max(1, template.m_shared.m_maxStackSize));
                if (!Equip.Matching.WornFits(item.Worn, template.m_shared.m_itemType))
                {
                    item.Worn = WornAs.None;   // a place the item can't be worn in (a damaged or made-up code: R1, finding 4)
                }
            }
            return loadout;
        }

        /// <summary>A loadout as a share code, as the settings say (with each item's place, or without).</summary>
        internal static string Write(Loadout loadout) => ShareCode.Write(loadout, Settings.CodesCarryPlaces.Value);
    }
}
