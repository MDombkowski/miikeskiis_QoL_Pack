// The QoL Mods catalogue, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Catalogue\. Every mod that uses
// it carries a copy in its own parts\Catalogue\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QoLMods.Catalogue
{
    /// <summary>The three kinds of list a browsing window shows: pieces to build, things to craft, items to take.</summary>
    internal enum Shelf
    {
        Build,
        Craft,
        Take,
    }

    /// <summary>What the list on the left can pick.</summary>
    internal enum Pick
    {
        All,
        Favorites,
        List,
        Category,
        MaterialFamily,
        Material,
        Kind,
        Station,
        Family,
        Member,
    }

    /// <summary>A pick on the left: All, the favorites, one of his lists, or one of the tab's own values.</summary>
    internal readonly struct Selection : IEquatable<Selection>
    {
        internal readonly Pick Pick;
        internal readonly string Value;
        internal readonly string Member;
        internal readonly int List;

        internal Selection(Pick pick, string value = null, string member = null, int list = -1)
        {
            Pick = pick;
            Value = value;
            Member = member;
            List = list;
        }

        internal static Selection All => new Selection(Pick.All);

        public bool Equals(Selection other) => Pick == other.Pick && Value == other.Value && Member == other.Member && List == other.List;

        public override bool Equals(object obj) => obj is Selection other && Equals(other);

        public override int GetHashCode() => ((int)Pick * 397) ^ (Value?.GetHashCode() ?? 0) ^ (Member?.GetHashCode() ?? 0) ^ List;
    }

    /// <summary>
    /// One thing a browsing window lists, classified: a piece (Build), a recipe or what a station makes (Craft), or an
    /// item (Take). A module makes these from the game (Add to Cart's Catalogue) and hands them to the rules below.
    /// </summary>
    internal sealed class Listing
    {
        internal Shelf Shelf;

        /// <summary>Unique on its shelf.</summary>
        internal string Key;

        /// <summary>Its name in the game's favorites (GameLists): a piece's prefab, or "item:" and the item's prefab, so one
        /// item has one star on every tab.</summary>
        internal string FavoriteKey;

        internal string Name = string.Empty;

        /// <summary>Its biome of origin, as a label (a piece's: the latest of what it and its station cost).</summary>
        internal string Biome = Taxonomy.OtherBiome;

        /// <summary>Build: its place in its tool's piece table, the game's own order.</summary>
        internal int GameOrder;

        /// <summary>Craft: the station level it needs.</summary>
        internal int Level = 1;

        /// <summary>Craft: the station that makes it, as a label ("By Hand" for none).</summary>
        internal string Station = string.Empty;

        /// <summary>Build: where the piece sits.</summary>
        internal PieceHome Piece;

        /// <summary>Craft: the item it makes; Take: the item itself.</summary>
        internal ItemHome Item;

        internal float Health;
        internal float Stamina;
        internal float Eitr;
        internal float Duration;

        /// <summary>What the filter box matches, lower case: the name, what it costs, its categories or family, its
        /// biome and its station.</summary>
        internal string Search = string.Empty;

        internal int BiomeRank => Taxonomy.BiomeRank(Biome);

        internal string Kind => Item?.Kind ?? string.Empty;

        /// <summary>Craft: the kind's sub-heading (a tool's use, a weapon's type, a food's type …); Take: Gear's.</summary>
        internal string Sub => Item == null ? string.Empty : Shelf == Shelf.Craft ? Item.KindSub : Item.Sub;

        internal string Family => Item?.Family ?? string.Empty;

        internal string Section => Item?.Section ?? string.Empty;

        internal string Slot => Item?.Slot ?? string.Empty;

        internal string Preparation => Item?.Preparation ?? string.Empty;

        internal int ExplicitOrder => Item?.ExplicitOrder ?? 999;
    }

    /// <summary>How headings are made: a key per thing (null: no heading of its own), and the headings' order.</summary>
    internal sealed class Dimension
    {
        internal readonly string Id;
        internal readonly string Label;
        internal readonly Func<Listing, string> Key;
        internal readonly Func<string, double> Order;

        /// <summary>True when the headings are biomes (a window may colour them).</summary>
        internal readonly bool ByBiome;

        internal Dimension(string id, string label, Func<Listing, string> key, Func<string, double> order, bool byBiome = false)
        {
            Id = id;
            Label = label;
            Key = key;
            Order = order;
            ByBiome = byBiome;
        }
    }

    /// <summary>One choice in the Group list.</summary>
    internal sealed class GroupOption
    {
        internal string Id;
        internal string Label;
        internal Dimension Dimension;
    }

    /// <summary>One choice in the Sort list.</summary>
    internal sealed class SortOption
    {
        internal string Id;
        internal string Label;
    }

    /// <summary>One row of the list on the left, with its members when it has them.</summary>
    internal sealed class TreeRow
    {
        internal string Label;
        internal Selection Pick;
        internal readonly List<TreeRow> Members = new List<TreeRow>();
    }

    /// <summary>A titled block of the list on the left (Categories, Materials, Kinds, Made At, Families).</summary>
    internal sealed class TreeBlock
    {
        internal string Title;
        internal readonly List<TreeRow> Rows = new List<TreeRow>();
    }

    /// <summary>One row of the list on the left as a window shows it: with its count, and whether it opens.</summary>
    internal sealed class ShownRow
    {
        internal TreeRow Row;
        internal int Count;

        /// <summary>A member, shown under its open family.</summary>
        internal bool Member;

        /// <summary>For a family with members to show: its name in the set of open families (Browse.OpenId); else null.</summary>
        internal string OpenId;

        internal bool Open;
    }

    /// <summary>
    /// The browsing rules every window of his mods shares (the browsing philosophy; PROJECT.md, rule 7): the left list
    /// picks what shows, Auto headings follow the pick and name themselves, Group offers only choices that would change
    /// the view, progression is the default order. Ported from the mock-up's template.html (autoDim, groupOptions,
    /// sortOptions, sorter, inSel, railModel), which is the agreed design in runnable form.
    /// </summary>
    internal static class Browse
    {
        internal const string Auto = "auto";
        internal const string None = "none";
        internal const string ByBiome = "biome";
        internal const string Progression = "progression";
        internal const string ByName = "name";

        /// <summary>The food sorts, highest first (his call, 2026-09-26): they appear only while food is shown.</summary>
        internal static readonly (string Id, string Label)[] FoodSorts =
        {
            ("health", "Health"), ("stamina", "Stamina"), ("eitr", "Eitr"), ("duration", "Duration"),
        };

        private static readonly StringComparer Names = StringComparer.Create(CultureInfo.InvariantCulture, true);

        // ---- The ways to make headings

        internal static readonly Dimension Biome = new Dimension("biome", "Biome", e => e.Biome, name => Taxonomy.BiomeRank(name), true);

        internal static readonly Dimension NoHeadings = new Dimension("none", "None", e => null, name => 0);

        internal static Dimension Category(Orders orders) => new Dimension("category", "Category",
            e => e.Piece == null ? null : e.Piece.Primary == Taxonomy.BuildingParent ? Taxonomy.OtherStructures : e.Piece.Primary,
            name => name == Taxonomy.OtherStructures ? orders.OtherStructuresPlace : orders.CategoryPlace(name));

        internal static Dimension Material(Orders orders) => new Dimension("material", "Material",
            e => e.Piece == null ? null : e.Piece.StructuralFamilies.Count > 0 ? e.Piece.Material : Taxonomy.OtherMaterials,
            name => name == Taxonomy.OtherMaterials ? 999 : Math.Min(orders.MaterialPlace(name), 999));

        internal static Dimension Kind(Orders orders) => new Dimension("kind", "Kind", e => e.Kind, orders.KindPlace);

        internal static Dimension Station(Orders orders) => new Dimension("station", "Made At", e => e.Station, orders.StationPlace);

        internal static Dimension Family(Orders orders) => new Dimension("family", "Family", e => e.Family, orders.FamilyPlace);

        internal static readonly Dimension Slot = new Dimension("slot", "Equipment Slot", e => e.Slot.Length > 0 ? e.Slot : null, name => PlaceIn(Taxonomy.SlotOrder, name));

        internal static readonly Dimension Preparation = new Dimension("prep", "Preparation", e => e.Preparation.Length > 0 ? e.Preparation : null, name => PlaceIn(Taxonomy.PreparationOrder, name));

        /// <summary>The choices Group may offer on each shelf, besides Auto and None.</summary>
        private static IEnumerable<Dimension> Candidates(Shelf shelf, Orders orders)
        {
            switch (shelf)
            {
                case Shelf.Build:
                    return new[] { Category(orders), Material(orders), Biome };
                case Shelf.Craft:
                    return new[] { Kind(orders), Station(orders), Slot, Preparation, Biome };
                default:
                    return new[] { Family(orders), Slot, Preparation, Biome };
            }
        }

        /// <summary>The dimension a pick on the left already fixes: grouping by it would change nothing (his call).</summary>
        private static string FixedBy(Pick pick)
        {
            switch (pick)
            {
                case Pick.Category:
                    return "category";
                case Pick.Kind:
                    return "kind";
                case Pick.Station:
                    return "station";
                case Pick.Family:
                case Pick.Member:
                    return "family";
                default:
                    return null;
            }
        }

        /// <summary>Auto: the natural next level down from what is picked on the left.</summary>
        internal static Dimension AutoDimension(Shelf shelf, Selection pick, Orders orders)
        {
            switch (shelf)
            {
                case Shelf.Build:
                    if (pick.Pick == Pick.Category)
                    {
                        if (pick.Value == Taxonomy.BuildingParent)
                        {
                            // A piece's heading is the first of the six it carries, in the game's order, so it never
                            // moves when the order of the headings is changed.
                            return new Dimension("building", "Category",
                                e => e.Piece == null ? null : Taxonomy.BuildingSubs.FirstOrDefault(sub => e.Piece.Categories.Contains(sub)) ?? Taxonomy.OtherStructures,
                                name => { int at = orders.BuildingSubs.IndexOf(name); return at < 0 ? 99 : at; });
                        }
                        return Array.IndexOf(Taxonomy.BuildingSubs, pick.Value) >= 0 ? Material(orders) : Biome;
                    }
                    if (pick.Pick == Pick.MaterialFamily)
                    {
                        if (Array.IndexOf(Taxonomy.StructuralFamilies, pick.Value) >= 0)
                        {
                            string family = pick.Value;
                            return new Dimension("member", "Material", e => LatestIn(e, family) ?? "Other", name => orders.MaterialPlace(name));
                        }
                        return new Dimension("member", "Material", e => e.Piece == null || e.Piece.Material.Length == 0 ? "Other" : e.Piece.Material, name => 0);
                    }
                    return Category(orders);
                case Shelf.Craft:
                    if (pick.Pick == Pick.Kind)
                    {
                        switch (pick.Value)
                        {
                            case "Weapons":
                                return Sub("Weapon Type", Taxonomy.WeaponOrder);
                            case "Ammo":
                                return Sub("Ammo Type", Taxonomy.AmmoOrder);
                            case "Tools":
                                return Sub("Use", Taxonomy.ToolUseOrder);
                            case "Food":
                                return Sub("Food Type", Members(orders, "Food"));
                            case "Meads and Potions":
                                return Sub("Effect", Members(orders, "Meads and Potions"));
                            case "Materials":
                                return Sub("Family", orders.Families);
                            default:
                                return Biome;
                        }
                    }
                    return Kind(orders);
                default:
                    if (pick.Pick == Pick.Family)
                    {
                        if (orders.FamilyMembers.TryGetValue(pick.Value, out List<string> members))
                        {
                            string label = pick.Value == "Gear" ? "Kind" : pick.Value == "Metals" ? "Metal" : pick.Value == "Meads and Potions" ? "Effect" : "Type";
                            return new Dimension("section", label, e => e.Section, name => PlaceIn(members, name));
                        }
                        return pick.Value == "Creature Parts" || pick.Value == "Trophies" ? Biome : OneHeading(pick.Value);
                    }
                    if (pick.Pick == Pick.Member)
                    {
                        if (pick.Value == "Gear" && (pick.Member == "Tools" || pick.Member == "Weapons" || pick.Member == "Ammo"))
                        {
                            return pick.Member == "Tools" ? Sub("Use", Taxonomy.ToolUseOrder)
                                : pick.Member == "Weapons" ? Sub("Weapon Type", Taxonomy.WeaponOrder)
                                : Sub("Ammo Type", Taxonomy.AmmoOrder);
                        }
                        return OneHeading(pick.Member);
                    }
                    return Family(orders);
            }
        }

        private static Dimension Sub(string label, IList<string> order) =>
            new Dimension("sub", label, e => e.Sub.Length > 0 ? e.Sub : "Other", name => PlaceIn(order, name));

        private static Dimension OneHeading(string name) => new Dimension("flat", "One Heading", e => name, heading => 0);

        private static List<string> Members(Orders orders, string family) =>
            orders.FamilyMembers.TryGetValue(family, out List<string> members) ? members : new List<string>();

        /// <summary>Of a piece's wood, stone or metal in one family, the one latest in the game's unlock order.</summary>
        private static string LatestIn(Listing e, string family)
        {
            if (e.Piece == null)
            {
                return null;
            }
            string best = null;
            int bestAt = -1;
            for (int i = 0; i < Taxonomy.StructuralMaterials.Length; i++)
            {
                (string _, string label, string materialFamily) = Taxonomy.StructuralMaterials[i];
                if (materialFamily == family && e.Piece.StructuralMaterials.Contains(label) && i > bestAt)
                {
                    best = label;
                    bestAt = i;
                }
            }
            return best;
        }

        private static double PlaceIn(IList<string> order, string name)
        {
            int at = order.IndexOf(name);
            return at < 0 ? 900 : at;
        }

        /// <summary>How many headings a dimension would make of these rows (things with no key of their own don't count).</summary>
        internal static int HeadingCount(Dimension dimension, IEnumerable<Listing> rows)
        {
            var keys = new HashSet<string>();
            foreach (Listing e in rows)
            {
                string key = dimension.Key(e);
                if (!string.IsNullOrEmpty(key))
                {
                    keys.Add(key);
                }
            }
            return keys.Count;
        }

        /// <summary>
        /// The Group list for what is shown: Auto, which names what it does ("Auto (Material)") when it makes two headings
        /// or more; every other way that would give two headings or more, except the one the pick on the left already
        /// fixes; and None.
        /// </summary>
        internal static List<GroupOption> GroupOptions(Shelf shelf, Selection pick, IList<Listing> rows, Orders orders)
        {
            Dimension auto = AutoDimension(shelf, pick, orders);
            bool autoUseful = HeadingCount(auto, rows) >= 2;
            var options = new List<GroupOption> { new GroupOption { Id = Auto, Label = autoUseful ? $"Auto ({auto.Label})" : "Auto", Dimension = auto } };
            string fixedBy = FixedBy(pick.Pick);
            foreach (Dimension candidate in Candidates(shelf, orders))
            {
                if (candidate.Id == auto.Id || (candidate.Label == auto.Label && autoUseful) || candidate.Id == fixedBy)
                {
                    continue;
                }
                if (HeadingCount(candidate, rows) >= 2)
                {
                    options.Add(new GroupOption { Id = candidate.Id, Label = candidate.Label, Dimension = candidate });
                }
            }
            options.Add(new GroupOption { Id = None, Label = "None", Dimension = NoHeadings });
            return options;
        }

        /// <summary>True when everything shown is food: then the food sorts appear.</summary>
        internal static bool IsFoodView(IList<Listing> rows) => rows.Count > 0 && rows.All(e => e.Family == "Food" || e.Kind == "Food");

        /// <summary>The Sort list: Progression and Name, the food sorts while food is shown, then what the window adds.</summary>
        internal static List<SortOption> SortOptions(IList<Listing> rows, IEnumerable<SortOption> extra)
        {
            var options = new List<SortOption>
            {
                new SortOption { Id = Progression, Label = "Progression" },
                new SortOption { Id = ByName, Label = "Name" },
            };
            if (IsFoodView(rows))
            {
                options.AddRange(FoodSorts.Select(food => new SortOption { Id = food.Id, Label = food.Label }));
            }
            if (extra != null)
            {
                options.AddRange(extra);
            }
            return options;
        }

        /// <summary>Progression: the order a player unlocks things, Meadows to the Deep North, then the game's own order.</summary>
        internal static int CompareProgression(Listing a, Listing b)
        {
            int order = a.BiomeRank.CompareTo(b.BiomeRank);
            if (order != 0)
            {
                return order;
            }
            switch (a.Shelf)
            {
                case Shelf.Build:
                    return a.GameOrder.CompareTo(b.GameOrder);
                case Shelf.Craft:
                    order = a.Level.CompareTo(b.Level);
                    return order != 0 ? order : Names.Compare(a.Name, b.Name);
                default:
                    order = a.ExplicitOrder.CompareTo(b.ExplicitOrder);
                    return order != 0 ? order : Names.Compare(a.Name, b.Name);
            }
        }

        internal static int CompareNames(Listing a, Listing b) => Names.Compare(a.Name, b.Name);

        /// <summary>The order inside each heading. <paramref name="number"/> gives the window's own sorts (Count, Recent,
        /// Frequent): highest first, ties in progression order.</summary>
        internal static Comparison<Listing> Sorter(string sortId, Func<Listing, double> number = null)
        {
            if (sortId == ByName)
            {
                return CompareNames;
            }
            switch (sortId)
            {
                case "health":
                    return Highest(e => e.Health);
                case "stamina":
                    return Highest(e => e.Stamina);
                case "eitr":
                    return Highest(e => e.Eitr);
                case "duration":
                    return Highest(e => e.Duration);
            }
            return number != null && sortId != Progression ? Highest(number) : CompareProgression;
        }

        private static Comparison<Listing> Highest(Func<Listing, double> value) => (a, b) =>
        {
            int order = value(b).CompareTo(value(a));
            return order != 0 ? order : CompareProgression(a, b);
        };

        /// <summary>The headings and what goes under each, in order. Things with no key of their own go under "Other";
        /// with None, everything goes under one heading named after the pick.</summary>
        internal static List<KeyValuePair<string, List<Listing>>> Sections(IEnumerable<Listing> rows, Dimension dimension, string whole,
                                                                           Comparison<Listing> order)
        {
            var sections = new Dictionary<string, List<Listing>>();
            foreach (Listing e in rows)
            {
                string key = dimension.Id == None ? whole : dimension.Key(e);
                if (string.IsNullOrEmpty(key))
                {
                    key = "Other";
                }
                if (!sections.TryGetValue(key, out List<Listing> list))
                {
                    sections[key] = list = new List<Listing>();
                }
                list.Add(e);
            }
            var result = sections.ToList();
            result.Sort((a, b) =>
            {
                int byOrder = dimension.Order(a.Key).CompareTo(dimension.Order(b.Key));
                return byOrder != 0 ? byOrder : Names.Compare(a.Key, b.Key);
            });
            foreach (KeyValuePair<string, List<Listing>> section in result)
            {
                List<Listing> sorted = Sorted(section.Value, order);
                section.Value.Clear();
                section.Value.AddRange(sorted);
            }
            return result;
        }

        /// <summary>Sorted, with things the order calls equal kept in the order they came (the game's own order): so
        /// the charcoal kiln's three coals, or bronze by one and by five, never swap places between two looks.</summary>
        internal static List<Listing> Sorted(IEnumerable<Listing> rows, Comparison<Listing> order) =>
            rows.OrderBy(row => row, Comparer<Listing>.Create(order)).ToList();

        /// <summary>Whether a thing is in what is picked on the left. The favorites and his lists are the game's own
        /// favorite categories, which the window reads for these two.</summary>
        internal static bool InPick(Listing e, Selection pick, Func<Listing, bool> starred, Func<Listing, int, bool> inList)
        {
            switch (pick.Pick)
            {
                case Pick.All:
                    return true;
                case Pick.Favorites:
                    return starred != null && starred(e);
                case Pick.List:
                    return inList != null && inList(e, pick.List);
                case Pick.Category:
                    return e.Piece != null && (pick.Value == Taxonomy.BuildingParent ? e.Piece.InBuilding : e.Piece.Categories.Contains(pick.Value));
                case Pick.MaterialFamily:
                    if (e.Piece == null)
                    {
                        return false;
                    }
                    return Array.IndexOf(Taxonomy.StructuralFamilies, pick.Value) >= 0
                        ? e.Piece.StructuralFamilies.Contains(pick.Value)
                        : e.Piece.MaterialFamily == pick.Value && e.Piece.StructuralFamilies.Count == 0;
                case Pick.Material:
                    if (e.Piece == null)
                    {
                        return false;
                    }
                    return IsStructural(pick.Value)
                        ? e.Piece.StructuralMaterials.Contains(pick.Value)
                        : e.Piece.Material == pick.Value && e.Piece.StructuralFamilies.Count == 0;
                case Pick.Kind:
                    return e.Kind == pick.Value;
                case Pick.Station:
                    return e.Station == pick.Value;
                case Pick.Family:
                    return e.Family == pick.Value;
                case Pick.Member:
                    return e.Family == pick.Value && e.Section == pick.Member;
                default:
                    return false;
            }
        }

        private static bool IsStructural(string label)
        {
            foreach ((string _, string material, string _) in Taxonomy.StructuralMaterials)
            {
                if (material == label)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// What the filter box searches, as the mock-up builds it: on Build the name, what it costs, its categories, what
        /// it is made of, its station and biome; on Craft the name, what it costs, its kind and sub-heading, its station
        /// and biome; on Take the name, its family, member and sub-heading, the game's item type and its biome. Lower case.
        /// </summary>
        internal static string SearchText(Listing e, IEnumerable<string> costNames, string itemType)
        {
            var parts = new List<string> { e.Name };
            switch (e.Shelf)
            {
                case Shelf.Build:
                    parts.Add(string.Join(" ", costNames ?? Enumerable.Empty<string>()));
                    parts.Add(e.Piece != null ? string.Join(" ", e.Piece.Categories) : string.Empty);
                    parts.Add(e.Piece?.Material ?? string.Empty);
                    parts.Add(e.Station);
                    break;
                case Shelf.Craft:
                    parts.Add(string.Join(" ", costNames ?? Enumerable.Empty<string>()));
                    parts.Add(e.Kind);
                    parts.Add(e.Sub);
                    parts.Add(e.Station);
                    break;
                default:
                    parts.Add(e.Family);
                    parts.Add(e.Section);
                    parts.Add(e.Sub);
                    parts.Add(itemType ?? string.Empty);
                    break;
            }
            parts.Add(e.Biome);
            return string.Join(" ", parts).ToLowerInvariant();
        }

        /// <summary>The filter box: every word must be found (so "stone floor" works), in the name, what it costs, its
        /// categories or family, its biome or its station.</summary>
        internal static bool Matches(Listing e, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }
            foreach (string word in query.ToLowerInvariant().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (e.Search.IndexOf(word, StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The pick's name, as the line above the results says it.</summary>
        internal static string Label(Selection pick, Func<int, string> listName)
        {
            switch (pick.Pick)
            {
                case Pick.All:
                    return "All";
                case Pick.Favorites:
                    return "Favorites";   // plain: the game's font may have no star; a window shows its own
                case Pick.List:
                    return listName?.Invoke(pick.List) ?? "List";
                case Pick.Member:
                    return $"{pick.Value} › {pick.Member}";
                default:
                    return pick.Value ?? string.Empty;
            }
        }

        /// <summary>How the line above the results says what Group is doing.</summary>
        internal static string Grouped(GroupOption option)
        {
            if (option.Id == None)
            {
                return "not grouped";
            }
            string label = option.Id == Auto ? option.Dimension.Label : option.Label;
            if (option.Id == Auto && option.Label == "Auto")
            {
                return "one heading";
            }
            return "by " + (label == "Made At" ? "station" : label.ToLowerInvariant());
        }

        /// <summary>A family's name in a window's set of open families.</summary>
        internal static string OpenId(Selection pick) => $"{pick.Pick}:{pick.Value}";

        /// <summary>A pick of one of his lists, by its number and its name.</summary>
        internal static Selection ListPick(int list, string name) => new Selection(Pick.List, name, list: list);

        /// <summary>
        /// A pick, after his lists may have changed (one deleted in the game's build menu moves the later ones down a
        /// number): a list is found again by its name; a list that is gone gives All. Any other pick is kept.
        /// </summary>
        internal static Selection Resolve(Selection pick, int listCount, Func<int, string> listName)
        {
            if (pick.Pick != Pick.List)
            {
                return pick;
            }
            if (pick.List >= 0 && pick.List < listCount && (pick.Value == null || listName(pick.List) == pick.Value))
            {
                return pick;
            }
            if (pick.Value != null)
            {
                // The game allows two lists of one name: look where the list can have gone, nearest first. A deletion
                // only ever moves a list down.
                for (int list = Math.Min(pick.List, listCount - 1); list >= 0; list--)
                {
                    if (listName(list) == pick.Value)
                    {
                        return ListPick(list, pick.Value);
                    }
                }
                for (int list = Math.Max(pick.List + 1, 0); list < listCount; list++)
                {
                    if (listName(list) == pick.Value)
                    {
                        return ListPick(list, pick.Value);
                    }
                }
            }
            return Selection.All;
        }

        /// <summary>
        /// What the list on the left shows of a tree: no dead ends, so only rows with something in them (while searching,
        /// every row, with its count of hits); a family's members only when families open (the setting) and it is open;
        /// no block left empty.
        /// </summary>
        internal static List<KeyValuePair<string, List<ShownRow>>> Shown(IEnumerable<TreeBlock> tree, Func<Selection, int> count, bool searching,
                                                                           bool expandable, ICollection<string> open)
        {
            var shown = new List<KeyValuePair<string, List<ShownRow>>>();
            foreach (TreeBlock block in tree)
            {
                var rows = new List<ShownRow>();
                foreach (TreeRow parent in block.Rows)
                {
                    int n = count(parent.Pick);
                    if (n == 0 && !searching)
                    {
                        continue;
                    }
                    var members = new List<ShownRow>();
                    if (expandable)
                    {
                        foreach (TreeRow member in parent.Members)
                        {
                            int m = count(member.Pick);
                            if (m > 0 || searching)
                            {
                                members.Add(new ShownRow { Row = member, Count = m, Member = true });
                            }
                        }
                    }
                    string id = members.Count > 0 ? OpenId(parent.Pick) : null;
                    bool isOpen = id != null && open != null && open.Contains(id);
                    rows.Add(new ShownRow { Row = parent, Count = n, OpenId = id, Open = isOpen });
                    if (isOpen)
                    {
                        rows.AddRange(members);
                    }
                }
                if (rows.Count > 0)
                {
                    shown.Add(new KeyValuePair<string, List<ShownRow>>(block.Title, rows));
                }
            }
            return shown;
        }

        /// <summary>
        /// The list on the left below All and his lists, for one shelf: Build's categories (Building Structures opening to
        /// its six) and materials (each family opening to its members), Craft's kinds and stations, Take's families (each
        /// opening to its members). A window shows only rows with something in them.
        /// </summary>
        internal static List<TreeBlock> Tree(Shelf shelf, IEnumerable<Listing> rows, Orders orders)
        {
            var blocks = new List<TreeBlock>();
            switch (shelf)
            {
                case Shelf.Build:
                {
                    var categories = new TreeBlock { Title = "Categories" };
                    foreach (string category in orders.Categories)
                    {
                        var row = new TreeRow { Label = category, Pick = new Selection(Pick.Category, category) };
                        if (category == Taxonomy.BuildingParent)
                        {
                            row.Members.AddRange(orders.BuildingSubs.Select(sub => new TreeRow { Label = sub, Pick = new Selection(Pick.Category, sub) }));
                        }
                        categories.Rows.Add(row);
                    }
                    blocks.Add(categories);
                    var materials = new TreeBlock { Title = "Materials" };
                    List<Listing> pieces = rows.Where(e => e.Piece != null).ToList();
                    foreach (string family in orders.MaterialFamilies)
                    {
                        var row = new TreeRow { Label = family, Pick = new Selection(Pick.MaterialFamily, family) };
                        IEnumerable<string> members = orders.MaterialMembers.TryGetValue(family, out List<string> structural)
                            ? structural
                            : pieces.Where(e => e.Piece.MaterialFamily == family && e.Piece.StructuralFamilies.Count == 0 && e.Piece.Material.Length > 0)
                                    .Select(e => e.Piece.Material).Distinct().OrderBy(name => name, Names);
                        row.Members.AddRange(members.Select(member => new TreeRow { Label = member, Pick = new Selection(Pick.Material, member) }));
                        materials.Rows.Add(row);
                    }
                    blocks.Add(materials);
                    break;
                }
                case Shelf.Craft:
                {
                    var kinds = new TreeBlock { Title = "Kinds" };
                    kinds.Rows.AddRange(orders.Kinds.Select(kind => new TreeRow { Label = kind, Pick = new Selection(Pick.Kind, kind) }));
                    blocks.Add(kinds);
                    var stations = new TreeBlock { Title = "Made At" };
                    var labels = new List<string>(orders.Stations);
                    // A station from another mod goes after his list, by name.
                    labels.AddRange(rows.Select(e => e.Station).Where(station => station.Length > 0 && !orders.Stations.Contains(station)).Distinct().OrderBy(name => name, Names));
                    stations.Rows.AddRange(labels.Select(station => new TreeRow { Label = station, Pick = new Selection(Pick.Station, station) }));
                    blocks.Add(stations);
                    break;
                }
                default:
                {
                    var families = new TreeBlock { Title = "Families" };
                    foreach (string family in orders.Families)
                    {
                        var row = new TreeRow { Label = family, Pick = new Selection(Pick.Family, family) };
                        if (orders.FamilyMembers.TryGetValue(family, out List<string> members))
                        {
                            row.Members.AddRange(members.Select(member => new TreeRow { Label = member, Pick = new Selection(Pick.Member, family, member) }));
                        }
                        families.Rows.Add(row);
                    }
                    blocks.Add(families);
                    break;
                }
            }
            return blocks;
        }
    }
}
