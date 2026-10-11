using System;
using System.Collections.Generic;

namespace TopperUpper.Logic
{
    /// <summary>
    /// One thing a light or station takes, in its own order of priority: the item (by the key the counts use, its shared
    /// name), what it becomes, and how many of it make how many of that. Plain values, so the scratch harness checks the
    /// rule without the game.
    /// </summary>
    internal sealed class Lane
    {
        /// <summary>The item it takes ("$item_blackmetalscrap").</summary>
        internal string Input;

        /// <summary>The item's prefab name ("BlackMetalScrap"), as the station's own message names it; not used by the rule.</summary>
        internal string Prefab;

        /// <summary>What it becomes ("$item_blackmetal"), or null for fuel and a light's fuel, which make nothing to count.</summary>
        internal string Product;

        /// <summary>How many of the input make one batch of products (the Frigid Kiln: 5 Ice for one Liquid Frost; else 1).</summary>
        internal int UnitsPerProduct = 1;

        /// <summary>How many products one batch is (a fermenter's mead base: 6 meads; else 1).</summary>
        internal int ProductsPerBatch = 1;

        /// <summary>Units of this input already in this station toward its next products, beyond whole batches already
        /// counted: the Frigid Kiln's Ice; 0 for every other station, whose queue counts whole.</summary>
        internal float PendingUnits;
    }

    /// <summary>What a plan gives one lane: how many of its input go in, and how many products that makes.</summary>
    internal sealed class Grant
    {
        internal Lane Lane;
        internal string Input;
        internal string Product;
        internal int Amount;
        internal int Products;

        /// <summary>How many products <paramref name="given"/> of this grant's input make (fewer than planned when the
        /// chests gave less).</summary>
        internal int ProductsOf(int given) =>
            Product == null ? 0 : FeedRule.ProductsFrom(given, Lane.UnitsPerProduct, Lane.ProductsPerBatch, Lane.PendingUnits);
    }

    /// <summary>
    /// His rules for how much goes into a light or station (card A77; the research's section 3.4): for each input in the
    /// station's order, the most it may take is the smallest of the room left, the count above the input's reserve, what's
    /// still wanted below its product's maximum, and what the station's chests can give (leave-one already off); the room
    /// left passes to the next input. An input stopped by its reserve, its maximum or the never-take list passes its turn.
    /// Reserves and maximums are both ceilings, and priority only decides who gets the room first.
    /// </summary>
    internal static class FeedRule
    {
        /// <summary>No limit (no reserve, no maximum).</summary>
        internal const int Unlimited = int.MaxValue;

        /// <summary>
        /// The plan for one light or station. <paramref name="aboveReserve"/>: how many of an input may be taken by the
        /// reserve (Unlimited without one); <paramref name="productsWanted"/>: how many more of a product may be made by its
        /// maximum (Unlimited without one), counting what's queued and lying already; <paramref name="canGive"/>: what the
        /// chests this feed may use can give of an input; <paramref name="never"/>: items never taken. Each is asked once
        /// per item and spent as the plan goes, so an item in two lanes, or a product two inputs make, is counted once.
        /// </summary>
        internal static List<Grant> Plan(int room, IList<Lane> lanes, Func<string, int> aboveReserve, Func<string, int> productsWanted,
                                         Func<string, int> canGive, Func<string, bool> never)
        {
            var grants = new List<Grant>();
            if (room <= 0 || lanes == null)
            {
                return grants;
            }
            var reserveLeft = new Dictionary<string, int>(StringComparer.Ordinal);
            var giveLeft = new Dictionary<string, int>(StringComparer.Ordinal);
            var wantedLeft = new Dictionary<string, int>(StringComparer.Ordinal);
            int roomLeft = room;
            foreach (Lane lane in lanes)
            {
                if (roomLeft <= 0)
                {
                    break;
                }
                if (lane?.Input == null || (never != null && never(lane.Input)))
                {
                    continue;
                }
                if (!reserveLeft.ContainsKey(lane.Input))
                {
                    reserveLeft[lane.Input] = Math.Max(0, aboveReserve != null ? aboveReserve(lane.Input) : Unlimited);
                    giveLeft[lane.Input] = Math.Max(0, canGive != null ? canGive(lane.Input) : 0);
                }
                int amount = Math.Min(roomLeft, Math.Min(reserveLeft[lane.Input], giveLeft[lane.Input]));
                if (lane.Product != null)
                {
                    if (!wantedLeft.ContainsKey(lane.Product))
                    {
                        wantedLeft[lane.Product] = Math.Max(0, productsWanted != null ? productsWanted(lane.Product) : Unlimited);
                    }
                    amount = Math.Min(amount, MostUnits(wantedLeft[lane.Product], lane.UnitsPerProduct, lane.ProductsPerBatch, lane.PendingUnits));
                }
                if (amount <= 0)
                {
                    continue;   // stopped by its reserve, its maximum or its chests: its turn passes
                }
                int products = lane.Product != null ? ProductsFrom(amount, lane.UnitsPerProduct, lane.ProductsPerBatch, lane.PendingUnits) : 0;
                grants.Add(new Grant { Lane = lane, Input = lane.Input, Product = lane.Product, Amount = amount, Products = products });
                roomLeft -= amount;
                reserveLeft[lane.Input] = Sub(reserveLeft[lane.Input], amount);
                giveLeft[lane.Input] -= amount;
                if (lane.Product != null)
                {
                    wantedLeft[lane.Product] = Sub(wantedLeft[lane.Product], products);
                }
            }
            return grants;
        }

        /// <summary>
        /// The most units of an input that make no more than <paramref name="wanted"/> new products, given
        /// <paramref name="pending"/> units already in the station. The station spends its units as it works, a product
        /// in progress included (the Frigid Kiln burns its Ice through each product's 30 s: 24.83 Ice left after a second
        /// still makes 5 Liquid Frost), so P pending units make ceil(P / k) products (Pending), and x more make
        /// ceil((P + x) / k) - ceil(P / k) new batches of n. None while not even one batch is wanted (a fermenter's six
        /// meads when five are wanted: it never overshoots the maximum). Unlimited stays unlimited.
        /// </summary>
        internal static int MostUnits(int wanted, int unitsPerProduct, int productsPerBatch, float pending)
        {
            if (wanted == Unlimited)
            {
                return Unlimited;
            }
            int k = Math.Max(1, unitsPerProduct);
            int n = Math.Max(1, productsPerBatch);
            int batches = Math.Max(0, wanted) / n;
            if (batches <= 0)
            {
                return 0;
            }
            if (k == 1)
            {
                return batches;
            }
            double p = Math.Max(0.0, pending);
            // ceil((p + x) / k) <= ceil(p / k) + batches  <=>  x <= k * (ceil(p / k) + batches) - p.
            double most = Math.Floor(k * (Pending(p, k) + batches) - p + 1e-6);
            return most <= 0 ? 0 : most >= Unlimited ? Unlimited : (int)most;
        }

        /// <summary>How many new products <paramref name="units"/> more units make (see MostUnits).</summary>
        internal static int ProductsFrom(int units, int unitsPerProduct, int productsPerBatch, float pending)
        {
            int k = Math.Max(1, unitsPerProduct);
            int n = Math.Max(1, productsPerBatch);
            if (k == 1)
            {
                return units * n;
            }
            double p = Math.Max(0.0, pending);
            return (Pending(p + units, k) - Pending(p, k)) * n;
        }

        /// <summary>How many products <paramref name="units"/> units already in a station make, the one in progress
        /// included: ceil(units / k), with a hair's slack for float fuel (the game zeroes fuel below 0.0001).</summary>
        internal static int Pending(double units, int unitsPerProduct)
        {
            int k = Math.Max(1, unitsPerProduct);
            return units <= 1e-4 ? 0 : (int)Math.Ceiling(units / k - 1e-6);
        }

        /// <summary>How many of an input may be taken by its reserve: the count above it, or Unlimited without one.</summary>
        internal static int AboveReserve(int count, int? reserve) => reserve.HasValue ? Math.Max(0, count - reserve.Value) : Unlimited;

        /// <summary>How many more of a product may be made by its maximum: the maximum less what's counted, or Unlimited
        /// without one.</summary>
        internal static int Wanted(int counted, int? maximum) => maximum.HasValue ? Math.Max(0, maximum.Value - counted) : Unlimited;

        private static int Sub(int left, int used) => left == Unlimited ? Unlimited : Math.Max(0, left - used);
    }
}
