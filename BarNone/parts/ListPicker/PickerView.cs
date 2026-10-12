// The QoL Mods list picker, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ListPicker\. Every mod that uses
// it carries a copy in its own parts\ListPicker\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Linq;
using QoLMods.Catalogue;

namespace QoLMods.ListPicker
{
    /// <summary>One row of the list on the left: a pick (with its count), or a block's title.</summary>
    internal sealed class RailRow
    {
        /// <summary>The pick's lasting name, so a list reopens on it ("all", "onlist", "cat:Category:Flooring" …).</summary>
        internal string Id = string.Empty;

        internal string Label = string.Empty;
        internal int Count;

        /// <summary>A member under its family.</summary>
        internal bool Member;

        /// <summary>A block's title ("Categories", "Families"), not a pick.</summary>
        internal bool Title;

        /// <summary>For a family whose members can show: its name in the set of open families; else null.</summary>
        internal string OpenId;

        internal bool Open;

        internal Func<Thing, bool> Test = thing => true;

        /// <summary>The catalogue's pick it is, for Auto's headings; All for the window's own picks.</summary>
        internal Selection Pick = Selection.All;
    }

    /// <summary>One line of the pictures: a heading, or a row of pictures.</summary>
    internal sealed class GridLine
    {
        internal string Heading;
        internal int Count;
        internal readonly List<Thing> Cells = new List<Thing>();
    }

    /// <summary>A choice of the Group or Sort list.</summary>
    internal sealed class Choice
    {
        internal string Id = string.Empty;
        internal string Label = string.Empty;
        internal Dimension Dimension;
    }

    /// <summary>
    /// What one tab of the picker shows, worked out from the game's things, the list's entries and the choices made
    /// (pure but for the catalogue's rules: PickerWindow draws it): the things the list can hold (and every one an entry
    /// names), the list on the left with its counts (All, On the list, the game's favourites and his lists, then the
    /// catalogue's own blocks; creatures by biome), what the pick and the search leave, and the headings Group makes with
    /// Sort's order inside each, in rows of pictures. The browsing philosophy (PROJECT.md rule 7) by the catalogue's own
    /// rules (Browse), as Add to Cart's window applies them.
    /// </summary>
    internal sealed class PickerView
    {
        internal readonly List<Thing> Things = new List<Thing>();
        internal readonly List<RailRow> Rail = new List<RailRow>();
        internal readonly List<GridLine> Lines = new List<GridLine>();
        internal readonly List<Choice> Groups = new List<Choice>();
        internal readonly List<Choice> Sorts = new List<Choice>();

        /// <summary>The pick the view was made for (the one asked for, or All when it has nothing in it).</summary>
        internal RailRow Picked;

        internal Choice Group;
        internal Choice Sort;

        /// <summary>How many pictures show.</summary>
        internal int Shown;

        /// <summary>
        /// Works the view out. <paramref name="onList"/> says whether an entry names a thing; <paramref name="starred"/>
        /// and <paramref name="inList"/> read the game's favourites and his lists (null without them).
        /// </summary>
        internal void Build(ThingKind kind, IEnumerable<Thing> things, Func<Thing, bool> onList, string search, string pickId,
                            string groupId, string sortId, ICollection<string> open, int columns,
                            Func<Thing, bool> starred, int lists, Func<int, string> listName, Func<Thing, int, bool> inList)
        {
            Things.Clear();
            Rail.Clear();
            Lines.Clear();
            Groups.Clear();
            Sorts.Clear();
            Things.AddRange(things);
            List<Thing> hits = Things.Where(thing => Matches(thing, search)).ToList();
            bool searching = !string.IsNullOrWhiteSpace(search);

            Add(new RailRow { Id = "all", Label = "All" }, hits);
            Add(new RailRow { Id = "onlist", Label = "On the List", Test = onList }, hits, keepEmpty: true);
            if (kind == ThingKind.Pieces || kind == ThingKind.Items)
            {
                if (starred != null)
                {
                    Add(new RailRow { Id = "fav", Label = "Favorites", Test = starred, Pick = new Selection(Pick.Favorites) }, hits);
                }
                for (int list = 0; inList != null && list < lists; list++)
                {
                    int number = list;
                    string name = listName?.Invoke(number) ?? "List";
                    Add(new RailRow { Id = "list:" + name, Label = name, Test = thing => inList(thing, number), Pick = Browse.ListPick(number, name) }, hits);
                }
                AddCatalogue(kind == ThingKind.Pieces ? Shelf.Build : Shelf.Take, hits, searching, open);
            }
            else if (kind == ThingKind.Creatures)
            {
                Add(new RailRow { Id = "tames", Label = "Can Be Tamed", Test = thing => thing.Tame }, hits);
                Add(new RailRow { Id = "bosses", Label = "Bosses", Test = thing => thing.Boss }, hits);
                var biomes = new List<RailRow>();
                foreach (string biome in Things.Select(thing => thing.Biome).Distinct().OrderBy(Taxonomy.BiomeRank).ThenBy(name => name, StringComparer.Ordinal))
                {
                    string label = biome;
                    var row = new RailRow { Id = "biome:" + label, Label = label, Test = thing => thing.Biome == label };
                    row.Count = hits.Count(row.Test);
                    if (row.Count > 0 || searching)
                    {
                        biomes.Add(row);
                    }
                }
                if (biomes.Count > 0)
                {
                    Rail.Add(new RailRow { Title = true, Label = "Biomes", Id = "title:biomes" });
                    Rail.AddRange(biomes);
                }
            }

            Picked = Rail.FirstOrDefault(row => !row.Title && row.Id == pickId && (row.Count > 0 || row.Id == "onlist")) ?? Rail[0];
            List<Thing> shown = hits.Where(Picked.Test).ToList();
            Shown = shown.Count;
            var byListing = new Dictionary<Listing, Thing>();
            foreach (Thing thing in shown)
            {
                if (thing.Listing != null && !byListing.ContainsKey(thing.Listing))
                {
                    byListing[thing.Listing] = thing;
                }
            }
            List<Listing> listings = byListing.Keys.ToList();

            // Group: the catalogue's choices on its shelves (Auto names itself; only choices that change the view);
            // creatures by biome; the game's item types in one heading.
            if (kind == ThingKind.Pieces || kind == ThingKind.Items)
            {
                Shelf shelf = kind == ThingKind.Pieces ? Shelf.Build : Shelf.Take;
                foreach (GroupOption option in Browse.GroupOptions(shelf, Picked.Pick, listings, Orders.Default))
                {
                    Groups.Add(new Choice { Id = option.Id, Label = option.Label, Dimension = option.Dimension });
                }
            }
            else if (kind == ThingKind.Creatures)
            {
                bool useful = Browse.HeadingCount(Browse.Biome, listings) >= 2;
                Groups.Add(new Choice { Id = Browse.Auto, Label = useful ? "Auto (Biome)" : "Auto", Dimension = Browse.Biome });
                Groups.Add(new Choice { Id = Browse.None, Label = "None", Dimension = Browse.NoHeadings });
            }
            else
            {
                Groups.Add(new Choice { Id = Browse.None, Label = "None", Dimension = Browse.NoHeadings });
            }
            Group = Groups.FirstOrDefault(choice => choice.Id == groupId) ?? Groups[0];
            foreach (SortOption option in Browse.SortOptions(listings, null))
            {
                Sorts.Add(new Choice { Id = option.Id, Label = option.Label });
            }
            Sort = Sorts.FirstOrDefault(choice => choice.Id == sortId) ?? Sorts[0];

            Comparison<Listing> order = kind == ThingKind.ItemTypes
                ? (a, b) => a.GameOrder.CompareTo(b.GameOrder)
                : Sort.Id == Browse.ByName ? Browse.CompareNames : Browse.Sorter(Sort.Id);
            string whole = Picked.Label;
            foreach (KeyValuePair<string, List<Listing>> section in Browse.Sections(listings, Group.Dimension, whole, order))
            {
                Lines.Add(new GridLine { Heading = section.Key, Count = section.Value.Count });
                GridLine row = null;
                foreach (Listing listing in section.Value)
                {
                    if (row == null || row.Cells.Count >= Math.Max(1, columns))
                    {
                        row = new GridLine();
                        Lines.Add(row);
                    }
                    row.Cells.Add(byListing[listing]);
                }
            }
        }

        // A row of the list on the left, with its count of hits; a pick with nothing in it shows only while searching
        // (no dead ends), but On the list always shows.
        private void Add(RailRow row, List<Thing> hits, bool keepEmpty = false)
        {
            row.Count = hits.Count(row.Test);
            if (row.Count > 0 || keepEmpty || row.Id == "all")
            {
                Rail.Add(row);
            }
        }

        // The catalogue's own blocks of the list on the left (Build's categories and materials, Take's families), with a
        // family's members under it while it is open; only rows with something in them (while searching, every row).
        private void AddCatalogue(Shelf shelf, List<Thing> hits, bool searching, ICollection<string> open)
        {
            var listings = new List<Listing>();
            foreach (Thing thing in Things)
            {
                if (thing.Listing != null)
                {
                    listings.Add(thing.Listing);
                }
            }
            List<TreeBlock> tree = Browse.Tree(shelf, listings, Orders.Default);
            Func<Selection, int> count = pick => hits.Count(thing => thing.Listing != null && Browse.InPick(thing.Listing, pick, null, null));
            foreach (KeyValuePair<string, List<ShownRow>> block in Browse.Shown(tree, count, searching, true, open))
            {
                Rail.Add(new RailRow { Title = true, Label = block.Key, Id = "title:" + block.Key });
                foreach (ShownRow shown in block.Value)
                {
                    Selection pick = shown.Row.Pick;
                    Rail.Add(new RailRow
                    {
                        Id = "cat:" + pick.Pick + ":" + pick.Value + ":" + pick.Member,
                        Label = shown.Row.Label,
                        Count = shown.Count,
                        Member = shown.Member,
                        OpenId = shown.OpenId,
                        Open = shown.Open,
                        Pick = pick,
                        Test = thing => thing.Listing != null && Browse.InPick(thing.Listing, pick, null, null),
                    });
                }
            }
        }

        /// <summary>The id of a catalogue pick's row (for a list that opens on one).</summary>
        internal static string IdOf(Selection pick) => "cat:" + pick.Pick + ":" + pick.Value + ":" + pick.Member;

        /// <summary>The search box: every word in its name as shown, its prefab's name or where it sits.</summary>
        internal static bool Matches(Thing thing, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }
            string within = ((thing.Listing?.Search ?? string.Empty) + " " + thing.Shown + " " + thing.Name + " " + thing.Where).ToLowerInvariant();
            foreach (string word in search.ToLowerInvariant().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (within.IndexOf(word, StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
