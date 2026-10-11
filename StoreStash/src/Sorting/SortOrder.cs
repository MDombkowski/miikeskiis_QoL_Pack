using System;
using System.Collections.Generic;
using System.Globalization;
using QoLMods.Catalogue;

namespace StoreStash
{
    /// <summary>The orders a sort can follow (the setting "Sort order").</summary>
    internal enum SortOrderChoice
    {
        /// <summary>The pack's own order, as Add to Cart lists the Take tab: families, each family's members, then
        /// progression (Meadows to the Deep North).</summary>
        PacksOrder,

        /// <summary>By the game's own item type, then the shown name.</summary>
        ByType,

        /// <summary>By the name he reads.</summary>
        ByName,

        /// <summary>The heaviest stacks first.</summary>
        ByWeight,

        /// <summary>What a trader pays for one, most first.</summary>
        ByValue,
    }

    /// <summary>What an order compares about one stack, worked out once per sort (card A53). Plain values, so the scratch
    /// harness can compare without the game.</summary>
    internal sealed class SortKey
    {
        /// <summary>The pack's order: the family's place, its member's, and Gear's sub-heading's (a tool's use, a weapon's
        /// type, the ammunition's), each from the catalogue part's orders.</summary>
        internal double Family;

        internal double Member;

        internal double Sub;

        /// <summary>Progression: the biome's place (Meadows first, "Other" last), then the item's hand-placed order.</summary>
        internal int Biome;

        internal int Explicit = 999;

        /// <summary>The game's item type, by its number.</summary>
        internal int Type;

        /// <summary>The name he reads, and the game's own name for it ("$item_wood"), the last word between two names that
        /// read alike.</summary>
        internal string Name = string.Empty;

        internal string Token = string.Empty;

        internal int Quality;

        internal int WorldLevel;

        internal float Durability;

        /// <summary>One's weight, as the game weighs it (quality included): a stack weighs this times its amount, the
        /// amount as planned, after any merge.</summary>
        internal float UnitWeight;

        /// <summary>What a trader pays for one.</summary>
        internal int Value;
    }

    /// <summary>
    /// The orders a sort can follow, as comparisons of two stacks' keys. Every order ends the same way: the name he reads
    /// (ignoring case), the game's name, the highest quality first, then the higher world level, the fuller stack, the
    /// less worn; SortPlan's last word is where each stack was, so equal stacks keep their order and a sorted grid sorted
    /// again doesn't change.
    /// </summary>
    internal static class SortOrder
    {
        private static readonly StringComparer Names = StringComparer.Create(CultureInfo.InvariantCulture, true);

        internal static Comparison<SortEntry> For(SortOrderChoice choice)
        {
            switch (choice)
            {
                case SortOrderChoice.ByType:
                    return (a, b) => Then(a.Key.Type.CompareTo(b.Key.Type), a, b);
                case SortOrderChoice.ByName:
                    return (a, b) => Then(0, a, b);
                case SortOrderChoice.ByWeight:
                    return (a, b) => Then(Weight(b).CompareTo(Weight(a)), a, b);
                case SortOrderChoice.ByValue:
                    return (a, b) => Then(b.Key.Value.CompareTo(a.Key.Value), a, b);
                default:
                    return Packs;
            }
        }

        /// <summary>A stack's weight as planned: one's weight times its planned amount.</summary>
        internal static float Weight(SortEntry entry) => entry.Key.UnitWeight * entry.NewStack;

        /// <summary>The pack's order: family, member, sub-heading, biome, the hand-placed order, then the common end.</summary>
        internal static int Packs(SortEntry a, SortEntry b)
        {
            SortKey x = a.Key;
            SortKey y = b.Key;
            int order = x.Family.CompareTo(y.Family);
            if (order == 0)
            {
                order = x.Member.CompareTo(y.Member);
            }
            if (order == 0)
            {
                order = x.Sub.CompareTo(y.Sub);
            }
            if (order == 0)
            {
                order = x.Biome.CompareTo(y.Biome);
            }
            if (order == 0)
            {
                order = x.Explicit.CompareTo(y.Explicit);
            }
            return Then(order, a, b);
        }

        // The common end of every order, after its own first comparison.
        private static int Then(int order, SortEntry a, SortEntry b)
        {
            if (order != 0)
            {
                return order;
            }
            SortKey x = a.Key;
            SortKey y = b.Key;
            order = Names.Compare(x.Name, y.Name);
            if (order == 0)
            {
                order = string.CompareOrdinal(x.Token, y.Token);
            }
            if (order == 0)
            {
                order = y.Quality.CompareTo(x.Quality);
            }
            if (order == 0)
            {
                order = y.WorldLevel.CompareTo(x.WorldLevel);
            }
            if (order == 0)
            {
                order = b.NewStack.CompareTo(a.NewStack);   // the amount as planned, after any merge
            }
            if (order == 0)
            {
                order = y.Durability.CompareTo(x.Durability);
            }
            return order;
        }

        /// <summary>
        /// The pack's order's places for an item, from where the catalogue part puts it: its family, its member (Gear's
        /// Tools, Weapons …; Metals' Copper, Tin …), Gear's sub-heading, its biome's place and its hand-placed order.
        /// Trophies and Creature Parts have biomes for members, so their biome orders them.
        /// </summary>
        internal static void PlacePacks(SortKey key, ItemHome home, string biome, Orders orders)
        {
            home = home ?? ItemHome.Unknown;
            key.Family = orders.FamilyPlace(home.Family);
            key.Member = orders.MemberPlace(home.Family, home.Section);
            key.Sub = home.Family == "Gear" ? SubPlace(home.Section, home.Sub) : 0;
            key.Biome = Taxonomy.BiomeRank(biome);
            key.Explicit = home.ExplicitOrder;
        }

        private static double SubPlace(string section, string sub)
        {
            IList<string> order = section == "Tools" ? Taxonomy.ToolUseOrder
                : section == "Weapons" ? Taxonomy.WeaponOrder
                : section == "Ammo" ? Taxonomy.AmmoOrder
                : null;
            if (order == null)
            {
                return 0;
            }
            int at = order.IndexOf(sub);
            return at < 0 ? 900 : at;
        }

        // The orders from a family line, read once per line (the setting can change while the game runs).
        private static string ordersLine;
        private static Orders ordersRead;

        /// <summary>The catalogue's orders with the families as the setting lists them (empty: his order).</summary>
        internal static Orders OrdersFrom(string familyLine)
        {
            if (ordersRead == null || familyLine != ordersLine)
            {
                ordersLine = familyLine;
                ordersRead = Orders.From(null, null, null, null, familyLine);
            }
            return ordersRead;
        }
    }
}
