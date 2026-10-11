// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;

namespace QoLMods.Chests
{
    /// <summary>
    /// Which chest a quick-stacked item may go to, and which first, worked out in this one place (card A52, decision 9), so
    /// every module that stacks follows the same routing: StoreStash's P, its personal key (a Filter of its own, card A43),
    /// Loadouts' put-away (its own Pick), and chests' setups (card A41, StoreStash's "Chest setups": StackRules.Setups).
    /// An item goes to a chest that already holds one by name (the game's own Place stacks rule), a trophy to any chest
    /// holding a trophy when the rules say so (or first to one holding trophies of its biome, card A82), and with "only
    /// where held" off to any chest; the caller's Filter has the
    /// last word, and a chest someone else marked as theirs (ChestMine) takes nothing while the rules leave it alone. A
    /// set-up chest takes what its setup takes, even when it holds none of it yet, and nothing else (unless
    /// SetupRules.KeepHeld). Which chest first is the caller's list of rules (StackRules.Order, card A41's second round,
    /// decision 28): the first rule that tells two chests apart decides, and each rule makes tiers the planner fills one
    /// after the other, so the chest a rule puts first gets all it can take before the next is touched; chests no rule
    /// tells apart go by rank, then the nearest.
    /// </summary>
    internal static class StackRouting
    {
        /// <summary>The order when the caller gives none: StoreStash 0.4.0's, with its three switches on.</summary>
        internal static readonly ChestOrder[] DefaultOrder = { ChestOrder.SetUp, ChestOrder.Specific, ChestOrder.Most };

        /// <summary>Whether <paramref name="chest"/>, holding <paramref name="contents"/>, may take
        /// <paramref name="item"/>.</summary>
        internal static bool Accepts(Container chest, Inventory contents, ItemDrop.ItemData item, StackRules rules) =>
            Rank(chest, contents, item, rules) >= 0;

        /// <summary>The rank of a chest meant for the item: it holds that item already, or its setup takes it.</summary>
        internal const int MeantFor = 0;

        /// <summary>The rank of a trophy chest for a trophy: one holding any trophy (TrophyRule.AnyTrophyChest), or one
        /// holding a trophy of its biome (TrophyRule.SameBiomeTrophyChest and SameBiomeTrophyChestOnly, card A82).</summary>
        internal const int TrophyChest = 1;

        /// <summary>The rank of a trophy chest holding no trophy of its biome, for TrophyRule.SameBiomeTrophyChest: after
        /// those that do (card A82).</summary>
        internal const int OtherTrophyChest = 2;

        /// <summary>The rank of any other chest the rules allow ("only where held" off). Ranks below it are a chest meant
        /// for the item in some way (ChestStacker counts such a chest someone has open as one in use).</summary>
        internal const int AnyChest = 3;

        /// <summary>
        /// How good a home <paramref name="chest"/> is for <paramref name="item"/>: MeantFor (0) if it holds that item
        /// already, or its setup takes it; TrophyChest (1) a trophy chest for a trophy (of its biome, under the biome
        /// rules); OtherTrophyChest (2) another trophy chest, under TrophyRule.SameBiomeTrophyChest; AnyChest (3) any other
        /// chest the rules allow; -1 if the rules don't let it take the item. Among chests the order's rules don't tell
        /// apart, a better rank comes first, and within one rank the nearest (Tiers).
        /// </summary>
        internal static int Rank(Container chest, Inventory contents, ItemDrop.ItemData item, StackRules rules) =>
            Rank(chest, contents, item, rules, SetupOf(chest, rules));

        // The same, with the chest's setup already read (null: it has none): Tiers reads each chest's once per plan.
        private static int Rank(Container chest, Inventory contents, ItemDrop.ItemData item, StackRules rules, Func<ItemDrop.ItemData, int> setup)
        {
            if (rules.Filter != null && !rules.Filter(chest, item))
            {
                return -1;
            }
            // Read from the chest's record, so it holds in every game on the pack, whichever module stacks (card A43).
            if (rules.LeaveOthersMarked && ChestMine.MarkedByOther(chest, ChestClaims.PlayerId()))
            {
                return -1;
            }
            bool holdsIt = contents.ContainsItemByName(item.m_shared.m_name);
            if (setup != null)
            {
                // A set-up chest takes what its setup takes, and is meant for it as a chest holding it is; nothing else
                // (strict), unless it also takes what it already holds.
                return Rank(setup(item) > 0, holdsIt, rules.Setups);
            }
            // A chest's trophies are looked through only for a trophy it doesn't hold that may go to a trophy chest.
            bool trophy = StackItems.IsTrophy(item);
            bool trophyChest = false;
            bool ofItsBiome = false;
            if (!holdsIt && trophy && UsesTrophyChests(rules.Trophies))
            {
                Heightmap.Biome biome = rules.Trophies == TrophyRule.AnyTrophyChest ? Heightmap.Biome.None : BiomeOf(item.m_shared.m_name, rules.TrophyBiome);
                TrophiesHeld(contents, biome, rules.Trophies == TrophyRule.AnyTrophyChest ? null : rules, out trophyChest, out ofItsBiome);
            }
            return Rank(holdsIt, trophy, trophyChest, ofItsBiome, rules);
        }

        /// <summary>The rank from the facts alone (see the other Rank), for the scratch test harness too: a chest holding a
        /// trophy counts as holding one of the trophy's biome as well.</summary>
        internal static int Rank(bool holdsIt, bool isTrophy, bool holdsATrophy, StackRules rules) =>
            Rank(holdsIt, isTrophy, holdsATrophy, holdsATrophy, rules);

        /// <summary>The rank from the facts alone (see the other Rank): <paramref name="holdsATrophy"/>, the chest holds a
        /// trophy of any kind; <paramref name="holdsItsBiome"/>, a trophy of the item's biome (card A82).</summary>
        internal static int Rank(bool holdsIt, bool isTrophy, bool holdsATrophy, bool holdsItsBiome, StackRules rules)
        {
            if (holdsIt)
            {
                return MeantFor;
            }
            if (isTrophy)
            {
                switch (rules.Trophies)
                {
                    case TrophyRule.AnyTrophyChest:
                        if (holdsATrophy)
                        {
                            return TrophyChest;
                        }
                        break;
                    case TrophyRule.SameBiomeTrophyChest:
                        if (holdsItsBiome)
                        {
                            return TrophyChest;
                        }
                        if (holdsATrophy)
                        {
                            return OtherTrophyChest;
                        }
                        break;
                    case TrophyRule.SameBiomeTrophyChestOnly:
                        // Only its biome's trophy chest, or none: never any other chest, whatever "only where held" says.
                        return holdsItsBiome ? TrophyChest : -1;
                }
            }
            return rules.OnlyWhereHeld ? -1 : AnyChest;
        }

        /// <summary>Whether a trophy may go to a chest holding other trophies under this rule (not LikeAnyItem or
        /// Never).</summary>
        internal static bool UsesTrophyChests(TrophyRule rule) =>
            rule == TrophyRule.AnyTrophyChest || rule == TrophyRule.SameBiomeTrophyChest || rule == TrophyRule.SameBiomeTrophyChestOnly;

        /// <summary>
        /// Whether a chest holds a trophy of any kind (<paramref name="any"/>), and one of <paramref name="biome"/>
        /// (<paramref name="ofBiome"/>), each trophy's biome read through <paramref name="rules"/>' TrophyBiome (null rules:
        /// the biome isn't asked, and ofBiome is false). A trophy the reading can't place is of Heightmap.Biome.None, which
        /// matches only None.
        /// </summary>
        internal static void TrophiesHeld(Inventory contents, Heightmap.Biome biome, StackRules rules, out bool any, out bool ofBiome)
        {
            any = false;
            ofBiome = false;
            foreach (ItemDrop.ItemData held in contents.GetAllItems())
            {
                if (held?.m_shared == null || !StackItems.IsTrophy(held))
                {
                    continue;
                }
                any = true;
                if (rules == null)
                {
                    return;   // any trophy is enough
                }
                if (BiomeOf(held.m_shared.m_name, rules.TrophyBiome) == biome)
                {
                    ofBiome = true;
                    return;
                }
            }
        }

        // A trophy's biome as the caller reads it; None without a reading (every trophy then of one unknown biome).
        private static Heightmap.Biome BiomeOf(string sharedName, Func<string, Heightmap.Biome> reading) =>
            reading != null && sharedName != null ? reading(sharedName) : Heightmap.Biome.None;

        /// <summary>A set-up chest's rank from the facts alone: 0 when its setup takes the item, or it holds it and keeps
        /// what it holds; else -1.</summary>
        internal static int Rank(bool setupTakesIt, bool holdsIt, SetupRules setups) =>
            setupTakesIt || (holdsIt && setups != null && setups.KeepHeld) ? 0 : -1;

        /// <summary>
        /// For each item, the chests (by index into <paramref name="chests"/>, which is nearest first) that may take it,
        /// best first: the tiers of Tiers one after another.
        /// </summary>
        internal static List<List<int>> Candidates(IList<ItemDrop.ItemData> items, IList<Container> chests, IList<Inventory> contents, StackRules rules)
        {
            var all = new List<List<int>>();
            foreach (List<List<int>> tiers in Tiers(items, chests, contents, rules))
            {
                var order = new List<int>();
                foreach (List<int> tier in tiers)
                {
                    order.AddRange(tier);
                }
                all.Add(order);
            }
            return all;
        }

        /// <summary>
        /// For each item, the chests (by index into <paramref name="chests"/>, which is nearest first) that may take it, in
        /// tiers the planner fills one after the other (StackPlanner.Plan), as StackRules.Order says (Order, below): with the
        /// default order, the set-up chests whose setup takes it come first (the more specific setup first, then the one
        /// holding the most of it, each its own tier), then the rest by rank and nearness, in one tier. What the set-up
        /// chests can't fit goes on to the rest only with SetupRules.Overflow; an item no set-up chest takes goes to chests
        /// without a setup only with SetupRules.Others (set-up chests keeping what they hold still take it).
        /// <paramref name="away"/> are chests the quick stack couldn't have this time (someone else has one open, its owner
        /// didn't answer): a set-up chest among them that takes an item still counts as its home for Overflow and Others,
        /// so with Overflow off what it would have taken stays with him rather than going to other chests. A chest's setup,
        /// and whether it's one of his personal chests, are read once per chest for each plan. The items' tiers are set
        /// side by side (Aligned), so a set-up chest's room goes to what its setup takes before what it merely holds (an
        /// empty tier where an item has none).
        /// </summary>
        internal static List<List<List<int>>> Tiers(IList<ItemDrop.ItemData> items, IList<Container> chests, IList<Inventory> contents, StackRules rules,
                                                     ICollection<Container> away = null)
        {
            var setups = new Func<ItemDrop.ItemData, int>[chests.Count];
            for (int c = 0; c < chests.Count; c++)
            {
                setups[c] = SetupOf(chests[c], rules);
            }
            List<ChestOrder> order = Effective(rules.Order, rules.Setups != null);
            bool[] personal = null;
            if (rules.Personal != null && order.Contains(ChestOrder.Personal))
            {
                personal = new bool[chests.Count];
                for (int c = 0; c < chests.Count; c++)
                {
                    personal[c] = chests[c] != null && rules.Personal(chests[c]);
                }
            }
            var awayChests = new List<Container>();
            var awaySetups = new List<Func<ItemDrop.ItemData, int>>();
            if (away != null && rules.Setups != null)
            {
                foreach (Container chest in away)
                {
                    Func<ItemDrop.ItemData, int> setup = chest != null ? SetupOf(chest, rules) : null;
                    if (setup != null && !chests.Contains(chest))
                    {
                        awayChests.Add(chest);
                        awaySetups.Add(setup);
                    }
                }
            }
            var all = new List<List<Tier>>(items.Count);
            var homes = new List<Place>();
            var rest = new List<Place>();
            foreach (ItemDrop.ItemData item in items)
            {
                homes.Clear();
                rest.Clear();
                for (int c = 0; c < chests.Count; c++)
                {
                    int rank = Rank(chests[c], contents[c], item, rules, setups[c]);
                    if (rank < 0)
                    {
                        continue;
                    }
                    int fit = setups[c] != null ? setups[c](item) : 0;
                    bool mine = personal != null && personal[c];
                    if (fit > 0)
                    {
                        homes.Add(new Place(c, rank, fit, Held(contents[c], item.m_shared.m_name), setUp: true, mine));
                    }
                    else
                    {
                        rest.Add(new Place(c, rank, 0, 0, setups[c] != null, mine));
                    }
                }
                bool homeAway = false;
                for (int a = 0; a < awayChests.Count && !homeAway; a++)
                {
                    // By the same rules (the caller's filter too), as if it were here.
                    homeAway = awaySetups[a](item) > 0 && Rank(awayChests[a], awayChests[a].GetInventory(), item, rules, awaySetups[a]) >= 0;
                }
                all.Add(OrderTiers(homes, rest, rules.Setups, order, homeAway));
            }
            return Aligned(all, Band(order));
        }

        /// <summary>One tier of an item's chests (by index into the nearest-first list), with the keys every chest in it has
        /// for the rules that count (Key): the higher the keys, the earlier the tier.</summary>
        internal sealed class Tier
        {
            internal readonly int[] Keys;
            internal readonly List<int> Chests = new List<int>();

            internal Tier(int[] keys)
            {
                Keys = keys;
            }
        }

        /// <summary>
        /// Every item's tiers set side by side (card A41's second round, reviews 1 and 2): the planner fills every item's
        /// first tier, then every item's second, and so on, so each tier goes to its pass among all the items' tiers, with
        /// an empty tier where an item has none. The passes run by a tier's band first (its keys for the rules down to and
        /// including SetUp, or all of them without SetUp), the higher first; then by its place in its own item's list, so
        /// an item's first choice comes before another item's second, as it always did; then by its other keys, the higher
        /// first. So a set-up chest's room goes to what its setup takes before what it merely holds ("A set-up chest also
        /// takes what it holds"); everything else is as with each item's tiers on their own, and chests without a setup go
        /// as they always did. <paramref name="band"/> is how many of the keys make the band (Band). Each item's own tiers
        /// keep their order; with no rule that counts, each item has one tier and every tier is in the one pass.
        /// </summary>
        internal static List<List<List<int>>> Aligned(List<List<Tier>> perItem, int band)
        {
            // Every tier's pass, as one row of numbers compared the higher first: its band, its place in its item's list
            // (negated: the earlier first), then its other keys.
            var passes = new Dictionary<string, int[]>(StringComparer.Ordinal);
            var passOf = new List<List<string>>(perItem.Count);
            foreach (List<Tier> tiers in perItem)
            {
                var names = new List<string>(tiers.Count);
                for (int depth = 0; depth < tiers.Count; depth++)
                {
                    int[] keys = tiers[depth].Keys;
                    int split = Math.Min(Math.Max(band, 0), keys.Length);
                    var pass = new int[keys.Length + 1];
                    Array.Copy(keys, 0, pass, 0, split);
                    pass[split] = -depth;
                    Array.Copy(keys, split, pass, split + 1, keys.Length - split);
                    string name = string.Join(",", pass);
                    passes[name] = pass;
                    names.Add(name);
                }
                passOf.Add(names);
            }
            var order = new List<int[]>(passes.Values);
            order.Sort((a, b) => CompareKeys(b, a));   // the higher first
            var index = new Dictionary<string, int>(order.Count, StringComparer.Ordinal);
            for (int p = 0; p < order.Count; p++)
            {
                index[string.Join(",", order[p])] = p;
            }
            int count = Math.Max(1, order.Count);
            var all = new List<List<List<int>>>(perItem.Count);
            for (int i = 0; i < perItem.Count; i++)
            {
                // The planner only reads tiers: an empty one is the one shared empty list.
                var aligned = new List<List<int>>(count);
                for (int p = 0; p < count; p++)
                {
                    aligned.Add(NoChests);
                }
                for (int depth = 0; depth < perItem[i].Count; depth++)
                {
                    aligned[index[passOf[i][depth]]] = perItem[i][depth].Chests;
                }
                all.Add(aligned);
            }
            return all;
        }

        // An empty tier, shared (never written to).
        private static readonly List<int> NoChests = new List<int>();

        /// <summary>How many of a tier's keys make its band (Aligned): the rules down to and including SetUp, or all of
        /// them while SetUp doesn't count.</summary>
        internal static int Band(List<ChestOrder> order)
        {
            int setUp = order.IndexOf(ChestOrder.SetUp);
            return setUp >= 0 ? setUp + 1 : order.Count;
        }

        // Two passes, number by number (every pass of one plan has as many).
        private static int CompareKeys(int[] a, int[] b)
        {
            for (int r = 0; r < a.Length && r < b.Length; r++)
            {
                if (a[r] != b[r])
                {
                    return a[r].CompareTo(b[r]);
                }
            }
            return a.Length.CompareTo(b.Length);
        }

        /// <summary>One chest that may take an item, as the tiers are ordered: its place in the nearest-first list, its
        /// rank, how its setup takes the item (0: it doesn't), how much of the item it holds, whether it has a setup, and
        /// whether it's one of his personal chests (StackRules.Personal).</summary>
        internal readonly struct Place
        {
            internal readonly int Chest;
            internal readonly int Rank;
            internal readonly int Fit;
            internal readonly int Held;
            internal readonly bool SetUp;
            internal readonly bool Personal;

            internal Place(int chest, int rank, int fit, int held, bool setUp, bool personal = false)
            {
                Chest = chest;
                Rank = rank;
                Fit = fit;
                Held = held;
                SetUp = setUp;
                Personal = personal;
            }
        }

        /// <summary>
        /// The rules that count, first to last (card A41's second round, decision 28): the caller's order (null: the
        /// default, DefaultOrder), each rule once; without setups (<paramref name="setups"/> false) not SetUp, Specific or
        /// Most; and Specific and Most only after SetUp, since they tell set-up chests apart only.
        /// </summary>
        internal static List<ChestOrder> Effective(IList<ChestOrder> order, bool setups)
        {
            var effective = new List<ChestOrder>(4);
            bool setUpBefore = false;
            foreach (ChestOrder rule in order ?? DefaultOrder)
            {
                if (effective.Contains(rule))
                {
                    continue;
                }
                switch (rule)
                {
                    case ChestOrder.SetUp:
                        if (!setups)
                        {
                            continue;
                        }
                        setUpBefore = true;
                        break;
                    case ChestOrder.Specific:
                    case ChestOrder.Most:
                        if (!setups || !setUpBefore)
                        {
                            continue;
                        }
                        break;
                    case ChestOrder.Personal:
                        break;
                    default:
                        continue;   // a rule this version doesn't know
                }
                effective.Add(rule);
            }
            return effective;
        }

        /// <summary>
        /// A chest's key for one rule: the higher, the earlier. SetUp: 1 for a set-up chest whose setup takes the item
        /// (<paramref name="home"/>), else 0. Specific: a home's fit (how specific its setup is), else 0. Most: how much of the
        /// item a home holds, else 0, so chests without a setup stay in their order among themselves. Personal: 1 for one of
        /// his personal chests, else 0.
        /// </summary>
        internal static int Key(ChestOrder rule, Place place, bool home)
        {
            switch (rule)
            {
                case ChestOrder.SetUp:
                    return home ? 1 : 0;
                case ChestOrder.Specific:
                    return home ? place.Fit : 0;
                case ChestOrder.Most:
                    return home ? place.Held : 0;
                case ChestOrder.Personal:
                    return place.Personal ? 1 : 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// The tiers for one item, from its places alone (so the scratch harness checks the rules without the game):
        /// <paramref name="homes"/> are the set-up chests whose setup takes it, <paramref name="rest"/> every other chest
        /// that may, <paramref name="order"/> the caller's rules (StackRules.Order; null: the default), and
        /// <paramref name="homeAway"/> says a set-up chest that takes it is out of reach this time. The chests are sorted by
        /// their keys for the rules that count (Effective, Key), the first rule first, each the higher first; then by rank;
        /// then nearest first, by the chest's place in the nearest-first list: a total order, so the same chests always come
        /// in the same order (List.Sort isn't stable). A new tier starts wherever the keys change, so every rule is a tier
        /// boundary; with no rule that counts, one tier.
        /// </summary>
        internal static List<List<int>> Order(List<Place> homes, List<Place> rest, SetupRules setups, IList<ChestOrder> order, bool homeAway = false)
        {
            List<List<int>> chests = OrderTiers(homes, rest, setups, Effective(order, setups != null), homeAway).ConvertAll(tier => tier.Chests);
            if (chests.Count == 0)
            {
                chests.Add(new List<int>());   // no chest may take it: one empty tier
            }
            return chests;
        }

        /// <summary>The same, as tiers with their keys (none when no chest may take the item), the rules that count already
        /// worked out (<paramref name="order"/>, from Effective: Tiers works them out once for every item).</summary>
        internal static List<Tier> OrderTiers(List<Place> homes, List<Place> rest, SetupRules setups, List<ChestOrder> order, bool homeAway)
        {
            bool hasHome = homes.Count > 0 || homeAway;
            var places = new List<Place>(homes.Count + rest.Count);
            places.AddRange(homes);
            foreach (Place place in rest)
            {
                // Beside set-up chests that take it, the rest only get what those can't fit; with none, chests without a
                // setup take it only if the setups leave them the rest.
                bool keep = setups == null || (hasHome ? setups.Overflow : setups.Others || place.SetUp);
                if (keep)
                {
                    places.Add(place);
                }
            }
            // Each place's keys, a rule after another (the homes come first in places).
            var keys = new int[places.Count, order.Count];
            for (int p = 0; p < places.Count; p++)
            {
                for (int r = 0; r < order.Count; r++)
                {
                    keys[p, r] = Key(order[r], places[p], p < homes.Count);
                }
            }
            var sorted = new List<int>(places.Count);
            for (int p = 0; p < places.Count; p++)
            {
                sorted.Add(p);
            }
            sorted.Sort((a, b) =>
            {
                for (int r = 0; r < order.Count; r++)
                {
                    if (keys[a, r] != keys[b, r])
                    {
                        return keys[b, r].CompareTo(keys[a, r]);
                    }
                }
                Place x = places[a], y = places[b];
                return x.Rank != y.Rank ? x.Rank.CompareTo(y.Rank) : x.Chest.CompareTo(y.Chest);
            });
            var tiers = new List<Tier>();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i == 0 || !SameKeys(keys, sorted[i - 1], sorted[i], order.Count))
                {
                    var tierKeys = new int[order.Count];
                    for (int r = 0; r < order.Count; r++)
                    {
                        tierKeys[r] = keys[sorted[i], r];
                    }
                    tiers.Add(new Tier(tierKeys));
                }
                tiers[tiers.Count - 1].Chests.Add(places[sorted[i]].Chest);
            }
            return tiers;
        }

        private static bool SameKeys(int[,] keys, int a, int b, int rules)
        {
            for (int r = 0; r < rules; r++)
            {
                if (keys[a, r] != keys[b, r])
                {
                    return false;
                }
            }
            return true;
        }

        // A chest's setup, as the caller reads it (null: none, or setups ignored).
        private static Func<ItemDrop.ItemData, int> SetupOf(Container chest, StackRules rules) =>
            rules.Setups?.Of != null && chest != null ? rules.Setups.Of(chest) : null;

        /// <summary>How many of an item (by name, any quality or world level) a chest holds.</summary>
        internal static int Held(Inventory contents, string name)
        {
            int held = 0;
            foreach (ItemDrop.ItemData item in contents.GetAllItems())
            {
                if (item.m_shared.m_name == name)
                {
                    held += item.m_stack;
                }
            }
            return held;
        }

        /// <summary>Whether a chest holds a trophy of any kind.</summary>
        internal static bool HoldsAnyTrophy(Inventory contents)
        {
            foreach (ItemDrop.ItemData item in contents.GetAllItems())
            {
                if (StackItems.IsTrophy(item))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
