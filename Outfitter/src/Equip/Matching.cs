using Outfitter.Loadouts;

namespace Outfitter.Equip
{
    /// <summary>
    /// What counts as a loadout's item (card A34, decision 5): stackable items by kind (the prefab, and the quality where
    /// the kind has qualities), counted across stacks; gear by kind at the saved quality or better (a setting), its style
    /// and its maker only when a setting asks. Which copy comes first: the best quality (unless the quality must be exact),
    /// then the least worn (or most worn, a setting). What is gear in the swap-gear modes: armour and weapons always,
    /// arrows and bolts, tools and torches each by a setting. Pure, so the scratch harness reaches it.
    /// </summary>
    internal static class Matching
    {
        /// <summary>Whether an item of this type counts as gear under these rules.</summary>
        internal static bool IsGear(ItemDrop.ItemData.ItemType type, EquipRules rules)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket:
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield:
                    return true;
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                    return rules.AmmoIsGear;
                case ItemDrop.ItemData.ItemType.Tool:
                    return rules.ToolsAreGear;
                case ItemDrop.ItemData.ItemType.Torch:
                    return rules.TorchesAreGear;
                default:
                    return false;
            }
        }

        /// <summary>Whether a quality answers a saved one under the rule.</summary>
        internal static bool QualityOk(int have, int saved, GearQuality rule)
        {
            switch (rule)
            {
                case GearQuality.Exact:
                    return have == saved;
                case GearQuality.Any:
                    return true;
                default:
                    return have >= saved;
            }
        }

        /// <summary>Whether an item (already known to be the loadout item's kind) answers it: its quality (where the kind
        /// has qualities), its style and its maker by the rules. "This exact item" asks a piece of gear for all it knows
        /// of the one saved: its maker, its quality, its style and other mods' data on it (a share code knows no maker
        /// and no data, so those aren't asked of one read from a code).</summary>
        internal static bool Answers(ItemDrop.ItemData item, LoadoutItem want, EquipRules rules)
        {
            bool levels = item.m_shared.m_maxQuality > 1;
            bool exact = rules.ExactItem && Single(item);
            if (levels && !QualityOk(item.m_quality, want.Quality, exact ? GearQuality.Exact : StackableOrGear(item, rules)))
            {
                return false;
            }
            if ((rules.MatchStyle || exact) && item.m_shared.m_variants > 1 && item.m_variant != want.Variant)
            {
                return false;
            }
            if (exact && want.CrafterId != 0 &&
                (item.m_crafterID != want.CrafterId || (item.m_crafterName ?? string.Empty) != want.CrafterName))
            {
                return false;
            }
            if (exact && want.Data != null && Capture.DataOf(item) != want.Data)
            {
                return false;
            }
            return true;
        }

        // Stackable items with qualities (fish) must match their quality exactly, as the game only stacks equal ones;
        // gear follows the rule.
        private static GearQuality StackableOrGear(ItemDrop.ItemData item, EquipRules rules) =>
            Single(item) ? rules.Quality : GearQuality.Exact;

        /// <summary>An item that never stacks (gear): each copy is one item.</summary>
        internal static bool Single(ItemDrop.ItemData item) => item.m_shared.m_maxStackSize <= 1;

        /// <summary>Broken: the game won't put it on.</summary>
        internal static bool Broken(ItemDrop.ItemData item) => item.m_shared.m_useDurability && item.m_durability <= 0f;

        /// <summary>Which of two copies comes first (negative: <paramref name="a"/>): the better quality (unless the rule is
        /// Exact, where every copy has the same), then the least worn (or most worn, by the rule), a broken copy last.</summary>
        internal static int Compare(ItemDrop.ItemData a, ItemDrop.ItemData b, EquipRules rules)
        {
            bool brokenA = Broken(a), brokenB = Broken(b);
            if (brokenA != brokenB)
            {
                return brokenA ? 1 : -1;
            }
            if (rules.Quality != GearQuality.Exact && a.m_quality != b.m_quality)
            {
                return b.m_quality.CompareTo(a.m_quality);
            }
            float wearA = Worn(a), wearB = Worn(b);
            if (wearA != wearB)
            {
                return rules.Prefer == PreferCopy.LeastWorn ? wearA.CompareTo(wearB) : wearB.CompareTo(wearA);
            }
            return 0;
        }

        /// <summary>How worn an item is, 0 (as new) to 1 (broken); 0 for an item that doesn't wear.</summary>
        internal static float Worn(ItemDrop.ItemData item)
        {
            if (!item.m_shared.m_useDurability)
            {
                return 0f;
            }
            float max = item.GetMaxDurability();
            return max > 0f ? 1f - UnityEngine.Mathf.Clamp01(item.m_durability / max) : 0f;
        }

        /// <summary>Whether an item of this type can be worn in this place (a share code or a hand-edited file may say
        /// anything: R1, findings 4 and 19). A one-handed weapon, two-handed weapon, tool or torch in the right hand; a
        /// shield, bow, left-handed staff or torch in the left; each piece of armour, cape, belt, trinket and ammo in its own
        /// place. None always fits.</summary>
        internal static bool WornFits(WornAs worn, ItemDrop.ItemData.ItemType type)
        {
            switch (worn)
            {
                case WornAs.None:
                    return true;
                case WornAs.RightHand:
                    return type == ItemDrop.ItemData.ItemType.OneHandedWeapon || type == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
                           type == ItemDrop.ItemData.ItemType.Tool || type == ItemDrop.ItemData.ItemType.Torch;
                case WornAs.LeftHand:
                    return type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.Bow ||
                           type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || type == ItemDrop.ItemData.ItemType.Torch;
                default:
                    return BodyPlace(type) == worn;
            }
        }

        /// <summary>The body place an item of this type is worn in (for "keep wearing mine when a piece is missing"); None
        /// for hand items, which are told apart by hand.</summary>
        internal static WornAs BodyPlace(ItemDrop.ItemData.ItemType type)
        {
            switch (type)
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
                    return WornAs.Utility;
                case ItemDrop.ItemData.ItemType.Trinket:
                    return WornAs.Trinket;
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                    return WornAs.Ammo;
                default:
                    return WornAs.None;
            }
        }
    }
}
