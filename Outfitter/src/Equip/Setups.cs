using System;
using System.Collections.Generic;
using QoLMods.Catalogue;
using QoLMods.Chests;

namespace Outfitter.Equip
{
    /// <summary>
    /// Chests' setups (StoreStash's chest setups, card A41) as Outfitter reads them: the text in a chest's own record
    /// (ChestSetup.Read, the chest service) read as the catalogue's picks (ItemPicks), each text once, since this runs for
    /// every item and chest of a plan. Outfitter only reads setups; StoreStash writes them. A text from a newer version or
    /// too long takes nothing (ItemPicks), so such a chest counts as set up and takes nothing, as in StoreStash.
    /// </summary>
    internal static class Setups
    {
        private const int Kept = 512;

        private static readonly Dictionary<string, ItemPicks> Read = new Dictionary<string, ItemPicks>(StringComparer.Ordinal);

        /// <summary>The chest's setup text; the chest service's reading in play, another in the scratch harness.</summary>
        internal static Func<Container, string> TextOf = ChestSetup.Read;

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
            ItemPicks picks = chest != null ? Of(TextOf(chest)) : null;
            if (picks == null)
            {
                return null;
            }
            return item => item?.m_shared == null ? 0 : picks.Fit(item.m_shared.m_name);
        }
    }
}
