using System;
using System.Collections.Generic;
using QoLMods.Catalogue;
using QoLMods.Chests;

namespace TopperUpper
{
    /// <summary>
    /// Chests' setups (StoreStash's chest setups, card A41) as Topper-Upper reads them, to put things away by the quick
    /// stack's rules: the text in a chest's own record (ChestSetup.Read) read as the catalogue's picks (ItemPicks), each text
    /// once, since this runs for every item and chest of a plan. Topper-Upper only reads setups; StoreStash writes them. A
    /// text from a newer version or too long takes nothing (ItemPicks), so such a chest counts as set up and takes nothing,
    /// as in StoreStash.
    /// </summary>
    internal static class Setups
    {
        private const int Kept = 512;

        private static readonly Dictionary<string, ItemPicks> Read = new Dictionary<string, ItemPicks>(StringComparer.Ordinal);

        /// <summary>A setup from its text, read once and kept; null for none (an empty text, or picks with nothing in them).</summary>
        internal static ItemPicks Of(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            if (!Read.TryGetValue(text, out ItemPicks picks))
            {
                if (Read.Count >= Kept)
                {
                    Read.Clear();
                }
                picks = ItemPicks.Parse(text);
                Read[text] = picks;
            }
            return picks == null || picks.IsEmpty ? null : picks;
        }

        /// <summary>How strongly a chest's setup takes an item (SetupRules.Of): null for a chest without one, else the
        /// catalogue's fit (0: not at all).</summary>
        internal static Func<ItemDrop.ItemData, int> Reading(Container chest)
        {
            ItemPicks picks = chest != null ? Of(ChestSetup.Read(chest)) : null;
            if (picks == null)
            {
                return null;
            }
            return item => item?.m_shared == null ? 0 : picks.Fit(item.m_shared.m_name);
        }
    }
}
